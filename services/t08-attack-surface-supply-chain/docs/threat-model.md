# Threat model: T08 — Attack surface, supply chain i release trust

Obuhvata R1-18, R2-13 i R3-17. Sav rad je nad simulatorima i testnim podacima (rezervisani domeni `example.com/.org`,
TEST-NET opsezi `192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24`); nikakvo stvarno skeniranje ni napadi.

## Opseg i trust granice

- **Asset:** registar izloženosti (Exposure), registar zavisnosti i snapshot-ovi, release artefakti, provenance dokazi, trust policy i odluke.
- **Vlasnik:** T08 (tim 02). Referenciramo tuđe: asset i nalazi (T01), zone (T11), kontrole (T05), audit (T04), identitet (T12), dozvole (T02).
- **Ulazni akteri/sistemi:** korisnici API-ja (SA, RO, AN, AU — vidi [`security-matrix.md`](security-matrix.md)); adapteri ka drugim timovima; simulirani CI/build izvor.
- **Trust granica:**
  1. **Klijent → T08 API** — identitet i scope se određuju na serveru iz validiranog konteksta, ne iz polja zahteva.
  2. **T08 ↔ adapteri drugih timova** (`C-ASSET-v1`, `C-ZONE-v1`, `C-AUDIT-v1`, kasnije nalazi i kontrole) — njihovi odgovori su spoljni ulaz: validiraju se, a nedostupnost nije uspeh.
  3. **Simulirani CI/build izvor → R3-17** — provenance su tvrdnje, ne dokaz.

## Lanac: asset → misuse → kontrola → test → evidence

Kolona **Test (tip)** je vrsta testa, a **Konkretan test** su stvarni testovi iz [`tests/T08.Tests`](../tests/T08.Tests).
**Evidence** je rezultat `dotnet test` (129/129, 2026-10-26, radno stablo pre commit-a); `—` znači da testa još nema.
Klase: `CE` = `Exposures/ClassifyExposureTests`, `RF` = `Exposures/ReviewFixesTests`, `HT` = `Api/ClassifyHttpTests`,
`DA` = `Infrastructure/DirectoryAdapterTests`, `AU` = `Audit/AuditTests`.

| # | Celina | Asset | Misuse | Kontrola | Test (tip) | Konkretan test | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| M1 | R1-18 | Poverljivost exposure-a po scope-u | Korisnik drugog scope-a menja/čita tuđ exposure poznavajući samo ID | serverska scope provera pre dozvole; isti odgovor kao za nepostojeći | negativan (SEC-BASE-02) | `CE.Exposure_in_another_scope_looks_exactly_like_a_missing_one`, `CE.Scope_is_checked_before_permission...`, `RF.Exposure_in_another_scope_is_audited_as_denied...`, `RF.Asset_that_moved_to_another_scope_is_not_classified`, `HT.Missing_foreign_scope_and_malformed_ids_give_the_same_404...` | pokriveno samo za classify; endpoint za čitanje detalja ne postoji |
| M2 | R1-18 | Integritet registra (vlasnik, obrazloženje) | Registracija bez vlasnika ili obrazloženja | validacija na granici, stabilan kod greške | unit/negativan | `CE.Active_exposure_requires_owner_and_justification` (domen) | delimično: use case registracije ne postoji |
| M3 | R1-18 | Značenje klasifikacije | Nepoznat exposure postaje „ranjivost" ili „bezbedan" | `Unknown` = „za proveru" kao jedini ishod | unit nad domenom | `CE.New_exposure_starts_as_unknown`, `CE.Returning_to_unknown_is_allowed_and_clears_expiry`, `CE.Undefined_enum_value_is_rejected`, `HT.Invalid_classification_value_is_400...` | pokriveno |
| M4 | R1-18 | Klasifikacija i izloženost kritičnog servisa | Tiha promena klasifikacije/izloženosti kritičnog servisa; neovlašćena promena | dozvola + obavezan audit događaj (`classification.changed`, `critical.changed`) | deny test + provera audita | `CE.Actor_without_classify_permission_is_forbidden_and_the_denial_is_audited`, `CE.Analyst_classifies_and_a_high_sensitivity...`, `CE.Critical_asset_classification_also_writes_the_critical_exposure_event`, `CE.Non_critical_asset_does_not_write...`, `HT.Actor_without_classify_permission_is_403...`, `HT.Role_matrix_allows_only_SA_and_AN_to_classify`, `HT.Missing_token_is_401...` | pokriveno; uloge su samo `perms` u dev tokenu (T12/T02) |
| M5 | R1-18 | Integritet veze exposure ↔ asset/zona | Veza sa nepostojećim/dekomisionisanim asset-om ili neaktivnom zonom | adapter odbija; stub vraća not found/inactive | negativan + contract | `CE.Decommissioned_asset_blocks_classification`, `RF.Unknown_asset_is_not_usable`, `RF.Inactive_asset_is_not_usable`, `DA.Decommissioned_asset_in_200_body_is_inactive_not_found`, `DA.Inactive_zone_in_200_body_is_inactive` | delimično: nema zajedničkog contract testa stub ↔ adapter; zona nema potrošača |
| M6 | R1-18 | Nepromenljivost zatvorenog exposure-a | Zatvoren exposure se menja ili briše | terminalno stanje, izmena odbijena | unit | `CE.Closed_exposure_cannot_be_classified`, `CE.Closed_exposure_is_rejected`, `RF.Closed_exposure_is_rejected_before_the_asset_is_read...` | pokriveno za izmenu; brisanje ne postoji u API-ju |
| M7 | R1-18/sve | Pouzdanost odluke (fail-closed) | Upstream (T01/T11/T04) nedostupan i to se tumači kao uspeh | nedostupnost se mapira u grešku (503), ne u prazan uspeh | negativan (adapter + API) | `CE.Asset_service_down_fails_closed_without_changing_anything`, `HT.Asset_service_down_is_503_and_nothing_changes`, `DA.Malformed_or_mismatched_200_is_unavailable_never_success`, `DA.Timeout_is_unavailable`, `AU.Unavailable_sink_reports_unavailable_and_stores_nothing` | pokriveno; stvarni 5 s `HttpClient` timeout se simulira izuzetkom |
| M8 | R2-13 | Registar zavisnosti | Zavisnost bez verzije/identiteta prođe uvoz; nepoznat izvor ostane nevidljiv | validacija; `Unknown` source = risk signal | negativan + uvozni | — | — (nije implementirano) |
| M9 | R2-13 | Životni ciklus rizika | Rizik zatvoren bez nove verzije i bez izuzetka; ponovljen uvoz duplira | invarijanta zatvaranja; idempotentnost | negativan; dvostruki uvoz | — | — (nije implementirano) |
| M10 | R2-13/R3-17 | Odobravanje izuzetaka | Izuzetak odobrava podnosilac ili neovlašćen korisnik | permission + razdvajanje dužnosti | deny test | — | — (nije implementirano) |
| M11 | R3-17 | Release odluka | Release samo sa mutable tagom (`latest`) dobije allow; nedostatak dokaza = „bezbedno" | obavezan digest; nedostatak dokaza ≠ allow | negativan + unit | — | — (nije implementirano) |
| M12 | R3-17 | Snapshot i odluka | Snapshot izmenjen posle odluke; nov kritičan nalaz posle allow | nepromenljiv snapshot, odluka čuva `snapshotId` i `policyVersion`; ponovna evaluacija | reproducibilnost; allow → block | — | — (nije implementirano) |
| M13 | sve | Tajne u audit-u/DTO-u/seed-u | Tajne/tokeni u logu, audit-u, seed-u ili DTO-u | audit i DTO bez tajni; seed samo izmišljeni podaci | provera audita; pregled seed-a | `AU.Secret_like_detail_keys_are_rejected`, `AU.Classification_change_carries_identifiers_and_codes_only`, `AU.Details_over_2_KB_are_rejected` | delimično: filtriraju se samo **nazivi** ključeva, ne vrednosti; seed nije pregledan |

## Bezbednosni zahtev i kontrola

- **Zahtev:** SEC-BASE-01..06, SEC-CONV-02/03/04/05/06/08, SEC-DEP-01..05; pravila R1-18, R2-13 i R3-17 iz specifikacije.
- **Kontrola:** serverska provera uloge + objekta/scope-a (deny-by-default); validacija ulaza na granici; domen koji ne dozvoljava nevalidna stanja
  (npr. `Temporary` bez roka, izmena `Closed`); adapteri koji nedostupnost upstream-a ne pretvaraju u uspeh.
- **Audit/evidence:** događaj sa poljima iz SEC-CONV-04 (vreme, actor, akcija, target, ishod, correlation ID, osetljivost), bez tajni.
  R1-18: registrovan, promenjena klasifikacija, potvrđen, zatvoren, promena izloženosti kritičnog servisa, odbijen pristup.
  R2-13: komponenta, uvoz (uspeh/neuspeh), vezan nalaz, plan, odobren izuzetak.
  R3-17: release, odluka (`policyVersion`, `snapshotId`), izuzetak, ponovna evaluacija, odbijen pristup build detaljima.
- **Stanje audit događaja R1-18 u toku:** promenjena klasifikacija, promena izloženosti kritičnog servisa i odbijen pristup (scope/dozvola) su povezani i testirani;
  registrovan, potvrđen i zatvoren postoje samo kao builder-i, bez use case-a.

## Testovi

Stanje na 2026-10-26: **129/129 prošlo** (`dotnet test`, `services/t08-attack-surface-supply-chain`). Promene još nisu commit-ovane.

- **Pozitivan scenario (implementirano):** klasifikacija exposure-a ovlašćenog korisnika (domen, handler i HTTP: `HT.Allowed_actor_classifies_and_actor_comes_from_the_token_not_the_body`).
  **Planirano:** registracija exposure-a, uvoz zavisnosti, allow za release sa kompletnim dokazima.
- **Negativan/security scenario:** M1, M3, M4, M6, M7 pokriveni; M2, M5, M13 delimično; M8–M12 planirani (R2-13, R3-17).
  Prioritet za KT1 (M1, M4, M5, M7): M1, M4, M7 pokriveni, M5 čeka zajednički contract test.
- **Automatizovani test:** NUnit + Moq u [`tests/T08.Tests`](../tests/T08.Tests); HTTP testovi pokreću pravi Kestrel preko `T08Host`.
- **Nije dokazano:** da svaki negativan test pada kad se kontrola ukloni (mutaciona provera nije rađena).

## Preostali rizik / ograničenja

- Do prihvatanja ugovora (`ACCEPTED`) adapteri rade nad stubovima; razlika stub ↔ stvarni servis može da otkrije nepodudaranje polja.
- Identitet i dozvole su stub dok T12/T02 ne objave ugovor; mapiranje SA/AN → `exposure.classify` ne postoji u kodu, a HTTP testovi uloge predstavljaju skupom dozvola u tokenu.
- Provenance i nalazi dolaze iz simulatora; ne dokazuju stvarnu bezbednost build-a.
- Kritičnost asset-a se čita preko adaptera u trenutku akcije; kasnija promena kritičnosti ne menja već upisan audit.
- Audit bafer je u memoriji; događaji se gube pri restartu, a prekoračenje se samo broji.
- Threat model je za KT1 početni; R2-13 i R3-17 redovi se dopunjuju kad se UC-ovi implementiraju.

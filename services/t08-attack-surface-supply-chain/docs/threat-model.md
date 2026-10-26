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

## Pretnja / misuse scenario

| # | Celina | Misuse | Kontrola | Test |
| --- | --- | --- | --- | --- |
| M1 | R1-18 | Korisnik drugog scope-a čita detalje tuđeg endpoint-a poznavajući samo ID | serverska scope provera; isti odgovor kao za nepostojeći | negativan (SEC-BASE-02) |
| M2 | R1-18 | Registracija bez vlasnika ili obrazloženja | validacija na granici, stabilan kod greške | unit/negativan |
| M3 | R1-18 | Nepoznat exposure postaje „ranjivost" ili „bezbedan" | `Unknown` = „za proveru" kao jedini ishod | unit nad domenom |
| M4 | R1-18 | Tiha promena klasifikacije/izloženosti kritičnog servisa | dozvola + obavezan audit događaj | deny test + provera audita |
| M5 | R1-18 | Veza sa nepostojećim/dekomisionisanim asset-om ili neaktivnom zonom | adapter odbija; stub vraća not found/inactive | negativan + contract |
| M6 | R1-18 | Zatvoren exposure se menja ili briše | terminalno stanje, izmena odbijena | unit |
| M7 | R1-18/sve | Upstream (T01/T11/T04) nedostupan i to se tumači kao uspeh | 503 se mapira u grešku, ne u prazan uspeh | negativan (adapter) |
| M8 | R2-13 | Zavisnost bez verzije/identiteta prođe uvoz; nepoznat izvor ostane nevidljiv | validacija; `Unknown` source = risk signal | negativan + uvozni |
| M9 | R2-13 | Rizik zatvoren bez nove verzije i bez izuzetka; ponovljen uvoz duplira | invarijanta zatvaranja; idempotentnost | negativan; dvostruki uvoz |
| M10 | R2-13/R3-17 | Izuzetak odobrava podnosilac ili neovlašćen korisnik | permission + razdvajanje dužnosti | deny test |
| M11 | R3-17 | Release samo sa mutable tagom (`latest`) dobije allow; nedostatak dokaza = „bezbedno" | obavezan digest; nedostatak dokaza ≠ allow | negativan + unit |
| M12 | R3-17 | Snapshot izmenjen posle odluke; nov kritičan nalaz posle allow | nepromenljiv snapshot, odluka čuva `snapshotId` i `policyVersion`; ponovna evaluacija | reproducibilnost; allow → block |
| M13 | sve | Tajne/tokeni u logu, audit-u, seed-u ili DTO-u | audit i DTO bez tajni; seed samo izmišljeni podaci | provera audita; pregled seed-a |

## Bezbednosni zahtev i kontrola

- **Zahtev:** SEC-BASE-01..06, SEC-CONV-02/03/04/05/06/08, SEC-DEP-01..05; pravila R1-18, R2-13 i R3-17 iz specifikacije.
- **Kontrola:** serverska provera uloge + objekta/scope-a (deny-by-default); validacija ulaza na granici; domen koji ne dozvoljava nevalidna stanja
  (npr. `Temporary` bez roka, izmena `Closed`); adapteri koji nedostupnost upstream-a ne pretvaraju u uspeh.
- **Audit/evidence:** događaj sa poljima iz SEC-CONV-04 (vreme, actor, akcija, target, ishod, correlation ID, osetljivost), bez tajni.
  R1-18: registrovan, promenjena klasifikacija, potvrđen, zatvoren, promena izloženosti kritičnog servisa, odbijen pristup.
  R2-13: komponenta, uvoz (uspeh/neuspeh), vezan nalaz, plan, odobren izuzetak.
  R3-17: release, odluka (`policyVersion`, `snapshotId`), izuzetak, ponovna evaluacija, odbijen pristup build detaljima.

## Testovi

Svi testovi su u ovom trenutku **planirani** (obavezni do KT1/KT2); status se ažurira u PR-u koji ih uvodi.

- **Pozitivan scenario:** registracija i klasifikacija exposure-a ovlašćenog korisnika; ispravan uvoz zavisnosti; allow za release sa kompletnim dokazima.
- **Negativan/security scenario:** M1–M13 gore; prioritet za KT1: M1, M4, M5, M7.
- **Automatizovani test:** NUnit + Moq u [`tests/T08.Tests`](../tests/T08.Tests); svaki negativan test mora da pada kad se kontrola ukloni.

## Preostali rizik / ograničenja

- Do prihvatanja ugovora (`ACCEPTED`) adapteri rade nad stubovima; razlika stub ↔ stvarni servis može da otkrije nepodudaranje polja.
- Identitet i dozvole su stub dok T12/T02 ne objave ugovor; prava provera uloga zavisi od njih.
- Provenance i nalazi dolaze iz simulatora; ne dokazuju stvarnu bezbednost build-a.
- Kritičnost asset-a se čita preko adaptera u trenutku akcije; kasnija promena kritičnosti ne menja već upisan audit.
- Threat model je za KT1 početni; R2-13 i R3-17 redovi se dopunjuju kad se UC-ovi implementiraju.

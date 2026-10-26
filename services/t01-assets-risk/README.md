# T01 — Asset-i, ranjivosti i procena rizika

> **Status:** `active` — CI pokreće testove servisa (`tests` profil, NUnit) na svakom PR-u.
> Pravila strukture i konvencija: [README u korenu repozitorijuma](../../README.md).

## Odgovornost tima

Projektne celine:

- **R1-02** — Asset inventory i kritičnost resursa
- **R2-11** — Vulnerability registry i remediation
- **R3-03** — Risk register i procena bezbednosnog rizika

Tim: 6  ·  Stack: .NET 8 / ASP.NET Core / MySQL 8.4 / NUnit

T01 vodi inventar asset-a sa kritičnošću i vlasnikom, registar ranjivosti sa remediation tokom
i registar rizika; ne upravlja identitetima, domenima, dozvolama ni audit skladištem — to dobija
od T12, T11, T02 i T04 preko ugovora.

Struktura (Clean Architecture, zavisnosti samo ka unutra: Api → Infrastructure → Application → Domain):

```
Scutum.T01.sln
src/Scutum.T01.Domain          entiteti, statusi, invarijante (ne zavisi ni od čega)
src/Scutum.T01.Application     use-case-ovi i portovi
src/Scutum.T01.Infrastructure  persistence, klijenti drugih timova
src/Scutum.T01.Api             ASP.NET Core minimal API (/health)
tests/Scutum.T01.Tests         NUnit testovi
```

## Pokretanje

Linux / WSL / Git Bash:

```bash
bash scripts/scutum.sh network      # jednom po računaru
bash scripts/scutum.sh up t01          # api + baza
curl http://localhost:8001/health  # očekivano: OK
bash scripts/scutum.sh test t01        # isto što radi CI
bash scripts/scutum.sh down t01        # dodajte --volumes da obrišete podatke baze
```

Windows PowerShell / Command Prompt:

```powershell
scripts\scutum.bat network
scripts\scutum.bat up t01
curl http://localhost:8001/health
scripts\scutum.bat test t01
scripts\scutum.bat down t01
```

Preduslovi, izbor terminala i rešavanje problema opisani su u [README u korenu
repozitorijuma](../../README.md#preduslovi-i-brzi-početak-prvi-put-na-računaru).

| Šta | Gde |
| --- | --- |
| API (spolja) | `http://localhost:8001` (unutar mreže `scutum-net`: `http://t01-api:8080`) |
| UI / demo (opciono) | `http://localhost:8101` |
| Baza (samo za vaš alat) | `127.0.0.1:3401` |
| Konfiguracija | `.env` (iz `.env.example`), bez tajni u repozitorijumu |

Lokalno bez Docker-a (iz `services/t01-assets-risk/`, potreban .NET 8 SDK; `global.json` zaključava SDK na 8):

```bash
dotnet build
dotnet test
```

Migracije, seed/demo podaci i potrebne promenljive okruženja: stižu u KT2 (SEC-OPS-02, SEC-OPS-03).
Do tada servis ne koristi bazu; promenljive su u `.env.example`.

## Zavisnosti od drugih timova

Dok upstream servis ne postoji, koristimo stub sa normalnim i failure odgovorom; stub ne sadrži
security logiku drugog tima (SEC-DEP-01..05). Endpoint-i posle reči „Očekujemo“ su naš predlog minimalnog ugovora (DRAFT) dok upstream tim ne objavi svoj.

| Servis / ugovor | Owner | Kako ga koristimo | Stub u razvoju |
| --- | --- | --- | --- |
| R1-01 domeni (`T11_BASE_URL`) | T11 | R1-02 registracija: domen mora postojati i biti aktivan. Očekujemo `GET /api/v1/domains/{id}` → `{id, organizationId, status: Active\|Inactive}`, 404 ako ne postoji | da |
| R1-03 identiteti (`T12_BASE_URL`) | T12 | vlasnik asset-a (R1-02), remediation owner (R2-11), acceptedBy (R3-03). Očekujemo `GET /api/v1/identities/{id}` → `{id, status}`, 404 ako ne postoji | da |
| R1-05 dozvole | T02 | dozvole subjekta u domenu (`asset.write`, `finding.close`, `risk.accept`…) iz claim-ova JWT-a ili `GET /api/v1/subjects/{id}/permissions?domainId=` | da (test JWT) |
| R1-09 audit (`T04_BASE_URL`) | T04 | sve izmene u T01 šalju audit događaj sa poljima iz SEC-CONV-04. Očekujemo `POST /api/v1/events` | da (`InMemoryAuditSink`) |
| R1-16 katalog kontrola (`T05_BASE_URL`) | T05 | R3-03 povezivanje kontrole sa rizikom. Očekujemo `GET /api/v1/controls/{id}` → `{id, name, status}` | da |
| R1-12 simulatori | nema vlasnika, pravi T01 | R2-11 uvoz simuliranih nalaza iz `sim/findings.json` | sopstveni simulator |

## Ugovori koje ovaj servis nudi

Nijedan ugovor T01 još nije objavljen u [`contracts/REGISTRY.md`](../../contracts/REGISTRY.md).
Formalni DRAFT `C-ASSET-v1` (`contracts/t01/C-ASSET-v1/` + red u registru) stiže u posebnom PR-u.
Do tada consumer timovi ovu tabelu ne tretiraju kao dogovoren ugovor.

| Ugovor | Stanje | Planirani consumeri |
| --- | --- | --- |
| `C-ASSET-v1`: asset po ID-ju i pretraga po domenu/vlasniku | planiran, nije objavljen (DRAFT u posebnom PR-u) | T03 (R1-06), T08 (R1-18, R2-13, R3-17), T11 (R1-14), T15 (R1-10, R2-20), T28 (R2-09), T30 (R2-16) |
| `C-FINDING-v1`: nalazi iz R2-11 | planiran, nije objavljen (posle KT2) | T08 (R2-13, R3-17) |
| `C-RISK-v1`: rizici iz R3-03 | planiran, nije objavljen (posle KT2) | T14 (R3-08), T26 (R3-23) |

## Trust granica i bezbednost

- Threat model: [`docs/threat-model.md`](docs/threat-model.md)
- Odluke (ADR): [`docs/adr/`](../../docs/adr/) — fajlovi sa prefiksom `t01-`
- Demo scenario (normalan i negativan): [`docs/demo.md`](docs/demo.md)

## Poznata ograničenja i residual risk

<!-- Šta kontrola ne pokriva i zašto je rizik prihvatljiv ili otvoren. -->

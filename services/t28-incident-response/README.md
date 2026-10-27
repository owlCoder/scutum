# T28 — Incidenti, istrage, response

> **Status:** `active` — CI pokreće testove servisa (`tests` profil, NUnit) na svakom PR-u.
> Pravila strukture i konvencija: [README u korenu repozitorijuma](../../README.md).

## Odgovornost tima

Projektne celine:

- **R2-09** — Security incident management
- **R2-10** — Investigation, evidence i timeline
- **R3-06** — Automatizacija containment i response akcija

Tim:   ·  Stack: .NET 8 / ASP.NET Core / MySQL 8.4 / NUnit

Početni skelet servisa: sluša na 8080 i odgovara `OK` na `GET /health`. Use-case-ovi celina
još nisu implementirani — ostavljeni su prazni Clean Architecture slojevi za dalji rad.

```
Scutum.T28.sln
src/Scutum.T28.Domain
src/Scutum.T28.Application
src/Scutum.T28.Infrastructure
src/Scutum.T28.Api             /health
tests/Scutum.T28.Tests
```

## Pokretanje

Linux / WSL / Git Bash:

```bash
bash scripts/scutum.sh network
bash scripts/scutum.sh up t28
curl http://localhost:8028/health
bash scripts/scutum.sh test t28
bash scripts/scutum.sh down t28
```

Windows PowerShell / Command Prompt:

```powershell
scripts\scutum.bat network
scripts\scutum.bat up t28
curl http://localhost:8028/health
scripts\scutum.bat test t28
scripts\scutum.bat down t28
```

Preduslovi, izbor terminala i rešavanje problema opisani su u [README u korenu
repozitorijuma](../../README.md#preduslovi-i-brzi-početak-prvi-put-na-računaru).

| Šta | Gde |
| --- | --- |
| API (spolja) | `http://localhost:8028` (unutar mreže `scutum-net`: `http://t28-api:8080`) |
| UI / demo (opciono) | `http://localhost:8128` |
| Baza (samo za vaš alat) | `127.0.0.1:3428` |
| Konfiguracija | `.env` (iz `.env.example`), bez tajni u repozitorijumu |

Lokalno bez Docker-a (iz `services/t28-incident-response/`, potreban .NET 8 SDK):

```bash
dotnet build
dotnet test
```

Migracije, seed/demo podaci i potrebne promenljive okruženja: stižu kasnije (SEC-OPS-02, SEC-OPS-03).
Do tada servis ne koristi bazu.

## Zavisnosti od drugih timova

| Servis / ugovor | Owner | Kako ga koristimo | Stub u razvoju |
| --- | --- | --- | --- |
| <!-- npr. C-AUDIT-v1 --> | <!-- T04 --> | <!-- env varijabla, endpoint --> | <!-- da / ne --> |

## Ugovori koje ovaj servis nudi

Upisani su i u [`contracts/REGISTRY.md`](../../contracts/REGISTRY.md).

| Ugovor | Stanje | Consumeri |
| --- | --- | --- |
| <!-- npr. C-INCIDENT-v1 --> | <!-- DRAFT --> | <!-- T07, T04 --> |

## Trust granica i bezbednost

- Threat model: [`docs/threat-model.md`](docs/threat-model.md)
- Odluke (ADR): [`docs/adr/`](../../docs/adr/) — fajlovi sa prefiksom `t28-`
- Demo scenario (normalan i negativan): [`docs/demo.md`](docs/demo.md)

## Poznata ograničenja i residual risk

Skelet izlaže samo `GET /health`. Incident, evidence i response još nisu implementirani.

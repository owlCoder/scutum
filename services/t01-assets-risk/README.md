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

| Servis / ugovor | Owner | Kako ga koristimo | Stub u razvoju |
| --- | --- | --- | --- |
| <!-- npr. C-AUDIT-v1 --> | <!-- T04 --> | <!-- env varijabla, endpoint --> | <!-- da / ne --> |

## Ugovori koje ovaj servis nudi

Upisani su i u [`contracts/REGISTRY.md`](../../contracts/REGISTRY.md).

| Ugovor | Stanje | Consumeri |
| --- | --- | --- |
| <!-- npr. C-TELEMETRY-v1 --> | <!-- DRAFT --> | <!-- T07, T04 --> |

## Trust granica i bezbednost

- Threat model: [`docs/threat-model.md`](docs/threat-model.md)
- Odluke (ADR): [`docs/adr/`](../../docs/adr/) — fajlovi sa prefiksom `t01-`
- Demo scenario (normalan i negativan): [`docs/demo.md`](docs/demo.md)

## Poznata ograničenja i residual risk

<!-- Šta kontrola ne pokriva i zašto je rizik prihvatljiv ili otvoren. -->

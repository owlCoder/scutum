# T10 — Log izvori, telemetry ingestion i detection tuning

> **Status:** `active` — CI pokreće `tests` profil (`t10-tests-api` i `t10-tests-ui`) na svakom PR-u.
> Kostur je postavljen; funkcionalnosti celina se dodaju po Tapiz karticama.
> Pravila strukture i konvencija: [README u korenu repozitorijuma](../../README.md).

## Odgovornost tima

Projektne celine:

- **R1-19** — Security data-source i log-source registry
- **R2-17** — Security telemetry ingestion i normalizacija
- **R3-15** — Detection engineering, simulacija i tuning pravila

Tim: 1  ·  Stack: .NET 10 + EF Core + PostgreSQL 17 (backend); Next.js 16 (frontend)

## Struktura

```text
Scutum.T10.slnx          jedan .NET solution za backend i njegove testove
global.json              .NET SDK 10.0.x
Directory.*.props        zajednička podešavanja i verzije NuGet paketa
dotnet-tools.json        lokalni alati (dotnet-ef): `dotnet tool restore`
src/
  backend/               .NET 10 Web API — raspored i pravila: src/backend/README.md
  frontend/              Next.js aplikacija — samostalan npm projekat sa svojim Dockerfile-om i testovima
tests/
  backend/               .NET test projekti (UnitTests, IntegrationTests)
docs/                    threat model, demo scenario
```

Frontend testovi (Vitest, Playwright) su namerno u `src/frontend/`, a ne u `tests/`: alati traže `node_modules`
od mesta gde je test fajl, pa testovi van npm projekta ne bi našli `react` i `next`.

<!-- Jednom rečenicom: šta ovaj servis radi i šta NE radi (granica odgovornosti). -->

## Pokretanje

Linux / WSL / Git Bash:

```bash
bash scripts/scutum.sh network      # jednom po računaru
bash scripts/scutum.sh up t10          # api + ui + baza (PostgreSQL)
curl http://localhost:8010/health  # očekivano: OK
bash scripts/scutum.sh test t10        # isto što radi CI
bash scripts/scutum.sh down t10        # dodajte --volumes da obrišete podatke baze
```

Windows PowerShell / Command Prompt:

```powershell
scripts\scutum.bat network
scripts\scutum.bat up t10
curl http://localhost:8010/health
scripts\scutum.bat test t10
scripts\scutum.bat down t10
```

Preduslovi, izbor terminala i rešavanje problema opisani su u [README u korenu
repozitorijuma](../../README.md#preduslovi-i-brzi-početak-prvi-put-na-računaru).

| Šta | Gde |
| --- | --- |
| API (spolja) | `http://localhost:8010` (unutar mreže `scutum-net`: `http://t10-api:8080`) |
| UI | `http://localhost:8110` (`/health` vraća `{"status":"ok"}`) |
| Baza (samo za vaš alat) | `127.0.0.1:3410` |
| Konfiguracija | `.env` (iz `.env.example`), bez tajni u repozitorijumu |

Ako ste `.env` napravili pre nego što je dodat frontend, obrišite ga da ga `up` ponovo napravi iz `.env.example`
(potrebni su `UI_PORT` i `T10_API_BASE_URL`).

### Backend lokalno (hot reload)

Potreban je .NET SDK 10. Baza radi u Docker-u, a API lokalno. Komande se kucaju iz `services/t10-telemetry-detection/`:

```bash
docker compose up -d t10-db                       # samo PostgreSQL na 127.0.0.1:3410
dotnet tool restore                               # jednom: lokalni dotnet-ef
dotnet user-secrets set "ConnectionStrings:T10" \
  "Host=localhost;Port=3410;Database=t10_db;Username=t10_app;Password=dev-only-app-change-me" \
  --project src/backend/Scutum.T10.Api           # jednom; lozinka ostaje van repoa
dotnet watch --project src/backend/Scutum.T10.Api # http://localhost:8010 (/health, /openapi/v1.json)
dotnet test Scutum.T10.slnx                       # svi backend testovi
```

Migracije (EF Core, u `src/backend/Scutum.T10.Data/Migrations/`):

```bash
dotnet ef migrations add <ImeMigracije> -p src/backend/Scutum.T10.Data -s src/backend/Scutum.T10.Api
dotnet ef database update -p src/backend/Scutum.T10.Data -s src/backend/Scutum.T10.Api
```

Kada backend doda nove endpointe, tipovi za frontend se osvežavaju sa `npm run gen:api` iz `src/frontend/`
(API mora da radi na 8010).

### Frontend lokalno (hot reload)

Potrebni su Node.js 24 i npm. Iz `src/frontend/`:

```bash
npm ci                      # prvi put i posle izmene package-lock.json
npm run dev                 # http://localhost:8110 (T10_API_BASE_URL podrazumevano nije postavljen)
npm run lint                # ESLint
npm run typecheck           # tipovi ruta + TypeScript
npm test                    # Vitest (isto što radi t10-tests-ui)
npm run e2e                 # Playwright; prvi put: npx playwright install chromium
npm run gen:api             # tipovi API klijenta iz OpenAPI-ja t10-api (kada backend radi na 8010)
```

Pravila frontenda: token i adresa API-ja postoje samo na serveru (`import "server-only"` u `src/lib/api` i
`src/lib/auth`), poslovna logika je u `src/features/<modul>/`, a `src/app/` samo slaže stranice.
Autorizaciju uvek donosi API; UI sakriva dugmad samo radi preglednosti.

Migracije, seed/demo podaci i potrebne promenljive okruženja: <!-- opisati gde su i kako se pokreću (SEC-OPS-02, SEC-OPS-03) -->

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
- Odluke (ADR): [`docs/adr/`](../../docs/adr/) — fajlovi sa prefiksom `t10-`
- Demo scenario (normalan i negativan): [`docs/demo.md`](docs/demo.md)

## Poznata ograničenja i residual risk

- `npm audit` prijavljuje ranjivost u paketu `braces` (preko `eslint-config-next` → `fast-glob`). Paket se koristi
  samo za lint pri razvoju i ne ulazi u build aplikacije; ispravka koju nudi `npm audit` vraća Next.js na verziju 14.
  Pratimo novu verziju `eslint-config-next`.
- Content-Security-Policy još nije uključen; dolazi sa nonce-om u `proxy.ts`, zajedno sa autentikacijom.

# T10 backend

.NET 10 Web API sa EF Core i PostgreSQL-om. Pokretanje, migracije i testovi su u [README-u servisa](../../README.md#backend-lokalno-hot-reload).

## Projekti i smer zavisnosti

```text
Scutum.T10.Api  →  Scutum.T10.Services  →  Scutum.T10.Data  →  Scutum.T10.Domain
```

Projekat sme da koristi samo projekte desno od sebe. `ProjectDependencyRulesTests` u UnitTests pada ako se pravilo prekrši
(npr. Domain počne da koristi EF Core).

| Projekat | Šta ide u njega | Šta NE ide |
| --- | --- | --- |
| `Scutum.T10.Domain` | entiteti, enumi i statusi, poslovna pravila i invarijante, greške sa stabilnim kodom | EF Core, HTTP, DTO-i |
| `Scutum.T10.Data` | `T10DbContext`, `Configurations/<Celina>/` (IEntityTypeConfiguration), `Migrations/`, `Seed/`, `External/` (klijenti ka drugim timovima iza interfejsa) | poslovna pravila, provera dozvola |
| `Scutum.T10.Services` | servis po oblasti (npr. `SourceService`), DTO-i zahteva i odgovora, provera scope-a i dozvola, upis audit događaja | HTTP detalji (status kodovi, rute) |
| `Scutum.T10.Api` | `Program.cs`, `Endpoints/<Celina>/` (minimal API), `Auth/` (JWT, trenutni korisnik), `Errors/` (ProblemDetails sa `code`) | upiti nad bazom, poslovna pravila |

## Folderi po celinama

U svakom projektu postoje isti folderi, pa svaki podtim radi u svojim fajlovima:

| Folder | Celina |
| --- | --- |
| `Sources/` | R1-19 — registar security i log izvora |
| `Ingestion/` | R2-17 — prijem i normalizacija telemetrije |
| `Detection/` | R3-15 — detection engineering i tuning |
| `Common/` | zajedničko za više celina (samo kada je zaista zajedničko) |

Testovi prate isti raspored: `tests/backend/Scutum.T10.UnitTests/<Celina>/` i `.../IntegrationTests/<Celina>/`.

## Dogovori

- Konfiguracija entiteta ide u zasebnu `IEntityTypeConfiguration<T>` klasu u `Data/Configurations/<Celina>/`;
  `T10DbContext` ih učitava sam. Imena tabela i kolona su snake_case (`EFCore.NamingConventions`).
- Jedna migracija po promeni modela, sa opisnim imenom (`AddSecurityDataSource`), u istom PR-u kao i promena.
- Vreme se dobija preko `TimeProvider` (ubacuje se kroz DI), ne preko `DateTime.UtcNow`, da bi testovi bili deterministički.
- Tajne (lozinke, tokeni) nikad u `appsettings*.json`: lokalno `dotnet user-secrets`, u compose-u promenljive iz `.env`.
- Autorizacija se proverava u Services, na svakoj operaciji; tuđ resurs vraća isti odgovor kao nepostojeći.

# T03 — Object-level i API autorizacija i policy engine

Obuhvat: R1-06, R2-12, R3-01. Ovo je korigovani KT1 paket, sa izvršivim Read tokom.
Postojeća pravila vlasništva/scope-a i tri originalna testa su sačuvani.
Uz devet slučajeva u `ReadResourceBoundaryTests`, dodata su još dva outage
regresiona slučaja u `ReadResourceOutageTests` (ukupno 14 u test source-u).
Prekid asset ili permission servisa javno daje isti `Denied / RESOURCE_UNAVAILABLE`
kao nepostojeći ili tuđi resurs. Interni audit razlikuje `ASSET_UNAVAILABLE`,
`PERMISSIONS_UNAVAILABLE` i `RESOURCE_NOT_FOUND`. Status izvršavanja je u validaciji.
Update/ChangeOwner write tok, CAS i novi owner validation su PLAN KT2.
R2-12 i R3-01 su planirane celine, ne implementirani API/policy engine.
Asistent je prihvatio Read-only demonstraciju kao parcijalni KT1 dokaz;
konačna celina R1-06 time nije završena.

## Pokretanje bez Docker-a

Potreban je .NET 8 SDK; Python 3 samo za statičku proveru.
Iz ovog foldera:

```sh
dotnet restore Scutum.T03.sln
dotnet build Scutum.T03.sln --no-restore --configuration Release
dotnet test Scutum.T03.sln --no-build --configuration Release --logger "trx;LogFileName=security.trx" --results-directory TestResults
dotnet run --project src/Scutum.T03.ConsoleUi --configuration Release --no-build
python scripts/check_source.py
```

Na Windows-u otvoriti `Scutum.T03.sln` iz ovog foldera; komande iznad rade i u PowerShell-u.
Na Linux-u/Git Bash-u sve provere može pokrenuti `bash scripts/verify.sh`.

## Zajednički CI na main grani

Iz korena repozitorijuma, sa Docker Compose v2.24+:

```sh
bash scripts/scutum.sh test t03
```

Windows: `scripts\scutum.bat test t03`.
`status: active` omogućava globalnom CI-u da otkrije T03 izmene i pokrene dva test servisa:
`t03-tests` (restore/build/NUnit/demo) i `t03-tests-static` (Python provera).
TRX se upisuje u `TestResults/` i globalni CI ga čuva kao artefakt.
PR #2 je već merge-ovan. Ova grana koristi workflow i zajedničke skripte
iz svog `main` osnova i ne menja ih u okviru T03 PR-a.

KT1 koristi InMemoryStore i deterministički seed; baza i migracije nisu potrebne.
Rezervisani portovi iz zajedničke strukture ostaju 8003/8103/3403.
`t03-api` je zadržani infrastrukturni health scaffold: GET /health vraća OK,
ali NE predstavlja implementirani authorization HTTP API. Read se trenutno poziva
kroz aplikacioni handler, konzolni demo i NUnit. Docker testovi ne zavise od health
scaffold-a, baze niti živog servisa drugog tima.

## Dokumentacija

- [KT1](../../docs/KT1.md): stanje, plan, security matrica i obuhvat.
- [DRAFT ugovori](../../docs/UGOVORI.md): postojeći portovi i planirani contract testovi.
- [Ispravke i integracija](../../docs/T03-REVIEW-FIXES.md).
- [Threat model](docs/threat-model.md), [demo](docs/demo.md), [validacija](docs/VALIDACIJA.md).

Pre merge-a: dobiti uspešan globalni CI na konačnoj čistoj grani, sačuvati
NUnit TRX i povezati novi PR sa starim PR-ovima #11 i #1 koje zamenjuje.

# Status validacije — 21.10.2026.

| Provera | Rezultat |
| --- | --- |
| T03 Python provera strukture, ProjectReference i osnovnih obrazaca tajni | PASS |
| Sačuvan postojeći C# source i originalna tri testa | PASS: poređenje bajtova sa dostavljenom arhivom |
| Globalni CI i zajedničke skripte odgovaraju PR-u #2 | PASS: poređenje bajtova |
| Shell sintaksa | PASS: bash -n |
| Novi NUnit slučajevi | 9 dodatih; ukupno 12, izvršavanje nije potvrđeno |
| Novi .NET build/NUnit/demo | BLOKIRANO okruženjem pre build-a |
| Docker Compose/globalni CI | Nije izvršeno: Docker CLI nije dostupan |

.NET SDK 8.0.426 je pribavljen, ali `dotnet --info` pada sa
`System.ComponentModel.Win32Exception (2)` u `System.Diagnostics.Process.GetStat()`.
To nije rezultat neuspešnog testa; novi testovi ovde nisu ni pokrenuti.
Prema korisničkom review komentaru, build i tri originalna NUnit testa prošli su
u ranijem GitHub Actions run-u. To ne potvrđuje ovu novu reviziju.

## Reprodukcija

Iz korena repozitorijuma uz Docker Compose v2.24+:

```sh
bash scripts/scutum.sh test t03
```

Ili lokalno iz `services/t03-authorization/` uz .NET 8 SDK:

```sh
dotnet restore Scutum.T03.sln
dotnet build Scutum.T03.sln --configuration Release --no-restore
dotnet test Scutum.T03.sln --configuration Release --no-build --logger "trx;LogFileName=security.trx" --results-directory TestResults
dotnet run --project src/Scutum.T03.ConsoleUi --configuration Release --no-build
python scripts/check_source.py
```

Očekivanje: 12 uspešnih NUnit slučajeva i `DEMO PASS: 2/2 očekivana ishoda.`
Ovo je očekivanje za proveru, ne tvrdnja da je dobijen takav rezultat.
Sačuvati stvarni TRX i CI run na završnoj grani pre merge-a.

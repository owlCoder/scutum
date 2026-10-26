# T03 — evidencija validacije

## Potvrđen GitHub Actions rezultat za 3c00d0f

- Commit: `3c00d0f3296e4bda86b1dc3256243ad6ad88e507`.
- [Uspešan Actions run 40244326382](https://github.com/owlCoder/scutum/actions/runs/40244326382).
- [tests (t03) — log](https://github.com/owlCoder/scutum/actions/runs/40244326382/job/121322615708).
- [Artefakt test-results-t03](https://github.com/owlCoder/scutum/actions/runs/40244326382/artifacts/12617464688).

Potvrđeni su structure, secrets, selftest (scripts), detect changed services,
tests (t03) i ci-ok. Log/review potvrđuje 12/12 NUnit slučajeva, demo 2/2 i
statičku proveru. Artefakt postoji i nije istekao pri proveri 26.10.2026.
Ovo je dokaz prethodne revizije, pre ispravke outage regresije.

## Outage ispravka — lokalni dokaz, 26.10.2026.

Handler sada javno vraća `Denied / RESOURCE_UNAVAILABLE`, sa null resursom,
i tokom prekida asset ili permission servisa. Interni audit zadržava konkretan
razlog. Dodata su dva regresiona slučaja; source sada sadrži ukupno 14 slučajeva.

U okruženju za pripremu patch-a stvarno su izvršene sledeće provere:

| Provera | Rezultat |
| --- | --- |
| Novi outage slučajevi sa handlerom iz 3c00d0f | 2 izvršena, 2 pala; reprodukovan različit javni odgovor pri asset outage-u |
| Svi NUnit slučajevi sa ispravljenim handlerom | 14 izvršenih, 14 prošlo, 0 failed/skipped, exit 0 |
| Kompilacija testiranog C# source-a | Roslyn iz SDK 8.0.427, C# 12, nullable i warnings-as-errors; uspešna |

NUnit slučajevi su pokrenuti kroz privremeni NUnitLite runner sa NUnit/NUnitLite
paketima 4.6.1 na .NET runtime-u 8.0.33. Runner i dodatne zavisnosti nisu dodati
projektu. Standardni dotnet CLI/MSBuild u ovom okruženju padaju pri čitanju
procesnih metapodataka pre build-a; Docker CLI nije dostupan. Zato lokalni
NUnitLite prolaz nije predstavljen kao novi solution/Docker/Actions prolaz.

Stvarni lokalni NUnit XML dokazi su `outage-before.xml` i `outage-fixed.xml` u
`TestResults/`; folder je ignorisan u Git-u. Standardni Docker/CI put nastavlja
da proizvodi TRX i ostaje obavezan pre merge-a.

## Nova revizija — obavezna Docker i CI provera

Iz korena repozitorijuma:

```sh
bash scripts/scutum.sh test t03
```

U korisnikovom PowerShell okruženju koje već ima Git Bash:

```powershell
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/scutum.sh test t03
```

Proveriti da `t03-tests` prijavi 14/14 NUnit slučajeva i demo 2/2, a
`t03-tests-static` PASS. TRX treba da bude zapisan u
`services/t03-authorization/TestResults/security.trx`.

Posle push-a u istu granu `fix/t03-clean`, PR #12 treba da dobije novi Actions
run. Potrebni su uspešni structure, secrets, selftest (scripts), tests (t03) i
ci-ok, uz novi artefakt `test-results-t03`. Novi rezultat još nije potvrđen;
dopuniti ovu evidenciju i PR opis direktnim linkovima na novi run/artefakt.

## Obuhvat

Read-only demonstracija je prihvaćen parcijalni KT1 dokaz.
HTTP /health je infrastrukturni stub, a ne authorization endpoint.
Update, ChangeOwner i atomski write/CAS ostaju plan KT2.

## Potvrđen lokalni Docker prolaz — 26.10.2026.

Posle primene outage ispravke uspešno je izvršen scripts/scutum.sh test t03:
- t03-tests: 14/14 NUnit slučajeva, failed 0, skipped 0.
- Konzolni demo: 2/2 očekivana ishoda.
- t03-tests-static: PASS.
- Novi TRX: services/t03-authorization/TestResults/security.trx.

Prvi pokušaj je prekinut zbog zaključanog Compose metadata fajla.
Ponovljeno pokretanje je uspelo, uz COMPOSE_BAKE=false u terminalu.
Novi GitHub Actions rezultat za ovu ispravku još treba potvrditi.

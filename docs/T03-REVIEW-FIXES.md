# T03 — ispravke nakon review-a

## Prihvaćeni KT1 obuhvat

Asistent je prihvatio Read-only demonstraciju kao parcijalni KT1 dokaz.
Implementirani su `ResourceHandlers.Read`, deny-by-default ownership/scope
provera, lokalni audit, konzolni demo, DRAFT portovi/stubovi i NUnit testovi.
To nije završena celina R1-06 niti implementiran HTTP authorization API.
`t03-api` i njegov `/health` ostaju infrastrukturni BusyBox scaffold.
Update, ChangeOwner, provera novog owner-a i atomski write/CAS + audit su plan KT2.
R2-12 i R3-01 ostaju planirane celine.

## Čista integraciona grana

PR #2 je već merge-ovan. `fix/t03-clean` je 25.10.2026. kreirana iz preuzetog
`origin/main`, na commit-u `f6d7618`, a ne nastavkom stare istorije PR-a #11.
Iz `origin/fix/t03-review` preneti su samo `services/t03-authorization/`
i tri T03 dokumenta: `docs/KT1.md`, `docs/UGOVORI.md` i ovaj dokument.
Dokumentacija je zatim ažurirana prema novom review-u.

Zajednički `.github/workflows/ci.yml`, `scripts/*`, T01 i ostali timovi nisu
predmet T03 promene. Nova grana nasleđuje njihovo stanje iz `main`, uključujući
aktivnu T01 implementaciju, umesto starog scaffold snapshot-a.

Novi PR treba povezati sa PR-ovima #11 i #1 koje ovaj rad zamenjuje. Njihovo
zatvaranje/usaglašavanje ostaje korak koordinacije; ovde se ne tvrdi da su zatvoreni.

## Promene i stvarni dokazi

- `docs/KT1.md` razdvaja implementirani Read od plana za Update/ChangeOwner.
  Ne navodi nepostojeće write/CAS handlere ili testove kao realizovane dokaze.
- `docs/UGOVORI.md` označava portove i fixture contract testove kao DRAFT/plan,
  bez izmišljenog ACCEPTED statusa ili tvrdnje o udaljenoj integraciji.
- T03 solution, pet projekata, testovi i lokalne skripte nalaze se unutar servisa.
- Tri originalna NUnit slučaja su sačuvana. Dodato je devet graničnih slučajeva
  kroz sedam metoda: subject scope, dva grant scope slučaja, pozitivan ReadAny,
  neaktivan asset, neaktivan subjekt, dve nedostupne zavisnosti i jednak javni DTO.
- `status: active` omogućava detekciju T03 promena. Zajednički
  `scutum.sh test t03` treba da izvrši oba test servisa: `t03-tests`
  (.NET build/NUnit/demo) i `t03-tests-static` (Python provera).

## Validacija i uslov za merge

Lokalni PowerShell izlaz od 25.10.2026. pokazuje uspešan build i 12/12 testova
pre kreiranja čiste grane. To nije dokaz CI-a na novoj grani.
Status i ponovljive komande su u `services/t03-authorization/docs/VALIDACIJA.md`.

Pre merge-a potrebni su stvarni TRX rezultat, proverena oba Compose test servisa
i uspešan GitHub Actions run na konačnoj reviziji: `structure`, `secrets`,
`selftest (scripts)`, `tests (t03)` i `ci-ok`. Novi PR opis mora sadržati
ograničenja KT1 obuhvata i link do stvarnog CI run-a/artefakta.

## Primedba na rezervisane Compose portove

Proširenje provere published portova u zajedničkom
`scripts/ci/compose_policy.py` ostaje za zaseban infrastrukturni PR.
Ovaj T03 PR ne menja zajednički validator. T03 Compose koristi rezervisani
API port iz `.env.example` (`8003`) i vezuje ga samo na `127.0.0.1`;
UI i baza nisu pokrenuti u ovom InMemory KT1 toku. To ne otklanja opštu
primedbu na validator niti predstavlja dokaz njenog rešavanja.

# T03 — evidencija validacije

## Lokalna Docker provera — 26.10.2026.

Izvršena je zajednička skripta `scripts/scutum.sh test t03`
kroz Git Bash, u radnom stablu grane `fix/t03-clean`.

Stvarni rezultati:
- t03-tests: build uspešan; NUnit ukupno 12, passed 12, failed 0, skipped 0.
- Konzolni demo: DEMO PASS: 2/2 očekivana ishoda.
- t03-tests-static: PASS za strukturu, reference i osnovnu proveru tajni.
- NUnit TRX: services/t03-authorization/TestResults/security.trx.

Provera je izvršena pre commita. Konačni commit treba potvrditi kroz PR CI.
TestResults je ignorisan u Git-u; CI treba da sačuva TRX kao artefakt.

## GitHub Actions — još nije potvrđen

Pre merge-a potreban je uspešan run na konačnoj reviziji:
structure, secrets, selftest (scripts), tests (t03) i ci-ok.

U PR dodati link do stvarnog run-a i artefakta test-results-t03.
Lokalni prolaz nije zamena za GitHub Actions rezultat.

## Obuhvat

Read-only demonstracija je prihvaćen parcijalni KT1 dokaz.
HTTP /health je infrastrukturni stub, a ne authorization endpoint.
Update, ChangeOwner i atomski write/CAS ostaju plan KT2.

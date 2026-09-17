# Doprinos projektu SCUTUM

## Tok rada

1. Preuzmite zadatak dodeljen vašem timu u Tapiz Boards-u.
2. Ažurirajte lokalni `main` i napravite granu formata
   `feature/<celina>-<kratak-opis>` (npr. `feature/R1-05-rbac`).
3. Radite u manjim, smislenim commit-ovima.
4. Pokrenite relevantne build i test komande lokalno.
5. Otvorite Pull Request prema `main`, popunite PR šablon i povežite zadatak.
6. Odgovorite na review komentare; merge radi ovlašćeni član nakon odobrenja i
   uspešnog CI-ja.

## Bezbednosni zahtevi za svaki tim

Za dodeljenu celinu dokumentujte:

- asset i trust granicu;
- threat ili misuse scenario;
- bezbednosni zahtev i implementiranu kontrolu;
- najmanje jedan automatizovan negativni/security test;
- audit ili security događaj, kada je relevantno;
- preostali rizik ili poznato ograničenje.

Za značajne arhitektonske odluke dodajte kratak ADR u `docs/adr/` koristeći
[šablon](docs/templates/adr.md). Threat model čuvajte uz celinu ili u
`docs/threat-models/`, koristeći [šablon](docs/templates/threat-model.md).

## Pull Request pravila

- Nemojte direktno pushovati na `main`.
- PR treba da bude fokusiran na jednu celinu ili jasno odvojenu epiku.
- Promena auth/authz ugovora zahteva review timova koje promena pogađa.
- CI mora proći pre merge-a.
- Ne rešavajte review diskusiju bez dogovora s autorom komentara.

## Tajne i testni podaci

Nikada ne commitujte stvarne lozinke, API tokene, privatne ključeve ili
produkcione sertifikate. Koristite `.env.example`, testne vrednosti i simulatore.
Ako je tajna slučajno commitovana, odmah obavestite nastavni tim; nemojte je
samo obrisati u sledećem commit-u.

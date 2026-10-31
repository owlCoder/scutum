# ACC-05 i ACC-08 — komponenta autorizacije

| Stavka | Vrednost |
|---|---|
| Tim | Tim 3 |
| Zahtevi | ACC-05, ACC-08 |
| Status | Početna implementacija |
| Projekat | `backend/Platform.Access` |
| Studenti | PR 1/2023 Živanović Jovana<br>PR 16/2023 Musli Danijel<br>PR 18/2023 Spasić Veljko<br>PR 30/2023 Pavlović Sara<br>PR 32/2023 Stanković Pavle<br>PR 53/2023 Matić Željana<br>PR 60/2023 Todorović Nikolina |

## Dijagram toka podataka

```mermaid
flowchart LR
  U[Student / nastavnik / administrator]
  subgraph CLIENT[Granica poverenja: klijent]
    U -->|HTTP zahtev| API[Platform.Api]
  end
  subgraph SERVER[Granica poverenja: serverska platforma]
    API -->|server-side podaci| P[Proces odluke]
    P --> C[(Katalog akcija)]
    P --> R[(Registar politika)]
    P --> A[(Audit adapter)]
    P -->|Allow / Deny| API
  end
  API -->|generički rezultat| U
  API -.->|uloga/identitet iz zahteva se ignorišu| IGN[Klijentski header/body/query]
```
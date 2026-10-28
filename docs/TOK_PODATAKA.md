# Dijagram toka podataka — primer na nivou platforme

Ovo je samo početni, konceptualni primer toka. **KT1 zahteva zaseban DFD za deo svakog studenta**, sa stvarnim procesima, akterima i skladištima tog dela.

```mermaid
flowchart LR
  Student[Student] -->|Zahtev za vežbu| API[API + serverska provera dozvole]
  API -->|Provera konteksta| Access[Identitet i autorizacija]
  Access -->|Allow / Deny| API
  API -->|Dozvoljena revizija| Catalog[(Objavljene revizije)]
  API -->|Čuvanje odgovora| Attempt[(Pokušaji)]
  Attempt -->|Snapshot odgovora| Eval[Evaluacija]
  Eval -->|Ishod i feedback| Result[(Istorija rezultata)]
  API -->|Bezbednosni događaj| Audit[(Audit log)]
  Result -->|Dozvoljen prikaz| Student
```

## Šta je obavezno označiti u sopstvenom DFD-u

1. Ulazne i izlazne podatke, spoljne aktere i inicijatore zahteva.
2. Procese koji obrađuju podatke i skladišta kojima pristupaju.
3. Granice poverenja i tačke serverske provere autorizacije.
4. Negativan tok (odbijen zahtev / greška) i postupanje sa osetljivim podacima.
5. Šta je već implementirano, a šta je planirano.

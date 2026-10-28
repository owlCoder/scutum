# Projektna celina: [ID, naziv]

**Tim:** [broj]  
**Student:** [ime / indeks]  
**Zahtevi nove specifikacije:** [npr. LAB-01, ATT-03]

## Odgovornost i granice

- Obuhvaćeni procesi i korisnički scenariji:
- Ugovori prema drugim modulima:
- Stanje implementacije:

## Dijagram arhitekture dela

```mermaid
flowchart LR
  A[Ulazni API] --> B[Modul / servis]
  B --> C[(Podaci modula)]
```

Zameniti primer stvarnom arhitekturom.

## Dijagram toka podataka (DFD)

```mermaid
flowchart LR
  U[Spoljni akter] -->|Zahtev| P[Proces]
  P -->|Upis / čitanje| D[(Skladište)]
  P -->|Rezultat ili odbijanje| U
```

Zameniti primer stvarnim tokovima, uključujući granice poverenja.

## Bezbednosni uslovi

- Provere server-side pristupa:
- Validacija ulaza i osetljivi podaci:
- Negativni scenariji:

## Dokazi rada

- Izmenjeni fajlovi / commit / PR:
- Način pokretanja:
- Testovi i rezultat:
- Sledeći koraci:

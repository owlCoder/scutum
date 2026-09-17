# SCUTUM

> Platforma za upravljanje informacionom bezbednošću i digitalnim poverenjem.

SCUTUM je zajednički projekat za predmet **Osnove informacione bezbednosti**
(Fakultet tehničkih nauka – Primenjeno softversko inženjerstvo). Kroz timski
razvoj gradimo bezbednosnu platformu za upravljanje identitetima, pristupom,
politikama, tajnama, bezbednosnim događajima, incidentima, rizicima i dokazima.

Projekat je **isključivo defanzivnog karaktera**. Sumnjive aktivnosti i
nedozvoljeni pristupi reprodukuju se samo nad kontrolisanim simulatorima i
testnim identitetima.

## Početak rada

1. Pročitajte [kompletnu specifikaciju](docs/SCUTUM_OIB_Projektna_specifikacija.md).
2. Nastavni tim dodeljuje projektnu celinu (R1, R2 ili R3) i zadatak u Tapiz
   Boards-u.
3. Napravite feature granu iz `main`:

   ```bash
   git checkout main
   git pull
   git checkout -b feature/R1-01-kratak-opis
   ```

4. Implementirajte funkcionalnost, testove i potrebnu dokumentaciju.
5. Otvorite Pull Request prema `main` i povežite ga sa zadatkom.

Detalji procesa su u [CONTRIBUTING.md](CONTRIBUTING.md).

## Pravila rada

- Direktan push na `main` nije dozvoljen.
- Svaka promena ide kroz Pull Request i code review.
- Ne commitujte lozinke, tokene, sertifikate ni druge tajne.
- Autorizacija se proverava na serverskoj strani; UI ograničenja nisu dovoljna.
- Za bezbednosno relevantnu celinu obavezni su threat/misuse scenario,
  automatizovan negativni test i audit dokaz gde je relevantan.
- Ne implementirati sopstvene kriptografske algoritme; koristite proverene
  biblioteke i testne/simulirane kredencijale.

## Razvojni nivoi

| Nivo | Fokus |
| --- | --- |
| **R1 – Osnovni** | identiteti, resursi, klasifikacija, RBAC/autorizacija, politike i audit |
| **R2 – Operativni** | MFA, sesije, tajne, detekcija, incidenti i ranjivosti |
| **R3 – Napredni** | policy engine, access review, rizik, korelacija i automatizovan response |

Spisak svih celina i njihovih preduslova nalazi se u poglavlju 6 specifikacije.
Oznake R1/R2/R3 označavaju funkcionalne preduslove, a ne akademsku godinu.
Teme dodeljuje nastavni tim.

## Definition of Done

Pre nego što PR bude spreman za spajanje, tim treba da može da pokaže lanac:

```text
Asset → Threat / misuse → Security requirement → Control → Security test → Evidence
```

Minimalno se očekuju ispunjeni acceptance kriterijumi, automatizovani testovi,
jedan negativni bezbednosni scenario, ažurirana dokumentacija/ADR kada je
potrebno, uspešan CI i odobren review.

## Predložena struktura projekta

Arhitektura treba da jasno razdvaja domen, use-case sloj i infrastrukturu
(Clean Architecture ili ekvivalent). Referentna tehnologija je **.NET / C#**;
drugu tehnologiju odobrava nastavni tim uz dokaz interoperabilnosti i
ekvivalentnog nivoa testiranja.

```text
src/            aplikacioni kod
tests/          unit, integration i security testovi
docs/           specifikacije, ADR-ovi i threat modeli
.github/        CI i GitHub šabloni
```

## Korisne veze

- [Projektna specifikacija](docs/SCUTUM_OIB_Projektna_specifikacija.md)
- [Pravila doprinosa](CONTRIBUTING.md)
- [Šablon za threat model](docs/templates/threat-model.md)
- [Šablon za ADR](docs/templates/adr.md)

---

Pitanja o dodeli celine, preduslovima ili arhitekturi postavite kroz odgovarajući
issue ili nastavnom timu.

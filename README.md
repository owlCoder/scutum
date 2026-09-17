# SCUTUM

SCUTUM je platforma za upravljanje informacionom bezbednošću i digitalnim
poverenjem. Objedinjuje rad sa identitetima i pristupom, bezbednosnim
politikama, evidencijom resursa, audit događajima, incidentima, rizicima i
dokazima o primenjenim kontrolama.

Ovaj repozitorijum je zajedničko mesto za razvoj projekta u okviru predmeta
**Osnove informacione bezbednosti** na Fakultetu tehničkih nauka – Primenjeno
softversko inženjerstvo.

## Rad na projektu

Projektne celine, zahtevi i kriterijumi dostavljaju se studentima kroz zasebnu
projektnu specifikaciju u PDF formatu i kroz zadatke u Tapiz Boards-u.

- Za svaki zadatak napravite feature granu iz `main`.
- Promene se predaju Pull Request-om; direktan push na `main` nije dozvoljen.
- Pre merge-a potreban je review i uspešne provere.
- Ne commitujte lozinke, tokene, sertifikate ni druge tajne.

Detaljna pravila rada nalaze se u [CONTRIBUTING.md](CONTRIBUTING.md). Šabloni za
Pull Request, threat model i arhitektonske odluke nalaze se u ovom repozitorijumu.

## Predložena struktura

```text
src/            aplikacioni kod
tests/          automatizovani testovi
docs/           ADR-ovi i threat modeli po projektnim celinama
.github/        GitHub šabloni i CI konfiguracija
```

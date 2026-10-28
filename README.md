# Security Lab — platforma za interaktivne laboratorijske vežbe

Početni šablon zajedničkog studentskog projekta prema dokumentu `docs/spec/Projektna_Specifikacija_OIB.pdf`.

Security Lab je zajednički studentski projekat iz Osnova informacione bezbednosti (OIB). Razvija se kao jedinstvena nastavna platforma u formi **modularnog monolita**, sa jasno razdvojenim funkcionalnim celinama i njihovim ugovorima.

## Tehnološki okvir

Referentne tehnologije u specifikaciji: React / TypeScript (korisnički interfejs), ASP.NET Core / C# (API i moduli), PostgreSQL (baza) i NUnit (testiranje). Dozvoljena je obrazložena zamena komponenti ako se sačuvaju domenska pravila i bezbednosne granice.

## Pokretanje početnog šablona

Potrebni su .NET SDK 10 i Node.js 22+.

1. API: `cd backend/Platform.Api && dotnet run --urls http://localhost:5080`
2. Frontend, u drugom terminalu: `cd frontend && npm install && npm run dev`
3. Frontend na `http://localhost:5173`, health API na `http://localhost:5080/api/health`.

Ovaj kod je **samo početni scaffold**, nije implementacija funkcionalnih zahteva. U njemu nema studentskih naloga, stvarnih laboratorija, autorizacije, evaluator-a ili trajnog skladištenja.

## Organizacija

- `backend/Platform.Api/` — ulazni API; poslovni moduli dodaju se uz jasne granice.
- `frontend/` — početni React interfejs.
- `docs/ARHITEKTURA.md` — konceptualne komponente i pravila.
- `docs/TOK_PODATAKA.md` — početni tok podataka, uz obavezne zasebne DFD-ove za projektne celine.
- `docs/PROJEKTNE_CELINE.md` — upis ID oznaka i odgovornosti (ne dodeljuje automatski timove).
- `docs/KT1.md` i `docs/KT2.md` — kriterijumi provere.
- `docs/templates/PROJEKTNA_CELINA.md` — obrazac za svaki studentski deo.

## Kontrolne tačke

- **KT1 (4. vežba):** započet kod sopstvenog dela, dijagram arhitekture dela, dijagram toka podataka, dokaz individualnog doprinosa i kratka demonstracija.
- **KT2 (8. vežba):** realizovan i proveren dodeljeni deo, odgovarajuće integracije, dokumentacija, testovi i završna demonstracija.

Rad i provere usklađuju se sa projektnom specifikacijom u repozitorijumu.

## Rad u repozitorijumu

`main` sadrži integrisano stanje. Koristite kratkotrajne grane i pull request-ove po zadacima kada zaštita grane to zahteva; ne menjajte istoriju `main` pomoću `push --force`.

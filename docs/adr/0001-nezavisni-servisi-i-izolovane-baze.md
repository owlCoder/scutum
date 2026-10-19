# ADR-0001: Jedan repozitorijum, nezavisni servisi po temi i izolovane baze

- **Status:** Predložen
- **Datum:** 2026-10-19
- **Vlasnici:** Tim 1 (T10) kao autori predloga; nastavni tim kao odobravalac

## Kontekst

Na zajedničkom repozitorijumu radi oko 100 studenata u timovima od 6–10, svaki sa jednom temom (T01–T30,
20 tema u dodeli). Specifikacija ne propisuje framework, bazu ni transport (5.7), ali traži:
da vlasnik celine upravlja svojim persistence modelom i migracijama (SEC-CONV-10), clean clone koji se
pokreće po dokumentovanom postupku (SEC-OPS-01), migracije nad praznom bazom (SEC-OPS-02), CI nezavisan od lokalnog IDE-a
(SEC-OPS-07), ugovore sa životnim ciklusom (SEC-CONTRACT) i simulatore za nedostupne preduslove (SEC-DEP).
Podrazumevana tehnologija je .NET / C#, a druga je moguća uz odobrenje i dokaz interoperabilnosti (5.1).

Bez dogovora o strukturi, 20 timova u jednom `src/` bi stvaralo stalne konflikte, tuđe izmene šeme baze i ne bi
omogućilo različite tehnologije.

## Odluka

1. **Monorepo** sa jednim folderom po temi: `services/<tNN>-<slug>/`. Servis sadrži `src/`, `tests/`, `docs/`,
   `Dockerfile`, `docker-compose.yml`, `.env.example`, `service.yaml` i `README.md`.
2. **Jedna tema = jedan servis = jedna baza.** Baza je na privatnoj mreži projekta i nije na zajedničkoj mreži `scutum-net`;
   samo `api` servisa je vidljiv ostalim timovima. Portovi se objavljuju samo na `127.0.0.1` i računaju po
   formuli: api `8000+NN`, ui `8100+NN`, db `3400+NN`.
3. **Komunikacija samo preko ugovora** u `contracts/` (OpenAPI/šeme) sa registrom i životnim ciklusom iz specifikacije;
   nema deljenog koda između servisa.
4. **Tehnologija je izbor tima.** Šablon dolazi sa MySQL 8 i placeholder Dockerfile-om kao podrazumevanim,
   ne obaveznim. Zajednički interfejs je Docker: `GET /health`, port 8080 u kontejneru, profil `tests`.
5. **Globalni CI** (`.github/workflows/ci.yml`) proverava strukturu i tajne, a za izmenjene servise sa
   `status: active` pokreće `docker compose --profile tests` iz njihovog foldera. Servisi sa `status: scaffold` se preskaču.
6. **UI je opcioni i pripada servisu** (konzola, web, Swagger); nema zajedničkog front-a ni gateway-a u ovoj fazi.
7. **Stubovi** zamenjuju nedostupne upstream servise u razvoju (SEC-DEP); integrisani mod pokreće i prave servise od kojih tim zavisi.

## Posledice

- **Koristi:** izolacija promena i podataka, slobodan izbor tehnologije, ponovljivo pokretanje, CI koji ne zavisi od jezika.
- **Cena:** Docker je obavezan za CI i preporučen lokalno; više baza troši RAM, pa se nikad ne pokreće sve odjednom
  (solo i integrisani mod); timovi moraju da drže ugovore urednim.
- **Rizici:** bez gateway-a nema zajedničke tačke za autentikaciju (rešava se ugovorom sa T12/T09 i stubovima);
  minimalna provera tajni nije zamena za namenski alat; discipline oko `status: active` oslanja se na review.
- **Naknadno (opciono):** objavljivanje image-a servisa na GHCR posle merge-a, uz dozvolu nastavnog tima za packages.
- **Dokazi:** `check-structure.sh`, `check-secrets.sh` i contract testovi u CI-ju.

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
Ovaj README objašnjava **kako je repozitorijum organizovan i kako se u njemu radi**;
razlozi za strukturu su zapisani u [ADR-0001](docs/adr/0001-nezavisni-servisi-i-izolovane-baze.md).

## Arhitektura u kratkim crtama

Svaka tema (T01–T30) je **jedan samostalan servis sa sopstvenom bazom**. Timovi se oslanjaju jedni na druge
samo preko dokumentovanih ugovora (API-ja), nikad preko tuđeg koda ili tuđe baze.

```text
         (opciono) UI / demo svake teme
                       │
 ┌──────────┐ HTTP + ugovor (OpenAPI) ┌──────────┐
 │ t10-api  │◄───────────────────────►│ t04-api  │   ← zajednička mreža `scutum-net`
 └────┬─────┘                         └────┬─────┘
      │ privatna mreža projekta            │ privatna mreža projekta
 ┌────▼─────┐                         ┌────▼─────┐
 │ t10-db   │                         │ t04-db   │   ← baza je nevidljiva drugim timovima
 └──────────┘                         └──────────┘
```

Principi:

1. **Jedna tema = jedan servis = jedna baza.** Samo vlasnik teme menja svoju šemu i migracije (SEC-CONV-10).
2. **Samo `api` je na zajedničkoj mreži `scutum-net`.** Baza nije, a portovi se otvaraju samo na `127.0.0.1`.
3. **Ugovori umesto deljenog koda.** Nema zajedničkih biblioteka između servisa; granica je ugovor u [`contracts/`](contracts/README.md).
4. **Tehnologija je izbor tima** (vidi [Tehnologija](#tehnologija-i-konvencije-servisa)).
5. **Svaki servis može da se pokrene sam**, a upstream servise koji još ne postoje zamenjuju stubovi.

## Struktura repozitorijuma

```text
services/               po jedan folder za svaku temu: tNN-<slug>/
  _template/            šablon za novi servis (koristi ga scripts/new-service.sh)
  t10-telemetry-detection/
    src/                izvorni kod servisa (raspored unutar je odluka tima, npr. Clean Architecture slojevi)
    tests/              automatizovani testovi (unit, integration, negativni/security)
    docs/               threat-model.md, demo.md i ostala dokumentacija teme
    Dockerfile          stage `runtime` (servis) i stage `test` (testovi)
    docker-compose.yml  api + baza + `tests` profil (+ opcioni stubovi)
    .env.example        razvojne vrednosti promenljivih (kopija .env se ne commituje)
    service.yaml        metapodaci: tim, stack, status, portovi
    README.md           kako se servis pokreće, zavisnosti, ugovori, demo
contracts/              javni ugovori između timova + REGISTRY.md
docs/
  adr/                  arhitektonske odluke (ADR)
  templates/            šabloni za ADR i threat model
scripts/                scutum.sh (lokalni rad), new-service.sh, ci/ (provere koje koristi CI)
.github/                PR šablon, CODEOWNERS, workflows/ci.yml
```

## Teme, folderi i portovi

Folder se uvek zove `tNN-<slug>`: **prefiks `tNN` je stalan** (po njemu rade skripte i CI), slug je samo opis.
Broj teme nije broj tima; ko radi koju temu piše u `service.yaml` (polje `team`).

Portovi se računaju po formuli i **ne menjaju se**, da se timovi ne sudaraju: **API `8000+NN`, UI `8100+NN`, baza `3400+NN`**.

| Tema | Naziv | Folder | API | UI | Baza |
| --- | --- | --- | --- | --- | --- |
| T01 | Asset-i, ranjivosti i procena rizika | `t01-assets-risk` | 8001 | 8101 | 3401 |
| T02 | Uloge, privilegovani pristup i access review | `t02-privileged-access` | 8002 | 8102 | 3402 |
| T03 | Object-level i API autorizacija i policy engine | `t03-authorization` | 8003 | 8103 | 3403 |
| T04 | Audit, detekcija i korelacija događaja | `t04-audit-detection` | 8004 | 8104 | 3404 |
| T05 | Bezbednosne kontrole, atestiranje i compliance | `t05-controls-compliance` | 8005 | 8105 | 3405 |
| T06 | Pristanak, preference i privatnost | `t06-consent-privacy` | 8006 | 8106 | 3406 |
| T07 | Incident playbook i post-incident unapređenja | `t07-incident-playbooks` | 8007 | 8107 | 3407 |
| T08 | Attack surface, supply chain i release trust | `t08-attack-surface-supply-chain` | 8008 | 8108 | 3408 |
| T09 | Sesije, tokeni i adaptivna autentikacija | `t09-sessions-adaptive-auth` | 8009 | 8109 | 3409 |
| T10 | Log izvori, telemetry ingestion i detection tuning | `t10-telemetry-detection` | 8010 | 8110 | 3410 |
| T11 | Organizacije i trust granice | `t11-organizations-trust` | 8011 | 8111 | 3411 |
| T12 | Identity registry i autentikacija | `t12-identity-authn` | 8012 | 8112 | 3412 |
| T14 | Politike, waiver i risk acceptance | `t14-policies-waivers` | 8014 | 8114 | 3414 |
| T15 | Baseline, drift i control effectiveness | `t15-baseline-drift` | 8015 | 8115 | 3415 |
| T21 | Training i human-risk analitika | `t21-training-human-risk` | 8021 | 8121 | 3421 |
| T24 | Evidence repo i audit readiness | `t24-evidence-audit-readiness` | 8024 | 8124 | 3424 |
| T26 | Responsibility, exceptions i analytics | `t26-responsibility-exceptions` | 8026 | 8126 | 3426 |
| T28 | Incidenti, istrage, response | `t28-incident-response` | 8028 | 8128 | 3428 |
| T29 | Secrets, service-account review i posture | `t29-secrets-service-accounts` | 8029 | 8129 | 3429 |
| T30 | DLP, backup i exfiltration analytics | `t30-dlp-backup-exfiltration` | 8030 | 8130 | 3430 |
Nedostaju li vam tema ili folder (npr. dodata je nova tema), napravite ga skriptom:

```bash
bash scripts/new-service.sh T13 moj-slug "Naziv teme" -c "R1-xx|Naziv celine" --team 5
```

Na Windows-u istu skriptu pokrenite iz PowerShell-a ili Command Prompt-a:

```powershell
scripts\new-service.bat T13 moj-slug "Naziv teme" -c "R1-xx|Naziv celine" --team 5
```

## Preduslovi i brzi početak (prvi put na računaru)

Rad na projektu zahteva sledeće:

- **Git** za kloniranje repozitorijuma i rad sa granama;
- **Docker Desktop** sa uključenim Docker Compose v2 i pokrenutim Docker engine-om;
- na Windows-u **WSL 2** kao Docker Desktop backend (Docker Desktop ga koristi za Linux kontejnere);
- Bash okruženje: Linux/WSL ili Git Bash. Windows korisnici ne moraju ručno da ulaze u WSL:
  `scripts\scutum.bat` automatski koristi Git Bash, a zatim WSL ako Git Bash nije pronađen.

Proverite instalaciju pre prvog pokretanja:

```bash
git --version
docker --version
docker compose version
```

Na Windows-u ove provere pokrenite u PowerShell-u; Git Bash ili WSL su potrebni samo
ako želite da koristite `.sh` komande direktno. Docker Desktop mora biti pokrenut pre
`network`, `up` ili `test` komande.

```bash
git clone https://github.com/owlCoder/scutum.git && cd scutum

bash scripts/scutum.sh network        # jednom po računaru: pravi Docker mrežu scutum-net
bash scripts/scutum.sh list           # pregled svih servisa, statusa i portova
bash scripts/scutum.sh up t10         # pokreće api + bazu teme T10 (pravi .env iz .env.example)
curl http://localhost:8010/health     # očekivano: OK
bash scripts/scutum.sh test t10       # isto što radi CI (profil `tests`)
bash scripts/scutum.sh down t10       # gasi; dodajte --volumes da obrišete i podatke baze
```

Windows PowerShell / Command Prompt ekvivalent:

```powershell
git clone https://github.com/owlCoder/scutum.git
cd scutum

scripts\scutum.bat network
scripts\scutum.bat list
scripts\scutum.bat up t10
curl http://localhost:8010/health
scripts\scutum.bat test t10
scripts\scutum.bat down t10
```

`.bat` wrapper prosleđuje iste argumente canonical Bash skripti, pa su ponašanje,
Docker mreža, `.env` i portovi isti na svim sistemima. Nemojte pokretati isti servis
istovremeno iz više terminala ili iz više WSL distribucija.

Dok tim ne zameni placeholder, `up` podiže prazan servis koji odgovara `OK` na `/health` i pravu MySQL bazu.
Time se odmah vidi da pokretanje sa čistog klona radi (SEC-OPS-01).

### Environment konfiguracija

Svaki servis ima svoj `.env.example`. Lokalna skripta ga pri prvom `up` automatski kopira u `.env`;
kopija je namerno u `.gitignore` i ne sme se commit-ovati.

U `.env` se podešavaju razvojne vrednosti:

- `API_PORT` — port API-ja na host računaru;
- `DB_PORT_HOST` — port baze na `127.0.0.1` za alate poput DBeaver-a;
- `DB_HOST` i `DB_PORT` — adresa baze iz kontejnera (`DB_PORT` zavisi od baze: 3306 za MySQL, 5432 za PostgreSQL);
- `DB_NAME`, `DB_USER`, `DB_PASSWORD` i `MYSQL_ROOT_PASSWORD` — lokalna DB konfiguracija (tim koji menja bazu menja i ove promenljive).

`.env.example` sadrži samo bezbedne razvojne placeholder vrednosti. Za staging ili produkciju
vrednosti se prosleđuju kroz platformu za deployment/secrets manager; stvarne lozinke, tokene i
sertifikate nikada ne treba upisivati u repozitorijum.

## Kako vaš tim počinje

1. Otvorite folder svoje teme i popunite [`service.yaml`](services/_template/service.yaml) (`team`, `stack`). Portove ne menjajte.
2. Odaberite tehnologiju (vidi dole) i **zamenite placeholder `Dockerfile`** svojim; sačuvajte stage-ove `runtime` i `test`.
3. Kod ide u `src/`, testovi u `tests/`. Servis mora da sluša na portu **8080** u kontejneru i da odgovara na `GET /health`.
4. Kada `bash scripts/scutum.sh test <tNN> --force` prolazi, postavite `status: active` u `service.yaml`. Od tada CI testira vaš servis na svakom PR-u.
5. Dokumentujte (obavezno po specifikaciji, pogl. 11): odgovornost i trust granicu u `README.md` servisa, `docs/threat-model.md`,
   `docs/demo.md` (normalan i negativni scenario), poznata ograničenja i ugovore. ADR-ovi idu u [`docs/adr/`](docs/adr/) sa prefiksom teme, npr. `t10-0001-izbor-parsera.md`.
6. Granice prema drugim timovima definišite kroz [`contracts/`](contracts/README.md).

Ako već imate kod napisan na drugom mestu u repozitorijumu, premestite ga u folder svoje teme; zajednički koren (`src/`, `tests/`) više ne koristimo.

Preporuka za imenovanje: grana `feature/<celina>-<kratak-opis>` (prema [CONTRIBUTING.md](CONTRIBUTING.md)), a naslov PR-a `[T10] R2-17: kratak opis`.

## Tehnologija i konvencije servisa

**Svaki tim sam bira tehnologiju svog servisa.** Specifikacija preporučuje **.NET / C#**; druga tehnologija je dozvoljena
uz odobrenje nastavnog tima, dokaz interoperabilnosti i ekvivalentan nivo testiranja i bezbednosne kontrole (pogl. 5.1).
Zabeležite izbor u `service.yaml` (`stack`) i ADR-u ako odstupate od preporuke.

Šablon sadrži **MySQL 8 i placeholder Dockerfile kao podrazumevane vrednosti, ne kao obavezu**. Možete da promenite bazu ili jezik,
ali zajednička pravila (njih proverava CI) ostaju ista:

| Pravilo | Zašto |
| --- | --- |
| Ime compose projekta `scutum-tNN`, servisi `tNN-api`, `tNN-db`, `tNN-tests` (ili više: `tNN-tests-<naziv>`) | Nema sudara naziva u zajedničkoj mreži |
| Servis sluša na `8080`, `GET /health` vraća 200 | Isti način provere za sve jezike |
| Samo `api` je na mreži `scutum-net`; baza ostaje privatna | Niko ne može da dohvati tuđu bazu (SEC-CONV-10) |
| Portovi samo na `127.0.0.1`, po formuli iz tabele | Servisi na laptopu nisu izloženi mreži fakulteta |
| Profil `tests` pokreće sve testove i vraća exit kod ≠ 0 pri padu; više test servisa (npr. `t10-tests-api` i `t10-tests-ui`) se pokreće redom i pad bilo kog obara proveru | CI ne zna vaš jezik, samo poziva ovaj profil |
| Port baze u kontejneru (`DB_PORT`) bira tim; host port je uvek `3400+NN` | Slobodan izbor baze bez sudara portova |
| Konfiguracija kroz promenljive okruženja; `.env` se ne commituje | SEC-OPS-06, SEC-BASE-04 |
| Migracije formiraju šemu nad praznom bazom; seed pokazuje normalan i negativan scenario | SEC-OPS-02, SEC-OPS-03 |

## Zavisnosti od drugih timova: solo i integrisani mod

Svih 20 servisa i baza ne može da radi odjednom na jednom računaru, pa se nikad ne pokreće sve. Postoje dva moda:

- **Solo mod.** Pokrećete samo svoj servis (`up t10`). Servisi od kojih zavisite su **stubovi** (npr. WireMock/Mockoon napravljen iz OpenAPI-ja ugovora)
  sa normalnim i failure ishodima. Stub sme da imitira samo dogovoreno ponašanje, ne i security logiku upstream tima (SEC-DEP-03).
- **Integrisani mod.** Pokrećete svoj servis i prave servise od kojih zavisite (`up t10 t04`); skripta svakom koristi njegov folder i njegovu bazu.
  Servis je dostupan na `http://<tNN>-api:8080` unutar mreže `scutum-net` i na `localhost:<port>` sa računara.
  Ako tuđi servis još nije aktivan (`status: scaffold`), ostaje samo placeholder, pa za njega koristite stub.

Adresa upstream servisa je uvek promenljiva okruženja (npr. `T04_BASE_URL=http://t04-api:8080`), pa se prelazak sa stuba na pravi servis
ne menja u kodu. Stub u compose-u nosi isti alias (`t04-api`), pa **ne pokrećite stub i pravi servis istovremeno**.
Isti contract testovi treba da važe za oba (SEC-DEP-04).

MySQL kontejner zauzima stotinak MB ili više; šablon ga već štedi (`performance_schema` isključen, mali buffer pool),
ali proverite svoju potrošnju sa `docker stats` i pokrećite samo ono što vam treba.

## Ugovori između timova

Sve što druga celina sme da koristi (API, format događaja, format odluke) je **ugovor** sa owner-om, consumerima i
stanjem `DRAFT → ACCEPTED → DEPRECATED → RETIRED`. Ugovori i registar su u [`contracts/`](contracts/README.md).
Breaking promena prihvaćenog ugovora zahteva novu verziju i review pogođenih timova pre merge-a (SEC-CONTRACT-04).

## CI

GitHub Actions čita workflow-e samo iz `.github/workflows/`, zato postoji **jedan globalni** [`ci.yml`](.github/workflows/ci.yml).
Svoj CI ipak pišete vi, unutar svog foldera, jer CI samo poziva vaš profil `tests`:

1. `structure` — proverava da svaki servis prati strukturu i konvencije iz ovog README-a.
2. `secrets` — osnovna provera praćenih fajlova na `.env`, ključeve i tokene (ne zamenjuje namenske alate).
3. `tests (tNN)` — za svaki **izmenjen servis sa `status: active`** pokreće sve `tNN-tests*` servise profila `tests` iz njegovog foldera.
   Servisi sa `status: scaffold` se preskaču (`structure` upozorava kada scaffold servis već ima kod u `src/` ili `tests/`).
   Ako menjate zajedničke fajlove (`scripts/`, `.github/workflows/`), proveravaju se svi aktivni servisi.
   Izmena ugovora u `contracts/tNN/<C-IME-vN>/` pokreće testove owner-a i consumera navedenih u `contracts/REGISTRY.md`.
4. `ci-ok` — zbirni rezultat; to je provera koju treba tražiti kao obaveznu pre merge-a. Pada i kada detekcija izmenjenih servisa ne uspe.

Sve ovo možete pokrenuti lokalno iz Linux/WSL/Git Bash okruženja:
`bash scripts/ci/check-structure.sh`, `bash scripts/ci/check-secrets.sh`, `bash scripts/scutum.sh test <tNN>`.
Windows wrapper pokriva Docker workflow (`scripts\scutum.bat test <tNN>`); CI pomoćne
provere se pokreću kroz WSL ili Git Bash.
Test kontejner ima montiran `TestResults/` servisa na `/results`; šta vaši testovi tamo upišu (npr. `.trx`, JUnit XML), CI čuva kao artefakt (dokaz za audit).

## UI i demo

Specifikacija ne traži zajednički front. Traži **ponovljiv normalan i negativni demo scenario** po dokumentovanom postupku (pogl. 11.1).
Zato je UI opcioni i pripada servisu: web aplikacija (port `8100+NN`), konzolna aplikacija, Swagger/HTTP kolekcija ili skripta.
Granica prema drugim timovima ipak mora biti mrežni API iz ugovora, jer in-process portovi nisu dostupni drugim timovima.
Ako niste sigurni da vaš oblik demoa prolazi, pitajte nastavni tim. Autorizacija se nikad ne radi samo na UI nivou (pogl. 11.3).

## Bezbednosna pravila repozitorijuma

- Nikada ne commitujte `.env`, ključeve, sertifikate ni tokene; koristite `.env.example` sa razvojnim vrednostima. Ako se tajna slučajno commituje,
  odmah obavestite nastavni tim; brisanje u sledećem commit-u nije dovoljno (CONTRIBUTING.md).
- Ne pristupajte tuđoj bazi i ne menjajte tuđe tabele; tražite promenu preko ugovora i review-a tog tima.
- Ne objavljujte portove na `0.0.0.0`; koristite `127.0.0.1:<port>:<port>` kao u šablonu.
- Ne pišite sopstvenu kriptografiju, hash lozinki ni token algoritme.

## Ko sme šta da menja

- **Svoj folder** (`services/<vaš-tNN>-*/`): slobodno, kroz PR.
- **Tuđi folder:** ne menjate; predlog šaljete timu-vlasniku (issue ili PR sa njihovim review-om).
- **Zajednički fajlovi** (`README.md`, `scripts/`, `.github/`, `contracts/README.md`, `contracts/REGISTRY.md`, `docs/templates/`):
  PR sa review-om bar jednog drugog tima. U `contracts/REGISTRY.md` svaki tim dodaje samo svoje redove.
- Svaki tim u svom PR-u upisuje svoje GitHub korisnike u [`.github/CODEOWNERS`](.github/CODEOWNERS).

## Česti problemi

| Problem | Rešenje |
| --- | --- |
| Windows javlja da `scutum.bat` ne nalazi Bash/WSL | Instalirajte/omogućite WSL 2 i uključite ga kao Docker Desktop backend; zatim ponovo otvorite terminal. |
| `Cannot connect to the Docker daemon` | Pokrenite Docker Desktop i sačekajte da engine bude spreman; proverite `docker compose version`. |
| `network scutum-net declared as external, but could not be found` | `bash scripts/scutum.sh network` |
| `port is already allocated` | Neko pokreće drugi servis na tom portu ili je lokalna aplikacija zauzela port; portovi se ne menjaju, ugasite uljeza (`docker ps`) |
| Skripta pada na Windows-u sa `\r` greškama | Fajl je dobio CRLF završetke; `.gitattributes` to sprečava, a postojeći fajl popravite sa `git add --renormalize .` |
| `required variable DB_NAME is missing` | Nema `.env`; `bash scripts/scutum.sh up <tNN>` ga pravi, ili ručno `cp .env.example .env` |
| Baza ostaje sa starom šemom | `bash scripts/scutum.sh down <tNN> --volumes` pa ponovo `up` |

## Kasnije (opciono): gotovi image-i servisa

Kada nastavni tim dozvoli korišćenje GitHub Packages, CI može posle merge-a da objavljuje image svakog aktivnog servisa na GHCR,
pa bi timovi u integrisanom modu povlačili tuđe servise (`docker compose pull`) umesto da ih grade iz izvornog koda.
To nije deo prve faze i ne menja pravila iznad.

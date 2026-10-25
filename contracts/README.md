# Javni ugovori između timova

Ugovor je sve što druga celina sme da koristi: HTTP API (OpenAPI), format poruke/događaja, format policy odluke
ili format dokaza. **Timovi komuniciraju samo preko ugovora** — nikad preko tuđe baze ni tuđeg koda
(SEC-CONV-10, SEC-CONTRACT-01..06 iz specifikacije).

## Gde šta stoji

```text
contracts/
  REGISTRY.md                 registar svih ugovora (owner, consumeri, stanje)
  _template/                  šablon novog ugovora
  <tNN>/<C-IME-vN>/           npr. t04/C-AUDIT-v1/
      README.md               opis, pravila, stanje, consumeri, changelog
      openapi.yaml            (ili schema.json za događaje/poruke)
```

Ugovor živi u folderu **owner** teme (`contracts/t04/...` je ugovor koji nudi T04).

## Životni ciklus

```text
DRAFT -> ACCEPTED -> DEPRECATED -> RETIRED
```

- **DRAFT** — owner ga sme menjati dok ga nijedan drugi tim ne koristi kao prihvaćenu granicu.
- **ACCEPTED** — zahteva bar jedan izvršiv contract/security test (SEC-CONTRACT-03) i bar jednog potvrđenog consumera u PR-u.
- **Breaking promena** ACCEPTED ugovora = **nova verzija** (`C-AUDIT-v2`) ili koordinisana migracija; PR mora imati
  review pogođenih consumera pre merge-a (SEC-CONTRACT-04, SEC-CONV-07).
- **DEPRECATED** ostaje podržan tokom dogovorenog perioda; razlog i zamena upisani u README ugovora.
- **RETIRED** se uklanja tek kada nema aktivnih consumera.

## Šta svaki ugovor mora da definiše (SEC-CONV)

- tip, format i pravilo jedinstvenosti svakog izloženog identifikatora (SEC-CONV-01);
- subjekat, tenant/security domen, scope i assurance level u kontekstu poziva (SEC-CONV-02);
- stabilne mašinski prepoznatljive kodove grešaka, ne slobodan tekst (SEC-CONV-03);
- minimalna polja security događaja: vreme, actor/service identity, akcija, target, ishod, korelacioni ID, osetljivost (SEC-CONV-04);
- nikakve tajne, tokene ni kredencijale u DTO-ima, logovima i seed podacima (SEC-CONV-05);
- ograničenje veličine odgovora i pagination/filter za kolekcije (SEC-CONV-06);
- idempotency semantiku za ponovljive operacije (SEC-CONV-08) i strategiju konflikta za konkurentne izmene (SEC-CONV-09).

## Kako se dodaje ugovor

1. Kopirajte `contracts/_template/` u `contracts/<vaš-tNN>/C-IME-v1/` i popunite README + `openapi.yaml`.
2. Dodajte red u [`REGISTRY.md`](REGISTRY.md) (stanje `DRAFT`).
3. Obavestite consumere (PR opis, Tapiz); oni dodaju sebe u README ugovora.
4. Pre prelaska u `ACCEPTED` dodajte contract test koji se izvršava i nad stubom i nad pravim servisom.

## Stubovi (kada upstream servis još ne postoji)

Consumer pravi stub iz OpenAPI-ja (npr. WireMock/Mockoon) sa **normalnim i failure ishodima** (SEC-DEP-02).
Stub ne sme sadržati security logiku upstream tima (SEC-DEP-03). Isti contract testovi moraju moći da se pokrenu
nad pravim servisom kada postane dostupan (SEC-DEP-04); razlika stub ↔ stvarni servis je integracioni problem koji se rešava pre zatvaranja zadatka.

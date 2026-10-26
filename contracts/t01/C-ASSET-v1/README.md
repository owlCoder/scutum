# C-ASSET-v1 — asset po ID-ju (R1-02)

- **Owner:** T01 (servis `t01-api`)
- **Consumeri:** T08 (R1-18, R2-13, R3-17) — dopunjuju se po potvrdi ostalih consumera
- **Stanje:** DRAFT
- **Verzija:** v1
- **Transport:** HTTP/JSON, vidi [`openapi.yaml`](openapi.yaml)
- **Contract test:** nije napisan (obavezan pre ACCEPTED; T08 piše test nad stubom i nad pravim T01 servisom)

> **Poreklo:** minimalni DRAFT koji predlaže consumer (T08) jer mu je potreban za KT1. T01 ga potvrđuje
> ili menja; do tada T08 radi nad stubom. Ako T01 objavi sopstveni `C-ASSET-v1`, njihov tekst ima prednost
> a T08 prilagođava adapter.

## Svrha

Omogućava consumeru da proveri da asset postoji i da dobije vlasnika, kritičnost, status i scope.
T08 koristi to za: vezu exposure-a sa asset-om (R1-18-UC2), kritičnost za obavezni audit (R1-18),
vezu komponente sa asset-om (R2-13). Ugovor namerno NE obećava pretragu, izmenu asset-a ni listu.

## Pravila i semantika

- Identifikatori: `assetId` je string `^[a-z][a-z0-9-]{0,63}$`, jedinstven u T01; consumer ga tretira kao neprozirnu vrednost.
- Kontekst poziva: `Authorization` nosi identitet servisa/subjekta; scope se određuje na strani T01 iz tokena, ne iz parametara.
  Asset van scope-a pozivaoca vraća isti odgovor kao nepostojeći (`ASSET_NOT_FOUND`, 404).
- Kodovi grešaka: `ASSET_NOT_FOUND` (404), `ASSET_DECOMMISSIONED` (409), `UPSTREAM_UNAVAILABLE` (503).
  Consumer **ne sme** da 503 tumači kao „asset ne postoji" ni kao uspeh.
- `criticality`: `Low | Medium | High | Critical`. `status`: `Active | Decommissioned`.
- Limit odgovora / pagination: nije potrebno, jedan resurs.
- Idempotency i konflikti: samo čitanje, bezbedno za ponavljanje.
- Šta NIKAD ne sme biti u odgovoru: tajne, tokeni, podaci o asset-ima drugog scope-a.

## Changelog

| Verzija | Datum | Promena | Breaking? |
| --- | --- | --- | --- |
| v1 | 2026-10-26 | Početni DRAFT (predlog consumera T08) | — |

# C-AUDIT-v1 — upis security/audit događaja (R1-09)

- **Owner:** T04 (servis `t04-api`)
- **Consumeri:** T08 (R1-18, R2-13, R3-17); očekuje se da ga koriste i ostali timovi (T01 već planira `POST /api/v1/events`)
- **Stanje:** DRAFT
- **Verzija:** v1
- **Transport:** HTTP/JSON, vidi [`openapi.yaml`](openapi.yaml)
- **Contract test:** nije napisan (obavezan pre ACCEPTED)

> **Poreklo:** minimalni DRAFT koji predlaže consumer (T08) na osnovu SEC-CONV-04. T04 ga potvrđuje ili menja;
> do tada T08 piše u lokalni `InMemoryAuditSink` (isti port, bez security logike T04).

## Svrha

Jedinstven način da servis upiše audit događaj. Ugovor NE obećava pretragu, korelaciju ni detekciju — to je posao T04.

## Pravila i semantika

- Polja događaja (SEC-CONV-04): `occurredAt`, `actor` (identitet korisnika ili servisa), `action`, `target`, `outcome`
  (`Success | Failure`), `correlationId`, `sensitivity` (`Low | Medium | High`). Uspeh i neuspeh se razlikuju poljem `outcome`.
- Identifikatori: `eventId` generiše T04; `correlationId` je UUID koji šalje pozivalac.
- Kontekst poziva: izvor događaja (`source`) određuje T04 iz identiteta servisa u `Authorization`, ne veruje polju iz tela.
- Kodovi grešaka: `INVALID_EVENT` (400), `UPSTREAM_UNAVAILABLE` (503). Producer ne sme da izgubi događaj
  bez traga: pri 503 ga čuva/ponavlja ili beleži lokalno.
- Limit odgovora / pagination: nije potrebno (upis jednog događaja); `details` je ograničen na 2 KB.
- Idempotency: ponovljen upis sa istim `correlationId` + `action` + `target` + `occurredAt` ne pravi duplikat.
- Šta NIKAD ne sme biti u događaju: tajne, tokeni, lozinke, celi zahtevi/odgovori; `details` sadrži samo identifikatore i kodove.

## Changelog

| Verzija | Datum | Promena | Breaking? |
| --- | --- | --- | --- |
| v1 | 2026-10-26 | Početni DRAFT (predlog consumera T08) | — |

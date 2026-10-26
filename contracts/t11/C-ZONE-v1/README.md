# C-ZONE-v1 — security zona po ID-ju (R1-14)

- **Owner:** T11 (servis `t11-api`)
- **Consumeri:** T08 (R1-18)
- **Stanje:** DRAFT
- **Verzija:** v1
- **Transport:** HTTP/JSON, vidi [`openapi.yaml`](openapi.yaml)
- **Contract test:** nije napisan (obavezan pre ACCEPTED)

> **Poreklo:** minimalni DRAFT koji predlaže consumer (T08). T11 ga potvrđuje ili menja; do tada T08 radi nad stubom.

## Svrha

Omogućava da R1-18-UC2 poveže exposure sa postojećom, aktivnom security zonom i da zna ko je vlasnik zone.
Ugovor namerno NE obećava listu zona, članstvo asset-a u zoni ni izmenu zone.

## Pravila i semantika

- Identifikatori: `zoneId` je string `^[a-z][a-z0-9-]{0,63}$`, jedinstven u T11; za consumera neproziran.
- Kontekst poziva: identitet i scope iz `Authorization`; zona van scope-a pozivaoca = `ZONE_NOT_FOUND` (404).
- Kodovi grešaka: `ZONE_NOT_FOUND` (404), `ZONE_INACTIVE` (409), `UPSTREAM_UNAVAILABLE` (503).
  503 se ne sme tumačiti kao „zona ne postoji" ni kao uspeh.
- `trustLevel`: `Internal | Dmz | External`. `status`: `Active | Inactive`.
- Limit odgovora / pagination: nije potrebno, jedan resurs.
- Idempotency i konflikti: samo čitanje.
- Šta NIKAD ne sme biti u odgovoru: tajne, mrežni detalji zone izvan potrebnog minimuma, podaci drugog domena.

## Changelog

| Verzija | Datum | Promena | Breaking? |
| --- | --- | --- | --- |
| v1 | 2026-10-26 | Početni DRAFT (predlog consumera T08) | — |

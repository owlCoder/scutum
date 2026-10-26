# Kodovi gresaka: T08 (SEC-CONV-03)

Izvor istine su konstante `ErrorCodes` (`T08.Application/Exposures/ClassifyExposure.cs`) i `ExposureErrorCodes`
(`T08.Domain/Exposures/Exposure.cs`) i mapa `StatusByCode` u `T08.Api/ExposureEndpoints.cs`.
Kod je stabilan i masinski prepoznatljiv; tekst poruke se ne vraca. Telo odgovora je uvek `{"code":"<KOD>"}`.
Kod koji nije u mapi nikad ne postaje uspeh: vraca se **500** (fail-closed).

## Kodovi API-ja (`POST /api/v1/exposures/{id}/classification`)

| Kod | HTTP | Kada | Audit |
| --- | --- | --- | --- |
| `UNAUTHENTICATED` | 401 | Nema tokena, token je neispravan ili istekao. Dodaje `WWW-Authenticate: Bearer`. | Ne (akter nepoznat) |
| `INVALID_CORRELATION_ID` | 400 | `X-Correlation-Id` fali, nije GUID ili je prazan GUID. | Ne |
| `INVALID_REQUEST` | 400 | Telo nije ispravan JSON, prazno je ili je vece od 4096 B. | Ne |
| `INVALID_CLASSIFICATION` | 400 | Vrednost nije tacno `Approved`, `Temporary` ili `Unknown` (brojevi i nepoznate vrednosti se odbijaju). | Ne |
| `INVALID_EXPIRY` | 400 | `Temporary` bez `expiresAt` ili sa `expiresAt` koji nije u buducnosti; ili `expiresAt` poslat uz `Approved`/`Unknown`. | Ne |
| `INVALID_EXPOSURE` | 400 | Domenska povreda pri registraciji/zatvaranju (prazan obavezan podatak). Classify endpoint je ne vraca; rezervisan za UC registracije i zatvaranja. | Ne |
| `FORBIDDEN` | 403 | Akter je u scope-u, ali nema dozvolu `exposure.classify`. | Da (`Denied`, Failure) |
| `NOT_FOUND` | 404 | Neispravan ID, nepostojeci exposure ili exposure iz tudjeg scope-a. Spolja se ne razlikuju. | Samo za tudji scope |
| `EXPOSURE_CLOSED` | 409 | Exposure je zatvoren (terminalno stanje). | Ne |
| `ASSET_NOT_USABLE` | 409 | Asset ne postoji, nije aktivan (dekomisionisan/neaktivan) ili ga T01 ne vidi. Nova veza se odbija. | Ne |
| `ASSET_SCOPE_MISMATCH` | 409 | Scope asset-a se razlikuje od scope-a exposure-a; T08 ne odlucuje nad nepoklopljenim scope-ovima. | Ne |
| `CONFLICT` | 409 | Exposure je u medjuvremenu izmenjen (optimisticka verzija, SEC-CONV-09). Pozivalac moze ponoviti. | Ne |
| `UPSTREAM_UNAVAILABLE` | 503 | T01 nedostupan, timeout (5 s) ili neispravan odgovor. Nista nije promenjeno. | Ne |

Redosled provere: 401, oblik zahteva (400), scope (404), dozvola (403), zatvoren (409), asset (503/409), domenska pravila (400), verzija (409).

## Registracija, potvrda, zatvaranje

Use case-ovi su u `T08.Application/Exposures/ExposureLifecycle.cs`, a izlozeni su rutama (isti tok: 401, `X-Correlation-Id`, telo do 4096 B):

| Ruta | Telo | Uspeh |
| --- | --- | --- |
| `POST /api/v1/exposures` | `{assetId, zoneId, justification}` | 201 `{exposureId, auditDelivered}` |
| `POST /api/v1/exposures/{id}/confirmation` | nema | 200 `{auditDelivered}` |
| `POST /api/v1/exposures/{id}/closure` | `{reasonCode}` | 200 `{auditDelivered}` |

Koriste iste kodove kao gore, uz jedan novi:

| Kod | HTTP | Kada |
| --- | --- | --- |
| `ZONE_NOT_USABLE` | 409 | Registracija: zona ne postoji ili je neaktivna (T11). Audit: ne. |

Vlasnik i scope exposure-a pri registraciji uzimaju se iz asset-a (T01); `ownerId`/`scope` poslati u telu se ignorisu.

Razlike u odnosu na classify:

- `FORBIDDEN` vazi i za objektnu proveru: dozvola (`exposure.register|confirm|close`) bez `exposure.manage.any` vazi samo za vlasnika
  (asset-a pri registraciji, exposure-a pri potvrdi/zatvaranju). Audit: `Denied`; pri registraciji je cilj `asset:{id}`.
- Registracija: tudj scope asset-a daje `ASSET_NOT_USABLE` (isto kao nepostojeci), a interno se audituje. `INVALID_EXPOSURE` i za neispravan ID asset-a/zone i za prazno obrazlozenje.
- Zatvaranje: razlog je stabilan kod (`ContractIdentifier`), inace `INVALID_EXPOSURE`. Dekomisionisan ili nepostojeci asset ne blokira zatvaranje
  (audit se tada vodi kao Critical); `UPSTREAM_UNAVAILABLE` ga blokira.
- Potvrda trazi upotrebljiv asset (`ASSET_NOT_USABLE` inace).

## Kodovi tudjih ugovora i kako ih T08 cita

Adapteri (`HttpAssetDirectory`, `HttpZoneDirectory`) odluku donose po **HTTP statusu**, ne po `code` iz tela:

| Upstream odgovor | `DirectoryOutcome` | Kod koji T08 vraca |
| --- | --- | --- |
| 200 i `status = Active` | `Found` | (nastavlja) |
| 200 i `status` razlicit od `Active` | `Inactive` | `ASSET_NOT_USABLE` |
| 404 (`ASSET_NOT_FOUND` / `ZONE_NOT_FOUND`) | `NotFound` | `ASSET_NOT_USABLE` |
| 409 (`ASSET_DECOMMISSIONED` / `ZONE_INACTIVE`) | `Inactive` | `ASSET_NOT_USABLE` |
| 503, drugi status, timeout, neispravan JSON ili polje | `Unavailable` | `UPSTREAM_UNAVAILABLE` |

Upstream kodovi se ne prosledjuju spolja: pozivalac T08 vidi samo kodove iz prve tabele.

## Pravila

- Novi kod se dodaje istovremeno u konstante, u `StatusByCode` i u ovu tabelu.
- Kod se nikad ne menja niti ponovo koristi za drugo znacenje (breaking promena).
- Telo greske ne sadrzi tajne, interne poruke ni podatke o resursima drugog scope-a.
- T08 jos nema sopstveni javni ugovor u `contracts/`; do tada je ovaj dokument jedini opis kodova.

# T03 — minimalni threat model

Granice: korisnički zahtev -> pouzdani aplikacioni Subject kontekst -> T03 handler;
T03 -> portovi R1-05/R1-02; T03 -> lokalni store/audit (buduća granica prema T04).
Klijent šalje ResourceId i CorrelationId, ne dozvole ni identitet za odluku.

| Celina / asset | Pretnja | Kontrola | Test / dokaz | Stanje |
| --- | --- | --- | --- | --- |
| R1-06 sadržaj resursa | BOLA: poznat tuđi ID | ownership + scoped grant | OtherOwnerCannotReadByKnownId | implementirano |
| R1-06 domen | prelazak scope granice | Subject.ScopeId == asset.ScopeId | Read_WhenSubjectScopeDiffers_DeniesEvenWithGrantForTargetScope | implementirano |
| R1-06 grant | ReadAny iz drugog domena | scope granta se proverava | Read_WhenGrantIsForAnotherScope_DeniesOwner | implementirano |
| R1-06 stanje | neaktivan subjekt/asset čita | provera aktivnosti | Read_WhenSubjectIsInactive_DeniesDespiteExistingPermission; Read_WhenAssetIsInactive_Denies | implementirano |
| R1-06 dostupnost | nedostupan upstream daje pristup | kontrolisan Unavailable bez resursa | Read_WhenDependencyIsUnavailable_FailsClosed | implementirano za PortStatus.Unavailable |
| R1-06 metadata | otkrivanje postojanja tuđeg ID-a | identičan javni DTO | Read_WhenResourceIsMissingOrForeign_ReturnsIdenticalPublicResponse | implementirano; timing jednakost nije garantovana |
| R2-12 API granica | zaobilaženje auth, prevelik input, previše zahteva | endpoint policy + scoped rate limit + input granice | planirani testovi T03-020..024 | PLAN KT3 |
| R3-01 policy odluka | nepouzdani atributi ili konflikt daju allow | pouzdani portovi, deterministički deny-overrides | planirani testovi T03-030..034 | PLAN KT3/KT4 |

Residual risk: nema realnog auth adaptera, trajnog skladišta, udaljenog audit adaptera,
HTTP endpoint-a za autorizaciju ni prihvaćenih cross-team ugovora. Trusted Subject je
fixture/composition-root ulaz. Port Unavailable je modelovan; timeout/izuzeci realnog
transporta još nisu obrađeni. Update/ChangeOwner konkurentnost i atomski audit su plan.
`lock` na pojedinačnim metodama InMemoryStore ne rešava buduću transakciju upisa.

# Security matrica: T08 (Subject – Action – Resource – Allow/Deny)

Uloge: **SA** Security Administrator · **RO** Resource Owner (samo svoj asset/domen) · **AN** Security Analyst/Operator ·
**AU** Auditor (samo čitanje) · **X** autentifikovan korisnik drugog scope-a ili bez uloge.
Sve što nije navedeno je **Deny** (deny-by-default). Prave dozvole stižu iz R1-05 (T02) i identitet iz R1-03 (T12); do tada stub
sa serverskim kontekstom `{ actorId, scope, permissions }`. Identitet i scope se nikad ne uzimaju iz polja zahteva (SEC-BASE-02).

| Akcija → resurs | SA | RO | AN | AU | X |
| --- | --- | --- | --- | --- | --- |
| Registrovati exposure | Allow | Allow (svoj) | Deny | Deny | Deny |
| Klasifikovati exposure (Approved / Temporary / Unknown) | Allow | Deny | Allow | Deny | Deny |
| Potvrditi / zatvoriti exposure | Allow | Allow (svoj) | Deny | Deny | Deny |
| Čitati detalje exposure-a | Allow | Allow (svoj) | Allow (u scope-u) | Allow (read-only) | Deny |
| Registrovati komponentu / uvesti dependency listu | Allow | Allow (svoj) | Deny | Deny | Deny |
| Povezati nalaz / kreirati remediation plan | Allow | Allow (svoj, plan) | Allow (nalaz) | Deny | Deny |
| Odobriti izuzetak (R2-13, R3-17) | Allow (≠ podnosilac) | Deny | Deny | Deny | Deny |
| Registrovati release i provenance | Allow | Allow (svoj) | Deny | Deny | Deny |
| Pokrenuti (ponovnu) evaluaciju release-a | Allow | Allow (svoj) | Allow | Deny | Deny |
| Čitati odluku i build detalje | Allow | Allow (svoj) | Allow (u scope-u) | Allow (read-only) | Deny |

## Dodatna pravila

- **Objektna provera:** „Allow (svoj)" i „u scope-u" se proveravaju nad konkretnim resursom, ne samo nad ulogom (role check bez objekta nije dovoljan).
- **Isti odgovor:** resurs van scope-a vraća isti odgovor kao nepostojeći (nema curenja postojanja).
- **Razdvajanje dužnosti:** odobravalac izuzetka ne sme biti podnosilac.
- **Audit:** svaka promena (Allow) i svako odbijanje pristupa proizvodi audit događaj bez tajni; promena izloženosti kritičnog asset-a je obavezno auditovana.
- **Negativni testovi:** svaki red sa Deny u koloni X ima automatizovan test (SEC-BASE-02); test mora da pada kad se kontrola ukloni.

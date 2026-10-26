# R1-02 — Asset model i životni ciklus

## Namena

Asset predstavlja informacioni resurs: aplikaciju, servis, uređaj,
podatke ili drugi resurs.

Izvor zahteva: OIB projektna specifikacija, odeljak R1-02
„Asset inventory i kritičnost resursa“, strane 28–29.

Obavezni use-case-ovi su:

- registracija asset-a;
- dodela vlasnika;
- klasifikacija kritičnosti;
- promena statusa;
- pretraga po domenu i vlasniku.

Specifikacija zahteva jedinstven identitet asset-a, očuvanje
dekomisioniranog asset-a u istoriji i eksplicitnu, auditovanu kritičnost.

## Model Asset

Polja i pravila u nastavku predstavljaju domenski model T01.

| Polje | Tip | Namena i pravilo |
| --- | --- | --- |
| Id | AssetId | Jedinstven identifikator; generiše se pri registraciji i ne menja se. |
| Name | string | Obavezan naziv, najviše 200 karaktera; čuva se bez početnih i završnih razmaka. |
| Type | AssetType | Definisana enum vrednost; ne menja se nakon registracije. |
| DomainId | DomainId | Domen asset-a; identifikator nije Guid.Empty i ne menja se nakon registracije. |
| OwnerId | IdentityId | Odgovorni vlasnik; identifikator nije Guid.Empty. |
| Criticality | Criticality | Eksplicitno zadata, definisana vrednost zajedničkog enum-a. |
| Status | LifecycleStatus | Početni status je Active. |
| CreatedAt | DateTimeOffset | Vreme registracije; ne menja se. |
| UpdatedAt | DateTimeOffset | Vreme poslednje uspešne izmene; pri registraciji jednako CreatedAt. |

AssetId, DomainId, IdentityId i Criticality pripadaju zajedničkim
domenskim tipovima unutar T01 servisa.

## Enum-i

### AssetType

- Application = 1
- Service = 2
- Device = 3
- Data = 4
- Other = 5

### LifecycleStatus

- Active = 1 — aktivan resurs.
- Inactive = 2 — privremeno neaktivan resurs koji može ponovo da se aktivira.
- Decommissioned = 3 — trajno povučen resurs koji ostaje u istoriji.

Vrednost 0 nije definisana ni u jednom od ova dva enum-a.

## Životni ciklus

Sledeća matrica predstavlja pravilo životnog ciklusa T01.
Ona je projektna razrada zahteva za promenu statusa i očuvanje
istorije; specifikacija ne propisuje pojedinačno svih devet prelaza.

| Trenutni status | Novi status | Dozvoljeno |
| --- | --- | --- |
| Active | Active | Ne |
| Active | Inactive | Da |
| Active | Decommissioned | Da |
| Inactive | Active | Da |
| Inactive | Inactive | Ne |
| Inactive | Decommissioned | Da |
| Decommissioned | Active | Ne |
| Decommissioned | Inactive | Ne |
| Decommissioned | Decommissioned | Ne |

Prelaz u isti status se odbija jer ne predstavlja promenu.

Decommissioned je završno stanje bez reaktivacije. Zapis asset-a
ostaje sačuvan; dekomisioniranje ne predstavlja fizičko brisanje.

Nedefinisane vrednosti početnog ili ciljnog statusa se odbijaju.

## Izvršivo pravilo

Pravilo je definisano metodom:

`AssetLifecycle.CanTransition(LifecycleStatus current, LifecycleStatus next)`

Metoda vraća true za četiri dozvoljena prelaza iz matrice.
Za ostale slučajeve vraća false.

Metoda nema sporednih efekata: ne menja entitet, ne pristupa bazi,
ne poziva druge servise i ne šalje audit događaje.

Operacija promene statusa treba da proveri ovo pravilo pre izmene
stanja. Odbijeni prelaz ne sme promeniti Status niti UpdatedAt.

CanTransition proverava dozvoljenost prelaza, a ne ovlašćenje
korisnika da izvrši operaciju.

## Zavisnosti i granice odgovornosti

Obavezan funkcionalni preduslov iz specifikacije za R1-02 je R1-01.

Zavisnosti razrađene u [README-u T01 servisa](../../README.md):

- R1-01 — provera domena.
- R1-03 — provera identiteta vlasnika.
- R1-05 — provera dozvola korisnika.
- R1-09 — slanje audit događaja.

Postojanje i aktivnost domena, validnost vlasnika i ovlašćenja
korisnika proveravaju se u aplikacionom toku preko odgovarajućih
ugovora i adaptera. Domain sloj ne koristi HTTP niti direktno
pristupa bazi.

Naziv dozvole asset.write naveden je u README-u servisa.
Konačni nazivi dozvola, endpoint-i i uslovi provere vlasnika
usklađuju se sa prihvaćenim ugovorima nadležnih timova.
Njihovo navođenje ovde ne predstavlja potvrdu prihvatanja ugovora.

Kritičnost se eksplicitno zadaje pri registraciji, a njene promene
moraju biti auditovane. Slanje audit događaja nije odgovornost
metode CanTransition.

## Testovi

AssetLifecycleTests sadrži devet parametrizovanih NUnit slučajeva,
po jedan za svaki red matrice: četiri dozvoljena i pet zabranjenih
prelaza.

Negativni slučajevi proveravaju zabranu prelaska u isti status
i zabranu svih prelaza iz Decommissioned. Time je obuhvaćen
i pokušaj reaktivacije trajno povučenog asset-a.

Nedefinisane enum vrednosti odbijaju se implementacijom, ali nisu
obuhvaćene ovih devet test slučajeva.

Pokretanje lifecycle testova iz foldera services/t01-assets-risk:

    dotnet test --filter FullyQualifiedName~AssetLifecycleTests

Pokretanje svih testova T01 servisa kroz Docker, iz korena
repozitorijuma u Bash terminalu:

    bash scripts/scutum.sh test t01

Testovi pravila potvrđuju dozvoljenost prelaza. Provera da operacije
entiteta koriste pravilo, čuvaju stanje pri odbijanju i primenjuju
autorizaciju zahteva zasebne testove odgovarajućih operacija.
# R1-02 — Asset model i životni ciklus

## Namena i obim

Asset predstavlja informacioni resurs: aplikaciju, servis, uređaj,
podatke ili drugi resurs.

Izvor zahteva: OIB projektna specifikacija, R1-02
„Asset inventory i kritičnost resursa“, strane 28–29.

Ovaj dokument opisuje polja modela i pravila životnog ciklusa.
Enum-i i AssetLifecycle implementirani su na grani feature/R1-02-model.
Entitet Asset i njegove invarijante razvijaju se zasebno na grani
feature/R1-02-invariant; opis polja usklađen je sa tim modelom.

## Polja modela Asset

- Id — AssetId: jedinstveni identifikator, generiše se pri registraciji
  i ne menja se.
- Name — string: obavezan naziv, najviše 200 karaktera.
  Model odbija prazan naziv i naziv sastavljen samo od razmaka.
  Pri čuvanju uklanja početne i završne razmake.
- Type — AssetType: obavezna, definisana enum vrednost; ne menja se
  nakon registracije.
- DomainId — DomainId: identifikator domena kome asset pripada.
  Vrednost ne sme biti Guid.Empty; ne menja se nakon registracije.
- OwnerId — IdentityId: identifikator odgovornog vlasnika.
  Pri registraciji ne sme biti Guid.Empty.
- Criticality — Criticality: kritičnost mora biti eksplicitno zadata
  kao definisana vrednost zajedničkog enum-a.
- Status — LifecycleStatus: početna vrednost je Active.
- CreatedAt — DateTimeOffset: vreme registracije; ne menja se.
- UpdatedAt — DateTimeOffset: vreme poslednje uspešne izmene.
  Pri registraciji jednako je CreatedAt.

AssetId, DomainId, IdentityId i Criticality pripadaju zajedničkim
domenskim tipovima. Njihove definicije se ne dupliraju u ovom zadatku.

## Vrste asset-a

AssetType ima sledeće vrednosti:

- Application = 1
- Service = 2
- Device = 3
- Data = 4
- Other = 5

Vrednost 0 nije definisana i ne predstavlja validan tip.

## Statusi

LifecycleStatus ima sledeće vrednosti:

- Active = 1 — aktivan resurs.
- Inactive = 2 — privremeno neaktivan resurs, može ponovo da se aktivira.
- Decommissioned = 3 — trajno povučen resurs koji ostaje u istoriji.

Vrednost 0 nije definisana i ne predstavlja validan status.

## Pravila prelaza statusa

Specifikacija zahteva promenu statusa i očuvanje dekomisioniranog
asset-a u istoriji. Sledeća matrica je projektna odluka kojom se
precizira svih devet kombinacija prelaza:

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

Prelaz u isti status nije promena i metoda ga odbija.
Decommissioned je završno stanje: reaktivacija nije dozvoljena.
Nedefinisane vrednosti početnog ili ciljnog statusa takođe se odbijaju.

## Izvršivo pravilo

Pravilo je implementirano metodom:

AssetLifecycle.CanTransition(LifecycleStatus current, LifecycleStatus next)

Metoda vraća true za dozvoljen prelaz, a false za ostale slučajeve.
Nema sporednih efekata: ne menja Asset, ne upisuje podatke i ne šalje
audit događaje.

Pri integraciji promene statusa, domenska operacija treba da pozove
ovo pravilo pre izmene stanja. Odbijeni prelaz ne sme promeniti
Status niti UpdatedAt.

Sama metoda CanTransition nije implementacija celog use-case-a
promene statusa i ne proverava ovlašćenja korisnika.

## Invarijante i zavisnosti

- Asset mora imati jedinstven identitet.
- Dekomisioniran asset ostaje u istoriji; ne briše se fizički.
- U usvojenom domenskom modelu dekomisioniran asset više se ne menja.
- Kritičnost mora biti eksplicitna, a njena promena auditovana.
- Funkcionalni preduslov R1-02 je R1-01: domeni.
- Provera postojanja i aktivnog statusa domena pripada aplikacionom
  toku preko ugovora sa servisom domena.
- Provera vlasnika, autorizacija i slanje audit događaja rešavaju se
  kroz odgovarajuće aplikacione tokove i ugovore sa drugim servisima.
- Domain sloj ne poziva HTTP servise niti direktno pristupa bazi.

## Testovi i dokaz provere

AssetLifecycleTests sadrži devet parametrizovanih NUnit slučajeva,
po jedan za svaki red matrice prelaza.

Proveravaju se četiri dozvoljena i pet zabranjenih prelaza.
Negativni scenariji uključuju pokušaj reaktivacije dekomisioniranog
asset-a i pokušaj prelaska u isti status.

Nedefinisane enum vrednosti odbijaju se implementacijom, ali nisu
obuhvaćene ovih devet test slučajeva.

Pokretanje iz foldera services/t01-assets-risk:

dotnet test --filter FullyQualifiedName~AssetLifecycleTests

Prolazak ovih testova potvrđuje pravilo prelaza, a ne završetak
celog R1-02 use-case-a ili integracije sa drugim servisima.
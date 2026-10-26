# R2-11 · Vulnerability registry i remediation — model nalaza

Celina R2-11 vodi evidenciju poznatih slabosti (nalaza), njihovu vezu sa asset-ima i plan otklanjanja,
bez izvođenja napada nad realnim sistemima (specifikacija, str. 55).

- **Vlasnik celine:** T01 (član 4)
- **Preduslovi:** R1-02 (asset registry, preko `IAssetLookup`), R1-12 (izvor nalaza — nema vlasnika, koristimo simulator `sim/findings.json`)
- **Koristi je:** R3-03 (risk register, preko `IFindingLookup`)

## Backlog i redosled implementacije

Story-ji su u Tapiz Backlog-u, `[R2-11] UC-01` … `UC-05`.

| Red | Use-case | Preduslov | Bezbednosni zahtev | Negativni scenario |
|---|---|---|---|---|
| 1 | UC-01 Uvoz simuliranog nalaza | R1-12 (simulator) | `finding.import`; isti `Source + ExternalRef` ne pravi duplikat (SEC-CONV-08) | ponovljen uvoz → jedan nalaz; nalaz za tuđi domen se odbija |
| 2 | UC-02 Povezivanje sa asset-om | UC-01, R1-02 | asset mora postojati u domenu subjekta | nepostojeći ili tuđi asset → nalaz ostaje Incomplete |
| 3 | UC-03 Procena prioriteta | UC-02 | prioritet uzima i kritičnost asset-a, ne samo severity | nalaz bez asset-a ne dobija prioritet |
| 4 | UC-04 Dodela remediation owner-a | UC-03, R1-03 (T12, stub) | owner je aktivan identitet iz domena asset-a | nepostojeći identitet → dodela se odbija |
| 5 | UC-05 Zatvaranje nalaza uz dokaz | UC-04 | `finding.close`; zatvaranje samo uz verifikacioni dokaz | zatvaranje bez dokaza → `T01.INVALID_TRANSITION` |

## Entitet `VulnerabilityFinding`

| Polje | Tip | Pravilo |
|---|---|---|
| `Id` | `FindingId` (UUID) | generiše server, nikad klijent |
| `Source` + `ExternalRef` | `string` | par je jedinstven — ponovljen uvoz vraća postojeći nalaz (idempotentno) |
| `Title` | `string` 1–300 | obavezno |
| `Severity` | `Severity` (Info, Low, Medium, High, Critical) | dolazi iz skenera/simulatora |
| `AffectedAssetId` | `AssetId?` | `null` = nalaz je **Incomplete** |
| `Priority` | `P1`–`P4` | **računa se**, ne unosi se ručno |
| `RemediationOwnerId` | `IdentityId?` | obavezan pre prelaza u `Assigned` |
| `DueDate` | `DateTimeOffset?` | izvodi se iz prioriteta |
| `Status` | `FindingStatus` | počinje kao `Imported` |
| `Evidence` | `Kind`, `Reference`, `VerifiedBy`, `VerifiedAt` | obavezan za `Closed` |

Nalaz pripada domenu svog asset-a. Nalaz bez asset-a nema prioritet i ne može dalje od `Imported`.

## Statusi i prelazi

```
Imported ──► Triaged    samo ako AffectedAssetId != null i prioritet je izračunat
Triaged  ──► Assigned   samo ako postoji RemediationOwnerId
Assigned ──► Closed     samo uz Evidence
Closed                  kraj (ponovno otvaranje = moguće proširenje "retest workflow")
```

Svaki drugi prelaz (npr. `Imported → Closed`, `Triaged → Closed`, bilo šta iz `Closed`)
vraća `T01.INVALID_TRANSITION`.

## Prioritet

Pravilo iz specifikacije: **severity nije isto što i poslovni rizik**. Zato prioritet uzima i kritičnost asset-a.

```
score = (int)Severity × (int)Criticality
P1: score ≥ 12   P2: score ≥ 8   P3: score ≥ 4   P4: ostalo
```

| Primer | Severity | Criticality | Score | Prioritet |
|---|---|---|---|---|
| zastareli TLS na test serveru | High (3) | Low (1) | 3 | P4 |
| isti nalaz na produkcionom serveru | High (3) | Critical (4) | 12 | P1 |
| Critical nalaz na Medium asset-u | Critical (4) | Medium (2) | 8 | P2 |

Predlog formule tim potvrđuje i upisuje u ADR.

## Granica poverenja

- **Simulator → R2-11:** podaci iz `sim/findings.json` su nepouzdan ulaz. Simulator samo vraća nalaze i ne sadrži
  security logiku (SEC-DEP-03); validaciju (dužina naslova, poznat severity, duplikati) radi R2-11.
- **R2-11 → R1-02:** asset se proverava isključivo preko `IAssetLookup`, nikad čitanjem tabela R1-02.
  `null` znači i „ne postoji“ i „tuđi domen“ — korisnik ne saznaje da tuđi asset postoji (`T01.NOT_FOUND`, ADR t01-0001).
- **R3-03 → R2-11:** R3-03 čita nalaze samo preko `IFindingLookup` (`FindingSnapshot`).

## Simulator (R1-12)

R1-12 nema vlasnika, pa nalaze učitavamo iz `services/t01-assets-risk/sim/findings.json`.
Fajl sadrži i namerno loše primere za negativne testove:

| ExternalRef | Svrha |
|---|---|
| SIM-0001 | normalan nalaz sa asset-om |
| SIM-0002 | nalaz bez asset-a → ostaje Incomplete |
| SIM-0003 | Critical nalaz sa asset-om |
| SIM-0001 (ponovo) | duplikat → uvoz ne sme da napravi drugi nalaz |

## Kod (dani 6–9)

- `src/Scutum.T01.Domain/Vulnerabilities/FindingStatus.cs`
- `src/Scutum.T01.Domain/Vulnerabilities/FindingLifecycle.cs` — `Result<FindingStatus> Next(from, to, hasAsset, hasOwner, hasEvidence)`
- `tests/Scutum.T01.Tests/Domain/FindingLifecycleTests.cs` — Triaged bez asset-a se odbija, Closed bez dokaza se odbija, pozitivan prolaz kroz ceo tok

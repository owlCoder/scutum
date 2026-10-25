# Minimalni međutimski ugovori - DRAFT v1

> Napomena: u KT1 paketu su uključeni samo portovi i stubovi (DRAFT). Contract testovi nad stubovima navedeni ispod pripadaju KT2 i nisu u ovom paketu.

Read-only demonstracija prihvaćena je kao parcijalni KT1 dokaz. Portovi u ovom
dokumentu su C# interfejsi i lokalni stubovi; nisu implementirani HTTP endpoint-i.
Update, ChangeOwner i atomski write/CAS ostaju plan KT2.

Svi ugovori su **DRAFT**. Owner je naveden po celini; koordinaciona mapa identifikuje T12 Identity/Auth, T02 RBAC i T04 audit. Reviewer i dogovorena revizija upisuju se posle koordinacije. DRAFT ne prelazi u ACCEPTED samo zato što je T03 napisao stub ili test.

Izvor pravila: SCUTUM 5.7-5.9, str. 13-15. Stubovi podržavaju normalan odgovor, relevantan negativni odgovor i unavailable; ne implementiraju upstream security logiku. Transport u ovom paketu je lokalni C# port. HTTP putanje ispod su predlog za budući adapter i nisu pokrenuti endpoint-i.

## 1. Zajedničke konvencije

- ID je case-sensitive string od 1 do 64 ASCII znaka: početno malo slovo, zatim mala slova, cifre ili `-`. Jedinstven je u owner sistemu. Consumer ne zavisi od generatora ID-a.
- SecurityDomain/ScopeId je eksplicitan string; demo koristi dom-a i dom-b. Nema implicitnog globalnog wildcard grant-a.
- `PortStatus` ima Ok, NotFound i Unavailable. Value je prisutan samo za Ok; NotFound nije dozvola. Unavailable se ne pretvara u prazan successful rezultat.
- Vreme je UTC DateTimeOffset. CorrelationId je validan Guid. Polja ugovora nemaju tajne, tokene ili kredencijale.
- Sadašnji portovi traže jedan objekat po ID-u; nema nedefinisanog list endpoint-a. Predlog limita za efektivne grantove je 256 po odgovoru; stvarni adapter mora validirati taj limit i eventualno dogovoriti pagination pre promene semantike. Lokalni fixture ima najviše tri granta.
- Breaking promena ACCEPTED ugovora zahteva novu verziju ili koordinisanu migraciju. Lifecycle: DRAFT -> ACCEPTED -> DEPRECATED -> RETIRED.
- Vlasnik svake celine upravlja svojim podacima; T03 ne menja tabele R1-05/R1-02/R1-07/R1-08/R1-10.

## 2. C-RBAC-v1: efektivne dozvole

**Owner:** R1-05. **Consumer:** T03 R1-06; R3-01 kasnije. **Port:** `IEffectivePermissionsProvider.GetFor(subjectId)`. **Predlog transporta:** `GET /internal/v1/subjects/{id}/effective-permissions`.

Ok response mora biti aktuelni snapshot dozvola za traženi subject. Svaki grant sadrži tačnu Permission i ScopeId. Upstream računa Role/Assignment/EffectivePermission; T03 samo proverava da li grant odgovara operaciji i konkretnom scope-u resursa.

```json
{
  "status": "Ok",
  "value": {
    "grants": [
      { "permission": "resources:read:own", "scopeId": "dom-a" },
      { "permission": "resources:update:own", "scopeId": "dom-a" },
      { "permission": "resources:owner:change", "scopeId": "dom-a" }
    ]
  }
}
```

Negativni odgovor: `{"status":"NotFound","value":null}` za nepoznat subject. Poznat subject bez prava može dati Ok sa grants=[]; T03 ga odbija. Failure: `{"status":"Unavailable","value":null}`; T03 daje DEPENDENCY_UNAVAILABLE bez promene stanja.

**Planirani contract testovi (nisu implementirani):** `PermissionsProviderContract.KnownSubjectHasExplicitScope` i `UnknownSubjectIsNotGrantedImplicitly`. Plan za KT2/KT3 je apstraktna fixture klasa: kada realni adapter postoji, nova izvedena fixture implementira CreateProvider i iste testove izvršava nad dogovorenim testnim stanjem. Dodatni security testovi proveravaju pogrešan scope i offline stanje.

**Otvoreno za review:** freshness/snapshot semantika, stvarni identitet porta, limit grantova, ponašanje pri deaktivaciji i revoke-u, autentikacija internog transporta. Ne uvodi se proizvoljan cache bez dogovora.

## 3. C-ASSET-v1: identitet i scope asset-a

**Owner:** R1-02. **Consumer:** T03 R1-06. **Port:** `IAssetCatalog.Find(assetId)`. **Predlog:** `GET /internal/v1/assets/{id}/authorization-context`.

```json
{
  "status": "Ok",
  "value": { "id": "asset-a", "scopeId": "dom-a", "isActive": true }
}
```

NotFound i Unavailable imaju Value=null. IsActive=false ili scope mismatch znači deny. ProtectedResource.AssetId mora odgovarati vraćenom AssetSnapshot.Id. Resource owner vezu i sadržaj poseduje T03, a R1-02 je autoritet za scope/aktivnost inventara.

**Planirani contract testovi (nisu implementirani):** `AssetCatalogContract.KnownAssetHasAuthoritativeScope` i `UnknownAssetReturnsNoMetadata`. Postojeći security testovi: `Read_WhenAssetIsInactive_Denies`, `Read_WhenDependencyIsUnavailable_FailsClosed` i testovi scope izolacije u `ReadResourceBoundaryTests`. Missing asset contract test ostaje planiran.

**Otvoreno za review:** ID mapiranje, status arhiviranog asset-a, semantika promene domena tokom operacije i timeout realnog adaptera. Stub ne računa asset security odluku.

## 4. C-SUBJECT-v1: pouzdan kontekst i novi owner

**Owner:** R1-03 / identity adapter. **Consumer:** T03 Application. **Model:** Subject(Id, ScopeId, IsAuthenticated, IsActive, Assurance). **Port za transfer:** `ISubjectDirectory.Find(subjectId)`.

Kontekst dolazi iz potvrđene sesije/adapterskog poziva; nije iz korisničkog command DTO-a. Za novi owner potreban je aktivan subject istog scope-a, ali taj owner ne mora baš tada biti prijavljen. U seed-u ovo daje SubjectsStub; u produkcionoj/realnoj integraciji to radi owner celine.

Primer: Marko iz dom-a -> Ok; unknown -> NotFound; offline -> Unavailable. Inactive i dom-b su negativni scenariji transfera. Bez prijave i aktivnog trenutnog subjekta ne postoji dozvoljena operacija.

## 5. C-POLICY-v1: aktivna politika

**Owner:** R1-08. **Consumer:** T03 R2-12 i R3-01. **Port:** `IPolicyCatalog.GetActive(scopeId)`. **DTO:** PolicyId, Version, ScopeId, Status, ValidFrom, ValidUntil.

```json
{
  "status": "Ok",
  "value": {
    "policyId": "pol-001", "version": 1, "scopeId": "dom-a",
    "status": "Active", "validFrom": "2026-10-14T12:00:00Z", "validUntil": null
  }
}
```

Stub vraća fixture ili NotFound za drugi scope; postoji Unavailable fixture. R1-08 upravlja kreiranjem, verzionisanjem, aktivacijom i arhiviranjem. T03 u KT2 ima samo referencu i contract model. **Planirani consumer:** odbija neaktivnu, nevažeću ili nedostajuću politiku; ne menja objavljenu verziju.

**Planirani test (nije implementiran):** `PolicyStubHasVersionAndRejectsWrongScope`. Ovo ne dokazuje da je već implementirana životna istorija politika ili evaluacija važenja u R3-01.

## 6. C-BASELINE-v1: baseline konfiguracije

**Owner:** R1-10. **Consumer:** T03 R2-12. **Port:** `IBaselineProvider.GetFor(targetType)`. **DTO:** BaselineId, Version, TargetType, PolicyId, PolicyVersion.

Primer: base-001 v1, TargetType=Application, pol-001 v1. Unknown target type -> NotFound, offline -> Unavailable. **Planirani test (nije implementiran):** `BaselineStubPreservesPolicyLink`.

Za KT3 treba dogovoriti sam skup API konfiguracionih pravila i minimalne dozvoljene vrednosti. Tajne se ne prenose kao plain-text configuration. Baseline reference u KT2 nije kompletna provera konfiguracije i nije zamena za R1-10.

## 7. C-CLASS-v1: aktivna klasifikacija

**Owner:** R1-07. **Consumer:** T03 R3-01. **Port:** `IClassificationProvider.GetFor(resourceId)`. **DTO:** ResourceId, Level, DataOwnerId, Version.

Primer: res-001, Confidential, DataOwnerId=ana, Version=1. Nepostojeći resurs -> NotFound; offline -> Unavailable. **Planirani test (nije implementiran):** `ClassificationStubDoesNotInventMissingClassification`.

R1-07 odlučuje o nivou, promeni i odobrenju snižavanja. T03 ne sme iz nedostajuće klasifikacije zaključiti Public. Predlog budućeg R3 ponašanja je deny pri nepoznatoj/nevažećoj klasifikaciji. Veza trenutnog resource owner-a i DataOwner-a je predmet review-a; ne pretpostavlja se da transfer automatski menja upstream klasifikaciju.

## 8. E-SECURITY-v1: audit događaj

**Owner emitter-a:** T03. **Consumer:** R1-09 / centralni audit kada adapter bude spreman. **Schema:** SecurityEvent iz Application/Contracts.cs, verzija 1. Implementacija je u `services/t03-authorization/src/Scutum.T03.Application/Contracts.cs`; KT2 dokument nije deo paketa.

Minimalno: UTC vreme, actor, action, target, outcome, correlation, sensitivity; dodatno policy verzija i interni reason. Append-only lokalni audit je deo InMemoryStore. Ne postoji tvrdnja o isporuci udaljenom audit sistemu.

ScopeId u događaju je scope subjekta. Za cross-scope deny ne isporučuje se metadata tuđeg resursa u javni DTO. Pristup audit zapisima je privilegovan u realnom adapteru; ConsoleUi prikazuje samo lokalne sintetičke demo podatke.

## 9. Idempotency, konkurentnost i failure

Realizovani `Read` ne menja resurs, ali upisuje audit za svaki obrađeni zahtev.
Ponovljeno čitanje zato stvara novi audit zapis. CorrelationId služi praćenju,
ne predstavlja deduplication ključ. `Unavailable` odgovor porta ne daje pristup.

**PLAN KT2, nije implementiran:** Update/ChangeOwner sa ExpectedVersion,
VERSION_CONFLICT, povećanjem verzije i atomskim CAS + audit upisom. Trenutni
`IResourceStore` ima samo Find i Record; lock u InMemoryStore ne predstavlja
implementiran transakcioni tok promene vlasništva. Provera novog owner-a takođe
ostaje planirana. Realni adapter mora posebno definisati timeout, cancellation i
ponašanje pri izuzecima; trenutni stub simulira samo eksplicitan Unavailable rezultat.

## 10. Uslov prelaska u ACCEPTED

1. Stvarni owner i consumer pregledaju DTO, scope i failure semantiku.
2. Pozitivan i negativan contract/security test izvršeni su na dogovorenoj granici.
3. Upisani su reviewer, datum, revizija ugovora, commit i dokaz testa.
4. Dogovorene su otvorene tačke za aktuelnost i failure ponašanje.

U ovom paketu: DTO-i/stubovi i Read security testovi postoje. Navedene buduće contract fixture klase nisu implementirane. Međutimski review i prihvatanje ugovora nisu potvrđeni. Nema lažnog ACCEPTED statusa.

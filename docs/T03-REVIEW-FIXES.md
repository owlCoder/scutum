# T03 — ispravke nakon request changes

## Osnova

Korisnički paket `scutum(1).7z`, T03 grana sa poslednjim commit-om 752cc5c.
Zajednička struktura: preuzet patch PR-a https://github.com/owlCoder/scutum/pull/2,
11 commit-a do izvornog commit-a `9aed67bb7765909c7de839fd4c9d1b40aafd58e2`.
Patch je uspešno rekonstruisan nad priloženim origin/main (9099cf8).
Ovo je lokalna integracija sadržaja, ne tvrdnja da je PR #2 merge-ovan ili prihvaćen.

## Šta je promenjeno

1. `docs/KT1.md`: postojeći Read odvojen od planiranih Update/ChangeOwner tokova;
   uklonjene nepostojeće reference na testove, TAPIZ.csv i git bundle; verzionisani
   upis/CAS i atomski mutation audit jasno označeni kao plan KT2.
2. `docs/UGOVORI.md`: nepostojeće contract fixture klase označene kao plan;
   uklonjene tvrdnje o implementiranom CAS-u i referenca na nepostojeći KT2 dokument.
3. Svi T03 projekti, solution, props, SDK konfiguracija i pomoćne skripte premešteni
   u `services/t03-authorization/`. ProjectReference putanje unutar servisa sačuvane.
4. Dodato devet NUnit slučajeva (7 metoda sa parametrizacijom) uz tri originalna:
   scope subjekta, dva pogrešna scoped granta (ReadOwn/ReadAny), ispravan ReadAny,
   neaktivan asset, neaktivan subjekt, dva nedostupna porta i jednak javni odgovor.
5. Aktiviran T03 u `service.yaml`; Docker tests profil izvršava .NET testove/demo i
   Python statičku proveru. Nema obavezne baze za InMemory KT1 tok.
6. Dopunjeni lokalni README, demo, threat model i status validacije.

Postojeći C# aplikacioni/domain/infrastructure/demo kod i originalna tri testa
ostali su identični poslatom paketu. Globalni CI i sve zajedničke skripte ostali su
identični preuzetom PR-u #2; nisu zamenjeni T03-specifičnim workflow-om.

## Pre merge-a

- Potvrditi sa asistentom da je Read-only realizacija prihvatljiv KT1 obuhvat.
  ChangeOwner ostaje obavezan za konačni R1-06, a write/CAS i transfer su plan KT2.
- Usaglasiti redosled sa autorom PR-a #2: najpre zajednička struktura ili dogovorena
  integraciona grana, zatim T03 promene. Ne prepisivati novije zajedničke fajlove.
- Pokrenuti globalni CI na konačnoj grani; novi NUnit i Docker prolaz nisu potvrđeni
  u ovom okruženju. Vidi `services/t03-authorization/docs/VALIDACIJA.md`.
- Review/ACCEPTED status ugovora potvrđuju stvarni timovi; nije izmišljen u dokumentima.

## Kako koristiti isporuku

ZIP sadrži kompletan pregledni snapshot i patch prema korisničkom T03 paketu.
Otvoriti solution `services/t03-authorization/Scutum.T03.sln` iz NOVOG raspakovanog
foldera. Ne raspakovavati preko starog src/tests stabla: običan ZIP ne briše stare
fajlove i mogao bi ostaviti duplikate.

Za postojeću T03 granu prvo sačuvati svoje promene i proveriti da je radno stablo
čisto. U korenu repozitorijuma proveriti priloženi patch:

```sh
git apply --check /putanja/do/T03-ispravke.patch
```

Tek ako se provera uspešno završi, primeniti:

```sh
git apply /putanja/do/T03-ispravke.patch
```

Patch uključuje i snapshot zajedničkih promena PR-a #2 jer ga korisnička grana
još nema. Ako je #2 u međuvremenu integrisan, koristiti `T03-posle-PR2.patch`
nad čistom granom koja sadrži odgovarajuću verziju #2; taj patch sadrži samo T03
promene i ne menja globalni CI. Ne primenjivati oba patch-a jedan za drugim.
Ako `--check` prijavi konflikt, ne koristiti force; uskladiti sa stvarnim stanjem grane.

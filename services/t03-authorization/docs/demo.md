# T03 KT1 demo

Iz `services/t03-authorization/` pokrenuti:

```sh
dotnet run --project src/Scutum.T03.ConsoleUi --configuration Release
dotnet test Scutum.T03.sln --configuration Release
```

1. Seed: Ana i Marko u dom-a, res-001 pripada Ani. Oboje imaju ReadOwn.
2. Ana čita res-001: Granted, sadržaj je vraćen.
3. Marko zna isti ID: Denied, RESOURCE_UNAVAILABLE, Resource=null.
4. Demo ispisuje `DEMO PASS: 2/2 očekivana ishoda.`
5. NUnit dodatno proverava scope, pogrešan scope granta, neaktivnost, unavailable
   portove i jednakost kompletnog javnog odgovora za nepostojeći i tuđi resurs.
6. Audit razlozi se proveravaju u testovima; nisu deo javnog odgovora.

Ovaj demo ne demonstrira Update, ChangeOwner, HTTP autorizaciju niti R3 engine.

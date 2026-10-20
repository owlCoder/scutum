# T10 frontend

Next.js 16 aplikacija servisa T10. Pokretanje, komande i pravila su u [README-u servisa](../../README.md#frontend-lokalno-hot-reload).

```text
src/app/          rute i raspored (bez poslovne logike); (app)/ = zaštićeni deo, health/ = GET /health
src/features/     moduli: sources (R1-19), telemetry (R2-17), detection (R3-15)
src/components/   ui/ (shadcn, generisano) i layout/ (shell)
src/lib/api/      server-only klijent ka t10-api, generisani tipovi, mapiranje grešaka
src/lib/auth/     server-only sesija (dolazi sa autentikacijom)
src/test/         Vitest setup
e2e/              Playwright demo scenariji (lokalno, nisu u CI-ju)
```

# T28 — Incidenti, istrage, response

> **Status:** `scaffold` (prazan šablon). Kada servis dobije `tests` profil koji prolazi, promenite
> `status: active` u [`service.yaml`](service.yaml) — od tada ga CI testira na svakom PR-u.
> Pravila strukture i konvencija: [README u korenu repozitorijuma](../../README.md).

## Odgovornost tima

Projektne celine:

- **R2-09** — Security incident management
- **R2-10** — Investigation, evidence i timeline
- **R3-06** — Automatizacija containment i response akcija

Tim:   ·  Stack: nije odabran (specifikacija preporučuje .NET / C#; drugi stack uz odobrenje nastavnog tima)

<!-- Jednom rečenicom: šta ovaj servis radi i šta NE radi (granica odgovornosti). -->

## Pokretanje

Linux / WSL / Git Bash:

```bash
bash scripts/scutum.sh network      # jednom po računaru
bash scripts/scutum.sh up t28          # api + baza
curl http://localhost:8028/health  # očekivano: OK
bash scripts/scutum.sh test t28        # isto što radi CI
bash scripts/scutum.sh down t28        # dodajte --volumes da obrišete podatke baze
```

Windows PowerShell / Command Prompt:

```powershell
scripts\scutum.bat network
scripts\scutum.bat up t28
curl http://localhost:8028/health
scripts\scutum.bat test t28
scripts\scutum.bat down t28
```

Preduslovi, izbor terminala i rešavanje problema opisani su u [README u korenu
repozitorijuma](../../README.md#preduslovi-i-brzi-početak-prvi-put-na-računaru).

| Šta | Gde |
| --- | --- |
| API (spolja) | `http://localhost:8028` (unutar mreže `scutum-net`: `http://t28-api:8080`) |
| UI / demo (opciono) | `http://localhost:8128` |
| Baza (samo za vaš alat) | `127.0.0.1:3428` |
| Konfiguracija | `.env` (iz `.env.example`), bez tajni u repozitorijumu |

Migracije, seed/demo podaci i potrebne promenljive okruženja: <!-- opisati gde su i kako se pokreću (SEC-OPS-02, SEC-OPS-03) -->

## Zavisnosti od drugih timova

| Servis / ugovor | Owner | Kako ga koristimo | Stub u razvoju |
| --- | --- | --- | --- |
| <!-- npr. C-AUDIT-v1 --> | <!-- T04 --> | <!-- env varijabla, endpoint --> | <!-- da / ne --> |

## Ugovori koje ovaj servis nudi

Upisani su i u [`contracts/REGISTRY.md`](../../contracts/REGISTRY.md).

| Ugovor | Stanje | Consumeri |
| --- | --- | --- |
| <!-- npr. C-TELEMETRY-v1 --> | <!-- DRAFT --> | <!-- T07, T04 --> |

## Trust granica i bezbednost

- Threat model: [`docs/threat-model.md`](docs/threat-model.md)
- Odluke (ADR): [`docs/adr/`](../../docs/adr/) — fajlovi sa prefiksom `t28-`
- Demo scenario (normalan i negativan): [`docs/demo.md`](docs/demo.md)

## Poznata ograničenja i residual risk

<!-- Šta kontrola ne pokriva i zašto je rizik prihvatljiv ili otvoren. -->

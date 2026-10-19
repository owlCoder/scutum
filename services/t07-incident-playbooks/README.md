# T07 — Incident playbook i post-incident unapređenja

> **Status:** `scaffold` (prazan šablon). Kada servis dobije `tests` profil koji prolazi, promenite
> `status: active` u [`service.yaml`](service.yaml) — od tada ga CI testira na svakom PR-u.
> Pravila strukture i konvencija: [README u korenu repozitorijuma](../../README.md).

## Odgovornost tima

Projektne celine:

- **R1-28** — Security playbook katalog
- **R2-28** — Incident playbook execution tracking
- **R3-09** — Post-incident review i security improvement tracking

Tim:   ·  Stack: nije odabran (specifikacija preporučuje .NET / C#; drugi stack uz odobrenje nastavnog tima)

<!-- Jednom rečenicom: šta ovaj servis radi i šta NE radi (granica odgovornosti). -->

## Pokretanje

```bash
bash scripts/scutum.sh network      # jednom po računaru
bash scripts/scutum.sh up t07          # api + baza
curl http://localhost:8007/health  # očekivano: OK
bash scripts/scutum.sh test t07        # isto što radi CI
bash scripts/scutum.sh down t07        # dodajte --volumes da obrišete podatke baze
```

| Šta | Gde |
| --- | --- |
| API (spolja) | `http://localhost:8007` (unutar mreže `scutum-net`: `http://t07-api:8080`) |
| UI / demo (opciono) | `http://localhost:8107` |
| Baza (samo za vaš alat) | `127.0.0.1:3407` |
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
- Odluke (ADR): [`docs/adr/`](../../docs/adr/) — fajlovi sa prefiksom `t07-`
- Demo scenario (normalan i negativan): [`docs/demo.md`](docs/demo.md)

## Poznata ograničenja i residual risk

<!-- Šta kontrola ne pokriva i zašto je rizik prihvatljiv ili otvoren. -->

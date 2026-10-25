"""Proverava efektivni Compose config jednog servisa (izlaz `docker compose config --format json` na stdin-u).

Upotreba: docker compose ... config --format json | python3 compose_policy.py <tNN> <oznaka-za-poruke>
Ispisuje po jednu grešku po liniji i vraća exit kod 1 ako ih ima. Pošto radi nad parsiranim configom,
pokriva i kratku i dugačku `ports` sintaksu, interpolaciju promenljivih i profile.
"""
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

SHARED_NET = "scutum-net"
ALLOWED_HOST_IP = "127.0.0.1"


def check(cfg, tid):
    errors = []
    project = f"scutum-{tid}"
    api = f"{tid}-api"
    stub_prefix = f"{tid}-stub-"

    if cfg.get("name") != project:
        errors.append(f"ime compose projekta mora biti '{project}' (sada '{cfg.get('name')}')")

    networks = cfg.get("networks") or {}
    shared_keys = {key for key, net in networks.items() if (net or {}).get("name") == SHARED_NET}
    for key, net in networks.items():
        net = net or {}
        if key in shared_keys:
            if not net.get("external"):
                errors.append(f"mreža '{key}' ({SHARED_NET}) mora biti external; inače compose pravi privatnu kopiju")
        elif net.get("external"):
            errors.append(f"mreža '{key}' ne sme biti external; jedina zajednička mreža je {SHARED_NET}")
        elif net.get("name") != f"{project}_{key}":
            errors.append(f"mreža '{key}' ne sme imati ručno zadato ime ('{net.get('name')}'); "
                          "test projekat mora dobiti svoju kopiju")

    for key, vol in (cfg.get("volumes") or {}).items():
        vol = vol or {}
        if vol.get("external") or vol.get("name") != f"{project}_{key}":
            errors.append(f"volume '{key}' ne sme biti external niti imati ručno zadato ime; "
                          "inače 'scutum.sh test' deli podatke sa razvojnim okruženjem")

    services = cfg.get("services") or {}
    if api not in services:
        errors.append(f"nedostaje servis '{api}'")

    for name, svc in services.items():
        svc = svc or {}
        if not name.startswith(f"{tid}-"):
            errors.append(f"servis '{name}' mora imati prefiks '{tid}-'")
        if svc.get("container_name"):
            errors.append(f"servis '{name}': container_name nije dozvoljen (sudar imena i izolacija testova)")
        if svc.get("network_mode"):
            errors.append(f"servis '{name}': network_mode '{svc['network_mode']}' nije dozvoljen")
        for port in svc.get("ports") or []:
            if port.get("host_ip") != ALLOWED_HOST_IP:
                published = port.get("published") or "(nasumičan)"
                actual = port.get("host_ip") or "sve adrese (0.0.0.0)"
                errors.append(f"servis '{name}': port {published}->{port.get('target')} mora biti objavljen samo na "
                              f"{ALLOWED_HOST_IP} (sada: {actual})")

        on_shared = bool(set((svc.get("networks") or {}).keys()) & shared_keys)
        if name == api:
            if not on_shared:
                errors.append(f"servis '{api}' mora biti na mreži {SHARED_NET}")
        elif on_shared and not name.startswith(stub_prefix):
            errors.append(f"servis '{name}' ne sme biti na mreži {SHARED_NET} "
                          f"(dozvoljeno samo za '{api}' i stubove '{stub_prefix}*')")
    return errors


def main():
    tid, label = sys.argv[1], sys.argv[2]
    try:
        cfg = json.loads(sys.stdin.buffer.read())
    except ValueError as exc:
        print(f"{label}: efektivni compose config nije validan JSON ({exc})")
        return 1
    errors = check(cfg, tid)
    for msg in errors:
        print(f"{label}: {msg}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())

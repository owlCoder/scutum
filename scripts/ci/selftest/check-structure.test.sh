#!/usr/bin/env bash
# Negativni i pozitivni testovi za scripts/ci/check-structure.sh (compose politika: portovi, mreže, imena).
# Svaki slučaj pravi privremenu kopiju skripti sa jednim servisom iz šablona i menja mu docker-compose.yml.
# Potrebno: bash, git, docker compose v2.24+ (daemon NIJE potreban) i Python 3.
set -uo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0; failed=0
ok()  { echo "  ok   $1"; passed=$((passed + 1)); }
bad() { echo "  FAIL $1"; failed=$((failed + 1)); }

# Pravi čist mini-repo sa servisom t98-fixture iz šablona.
make_root() {
  local root="$WORK/$1"
  mkdir -p "$root/contracts" "$root/services"
  cp -R "$REPO/scripts" "$root/scripts"
  cp -R "$REPO/services/_template" "$root/services/_template"
  cp "$REPO/README.md" "$root/README.md"
  cp "$REPO/contracts/README.md" "$REPO/contracts/REGISTRY.md" "$root/contracts/"
  bash "$root/scripts/new-service.sh" T98 fixture "Fixture" >/dev/null
  git -C "$root" init -q
  printf '%s' "$root"
}

# case <naziv> <očekivani izlaz: pass|fail> <očekivan deo poruke ili ""> <sed izraz nad docker-compose.yml>
run_case() {
  local name="$1" expect="$2" msg="$3" expr="$4" root out code
  root="$(make_root "$name")"
  local compose="$root/services/t98-fixture/docker-compose.yml"
  [ -z "$expr" ] || sed -i -e "$expr" "$compose"
  out="$(bash "$root/scripts/ci/check-structure.sh" 2>&1)"; code=$?
  if [ "$expect" = pass ]; then
    if [ "$code" -eq 0 ]; then ok "$name"; else bad "$name (očekivan prolaz)"; printf '%s\n' "$out" | sed 's/^/       /'; fi
  else
    if [ "$code" -ne 0 ] && printf '%s' "$out" | grep -qF -- "$msg"; then
      ok "$name"
    else
      bad "$name (očekivana greška koja sadrži: $msg)"; printf '%s\n' "$out" | sed 's/^/       /'
    fi
  fi
}

API_LINE='\(      - "\)127\.0\.0\.1:\(${API_PORT[^}]*}:8080"\)'

echo "check-structure.sh: compose politika"
run_case "sablon-prolazi" pass "" ""
run_case "port-0.0.0.0-kratka-sintaksa" fail "mora biti objavljen samo na 127.0.0.1" \
  "s|$API_LINE|\\10.0.0.0:\\2|"
run_case "port-bez-adrese-kratka-sintaksa" fail "mora biti objavljen samo na 127.0.0.1" \
  "s|$API_LINE|\\1\\2|"
run_case "port-duga-sintaksa-bez-host_ip" fail "mora biti objavljen samo na 127.0.0.1" \
  's|^      - "127\.0\.0\.1:${API_PORT[^}]*}:8080"|      - target: 8080\n        published: "8098"|'
run_case "port-duga-sintaksa-host_ip-0.0.0.0" fail "mora biti objavljen samo na 127.0.0.1" \
  's|^      - "127\.0\.0\.1:${API_PORT[^}]*}:8080"|      - target: 8080\n        published: "8098"\n        host_ip: 0.0.0.0|'
run_case "port-duga-sintaksa-127.0.0.1-prolazi" pass "" \
  's|^      - "127\.0\.0\.1:${API_PORT[^}]*}:8080"|      - target: 8080\n        published: "8098"\n        host_ip: 127.0.0.1|'
run_case "baza-na-scutum-net" fail "servis 't98-db' ne sme biti na mreži scutum-net" \
  's|^    networks: \[default\]               # baza NIJE|    networks: [default, scutum-net]  # baza NIJE|'
run_case "api-nije-na-scutum-net" fail "servis 't98-api' mora biti na mreži scutum-net" \
  's|^    networks: \[default, scutum-net\]   # jedino api|    networks: [default]   # jedino api|'
run_case "scutum-net-nije-external" fail "mora biti external" \
  '/^    external: true$/d'
run_case "container_name" fail "container_name nije dozvoljen" \
  's|^  t98-api:$|  t98-api:\n    container_name: t98-api|'
run_case "volume-sa-fiksnim-imenom" fail "volume 'db-data' ne sme biti external niti imati ručno zadato ime" \
  's|^  db-data:$|  db-data:\n    name: deljena-baza|'
run_case "network_mode-host" fail "network_mode 'host' nije dozvoljen" \
  's|^    networks: \[default\]$|    network_mode: host|'
run_case "pogresno-ime-projekta" fail "ime compose projekta mora biti 'scutum-t98'" \
  's|^name: scutum-t98$|name: drugi-projekat|'
run_case "neispravan-compose" fail "'docker compose config' nije uspeo" \
  's|^services:$|services: [|'

echo "rezultat: $passed prošlo, $failed palo"
[ "$failed" -eq 0 ]

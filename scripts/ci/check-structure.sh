#!/usr/bin/env bash
# Proverava da svaki folder u services/ prati dogovorenu strukturu i konvencije (README.md u korenu repoa).
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
fail=0
err()  { echo "::error::$*"; fail=1; }
warn() { echo "::warning::$*"; }

get() { sed -n "s/^$2:[[:space:]]*//p" "$1/service.yaml" | head -1 | sed 's/[[:space:]]*#.*$//; s/^"//; s/"[[:space:]]*$//'; }

for f in README.md contracts/README.md contracts/REGISTRY.md; do
  [ -f "$f" ] || err "nedostaje $f"
done

prefixes=""
for dir in services/*/; do
  dir="${dir%/}"; name="$(basename "$dir")"
  case "$name" in _*) continue ;; esac
  if ! [[ "$name" =~ ^t([0-9]{2})-[a-z0-9]+(-[a-z0-9]+)*$ ]]; then
    err "$dir: ime foldera mora biti oblika tNN-slug (npr. t10-telemetry-detection)"; continue
  fi
  nn="${BASH_REMATCH[1]}"; n=$((10#$nn)); prefixes="$prefixes t$nn"

  for f in README.md service.yaml docker-compose.yml .env.example; do
    [ -f "$dir/$f" ] || err "$dir: nedostaje $f"
  done
  for d in src tests docs; do
    [ -d "$dir/$d" ] || err "$dir: nedostaje folder $d/"
  done
  [ -f "$dir/service.yaml" ] || continue

  [ "$(get "$dir" id | tr 'T' 't')" = "t$nn" ] || err "$dir/service.yaml: id mora biti T$nn"
  [ "$(get "$dir" api_port)" = "$((8000 + n))" ] || err "$dir/service.yaml: api_port mora biti $((8000 + n))"
  [ "$(get "$dir" ui_port)"  = "$((8100 + n))" ] || err "$dir/service.yaml: ui_port mora biti $((8100 + n))"
  [ "$(get "$dir" db_port)"  = "$((3400 + n))" ] || err "$dir/service.yaml: db_port mora biti $((3400 + n))"
  st="$(get "$dir" status)"
  case "$st" in scaffold|active) ;; *) err "$dir/service.yaml: status mora biti 'scaffold' ili 'active'" ;; esac

  if [ -f "$dir/.env.example" ]; then
    grep -q "^API_PORT=$((8000 + n))$" "$dir/.env.example" ||
      err "$dir/.env.example: API_PORT mora biti $((8000 + n))"
    grep -q "^DB_PORT_HOST=$((3400 + n))$" "$dir/.env.example" ||
      err "$dir/.env.example: DB_PORT_HOST mora biti $((3400 + n))"
    grep -q "^DB_HOST=t$nn-db$" "$dir/.env.example" ||
      err "$dir/.env.example: DB_HOST mora biti t$nn-db"
    grep -q '^DB_PORT=3306$' "$dir/.env.example" ||
      err "$dir/.env.example: DB_PORT mora biti 3306"
  fi

  if [ -f "$dir/docker-compose.yml" ]; then
    grep -q "^name: scutum-t$nn\$" "$dir/docker-compose.yml" || err "$dir/docker-compose.yml: očekujem 'name: scutum-t$nn'"
    grep -q 'scutum-net' "$dir/docker-compose.yml"            || err "$dir/docker-compose.yml: api mora biti na mreži scutum-net"
    if grep -Eq '^[[:space:]]*-[[:space:]]*"?[0-9]{2,5}:[0-9]+' "$dir/docker-compose.yml"; then
      err "$dir/docker-compose.yml: portovi se objavljuju samo na 127.0.0.1 (npr. \"127.0.0.1:80$nn:8080\")"
    fi
  fi
  if git ls-files --error-unmatch "$dir/.env" >/dev/null 2>&1; then
    err "$dir/.env je commit-ovan — ukloniti (git rm --cached) i rotirati vrednosti"
  fi
  if [ "$st" = "active" ]; then
    [ -f "$dir/docs/threat-model.md" ] || warn "$dir: aktivan servis nema docs/threat-model.md (obavezno po specifikaciji, 5.3)"
    [ -f "$dir/docs/demo.md" ]         || warn "$dir: aktivan servis nema docs/demo.md (normalni i negativni demo scenario, 11.1)"
  fi
done

dups="$(printf '%s\n' $prefixes | sort | uniq -d)"
[ -z "$dups" ] || err "više foldera za istu temu: $dups"

[ "$fail" -eq 0 ] && echo "Struktura repozitorijuma je u redu."
exit "$fail"

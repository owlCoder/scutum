#!/usr/bin/env bash
# SCUTUM pomoćna skripta za lokalni rad. Pokreće se iz bilo kog foldera:
#   bash scripts/scutum.sh <komanda> [tNN ...]
# Windows: koristite Git Bash ili WSL.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NET="scutum-net"

die() { echo "Greška: $*" >&2; exit 1; }

usage() {
  cat <<USAGE
Upotreba: bash scripts/scutum.sh <komanda> [argumenti]

  network                 napravi zajedničku Docker mrežu '$NET' (jednom po računaru)
  list                    prikaži sve servise, status i portove
  up   <tNN> [<tNN> ...]  pokreni servis(e) (api + baza) u pozadini
  down <tNN> [<tNN> ...]  zaustavi servis(e); dodajte --volumes da obrišete i podatke baze
  test <tNN> [--force]    pokreni 'tests' profil servisa (kao CI); --force pokreće i 'scaffold'

Primer integrisanog moda (vaš servis + servis od kog zavisite):
  bash scripts/scutum.sh up t10 t04
USAGE
}

norm() { printf '%s' "$1" | tr 'A-Z' 'a-z'; }

# t10 | T10 | services/t10-naziv  ->  apsolutna putanja foldera servisa
resolve() {
  local a; a="$(norm "$1")"; a="${a#services/}"; a="${a%/}"; a="${a%%-*}"
  [[ "$a" =~ ^t[0-9]{2}$ ]] || die "'$1' nije oznaka teme (očekujem npr. t10)"
  local matches=( "$ROOT"/services/"$a"-*/ )
  [ -d "${matches[0]}" ] || die "ne postoji folder services/$a-*"
  [ "${#matches[@]}" -eq 1 ] || die "više foldera za $a"
  printf '%s' "${matches[0]%/}"
}

svc_get() { # <dir> <ključ>
  sed -n "s/^$2:[[:space:]]*//p" "$1/service.yaml" | head -1 | sed 's/[[:space:]]*#.*$//; s/^"//; s/"[[:space:]]*$//'
}

need_docker() {
  command -v docker >/dev/null 2>&1 || die "Docker nije instaliran ili nije u PATH-u"
  docker compose version >/dev/null 2>&1 || die "potreban je Docker Compose v2 ('docker compose')"
}

ensure_net() {
  docker network inspect "$NET" >/dev/null 2>&1 || { echo "Pravim mrežu $NET"; docker network create "$NET" >/dev/null; }
}

ensure_env() { # <dir>
  if [ ! -f "$1/.env" ]; then
    [ -f "$1/.env.example" ] || die "nedostaje $1/.env.example"
    cp "$1/.env.example" "$1/.env"
    echo "Napravljen $(basename "$1")/.env iz .env.example (razvojne vrednosti; fajl je u .gitignore)"
  fi
}

cmd_list() {
  printf '%-5s %-9s %-6s %-6s %s\n' TEMA STATUS API DB FOLDER
  for d in "$ROOT"/services/t[0-9][0-9]-*/; do
    [ -f "$d/service.yaml" ] || continue
    d="${d%/}"
    printf '%-5s %-9s %-6s %-6s %s\n' "$(svc_get "$d" id)" "$(svc_get "$d" status)" \
      "$(svc_get "$d" api_port)" "$(svc_get "$d" db_port)" "services/$(basename "$d")"
  done
}

cmd_up() {
  [ $# -ge 1 ] || die "navedite bar jednu temu (npr. t10)"
  need_docker; ensure_net
  local a d id
  for a in "$@"; do
    d="$(resolve "$a")"; id="$(norm "$(svc_get "$d" id)")"
    ensure_env "$d"
    echo "==> up $id"
    ( cd "$d" && docker compose up -d --build )
    echo "    api: http://localhost:$(svc_get "$d" api_port)/health   baza: 127.0.0.1:$(svc_get "$d" db_port)"
  done
}

cmd_down() {
  local vol="" ids=() a
  for a in "$@"; do
    case "$a" in --volumes|-v) vol="--volumes" ;; *) ids+=("$a") ;; esac
  done
  [ "${#ids[@]}" -ge 1 ] || die "navedite bar jednu temu (npr. t10)"
  need_docker
  local d
  for a in "${ids[@]}"; do
    d="$(resolve "$a")"; ensure_env "$d"
    echo "==> down $a"
    ( cd "$d" && docker compose --profile tests down $vol --remove-orphans )
  done
}

cmd_test() {
  local force=0 target="" a
  for a in "$@"; do
    case "$a" in --force) force=1 ;; *) target="$a" ;; esac
  done
  [ -n "$target" ] || die "navedite temu (npr. t10)"
  local d id status
  d="$(resolve "$target")"; id="$(norm "$(svc_get "$d" id)")"; status="$(svc_get "$d" status)"
  if [ "$status" != "active" ] && [ "$force" -eq 0 ]; then
    echo "::notice::$id ima status '$status' - testovi se preskaču (postavite 'status: active' u service.yaml kada tests profil proradi)"
    return 0
  fi
  need_docker; ensure_net; ensure_env "$d"
  ( cd "$d"
    if ! docker compose config --profiles | grep -qx tests; then
      echo "::error::$id: docker-compose.yml nema profil 'tests' (servis ${id}-tests ili ${id}-tests-<naziv>)"; exit 1
    fi
    # Svi servisi profila 'tests' čije ime je tNN-tests ili tNN-tests-<naziv> (npr. t10-tests-api, t10-tests-ui).
    local suites
    suites="$(docker compose --profile tests config --services | grep -E "^${id}-tests(-[a-z0-9-]+)?\$" | sort || true)"
    if [ -z "$suites" ]; then
      echo "::error::$id: profil 'tests' nema servis ${id}-tests ni ${id}-tests-<naziv>"; exit 1
    fi

    # Testovi rade u ZASEBNOM compose projektu (scutum-tNN-test), da ne diraju razvojni 'up' istog servisa:
    #  - sopstveni kontejneri i volumeni (scutum-tNN-test_*), pa cleanup sa --volumes ne briše razvojnu bazu;
    #  - bez objavljenih portova, pa nema sudara sa već pokrenutim api/bazom na 127.0.0.1;
    #  - scutum-net je zamenjen privatnom mrežom projekta: aliasi (tNN-api, stubovi) rade unutar testa,
    #    a test kontejneri se ne pojavljuju na zajedničkoj mreži drugih timova.
    # Izolacija pretpostavlja da compose nema container_name ni ručno zadata imena volumena/mreža;
    # to proverava scripts/ci/check-structure.sh.
    local project="scutum-${id}-test" override=".scutum-test.override.yml" svc
    {
      echo "# Generiše scripts/scutum.sh test; ne menjati ručno."
      echo "services:"
      for svc in $(docker compose --profile '*' config --services); do
        printf '  %s:\n    ports: !reset []\n' "$svc"
      done
      printf 'networks:\n  %s: !override\n    driver: bridge\n' "$NET"
    } >"$override"
    local dc=(docker compose -p "$project" -f docker-compose.yml -f "$override" --profile tests)
    trap '"${dc[@]}" down --volumes --remove-orphans >/dev/null 2>&1 || true; rm -f "$override"' EXIT
    "${dc[@]}" down --volumes --remove-orphans >/dev/null 2>&1 || true   # ostaci prekinutog prethodnog pokretanja
    local suite failed=""
    for suite in $suites; do
      echo "==> test $id ($suite) [projekat $project]"
      "${dc[@]}" run --rm --build "$suite" || failed="$failed $suite"
    done
    if [ -n "$failed" ]; then
      echo "::error::$id: testovi nisu prošli:$failed"; exit 1
    fi
  )
}

case "${1:-}" in
  network) need_docker; ensure_net; echo "Mreža $NET postoji." ;;
  list)    cmd_list ;;
  up)      shift; cmd_up "$@" ;;
  down)    shift; cmd_down "$@" ;;
  test)    shift; cmd_test "$@" ;;
  -h|--help|help|"") usage ;;
  *) usage; die "nepoznata komanda '$1'" ;;
esac

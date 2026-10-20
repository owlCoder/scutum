#!/usr/bin/env bash
# Regresioni test: 'scutum.sh test tNN' ne sme da ugasi razvojni 'scutum.sh up tNN' niti da obriše njegove podatke.
# Koristi privremeni servis t98 sa busybox kontejnerima (bez build-a i bez prave baze).
# Potrebno: bash, pokrenut Docker daemon sa Compose v2.24+. Pravi mrežu scutum-net ako ne postoji (kao 'scutum.sh network').
set -uo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
WORK="$(mktemp -d)"
SVC="$WORK/services/t98-isolation"
DEV="scutum-t98"; TEST="scutum-t98-test"

passed=0; failed=0
ok()  { echo "  ok   $1"; passed=$((passed + 1)); }
bad() { echo "  FAIL $1"; failed=$((failed + 1)); }

cleanup() {
  ( cd "$SVC" 2>/dev/null && docker compose -p "$DEV" --profile tests down --volumes --remove-orphans >/dev/null 2>&1 )
  ( cd "$SVC" 2>/dev/null && docker compose -p "$TEST" --profile tests down --volumes --remove-orphans >/dev/null 2>&1 )
  rm -rf "$WORK"
}
trap cleanup EXIT

mkdir -p "$SVC"
cp -R "$REPO/scripts" "$WORK/scripts"
cat >"$SVC/service.yaml" <<'EOF'
id: T98
status: active
api_port: 8098
ui_port: 8198
db_port: 3498
EOF
cat >"$SVC/.env.example" <<'EOF'
API_PORT=8098
DB_PORT_HOST=3498
TESTS_EXIT=0
EOF
cat >"$SVC/docker-compose.yml" <<'EOF'
name: scutum-t98
services:
  t98-api:
    image: busybox:1.36
    command: ["sh", "-c", "mkdir -p /www && echo OK > /www/health && exec httpd -f -p 8080 -h /www"]
    ports:
      - "127.0.0.1:${API_PORT:?}:8080"
    networks: [default, scutum-net]
  t98-db:                      # "baza": čuva podatke u named volume-u i drži objavljen port kao prava baza
    image: busybox:1.36
    command: ["sh", "-c", "trap 'exit 0' TERM; while :; do sleep 1; done"]
    volumes: [db-data:/data]
    ports:
      - "127.0.0.1:${DB_PORT_HOST:?}:5432"
    networks: [default]
  t98-tests:
    profiles: [tests]
    image: busybox:1.36
    # Upisuje u "bazu" test projekta i proverava da je t98-api dostupan pod svojim imenom unutar test projekta.
    command:
      - sh
      - -c
      - >-
        echo test-podatak > /data/upisali-testovi;
        for i in $$(seq 1 30); do wget -qO- http://t98-api:8080/health && exit $${TESTS_EXIT:?}; sleep 0.5; done;
        echo 't98-api nije dostupan'; exit 2
    environment:
      TESTS_EXIT: ${TESTS_EXIT:?}
    volumes: [db-data:/data]
    depends_on: [t98-db, t98-api]
    networks: [default]
networks:
  scutum-net:
    external: true
    name: scutum-net
volumes:
  db-data:
EOF

dev_running() { (cd "$SVC" && docker compose -p "$DEV" ps --status running --services) | sort | tr '\n' ' '; }
# Putanja ide unutar 'sh -c' da je Git Bash na Windows-u ne bi prepisao u Windows putanju.
dev_file()    { (cd "$SVC" && docker compose -p "$DEV" exec -T t98-db sh -c "cat /data/$1 2>/dev/null"); }

echo "scutum.sh test: izolacija od razvojnog okruženja"
bash "$WORK/scripts/scutum.sh" up t98 >/dev/null || { echo "  FAIL priprema: 'scutum.sh up t98' nije uspeo"; exit 1; }
(cd "$SVC" && docker compose -p "$DEV" exec -T t98-db sh -c 'echo razvojni-podatak > /data/marker') ||
  { echo "  FAIL priprema: upis u razvojnu bazu"; exit 1; }

# 1) Uspešan test dok razvojni servis radi: nema sudara portova, razvojno okruženje ostaje netaknuto.
if bash "$WORK/scripts/scutum.sh" test t98 >"$WORK/test1.log" 2>&1; then
  ok "test prolazi dok radi razvojni 'up' (nema sudara portova 8098/3498)"
else
  bad "test prolazi dok radi razvojni 'up'"; sed 's/^/       /' "$WORK/test1.log"
fi
[ "$(dev_running)" = "t98-api t98-db " ] && ok "razvojni api i baza i dalje rade" || bad "razvojni kontejneri ugašeni: '$(dev_running)'"
[ "$(dev_file marker)" = "razvojni-podatak" ] && ok "podaci razvojne baze su sačuvani" || bad "razvojni podatak je izgubljen"
[ -z "$(dev_file upisali-testovi)" ] && ok "testovi su pisali u svoj volume, ne u razvojni" || bad "testovi su pisali u razvojni volume"
[ -z "$(docker ps -aq --filter "label=com.docker.compose.project=$TEST")" ] && ok "test kontejneri su uklonjeni" ||
  bad "test kontejneri su ostali"
[ -z "$(docker volume ls -q --filter "label=com.docker.compose.project=$TEST")" ] && ok "test volumeni su uklonjeni" ||
  bad "test volumeni su ostali"
[ ! -e "$SVC/.scutum-test.override.yml" ] && ok "privremeni override fajl je obrisan" || bad "override fajl je ostao"

# 2) Pad testa: komanda vraća grešku, a razvojno okruženje i dalje ostaje netaknuto.
sed -i 's/^TESTS_EXIT=0$/TESTS_EXIT=1/' "$SVC/.env"
if bash "$WORK/scripts/scutum.sh" test t98 >"$WORK/test2.log" 2>&1; then
  bad "pad testa mora da obori 'scutum.sh test'"
else
  ok "pad testa obara 'scutum.sh test'"
fi
[ "$(dev_running)" = "t98-api t98-db " ] && ok "posle pada razvojni api i baza i dalje rade" || bad "posle pada: '$(dev_running)'"
[ "$(dev_file marker)" = "razvojni-podatak" ] && ok "posle pada podaci razvojne baze su sačuvani" || bad "posle pada podatak je izgubljen"

echo "rezultat: $passed prošlo, $failed palo"
[ "$failed" -eq 0 ]

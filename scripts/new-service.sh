#!/usr/bin/env bash
# Pravi folder novog servisa iz services/_template.
# Upotreba:
#   bash scripts/new-service.sh T10 telemetry-detection "Log izvori, telemetry ingestion i detection tuning" \
#        -c "R1-19|Security data-source i log-source registry" -c "R2-17|Security telemetry ingestion i normalizacija" \
#        [--team 1] [--stack "opis stack-a"]
set -euo pipefail
shopt -u patsub_replacement 2>/dev/null || true   # bash 5.2: "&" u zameni ne sme da znači "pronađeni tekst"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
die() { echo "Greška: $*" >&2; exit 1; }

[ $# -ge 3 ] || die "upotreba: new-service.sh <TNN> <slug> \"<naziv teme>\" [-c \"Rx-yy|naziv\"]... [--team N] [--stack \"...\"]"
ID="$1"; SLUG="$2"; TITLE="$3"; shift 3
[[ "$ID" =~ ^T[0-9]{2}$ ]] || die "oznaka teme mora biti oblika T10"
[[ "$SLUG" =~ ^[a-z0-9]+(-[a-z0-9]+)*$ ]] || die "slug: samo mala slova, cifre i crtice"

TEAM=""; STACK="nije odabran (specifikacija preporučuje .NET / C#; drugi stack uz odobrenje nastavnog tima)"
COMP_INLINE=""; COMP_LIST=""
while [ $# -gt 0 ]; do
  case "$1" in
    -c) IFS='|' read -r cid cname <<<"$2"
        COMP_INLINE="${COMP_INLINE:+$COMP_INLINE, }$cid"
        COMP_LIST="${COMP_LIST}- **${cid}** — ${cname}"$'\n'; shift 2 ;;
    --team) TEAM="$2"; shift 2 ;;
    --stack) STACK="$2"; shift 2 ;;
    *) die "nepoznat argument '$1'" ;;
  esac
done

NN="${ID#T}"; LC="t$NN"; N=$((10#$NN))
DEST="$ROOT/services/$LC-$SLUG"
[ ! -e "$DEST" ] || die "$DEST već postoji"
compgen -G "$ROOT/services/$LC-*" >/dev/null && die "za $LC već postoji folder"

cp -R "$ROOT/services/_template" "$DEST"
while IFS= read -r -d '' f; do
  c="$(cat "$f"; printf x)"; c="${c%x}"
  c="${c//__COMPONENTS_LIST__/${COMP_LIST%$'\n'}}"
  c="${c//__COMPONENTS_INLINE__/$COMP_INLINE}"
  c="${c//__TITLE__/$TITLE}"
  c="${c//__STACK__/$STACK}"
  c="${c//__TEAM__/$TEAM}"
  c="${c//__SLUG__/$SLUG}"
  c="${c//__ID_LC__/$LC}"
  c="${c//__ID__/$ID}"
  c="${c//__API_PORT__/$((8000 + N))}"
  c="${c//__UI_PORT__/$((8100 + N))}"
  c="${c//__DB_PORT__/$((3400 + N))}"
  printf '%s' "$c" > "$f"
done < <(find "$DEST" -type f -print0)
echo "Napravljeno: services/$LC-$SLUG  (api $((8000+N)), ui $((8100+N)), db $((3400+N)))"

#!/usr/bin/env bash
# Ispisuje JSON niz oznaka AKTIVNIH servisa koje treba testirati, npr. ["t04","t10"].
# Upotreba: changed-services.sh <base-sha> [head-ref]
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
BASE="${1:-}"; HEAD_REF="${2:-HEAD}"

all_ids() { for d in services/t[0-9][0-9]-*/; do [ -d "$d" ] && basename "$d" | cut -c1-3; done; }
status_of() { sed -n 's/^status:[[:space:]]*\([a-z]*\).*/\1/p' services/"$1"-*/service.yaml 2>/dev/null | head -1; }

candidates=""
if [ -z "$BASE" ] || [[ "$BASE" =~ ^0+$ ]] || ! git cat-file -e "${BASE}^{commit}" 2>/dev/null; then
  candidates="$(all_ids)"
else
  files="$(git diff --name-only "$BASE" "$HEAD_REF")"
  if printf '%s\n' "$files" | grep -Eq '^(scripts/|\.github/workflows/|\.github/actions/)'; then
    candidates="$(all_ids)"          # promena zajedničke infrastrukture -> proveri sve aktivne servise
  else
    candidates="$(printf '%s\n' "$files" | sed -n 's#^services/\(t[0-9][0-9]\)-.*#\1#p')"
    # Izmena ugovora contracts/tNN/C-IME-vN/ testira owner-a (tNN) i consumere iz reda tog ugovora u REGISTRY.md.
    for c in $(printf '%s\n' "$files" | sed -n 's#^\(contracts/t[0-9][0-9]/[^/]*\)/.*#\1#p' | sort -u); do
      candidates="$candidates
$(printf '%s' "$c" | cut -d/ -f2)"
      consumers="$(awk -F'|' -v loc="$c/" '!/<!--/ && index($0, loc) { print $6 }' contracts/REGISTRY.md 2>/dev/null |
                   grep -oE '[Tt][0-9]{2}' | tr 'T' 't' || true)"
      [ -z "$consumers" ] || candidates="$candidates
$consumers"
    done
  fi
fi
candidates="$(printf '%s\n' $candidates | sort -u)"

out=""
for id in $candidates; do
  [ "$(status_of "$id")" = "active" ] && out="${out:+$out,}\"$id\""
done
printf '[%s]\n' "$out"

#!/usr/bin/env bash
# Minimalna provera tajni u praćenim fajlovima (osnovni nivo, SEC-BASE-04). Ne zamenjuje alate poput gitleaks.
# Ispisuje samo imena fajlova, nikad sadržaj.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
fail=0

names="$(git ls-files | grep -Ei '(^|/)\.env$|(^|/)\.env\.(local|prod|production|dev|test|staging)$|(^|/)secrets\.json$|\.(pfx|p12|key|jks|keystore)$' || true)"
if [ -n "$names" ]; then
  echo "::error::Praćeni fajlovi koji ne smeju u repozitorijum:"; printf '%s\n' "$names"; fail=1
fi

hits="$(git grep -lIE -e '-----BEGIN ([A-Z]+ )?PRIVATE KEY-----' -e 'AKIA[0-9A-Z]{16}' \
        -e 'gh[pousr]_[A-Za-z0-9]{36,}' -e 'xox[baprs]-[A-Za-z0-9-]{10,}' \
        -- . ':!scripts/ci/check-secrets.sh' || true)"
if [ -n "$hits" ]; then
  echo "::error::Fajlovi koji liče na da sadrže tajne (privatni ključ, API token):"; printf '%s\n' "$hits"
  echo "Ako je tajna stvarno commit-ovana, obavestite nastavni tim; brisanje u novom commit-u nije dovoljno."; fail=1
fi

[ "$fail" -eq 0 ] && echo "Nisu pronađene očigledne tajne."
exit "$fail"

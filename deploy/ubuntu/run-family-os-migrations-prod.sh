#!/usr/bin/env bash
# Apply FamilyOS pack migrations from migration-files.family-os.txt (no demo seed).
# Usage:
#   ./run-family-os-migrations-prod.sh "postgresql://user:pass@127.0.0.1:5432/db"
#   # or with libpq env (preferred when password has URI-reserved chars):
#   PGHOST=127.0.0.1 PGUSER=... PGDATABASE=... PGPASSWORD=... ./run-family-os-migrations-prod.sh
set -euo pipefail

CONN="${1:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ -d "$SCRIPT_DIR/migrations" ]]; then
  MIGRATIONS="$SCRIPT_DIR/migrations"
elif [[ -d "$SCRIPT_DIR/../../migrations" ]]; then
  MIGRATIONS="$(cd "$SCRIPT_DIR/../../migrations" && pwd)"
else
  MIGRATIONS="${MIGRATIONS_DIR:-/opt/kit-platform/migrations}"
fi

LIST="$SCRIPT_DIR/migration-files.family-os.txt"
if [[ ! -f "$LIST" ]]; then
  echo "Missing $LIST" >&2
  exit 1
fi

psql_run() {
  if [[ -n "$CONN" ]]; then
    psql "$CONN" "$@"
  else
    [[ -n "${PGUSER:-}" && -n "${PGDATABASE:-}" ]] || {
      echo "Usage: $0 <postgresql-connection-uri>" >&2
      echo "   or: PGHOST PGUSER PGDATABASE PGPASSWORD $0" >&2
      exit 1
    }
    psql "$@"
  fi
}

echo "=== FamilyOS migrations (pilot) ==="
if [[ -n "$CONN" ]]; then
  echo "Database: (uri provided)"
else
  echo "Database: ${PGUSER}@${PGHOST:-127.0.0.1}:${PGPORT:-5432}/${PGDATABASE}"
fi

while IFS= read -r line || [[ -n "$line" ]]; do
  # trim CRLF (Windows checkout), comments and blanks
  line="${line//$'\r'/}"
  file="$(echo "$line" | sed 's/#.*//' | xargs || true)"
  [[ -z "$file" ]] && continue
  path="$MIGRATIONS/$file"
  if [[ ! -f "$path" ]]; then
    echo "Missing: $path" >&2
    exit 1
  fi
  echo ">> $file"
  psql_run -v ON_ERROR_STOP=1 -f "$path"
done < "$LIST"

tables=$(psql_run -t -A -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='pack_family'")
evidence=$(psql_run -t -A -c "SELECT count(*) FROM information_schema.columns WHERE table_schema='pack_family' AND column_name='evidence_url'")
echo "=== Done: pack_family tables=$tables evidence_url_cols=$evidence ==="

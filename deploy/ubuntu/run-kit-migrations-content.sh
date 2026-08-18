#!/usr/bin/env bash
# Apply KIT Marketing / Content Park migrations from migration-files.content.txt.
# Do NOT merge into run-kit-migrations-prod.sh (Pharmacy).
# Usage:
#   ./run-kit-migrations-content.sh "postgresql://user:pass@127.0.0.1:5432/db"
#   PGHOST=127.0.0.1 PGUSER=... PGDATABASE=... PGPASSWORD=... ./run-kit-migrations-content.sh
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

LIST="$SCRIPT_DIR/migration-files.content.txt"
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

echo "=== Content Park migrations (KIT_MKT) ==="
if [[ -n "$CONN" ]]; then
  echo "Database: (uri provided)"
else
  echo "Database: ${PGUSER}@${PGHOST:-127.0.0.1}:${PGPORT:-5432}/${PGDATABASE}"
fi

while IFS= read -r line || [[ -n "$line" ]]; do
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

tables=$(psql_run -t -A -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='pack_content'")
oauth=$(psql_run -t -A -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='pack_content' AND table_name='facebook_oauth_pending'")
echo "=== Done: pack_content tables=$tables facebook_oauth_pending=$oauth ==="

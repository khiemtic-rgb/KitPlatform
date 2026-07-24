#!/usr/bin/env bash
# Apply FamilyOS pack migrations 192-199 on production DB (no demo seed).
# Usage: ./run-family-os-migrations-prod.sh "postgresql://user:pass@127.0.0.1:5432/novixa_prod"
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <postgresql-connection-uri>" >&2
  exit 1
fi

CONN="$1"
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

echo "=== FamilyOS migrations (pilot) ==="
echo "Database: $CONN"

while IFS= read -r line || [[ -n "$line" ]]; do
  # trim / skip comments and blanks
  file="$(echo "$line" | sed 's/#.*//' | xargs || true)"
  [[ -z "$file" ]] && continue
  path="$MIGRATIONS/$file"
  if [[ ! -f "$path" ]]; then
    echo "Missing: $path" >&2
    exit 1
  fi
  echo ">> $file"
  psql "$CONN" -v ON_ERROR_STOP=1 -f "$path"
done < "$LIST"

tables=$(psql "$CONN" -t -A -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='pack_family'")
evidence=$(psql "$CONN" -t -A -c "SELECT count(*) FROM information_schema.columns WHERE table_schema='pack_family' AND column_name='evidence_url'")
echo "=== Done: pack_family tables=$tables evidence_url_cols=$evidence ==="

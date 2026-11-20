#!/usr/bin/env bash
# KitPlatform production migrations (001→098, no demo seed)
# Usage: ./run-migrations-prod.sh "postgresql://KitPlatform:PASSWORD@127.0.0.1:5432/novixa_prod"
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
  echo "migrations/ not found (set MIGRATIONS_DIR)" >&2
  exit 1
fi

MIGRATIONS="${MIGRATIONS_DIR:-$MIGRATIONS}"
MANIFEST="${MIGRATION_MANIFEST:-$SCRIPT_DIR/migration-files.prod.txt}"

if [[ ! -f "$MANIFEST" ]]; then
  echo "Missing manifest: $MANIFEST" >&2
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "psql not found. Install: sudo apt install postgresql-client" >&2
  exit 1
fi

echo "=== KitPlatform PRODUCTION migrations (no demo seed) ==="
echo "Database: (uri provided)"
echo "Manifest: $MANIFEST"

while IFS= read -r file || [[ -n "$file" ]]; do
  file="${file//$'\r'/}"
  [[ -z "$file" || "$file" =~ ^# ]] && continue
  path="$MIGRATIONS/$file"
  if [[ ! -f "$path" ]]; then
    echo "Missing: $path" >&2
    exit 1
  fi
  echo ">> $file"
  psql "$CONN" -v ON_ERROR_STOP=1 -f "$path"
done < "$MANIFEST"

table_count=$(psql "$CONN" -t -A -c "SELECT COUNT(*)::text FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'")
perm_count=$(psql "$CONN" -t -A -c "SELECT COUNT(*)::text FROM permissions")
tenant_count=$(psql "$CONN" -t -A -c "SELECT COUNT(*)::text FROM tenants")

echo "=== Done: $table_count tables, $perm_count permissions, $tenant_count tenants ==="
echo "Next: open https://admin.novixa.vn/setup to create the first pharmacy."

#!/usr/bin/env bash
# Apply only pending production migrations (tracked in public.kit_schema_migrations).
# Usage: ./run-incremental-migrations-prod.sh "postgresql://user:pass@127.0.0.1:5432/novixa_prod"
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <postgresql-connection-uri>" >&2
  exit 1
fi

CONN="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ -n "${MIGRATIONS_DIR:-}" && -d "$MIGRATIONS_DIR" ]]; then
  MIGRATIONS="$MIGRATIONS_DIR"
elif [[ -d "$SCRIPT_DIR/migrations" ]]; then
  MIGRATIONS="$SCRIPT_DIR/migrations"
elif [[ -d "$SCRIPT_DIR/../../migrations" ]]; then
  MIGRATIONS="$(cd "$SCRIPT_DIR/../../migrations" && pwd)"
else
  echo "migrations/ not found (set MIGRATIONS_DIR)" >&2
  exit 1
fi

MANIFEST="${MIGRATION_MANIFEST:-$SCRIPT_DIR/migration-files.prod.txt}"

if [[ ! -f "$MANIFEST" ]]; then
  echo "Missing manifest: $MANIFEST" >&2
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "psql not found. Install: sudo apt install postgresql-client" >&2
  exit 1
fi

ledger_ready() {
  psql "$CONN" -tAc "SELECT to_regclass('public.kit_schema_migrations') IS NOT NULL" | tr -d '[:space:]'
}

is_applied() {
  local file="$1"
  psql "$CONN" -tAc "SELECT 1 FROM public.kit_schema_migrations WHERE filename = '$file' LIMIT 1" | tr -d '[:space:]'
}

record_applied() {
  local file="$1"
  psql "$CONN" -v ON_ERROR_STOP=1 -c \
    "INSERT INTO public.kit_schema_migrations (filename) VALUES ('$file') ON CONFLICT (filename) DO NOTHING"
}

echo "=== KitPlatform INCREMENTAL migrations ==="
echo "Database: (uri provided)"
echo "Manifest: $MANIFEST"

if [[ "$(ledger_ready)" != "t" ]]; then
  bootstrap="$MIGRATIONS/098_schema_migrations.sql"
  if [[ ! -f "$bootstrap" ]]; then
    echo "Missing bootstrap migration: $bootstrap" >&2
    exit 1
  fi
  echo ">> BOOTSTRAP public.kit_schema_migrations via 098_schema_migrations.sql"
  psql "$CONN" -v ON_ERROR_STOP=1 -f "$bootstrap"
fi

applied_count=0
skipped_count=0
pending_count=0

while IFS= read -r file || [[ -n "$file" ]]; do
  file="${file//$'\r'/}"
  [[ -z "$file" || "$file" =~ ^# ]] && continue

  path="$MIGRATIONS/$file"
  if [[ ! -f "$path" ]]; then
    echo "Missing migration file: $path" >&2
    exit 1
  fi

  if [[ "$(is_applied "$file")" == "1" ]]; then
    echo ">> SKIP $file"
    skipped_count=$((skipped_count + 1))
    continue
  fi

  pending_count=$((pending_count + 1))
  echo ">> APPLY $file"
  psql "$CONN" -v ON_ERROR_STOP=1 -f "$path"
  record_applied "$file"
  applied_count=$((applied_count + 1))
done < "$MANIFEST"

if [[ "$(ledger_ready)" != "t" ]]; then
  echo "WARN: kit_schema_migrations ledger missing after run" >&2
  exit 1
fi

echo "=== Incremental done: applied=$applied_count skipped=$skipped_count pending_ran=$pending_count ==="

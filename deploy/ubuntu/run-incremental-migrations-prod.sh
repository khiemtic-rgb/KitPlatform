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

# Same BOM strip and greenfield substitute as run-migrations-prod.sh.
# Files already present in kit_schema_migrations are skipped before this runs,
# so a database that recorded 060_family_members_status_align.sql never executes
# the substitute and never re-reads the historical file.
prepare_migration_sql() {
  local file="$1"
  local src="$2"
  local dst="$3"
  local compat="$SCRIPT_DIR/greenfield-compat/$file"
  if [[ -f "$compat" ]]; then
    echo ">> COMPAT_BODY $file historical_file_not_executed"
    src="$compat"
  fi
  local magic
  magic=$(od -An -t x1 -N 3 "$src" | tr -d '[:space:]' | tr '[:upper:]' '[:lower:]')
  if [[ "$magic" == "efbbbf" ]]; then
    tail -c +4 "$src" > "$dst"
  else
    cp "$src" "$dst"
  fi
}

psql_file() {
  local path="$1"
  if command -v cygpath >/dev/null 2>&1; then
    path="$(cygpath -w "$path")"
  fi
  psql "$CONN" -v ON_ERROR_STOP=1 -f "$path"
}

tmp=""
cleanup_migration_tmp() {
  if [[ -n "${tmp:-}" && -f "$tmp" ]]; then
    rm -f "$tmp"
  fi
}
trap cleanup_migration_tmp EXIT

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
echo "Database: $CONN"
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
  tmp="$(mktemp)"
  prepare_migration_sql "$file" "$path" "$tmp"
  psql_file "$tmp"
  rm -f "$tmp"
  tmp=""
  record_applied "$file"
  applied_count=$((applied_count + 1))
done < "$MANIFEST"

if [[ "$(ledger_ready)" != "t" ]]; then
  echo "WARN: kit_schema_migrations ledger missing after run" >&2
  exit 1
fi

echo "=== Incremental done: applied=$applied_count skipped=$skipped_count pending_ran=$pending_count ==="

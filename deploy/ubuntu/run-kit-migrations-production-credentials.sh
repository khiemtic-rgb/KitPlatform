#!/usr/bin/env bash
# Apply Production credential migrations from migration-files.production-credentials.txt.
# Do not merge into run-kit-migrations-content.sh, run-kit-migrations-prod.sh, or the Family OS runner.
# This script is not an approval to apply. It stops unless every precondition below is exactly true:
#   PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED
#   PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED
#   PRODUCTION_CREDENTIAL_KEY_BACKUP_VERIFIED
#   PRODUCTION_CREDENTIAL_KEY_RESTORE_VERIFIED
#   PRODUCTION_CREDENTIAL_MIGRATION_APPROVED
# It then runs check-production-credential-key.sh. A failing key check stops the script.
# Missing evidence is PRODUCTION_CREDENTIAL_PRECONDITION_MISSING:<name>.
# Usage:
#   ./run-kit-migrations-production-credentials.sh "postgresql://user:pass@127.0.0.1:5432/db"
#   PGHOST=127.0.0.1 PGUSER=... PGDATABASE=... PGPASSWORD=... ./run-kit-migrations-production-credentials.sh
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

LIST="$SCRIPT_DIR/migration-files.production-credentials.txt"
if [[ ! -f "$LIST" ]]; then
  echo "Missing $LIST" >&2
  exit 1
fi

require_exact_true() {
  local name="$1"
  local value="${!name-}"
  if [[ "$value" != "true" ]]; then
    echo "PRODUCTION_CREDENTIAL_PRECONDITION_MISSING:${name}"
    exit 1
  fi
}

require_exact_true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED
require_exact_true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED
require_exact_true PRODUCTION_CREDENTIAL_KEY_BACKUP_VERIFIED
require_exact_true PRODUCTION_CREDENTIAL_KEY_RESTORE_VERIFIED
require_exact_true PRODUCTION_CREDENTIAL_MIGRATION_APPROVED

key_check="$SCRIPT_DIR/check-production-credential-key.sh"
if [[ ! -f "$key_check" ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_MISSING"
  exit 1
fi
ENV_FILE="${ENV_FILE:-/etc/kit-platform/api.env}" bash "$key_check"

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

echo "=== Production credential migrations ==="
if [[ -n "$CONN" ]]; then
  echo "Database: (uri provided)"
else
  echo "Database: ${PGUSER}@${PGHOST:-127.0.0.1}:${PGPORT:-5432}/${PGDATABASE}"
fi

while IFS= read -r line || [[ -n "$line" ]]; do
  line="${line//$'\r'/}"
  file="$(echo "$line" | sed 's/#.*//' | xargs || true)"
  [[ -z "$file" ]] && continue
  case "$file" in
    377_pack_content_production_credential_domain.sql|378_pack_content_production_credential_secret.sql|379_pack_content_production_credential_freeze.sql) ;;
    *)
      echo "Refusing non-credential migration: $file" >&2
      exit 1
      ;;
  esac
  path="$MIGRATIONS/$file"
  if [[ ! -f "$path" ]]; then
    echo "Missing: $path" >&2
    exit 1
  fi
  echo ">> $file"
  psql_run -v ON_ERROR_STOP=1 -f "$path"
done < "$LIST"

echo "=== Production credential migrations finished ==="

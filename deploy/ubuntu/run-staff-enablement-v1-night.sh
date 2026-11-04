#!/usr/bin/env bash
# Staff Enablement V1 migrations, in the order listed in migration-files.staff-enablement-v1.txt.
# Not used by run-incremental-migrations-prod.sh.
# Refuses to run unless the night approval flag is set.
# Usage:
#   STAFF_NIGHT_RELEASE=approved ./run-staff-enablement-v1-night.sh "postgresql://.../DATABASE"
set -euo pipefail

if [[ "${STAFF_NIGHT_RELEASE:-}" != "approved" ]]; then
  echo "Refusing: set STAFF_NIGHT_RELEASE=approved for the night window only." >&2
  exit 2
fi

if [[ $# -lt 1 ]]; then
  echo "Usage: STAFF_NIGHT_RELEASE=approved $0 <postgresql-connection-uri>" >&2
  exit 1
fi

CONN="$1"
case "$CONN" in
  *novixa_prod*|*kitplatform_test*|*pharmacore*)
    if [[ "${CONFIRM_PRODUCTION_DATABASE:-}" != "yes" ]]; then
      echo "Refusing a production-looking database. Night production also needs CONFIRM_PRODUCTION_DATABASE=yes." >&2
      exit 2
    fi
    ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export MIGRATION_MANIFEST="$SCRIPT_DIR/migration-files.staff-enablement-v1.txt"
exec "$SCRIPT_DIR/run-incremental-migrations-prod.sh" "$CONN"

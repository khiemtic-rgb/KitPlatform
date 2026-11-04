#!/usr/bin/env bash
# Pharmacy release v1 migrations: ops-v2, then knowledge-v1, then perf-v1, each through
# run-incremental-migrations-prod.sh so already-recorded files are skipped.
# Not used by the daytime incremental runner. Refuses to run unless the night approval flag is set.
# Usage:
#   PHARMACY_NIGHT_RELEASE=approved ./run-pharmacy-release-v1-night.sh "postgresql://.../DATABASE"
set -euo pipefail

if [[ "${PHARMACY_NIGHT_RELEASE:-}" != "approved" ]]; then
  echo "Refusing: set PHARMACY_NIGHT_RELEASE=approved for the night window only." >&2
  exit 2
fi

if [[ $# -lt 1 ]]; then
  echo "Usage: PHARMACY_NIGHT_RELEASE=approved $0 <postgresql-connection-uri>" >&2
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
for manifest in \
  migration-files.pharmacy-ops-v2.txt \
  migration-files.pharmacy-knowledge-v1.txt \
  migration-files.pharmacy-perf-v1.txt; do
  echo "=== $manifest"
  MIGRATION_MANIFEST="$SCRIPT_DIR/$manifest" "$SCRIPT_DIR/run-incremental-migrations-prod.sh" "$CONN"
done

#!/usr/bin/env bash
# Apply Kit Platform kernel/pack migrations only (067-087) on existing production DB.
# Usage: ./run-kit-migrations-prod.sh "postgresql://user:pass@127.0.0.1:5432/novixa_prod"
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

files=(
  "067_platform_events.sql"
  "068_assessment_engine.sql"
  "069_assessment_pharmacy_v1_seed.sql"
  "070_stock_movements_tenant_index.sql"
  "070_assessment_pharmacy_v1_vietnamese.sql"
  "071_kit_schemas_foundation.sql"
  "072_kit_core_iam_tenant.sql"
  "073_kit_org_workspace.sql"
  "074_kit_common_storage_party.sql"
  "075_kit_metadata.sql"
  "076_kit_event_audit_notify.sql"
  "077_kit_workflow_integration_ai.sql"
  "078_pack_clinic_crm.sql"
  "079_kit_pack_registry_workspace_party_backfill.sql"
  "080_kit_workspace_pack_rls.sql"
  "081_kit_kernel_rls_workspace.sql"
  "082_pack_pharmacy_schema.sql"
  "083_pack_survey_schema.sql"
  "084_kit_provision_pharmacy_survey_workspace.sql"
  "085_pack_pharmacy_inventory_views.sql"
  "086_pack_pharmacy_report_read_views.sql"
  "087_phase_d_write_cutover.sql"
)

echo "=== KitPlatform KIT migrations 067-087 ==="
echo "Database: (uri provided)"

for file in "${files[@]}"; do
  path="$MIGRATIONS/$file"
  if [[ ! -f "$path" ]]; then
    echo "Missing: $path" >&2
    exit 1
  fi
  echo ">> $file"
  psql "$CONN" -v ON_ERROR_STOP=1 -f "$path"
done

kit_ws=$(psql "$CONN" -t -A -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='kit_workspace'")
pack_ph=$(psql "$CONN" -t -A -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='pack_pharmacy'")
echo "=== Done: kit_workspace tables=$kit_ws pack_pharmacy tables=$pack_ph ==="

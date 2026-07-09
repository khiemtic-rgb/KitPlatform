#!/usr/bin/env bash
# KitPlatform production migrations (001→087, no demo seed)
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

if ! command -v psql >/dev/null 2>&1; then
  echo "psql not found. Install: sudo apt install postgresql-client" >&2
  exit 1
fi

files=(
  "001_extensions.sql"
  "002_identity.sql"
  "003_catalog.sql"
  "004_inventory.sql"
  "005_procurement.sql"
  "006_sales.sql"
  "007_customer_app.sql"
  "008_product_images.sql"
  "009_product_name_similarity.sql"
  "010_supplier_payment_status.sql"
  "011_v2_schema_readiness.sql"
  "012_sales_draft_nullable_batch.sql"
  "013_sales_pos_discount.sql"
  "014_sales_return_payments.sql"
  "015_sales_shifts.sql"
  "016_customer_consents_and_outbox.sql"
  "017_sales_batch_source.sql"
  "018_sales_shift_link.sql"
  "019_customer_otp_auth.sql"
  "022_fix_loyalty_program_status.sql"
  "023_sales_loyalty_redeem.sql"
  "024_loyalty_unify_redeem_value.sql"
  "025_loyalty_max_redeem_percent.sql"
  "026_loyalty_fractional_points.sql"
  "028_customer_chat.sql"
  "029_customer_draft_orders.sql"
  "030_customer_draft_order_customer_hide.sql"
  "031_sales_voucher_discount.sql"
  "033_customer_reservations.sql"
  "034_customer_reservation_sales_order.sql"
  "035_inventory_adjustment_count_entries.sql"
  "036_system_admin_permissions.sql"
  "037_vietnamese_permission_labels.sql"
  "038_po_vat_rate_percent.sql"
  "039_procurement_vat_treatments.sql"
  "039_reports_permissions.sql"
  "040_product_national_drug_link.sql"
  "041_pilot_stability_features.sql"
  "042_low_stock_group_settings.sql"
  "043_warehouse_min_stock.sql"
  "044_active_ingredients_tenant.sql"
  "045_scale_branch_readiness.sql"
  "046_chat_branch_scope.sql"
  "047_procurement_placeholder_grn_discount.sql"
  "048_sales_customer_credit.sql"
  "049_customer_payments.sql"
  "050_fix_return_ar_adjustments.sql"
  "051_platform_multi_branch_i18n.sql"
  "052_family_members.sql"
  "053_health_records.sql"
  "054_care_reminders.sql"
  "055_repurchase_and_order_reminders.sql"
  "056_customer_app_branding.sql"
  "057_p6_medication_hub.sql"
  "058_p7_notifications_family_vitals.sql"
  "059_repurchase_order_level_schema.sql"
  "060_family_members_status_align.sql"
  "061_products_product_kind.sql"
  "062_health_care_schema_align.sql"
  "063_p9_engagement_notifications.sql"
  "064_p10b_customer_app_i18n.sql"
  "065_customer_engagement_analytics.sql"
  "066_customer_otp_pilot_admin.sql"
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
  "088_purchase_order_workflow.sql"
  "089_kap_decision_intelligence_schema.sql"
  "090_kap_pharmacy_intelligence_seed.sql"
  "092_qd540_integration_schema.sql"
  "seed-prod/001_base_permissions.sql"
)

echo "=== KitPlatform PRODUCTION migrations (no demo seed) ==="
echo "Database: $CONN"

for file in "${files[@]}"; do
  path="$MIGRATIONS/$file"
  if [[ ! -f "$path" ]]; then
    echo "Missing: $path" >&2
    exit 1
  fi
  echo ">> $file"
  psql "$CONN" -v ON_ERROR_STOP=1 -f "$path"
done

table_count=$(psql "$CONN" -t -A -c "SELECT COUNT(*)::text FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'")
perm_count=$(psql "$CONN" -t -A -c "SELECT COUNT(*)::text FROM permissions")
tenant_count=$(psql "$CONN" -t -A -c "SELECT COUNT(*)::text FROM tenants")

echo "=== Done: $table_count tables, $perm_count permissions, $tenant_count tenants ==="
echo "Next: open https://admin.novixa.vn/setup to create the first pharmacy."

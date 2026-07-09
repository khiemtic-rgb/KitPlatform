<#
.SYNOPSIS
  Chạy schema migrations Production — không seed demo, không dữ liệu mẫu loyalty/CDP.

.PARAMETER ConnectionString
  PostgreSQL URI, ví dụ postgresql://KitPlatform:***@db-host:5432/KitPlatform_nt_a

.EXAMPLE
  .\scripts\run-migrations-prod.ps1 -ConnectionString "postgresql://KitPlatform:secret@localhost:5432/KitPlatform_nt_a"

.NOTES
  Sau script này, chạy bootstrap cho từng nhà thuốc:
  .\scripts\bootstrap-first-tenant.ps1 -ConnectionString "..." -TenantCode NT_A ...
  Xem client/admin/pilot-go-live-checklist.md
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Migrations = Join-Path $Root "migrations"

$psqlCandidates = @(
    "C:\Program Files\PostgreSQL\18\bin\psql.exe",
    "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    "C:\Program Files\PostgreSQL\16\bin\psql.exe"
)
$psql = $psqlCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $psql) {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { $psql = $cmd.Source }
}

# Schema only — loại demo seed (020, 021, 027, 032, seed/*)
$files = @(
    "001_extensions.sql",
    "002_identity.sql",
    "003_catalog.sql",
    "004_inventory.sql",
    "005_procurement.sql",
    "006_sales.sql",
    "007_customer_app.sql",
    "008_product_images.sql",
    "009_product_name_similarity.sql",
    "010_supplier_payment_status.sql",
    "011_v2_schema_readiness.sql",
    "012_sales_draft_nullable_batch.sql",
    "013_sales_pos_discount.sql",
    "014_sales_return_payments.sql",
    "015_sales_shifts.sql",
    "016_customer_consents_and_outbox.sql",
    "017_sales_batch_source.sql",
    "018_sales_shift_link.sql",
    "019_customer_otp_auth.sql",
    "022_fix_loyalty_program_status.sql",
    "023_sales_loyalty_redeem.sql",
    "024_loyalty_unify_redeem_value.sql",
    "025_loyalty_max_redeem_percent.sql",
    "026_loyalty_fractional_points.sql",
    "028_customer_chat.sql",
    "029_customer_draft_orders.sql",
    "030_customer_draft_order_customer_hide.sql",
    "031_sales_voucher_discount.sql",
    "033_customer_reservations.sql",
    "034_customer_reservation_sales_order.sql",
    "035_inventory_adjustment_count_entries.sql",
    "036_system_admin_permissions.sql",
    "037_vietnamese_permission_labels.sql",
    "038_po_vat_rate_percent.sql",
    "039_procurement_vat_treatments.sql",
    "039_reports_permissions.sql",
    "040_product_national_drug_link.sql",
    "041_pilot_stability_features.sql",
    "042_low_stock_group_settings.sql",
    "043_warehouse_min_stock.sql",
    "044_active_ingredients_tenant.sql",
    "045_scale_branch_readiness.sql",
    "046_chat_branch_scope.sql",
    "047_procurement_placeholder_grn_discount.sql",
    "048_sales_customer_credit.sql",
    "049_customer_payments.sql",
    "050_fix_return_ar_adjustments.sql",
    "051_platform_multi_branch_i18n.sql",
    "052_family_members.sql",
    "053_health_records.sql",
    "054_care_reminders.sql",
    "055_repurchase_and_order_reminders.sql",
    "056_customer_app_branding.sql",
    "057_p6_medication_hub.sql",
    "058_p7_notifications_family_vitals.sql",
    "059_repurchase_order_level_schema.sql",
    "060_family_members_status_align.sql",
    "061_products_product_kind.sql",
    "062_health_care_schema_align.sql",
    "063_p9_engagement_notifications.sql",
    "064_p10b_customer_app_i18n.sql",
    "065_customer_engagement_analytics.sql",
    "066_customer_otp_pilot_admin.sql",
    "067_platform_events.sql",
    "068_assessment_engine.sql",
    "070_stock_movements_tenant_index.sql",
    "071_kit_schemas_foundation.sql",
    "072_kit_core_iam_tenant.sql",
    "073_kit_org_workspace.sql",
    "074_kit_common_storage_party.sql",
    "075_kit_metadata.sql",
    "076_kit_event_audit_notify.sql",
    "077_kit_workflow_integration_ai.sql",
    "078_pack_clinic_crm.sql",
    "079_kit_pack_registry_workspace_party_backfill.sql",
    "080_kit_workspace_pack_rls.sql",
    "081_kit_kernel_rls_workspace.sql",
    "082_pack_pharmacy_schema.sql",
    "083_pack_survey_schema.sql",
    "084_kit_provision_pharmacy_survey_workspace.sql",
    "085_pack_pharmacy_inventory_views.sql",
    "086_pack_pharmacy_report_read_views.sql",
    "087_phase_d_write_cutover.sql",
    "088_purchase_order_workflow.sql",
    "089_kap_decision_intelligence_schema.sql",
    "090_kap_pharmacy_intelligence_seed.sql",
    "092_qd540_integration_schema.sql",
    "seed-prod\001_base_permissions.sql"
)

Write-Host "=== KitPlatform PRODUCTION Migrations (no demo seed) ===" -ForegroundColor Cyan
Write-Host "Database: $ConnectionString"
Write-Host "psql: $psql"

if (-not $psql) {
    Write-Host "[LOI] Khong tim thay psql." -ForegroundColor Red
    exit 1
}

foreach ($file in $files) {
    $path = Join-Path $Migrations $file
    if (-not (Test-Path $path)) {
        Write-Host "[LOI] Thieu file: $path" -ForegroundColor Red
        exit 1
    }
    Write-Host ">> $file" -ForegroundColor Yellow
    & $psql $ConnectionString -v ON_ERROR_STOP=1 -f $path
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[LOI] Migration that bai: $file" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

$tableCount = & $psql $ConnectionString -t -A -c "SELECT COUNT(*)::text FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'"
$permCount = & $psql $ConnectionString -t -A -c "SELECT COUNT(*)::text FROM permissions"
Write-Host "=== XONG! $tableCount bang, $permCount quyen (chua co tenant) ===" -ForegroundColor Green
Write-Host "Tiep theo: .\scripts\bootstrap-first-tenant.ps1 cho moi nha thuoc." -ForegroundColor Yellow


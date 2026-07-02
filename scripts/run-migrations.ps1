param(
    [string]$ConnectionString = "postgresql://pharmacore:pharmacore_dev_2026@localhost:5432/pharmacore"
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

# Thứ tự đồng bộ với setup-and-migrate.ps1 (001 chạy riêng khi setup lần đầu)
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
    "020_loyalty_demo_transactions.sql",
    "021_loyalty_demo_vouchers.sql",
    "022_fix_loyalty_program_status.sql",
    "023_sales_loyalty_redeem.sql",
    "seed\001_demo_data.sql",
    "seed\002_admin_password.sql",
    "seed\003_more_customers.sql",
    "024_loyalty_unify_redeem_value.sql",
    "025_loyalty_max_redeem_percent.sql",
    "026_loyalty_fractional_points.sql",
    "027_customer_app_cdp_consent_demo.sql",
    "028_customer_chat.sql",
    "029_customer_draft_orders.sql",
    "030_customer_draft_order_customer_hide.sql",
    "031_sales_voucher_discount.sql",
    "032_customer_address_demo.sql",
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
    "065_customer_engagement_analytics.sql"
)

Write-Host "=== PharmaCore Migrations ===" -ForegroundColor Cyan
Write-Host "Database: $ConnectionString"
Write-Host "psql: $psql"

if (-not $psql) {
    Write-Host "[LOI] Khong tim thay psql. Chay: .\scripts\setup-and-migrate.ps1 -PostgresPassword <mat_khau_postgres>" -ForegroundColor Red
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
Write-Host "=== XONG! $tableCount bang + demo data ===" -ForegroundColor Green

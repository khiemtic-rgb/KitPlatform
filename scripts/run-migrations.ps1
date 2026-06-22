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
    "seed\001_demo_data.sql",
    "seed\002_admin_password.sql",
    "seed\003_more_customers.sql"
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

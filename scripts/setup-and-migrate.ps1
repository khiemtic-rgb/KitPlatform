param(
    [Parameter(Mandatory = $true)]
    [string]$PostgresPassword,

    [string]$PostgresUser = "postgres",
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$AppUser = "pharmacore",
    [string]$AppPassword = "pharmacore_dev_2026",
    [string]$Database = "pharmacore"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

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
if (-not $psql) {
    Write-Host "[LOI] Khong tim thay psql.exe. Kiem tra PostgreSQL da cai." -ForegroundColor Red
    exit 1
}

Write-Host "=== PharmaCore: Setup + Migrate ===" -ForegroundColor Cyan
Write-Host "psql: $psql"

$env:PGPASSWORD = $PostgresPassword

# Tao user + database
Write-Host ">> Tao user va database..." -ForegroundColor Yellow
$sqlSetup = @"
DO `$`$ BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$AppUser') THEN
        CREATE ROLE $AppUser WITH LOGIN PASSWORD '$AppPassword';
    END IF;
END `$`$;
ALTER ROLE $AppUser WITH LOGIN PASSWORD '$AppPassword';
"@
& $psql -U $PostgresUser -h $DbHost -p $DbPort -d postgres -v ON_ERROR_STOP=1 -c $sqlSetup

$dbExists = & $psql -U $PostgresUser -h $DbHost -p $DbPort -d postgres -t -A -c "SELECT 1 FROM pg_database WHERE datname = '$Database'"
if ($dbExists -ne "1") {
    & $psql -U $PostgresUser -h $DbHost -p $DbPort -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $Database OWNER $AppUser"
    Write-Host "   Database $Database da tao." -ForegroundColor Green
} else {
    Write-Host "   Database $Database da ton tai." -ForegroundColor DarkYellow
}

# Chạy extensions bằng postgres (cần quyền superuser)
$env:PGPASSWORD = $PostgresPassword
& $psql -U $PostgresUser -h $DbHost -p $DbPort -d $Database -v ON_ERROR_STOP=1 -f (Join-Path $Root "migrations\001_extensions.sql")

# Grant schema cho app user
& $psql -U $PostgresUser -h $DbHost -p $DbPort -d $Database -v ON_ERROR_STOP=1 -c "GRANT ALL ON SCHEMA public TO $AppUser; ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO $AppUser;"

# Chay migrations bang user app
$env:PGPASSWORD = $AppPassword
$conn = "postgresql://${AppUser}:${AppPassword}@${DbHost}:${DbPort}/${Database}"

$migrationFiles = @(
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
    "034_customer_reservation_sales_order.sql"
)

foreach ($file in $migrationFiles) {
    $path = Join-Path $Root "migrations\$file"
    Write-Host ">> $file" -ForegroundColor Yellow
    & $psql $conn -v ON_ERROR_STOP=1 -f $path
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[LOI] Migration that bai: $file" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

$tableCount = & $psql $conn -t -A -c "SELECT COUNT(*)::text FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'"
Write-Host "=== XONG! $tableCount bang + demo data ===" -ForegroundColor Green
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

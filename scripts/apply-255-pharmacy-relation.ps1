# Apply migrations/255_customer_pharmacy_relation.sql as postgres (table owner).
# Temporarily sets local trust in pg_hba (same pattern as setup-kitplatform-elevated.ps1).
# Requires: Administrator + PostgreSQL 18 service.

$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$hba = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf'
$bak = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf.kitplatform.bak'
$root = Split-Path -Parent $PSScriptRoot
$mig = Join-Path $root 'migrations\255_customer_pharmacy_relation.sql'

if (-not (Test-Path $psql)) { throw "psql not found: $psql" }
if (-not (Test-Path $mig)) { throw "Migration not found: $mig" }
if (-not (Test-Path $bak)) { Copy-Item $hba $bak -Force }

$content = Get-Content $hba -Raw
$new = $content -replace '(?m)^host\s+all\s+all\s+127\.0\.0\.1/32\s+scram-sha-256\s*$', 'host    all             all             127.0.0.1/32            trust'
$new = $new -replace '(?m)^host\s+all\s+all\s+::1/128\s+scram-sha-256\s*$', 'host    all             all             ::1/128                 trust'
Set-Content -Path $hba -Value $new -NoNewline

try {
    Restart-Service postgresql-x64-18 -Force
    Start-Sleep -Seconds 3

    Write-Host '>> Ensure kitplatform can own customers (for future ALTER)' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -v ON_ERROR_STOP=1 -c "ALTER TABLE IF EXISTS customers OWNER TO kitplatform;"

    Write-Host '>> Apply 255_customer_pharmacy_relation.sql' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -v ON_ERROR_STOP=1 -f $mig
    if ($LASTEXITCODE -ne 0) { throw "Migration failed exit=$LASTEXITCODE" }

    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "INSERT INTO kit_schema_migrations (filename, applied_at) VALUES ('255_customer_pharmacy_relation.sql', NOW()) ON CONFLICT DO NOTHING;" 2>$null

    Write-Host '>> Verify columns' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "SELECT column_name FROM information_schema.columns WHERE table_name='customers' AND column_name LIKE 'pharmacy%' OR (table_name='customers' AND column_name='acquisition_source') ORDER BY 1;"
    Write-Host 'MIGRATION_255_OK' -ForegroundColor Green
}
finally {
    if (Test-Path $bak) {
        Copy-Item $bak $hba -Force
        Restart-Service postgresql-x64-18 -Force
        Start-Sleep -Seconds 2
    }
}

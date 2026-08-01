# Apply migrations/262_repurchase_converted_sales_order.sql (local Postgres 18).
$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$hba = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf'
$bak = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf.kitplatform.bak'
$root = Split-Path -Parent $PSScriptRoot
$mig = Join-Path $root 'migrations\262_repurchase_converted_sales_order.sql'

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

    Write-Host '>> Apply 262_repurchase_converted_sales_order.sql' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -v ON_ERROR_STOP=1 -f $mig
    if ($LASTEXITCODE -ne 0) { throw "Migration failed exit=$LASTEXITCODE" }

    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "INSERT INTO kit_schema_migrations (filename, applied_at) VALUES ('262_repurchase_converted_sales_order.sql', NOW()) ON CONFLICT DO NOTHING;" 2>$null
    Write-Host 'MIGRATION_262_OK' -ForegroundColor Green
}
finally {
    if (Test-Path $bak) {
        Copy-Item $bak $hba -Force
        Restart-Service postgresql-x64-18 -Force
    }
}

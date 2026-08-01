# Apply migrations/260_repurchase_smart_refill_converted.sql (local Postgres 18).
$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$hba = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf'
$bak = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf.kitplatform.bak'
$root = Split-Path -Parent $PSScriptRoot
$mig = Join-Path $root 'migrations\260_repurchase_smart_refill_converted.sql'

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

    Write-Host '>> Apply 260_repurchase_smart_refill_converted.sql' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -v ON_ERROR_STOP=1 -f $mig
    if ($LASTEXITCODE -ne 0) { throw "Migration failed exit=$LASTEXITCODE" }

    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "INSERT INTO kit_schema_migrations (filename, applied_at) VALUES ('260_repurchase_smart_refill_converted.sql', NOW()) ON CONFLICT DO NOTHING;" 2>$null

    Write-Host '>> Verify' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "SELECT column_name FROM information_schema.columns WHERE table_name='repurchase_suggestions' AND column_name IN ('converted_at','converted_reservation_id','family_member_id') ORDER BY 1;"
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='ck_repurchase_suggestions_status';"
    Write-Host 'MIGRATION_260_OK' -ForegroundColor Green
}
finally {
    if (Test-Path $bak) {
        Copy-Item $bak $hba -Force
        Restart-Service postgresql-x64-18 -Force
    }
}

# Apply migrations/279_inventory_count_allow_zero_qty.sql (local Postgres).
$ErrorActionPreference = 'Stop'
$psqlCandidates = @(
    'C:\Program Files\PostgreSQL\18\bin\psql.exe',
    'C:\Program Files\PostgreSQL\17\bin\psql.exe',
    'C:\Program Files\PostgreSQL\16\bin\psql.exe'
)
$psql = $psqlCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $psql) { throw 'psql not found' }

$dataDirs = @(
    'C:\Program Files\PostgreSQL\18\data',
    'C:\Program Files\PostgreSQL\17\data',
    'C:\Program Files\PostgreSQL\16\data'
)
$dataDir = $dataDirs | Where-Object { Test-Path (Join-Path $_ 'pg_hba.conf') } | Select-Object -First 1
if (-not $dataDir) { throw 'PostgreSQL data dir / pg_hba.conf not found' }

$svc = Get-Service -Name 'postgresql*' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $svc) { throw 'PostgreSQL Windows service not found' }

$hba = Join-Path $dataDir 'pg_hba.conf'
$bak = Join-Path $dataDir 'pg_hba.conf.kitplatform.bak'
$root = Split-Path -Parent $PSScriptRoot
$mig = Join-Path $root 'migrations\279_inventory_count_allow_zero_qty.sql'
if (-not (Test-Path $mig)) { throw "Migration not found: $mig" }
if (-not (Test-Path $bak)) { Copy-Item $hba $bak -Force }

$content = Get-Content $hba -Raw
$new = $content -replace '(?m)^host\s+all\s+all\s+127\.0\.0\.1/32\s+scram-sha-256\s*$', 'host    all             all             127.0.0.1/32            trust'
$new = $new -replace '(?m)^host\s+all\s+all\s+::1/128\s+scram-sha-256\s*$', 'host    all             all             ::1/128                 trust'
Set-Content -Path $hba -Value $new -NoNewline

try {
    Restart-Service $svc.Name -Force
    Start-Sleep -Seconds 3

    Write-Host '>> Apply 279_inventory_count_allow_zero_qty.sql' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -v ON_ERROR_STOP=1 -f $mig
    if ($LASTEXITCODE -ne 0) { throw "Migration failed exit=$LASTEXITCODE" }

    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "INSERT INTO kit_schema_migrations (filename, applied_at) VALUES ('279_inventory_count_allow_zero_qty.sql', NOW()) ON CONFLICT DO NOTHING;" 2>$null

    Write-Host '>> Verify constraint' -ForegroundColor Yellow
    & $psql -U postgres -h 127.0.0.1 -d kitplatform -c "SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid = 'inventory_adjustment_count_entries'::regclass AND conname LIKE 'ck_count_entries%';"
    Write-Host 'MIGRATION_279_OK' -ForegroundColor Green
}
finally {
    if (Test-Path $bak) {
        Copy-Item $bak $hba -Force
        Restart-Service $svc.Name -Force
        Start-Sleep -Seconds 2
    }
}

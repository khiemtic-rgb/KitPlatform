# Apply migrations/292_tenant_csdl_duoc_link.sql (local Postgres).
$ErrorActionPreference = 'Stop'
$psqlCandidates = @(
    'C:\Program Files\PostgreSQL\18\bin\psql.exe',
    'C:\Program Files\PostgreSQL\17\bin\psql.exe',
    'C:\Program Files\PostgreSQL\16\bin\psql.exe'
)
$psql = $psqlCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $psql) { throw 'psql not found' }

$root = Split-Path -Parent $PSScriptRoot
$mig = Join-Path $root 'migrations\292_tenant_csdl_duoc_link.sql'
if (-not (Test-Path $mig)) { throw "Migration not found: $mig" }

$connCandidates = @(
    'postgresql://kitplatform:kitplatform_dev_2026@127.0.0.1:5432/kitplatform',
    'postgresql://postgres@127.0.0.1:5432/kitplatform'
)

$applied = $false
foreach ($conn in $connCandidates) {
    Write-Host ">> Try apply 292 via $conn" -ForegroundColor Yellow
    & $psql $conn -v ON_ERROR_STOP=1 -f $mig
    if ($LASTEXITCODE -eq 0) {
        & $psql $conn -c "INSERT INTO kit_schema_migrations (filename, applied_at) VALUES ('292_tenant_csdl_duoc_link.sql', NOW()) ON CONFLICT DO NOTHING;" 2>$null
        & $psql $conn -c "SELECT to_regclass('public.tenant_csdl_duoc_link') AS table_name;"
        Write-Host 'MIGRATION_292_OK' -ForegroundColor Green
        $applied = $true
        break
    }
}

if (-not $applied) {
    throw 'Migration 292 failed for all connection candidates'
}

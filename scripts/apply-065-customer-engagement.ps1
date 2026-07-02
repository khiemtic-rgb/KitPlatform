param(
    [string]$ConnectionString = "postgresql://pharmacore:pharmacore_dev_2026@localhost:5432/pharmacore"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$MigrationFile = Join-Path $Root "migrations\065_customer_engagement_analytics.sql"

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
    Write-Host '[CANH BAO] Khong tim thay psql — bo qua migration 065 (engagement).' -ForegroundColor Yellow
    exit 0
}

if (-not (Test-Path $MigrationFile)) {
    Write-Host "[CANH BAO] Thieu migration: $MigrationFile" -ForegroundColor Yellow
    exit 0
}

Write-Host ">> Customer Engagement: 065_customer_engagement_analytics.sql" -ForegroundColor Yellow
& $psql $ConnectionString -v ON_ERROR_STOP=1 -f $MigrationFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "[LOI] Migration 065 that bai." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[OK] Migration 065 applied." -ForegroundColor Green

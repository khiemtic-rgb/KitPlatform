<#
.SYNOPSIS
  Dry-run deploy tren may dev: build artifacts nhu production (khong can VPS).

.EXAMPLE
  .\scripts\local-prod-dry-run.ps1
  .\scripts\local-prod-dry-run.ps1 -ApiBaseUrl "http://localhost:5290"
#>
param(
    [string]$ApiBaseUrl = "http://localhost:5290",
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "=== Local production dry-run (no VPS) ===" -ForegroundColor Cyan
Write-Host "Build artifacts voi VITE_API_BASE_URL=$ApiBaseUrl"
Write-Host ""

& "$PSScriptRoot\deploy-production.ps1" -ApiBaseUrl $ApiBaseUrl -UseExistingNodeModules
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipSmoke) {
    Write-Host "`n=== Smoke test (API dev dang chay) ===" -ForegroundColor Cyan
    & "$PSScriptRoot\smoke-test-dev.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Can chay .\run-dev.bat truoc khi smoke test." -ForegroundColor Yellow
        exit $LASTEXITCODE
    }
}

Write-Host "`n--- Dry-run xong ---" -ForegroundColor Green
Write-Host "Artifacts: $root\publish\"
Write-Host "  api/           - co the test: `$env:ASPNETCORE_ENVIRONMENT='Production' + env vars"
Write-Host "  admin/         - mo index.html qua static server (can CORS/API rieng)"
Write-Host "  customer-app/"
Write-Host ""
Write-Host "Khi co VPS: copy publish\ + set env + migrate DB (xem publish\DEPLOY.txt)"

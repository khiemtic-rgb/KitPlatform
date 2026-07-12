# Smoke Novixa Connect C0 against local API
# Usage: .\scripts\smoke-connect-local.ps1
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$TenantCode = "NT_XUANHOA",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Connect local smoke ($TenantCode @ $BaseUrl) ===" -ForegroundColor Cyan

$auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ username = $User; password = $Pass; tenantCode = $TenantCode } | ConvertTo-Json)
if (-not $auth.accessToken) { throw "login failed" }
$h = @{ Authorization = "Bearer $($auth.accessToken)" }
Write-Host "[OK] Login" -ForegroundColor Green

$ov = Invoke-RestMethod "$BaseUrl/api/connect/overview" -Headers $h
if ($ov.packCode -ne "novixa_connect") { throw "unexpected packCode=$($ov.packCode)" }
if ($ov.phase -notin @("C4_booking", "C5_status_sync")) { throw "unexpected phase=$($ov.phase)" }
Write-Host "[OK] GET /api/connect/overview phase=$($ov.phase)" -ForegroundColor Green
Write-Host "     capabilities: $($ov.enabledCapabilities -join ', ')"
Write-Host "     non-goals: $($ov.explicitNonGoals -join ', ')"

Write-Host "`n=== Connect smoke PASS (overview) ===" -ForegroundColor Green
Write-Host "For org-link flow: .\scripts\smoke-connect-c1-local.ps1"
Write-Host "For doctor membership: .\scripts\smoke-connect-c2-local.ps1"
Write-Host "Admin UI: http://localhost:5173/connect/team"

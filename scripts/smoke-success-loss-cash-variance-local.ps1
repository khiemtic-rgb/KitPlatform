# Smoke Success EP03 AC2 — cash variance (compose sales_shifts)
# Usage: .\scripts\smoke-success-loss-cash-variance-local.ps1
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$Tenant = "NT_XUANHOA",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"
Write-Host "`n=== Loss cash-variance smoke ($Tenant @ $BaseUrl) ===" -ForegroundColor Cyan

$auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ username = $User; password = $Pass; tenantCode = $Tenant } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($auth.accessToken)" }

$loss = Invoke-RestMethod "$BaseUrl/api/success/loss/cash-variance" -Headers $h
if ($null -eq $loss.businessDate) { throw "businessDate missing" }
if ($null -eq $loss.threshold) { throw "threshold missing" }
if ($null -eq $loss.shifts) { throw "shifts missing" }

$cockpit = Invoke-RestMethod "$BaseUrl/api/success/owner-cockpit" -Headers $h
if ($null -eq $cockpit.riskStrip) { throw "riskStrip missing on owner-cockpit (EP01 inheritance break)" }
if ($cockpit.riskStrip.cashVarianceThreshold -ne $loss.threshold) {
    throw "threshold mismatch cockpit=$($cockpit.riskStrip.cashVarianceThreshold) loss=$($loss.threshold)"
}

Write-Host "[OK] date=$($loss.businessDate) threshold=$($loss.threshold) closed=$($loss.closedShiftCount) open=$($loss.openShiftCount) alerts=$($loss.alertCount)" -ForegroundColor Green
Write-Host "[OK] cockpit riskStrip alerts=$($cockpit.riskStrip.cashVarianceAlertCount) maxAbs=$($cockpit.riskStrip.maxAbsCashVarianceToday)" -ForegroundColor Green
Write-Host "`n=== Loss cash-variance smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/success/loss ($Tenant)"

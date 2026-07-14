# Smoke Success P2 Owner Cockpit
# Usage: .\scripts\smoke-success-owner-cockpit-local.ps1
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$Tenant = "NT_XUANHOA",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"
Write-Host "`n=== Owner Cockpit smoke ($Tenant @ $BaseUrl) ===" -ForegroundColor Cyan

$auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ username = $User; password = $Pass; tenantCode = $Tenant } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($auth.accessToken)" }

$cockpit = Invoke-RestMethod "$BaseUrl/api/success/owner-cockpit" -Headers $h
if ($null -eq $cockpit.overview) { throw "overview missing" }
if ($null -eq $cockpit.salesExtras) { throw "salesExtras missing" }
if ($null -eq $cockpit.inventoryExtras) { throw "inventoryExtras missing" }
if ($null -eq $cockpit.customers) { throw "customers missing" }
if ($null -eq $cockpit.riskStrip) { throw "riskStrip missing (EP03 AC2 additive — must not break EP01)" }

Write-Host "[OK] todayNet=$($cockpit.overview.sales.todayNetTotal) monthNet=$($cockpit.salesExtras.monthNetTotal)" -ForegroundColor Green
Write-Host "[OK] nearExpirySku=$($cockpit.inventoryExtras.nearExpirySkuCount) value=$($cockpit.inventoryExtras.nearExpiryStockValue)" -ForegroundColor Green
Write-Host "[OK] new7d=$($cockpit.customers.newCustomers7d) returning7d=$($cockpit.customers.returningCustomers7d)" -ForegroundColor Green
Write-Host "`n=== Owner Cockpit smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/success/cockpit ($Tenant)"

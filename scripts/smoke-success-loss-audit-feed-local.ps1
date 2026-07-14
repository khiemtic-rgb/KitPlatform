# Smoke Success EP03 AC1 — loss audit feed (compose activity_log)
# Usage: .\scripts\smoke-success-loss-audit-feed-local.ps1
#        .\scripts\smoke-success-loss-audit-feed-local.ps1 -BaseUrl "https://api.novixa.vn"
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$Tenant = "NT_XUANHOA",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"
Write-Host "`n=== Loss audit-feed smoke ($Tenant @ $BaseUrl) ===" -ForegroundColor Cyan

$auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ username = $User; password = $Pass; tenantCode = $Tenant } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($auth.accessToken)" }

$feed = Invoke-RestMethod "$BaseUrl/api/success/loss/audit-feed" -Headers $h
if ($null -eq $feed.fromUtc) { throw "fromUtc missing" }
if ($null -eq $feed.items) { throw "items missing" }
if ($null -eq $feed.attributionNotes) { throw "attributionNotes missing" }
if ($feed.pageSize -lt 1) { throw "pageSize invalid" }

$typed = Invoke-RestMethod "$BaseUrl/api/success/loss/audit-feed?eventType=order_create" -Headers $h
if ($null -eq $typed.items) { throw "filtered items missing" }
foreach ($row in @($typed.items)) {
    if ($row.eventType -ne "order_create") {
        throw "eventType filter leaked $($row.eventType)"
    }
}

Write-Host "[OK] range $($feed.fromUtc) -> $($feed.toUtc) total=$($feed.total) pageItems=$($feed.items.Count)" -ForegroundColor Green
Write-Host "[OK] eventType=order_create rows=$($typed.items.Count)" -ForegroundColor Green
Write-Host "`n=== Loss audit-feed smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: https://admin.novixa.vn/success/loss ($Tenant) tab Nhật ký"

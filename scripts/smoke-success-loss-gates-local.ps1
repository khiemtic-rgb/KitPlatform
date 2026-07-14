# Smoke Success EP03 AC5 — permission gates (cancel + approve policies present for ADMIN)
# Usage: .\scripts\smoke-success-loss-gates-local.ps1
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$Tenant = "NT_XUANHOA",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"
Write-Host "`n=== Loss AC5 gates smoke ($Tenant @ $BaseUrl) ===" -ForegroundColor Cyan

$auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ username = $User; password = $Pass; tenantCode = $Tenant } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($auth.accessToken)" }

try {
    $pending = Invoke-RestMethod "$BaseUrl/api/system/workflow/pos-discount/pending" -Headers $h
    Write-Host "[OK] pos-discount pending reachable (count=$($pending.Count))" -ForegroundColor Green
} catch {
    throw "pos-discount pending failed: $($_.Exception.Message)"
}

# Cancel endpoint must authorize for ADMIN (404/400 on missing id means Cancel policy OK)
$fakeId = [guid]::NewGuid()
try {
    Invoke-WebRequest "$BaseUrl/api/sales/orders/$fakeId/cancel" -Method POST -Headers $h -UseBasicParsing -ErrorAction Stop | Out-Null
    throw "unexpected success canceling missing order"
} catch {
    $resp = $_.Exception.Response
    if ($null -eq $resp) { throw $_ }
    $code = [int]$resp.StatusCode
    if ($code -eq 403) { throw "ADMIN got 403 on cancel - SalesCancel policy broken" }
    if ($code -ne 404 -and $code -ne 400) { throw "cancel unexpected status $code" }
    Write-Host "[OK] cancel gate allows ADMIN (status=$code for missing order)" -ForegroundColor Green
}

# Approve endpoint must authorize for ADMIN
try {
    Invoke-WebRequest "$BaseUrl/api/inventory/adjustments/$fakeId/approve" -Method POST -Headers $h -UseBasicParsing -ErrorAction Stop | Out-Null
    throw "unexpected success approving missing adjustment"
} catch {
    $resp = $_.Exception.Response
    if ($null -eq $resp) { throw $_ }
    $code = [int]$resp.StatusCode
    if ($code -eq 403) { throw "ADMIN got 403 on approve - InventoryApprove policy broken" }
    if ($code -ne 404 -and $code -ne 400) { throw "approve unexpected status $code" }
    Write-Host "[OK] approve gate allows ADMIN (status=$code for missing adjustment)" -ForegroundColor Green
}

Write-Host "`n=== Loss AC5 gates smoke PASS ===" -ForegroundColor Green

# POS ops smoke (authenticated) — customer search-by-phone + POS routes.
# Usage (DEMO or local):
#   .\scripts\smoke-pos-ops.ps1
#   .\scripts\smoke-pos-ops.ps1 -BaseUrl https://api.novixa.vn -TenantCode DEMO_PHARMACY
param(
    [string]$BaseUrl = 'http://localhost:5290',
    [string]$TenantCode = 'DEMO_PHARMACY',
    [string]$AdminUser = 'admin',
    [string]$AdminPassword = 'Admin@123',
    [string]$PhoneProbe = '0909'
)

$ErrorActionPreference = 'Stop'
$api = "$($BaseUrl.TrimEnd('/'))/api"
$passed = 0
$failed = @()

function Test-Step([string]$Name, [scriptblock]$Block) {
    try {
        & $Block
        Write-Host "[OK] $Name" -ForegroundColor Green
        $script:passed++
    }
    catch {
        Write-Host "[FAIL] $Name - $($_.Exception.Message)" -ForegroundColor Red
        $script:failed += $Name
    }
}

Write-Host "`n=== POS ops smoke ($BaseUrl / $TenantCode) ===" -ForegroundColor Cyan

$login = Invoke-RestMethod "$api/auth/login" -Method POST -ContentType 'application/json' `
    -Body (@{ username = $AdminUser; password = $AdminPassword; tenantCode = $TenantCode } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

Test-Step 'POS lookup mounted' {
    $code = try {
        Invoke-WebRequest "$api/sales/pos/lookup?barcode=__smoke__" -Headers $headers -SkipHttpErrorCheck -TimeoutSec 15 |
            Select-Object -ExpandProperty StatusCode
    } catch {
        if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { throw }
    }
    # 200 empty / 404 product / 400 all fine; 401/403 means auth broken
    if ($code -in 401, 403) { throw "HTTP $code" }
}

Test-Step 'Customer search by phone fragment' {
    $enc = [uri]::EscapeDataString($PhoneProbe)
    $rows = Invoke-RestMethod "$api/sales/customers?search=$enc" -Headers $headers
    $list = if ($rows -is [System.Array]) { $rows } elseif ($rows.items) { $rows.items } else { @($rows) }
    if ($null -eq $list) { throw 'null response' }
    # Empty result OK (tenant may lack matching phone); failure = non-200
}

Test-Step 'Sales orders list' {
    Invoke-RestMethod "$api/sales/orders?page=1&pageSize=5" -Headers $headers | Out-Null
}

Test-Step 'Current shift endpoint' {
    # 200 with shift or 204/404 when none — treat 401/403 as fail
    try {
        Invoke-RestMethod "$api/sales/shifts/current" -Headers $headers | Out-Null
    }
    catch {
        $code = [int]($_.Exception.Response.StatusCode)
        if ($code -in 401, 403) { throw "HTTP $code" }
        if ($code -notin 404, 204) {
            # Some tenants return 200 null body via REST — other codes ok if authorized
            if ($code -ge 500) { throw "HTTP $code" }
        }
    }
}

Test-Step 'Warehouses for POS branch scope' {
    $wh = Invoke-RestMethod "$api/inventory/warehouses" -Headers $headers
    $list = if ($wh -is [System.Array]) { $wh } elseif ($wh.items) { $wh.items } else { @($wh) }
    if (-not $list -or $list.Count -lt 1) { throw 'no warehouses' }
}

Write-Host "`nPassed: $passed  Failed: $($failed.Count)" -ForegroundColor Cyan
if ($failed.Count -gt 0) {
    Write-Host ("Failed steps: " + ($failed -join ', ')) -ForegroundColor Red
    exit 1
}
Write-Host '=== POS ops smoke OK ===' -ForegroundColor Green

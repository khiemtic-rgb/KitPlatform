$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5290'
$passed = 0
$failed = @()

function Test-Step([string]$Name, [scriptblock]$Block) {
    try {
        & $Block
        Write-Host "[OK] $Name" -ForegroundColor Green
        $script:passed++
    }
    catch {
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        Write-Host "       $($_.Exception.Message)" -ForegroundColor DarkRed
        $script:failed += $Name
    }
}

Write-Host "`n=== PharmaCore dev smoke test ===" -ForegroundColor Cyan

Test-Step 'API health' {
    $h = Invoke-RestMethod "$base/api/health" -TimeoutSec 5
    if ($h.status -ne 'ok') { throw "status=$($h.status)" }
}

Test-Step 'API health/db' {
    $h = Invoke-RestMethod "$base/api/health/db" -TimeoutSec 10
    if ($h.status -ne 'ok') { throw "status=$($h.status)" }
}

Test-Step 'Admin login' {
    $script:admin = Invoke-RestMethod "$base/api/auth/login" -Method POST -ContentType 'application/json' `
        -Body '{"username":"admin","password":"Admin@123"}'
    if (-not $script:admin.accessToken) { throw 'no token' }
    $script:adminH = @{ Authorization = "Bearer $($script:admin.accessToken)" }
}

Test-Step 'Customer OTP login' {
    Invoke-RestMethod "$base/api/customer-app/auth/request-otp" -Method POST -ContentType 'application/json' `
        -Body '{"phone":"0909123456","tenantCode":"DEMO_PHARMACY"}' | Out-Null
    $script:cust = Invoke-RestMethod "$base/api/customer-app/auth/verify-otp" -Method POST -ContentType 'application/json' `
        -Body '{"phone":"0909123456","code":"000000","tenantCode":"DEMO_PHARMACY"}'
    if (-not $script:cust.accessToken) { throw 'no token' }
    $script:custH = @{ Authorization = "Bearer $($script:cust.accessToken)" }
}

Test-Step 'Customer profile' {
    $p = Invoke-RestMethod "$base/api/customer-app/auth/me" -Headers $script:custH
    if (-not $p.fullName) { throw 'no profile' }
}

Test-Step 'Loyalty summary' {
    $l = Invoke-RestMethod "$base/api/customer-app/loyalty/summary" -Headers $script:custH
    if ($null -eq $l.programs -or $l.programs.Count -lt 1) { throw 'no programs' }
}

Test-Step 'Loyalty transactions' {
    $t = Invoke-RestMethod "$base/api/customer-app/loyalty/transactions" -Headers $script:custH
    if ($null -eq $t.items) { throw 'no items array' }
}

Test-Step 'Customer vouchers' {
    $v = Invoke-RestMethod "$base/api/customer-app/loyalty/vouchers" -Headers $script:custH
    if ($null -eq $v.items) { throw 'no items array' }
}

Test-Step 'Reminders list' {
    $r = Invoke-RestMethod "$base/api/customer-app/reminders" -Headers $script:custH
    if ($null -eq $r.items) { throw 'no items array' }
}

Test-Step 'CDP consents' {
    $c = Invoke-RestMethod "$base/api/customer-app/consents" -Headers $script:custH
    if ($null -eq $c.items) { throw 'no consents' }
}

Test-Step 'Push status' {
    $p = Invoke-RestMethod "$base/api/customer-app/push/status" -Headers $script:custH
    if (-not $p.publicKey) { throw 'no VAPID key' }
}

Test-Step 'Chat thread' {
    $chat = Invoke-RestMethod "$base/api/customer-app/chat/thread" -Headers $script:custH
    if (-not $chat.threadId) { throw 'no threadId' }
}

Test-Step 'Chat messages' {
    $msgs = Invoke-RestMethod "$base/api/customer-app/chat/messages" -Headers $script:custH
    if ($null -eq $msgs.items) { throw 'no items array' }
}

Test-Step 'Draft orders (customer)' {
    $d = Invoke-RestMethod "$base/api/customer-app/draft-orders" -Headers $script:custH
    if ($null -eq $d.items) { throw 'no items array' }
    $script:draftCount = $d.items.Count
}

Test-Step 'Reservations (customer)' {
    $res = Invoke-RestMethod "$base/api/customer-app/reservations" -Headers $script:custH
    if ($null -eq $res.items) { throw 'no items array' }
    $script:resCount = $res.items.Count
}

Test-Step 'Purchase history' {
    $pur = Invoke-RestMethod "$base/api/customer-app/purchases" -Headers $script:custH
    if ($null -eq $pur.items) { throw 'no items array' }
    $script:purchaseCount = $pur.items.Count
}

Test-Step 'Delivery addresses' {
    $addr = Invoke-RestMethod "$base/api/customer-app/addresses" -Headers $script:custH
    if ($null -eq $addr.items) { throw 'no items array' }
    $script:addrCount = $addr.items.Count
}

Test-Step 'Notification center (local UI)' {
    # Trung tâm thông báo lưu localStorage — không có API backend riêng.
    if (-not (Test-Path "$PSScriptRoot\..\client\customer-app\src\shared\notifications\customer-notifications.ts")) {
        throw 'missing notifications module'
    }
}

Test-Step 'Product catalog search' {
    $cat = Invoke-RestMethod "$base/api/customer-app/catalog/products?search=para&limit=5" -Headers $script:custH
    if ($null -eq $cat.items) { throw 'no items array' }
}

Test-Step 'Admin draft orders list' {
    $d = Invoke-RestMethod "$base/api/sales/customer-draft-orders" -Headers $script:adminH
    if ($null -eq $d.items) { throw 'no items array' }
}

Test-Step 'Admin reservations list' {
    $r = Invoke-RestMethod "$base/api/sales/customer-reservations" -Headers $script:adminH
    if ($null -eq $r.items) { throw 'no items array' }
}

Test-Step 'Admin chat threads' {
    $t = Invoke-RestMethod "$base/api/sales/customer-chat/threads" -Headers $script:adminH
    if ($null -eq $t.items) { throw 'no items array' }
}

Test-Step 'Loyalty settings (admin)' {
    Invoke-RestMethod "$base/api/loyalty/settings" -Headers $script:adminH | Out-Null
}

Test-Step 'POS customer loyalty lookup' {
    $profile = Invoke-RestMethod "$base/api/customer-app/auth/me" -Headers $script:custH
    $cid = $profile.customerId
    $loy = Invoke-RestMethod "$base/api/sales/pos/customer-loyalty?customerId=$cid&orderTotal=100000" -Headers $script:adminH
    if ($null -eq $loy.loyaltyEnabled) { throw 'invalid loyalty response' }
}

Test-Step 'POS current shift (warehouse)' {
    $whId = '22222222-2222-2222-2222-222222222201'
    try {
        Invoke-RestMethod "$base/api/sales/shifts/current?warehouseId=$whId" -Headers $script:adminH | Out-Null
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
        # 404 = chưa mở ca — vẫn OK cho smoke test
    }
}

Test-Step 'Voucher admin list' {
    $v = Invoke-RestMethod "$base/api/loyalty/vouchers" -Headers $script:adminH
    if ($null -eq $v.items) { throw 'no items array' }
}

Write-Host "`n--- Summary ---" -ForegroundColor Cyan
Write-Host "Passed: $passed"
Write-Host "Draft orders (customer): $script:draftCount"
Write-Host "Reservations (customer): $script:resCount"
Write-Host "Purchases (customer): $script:purchaseCount"
Write-Host "Addresses (customer): $script:addrCount"
if ($failed.Count -gt 0) {
    Write-Host "Failed ($($failed.Count)):" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "`nAll API smoke checks passed." -ForegroundColor Green

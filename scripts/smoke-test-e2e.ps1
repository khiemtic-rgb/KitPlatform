$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5290'
$passed = 0
$failed = @()
$notes = @()

function Test-Step([string]$Name, [scriptblock]$Block) {
    try {
        & $Block
        Write-Host "[OK] $Name" -ForegroundColor Green
        $script:passed++
    }
    catch {
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        Write-Host "       $($_.Exception.Message)" -ForegroundColor DarkRed
        if ($_.ErrorDetails.Message) { Write-Host "       $($_.ErrorDetails.Message)" -ForegroundColor DarkRed }
        $script:failed += $Name
    }
}

function Get-JsonError($err) {
    if ($err.ErrorDetails.Message) { return $err.ErrorDetails.Message }
    return $err.Exception.Message
}

Write-Host "`n=== PharmaCore E2E dev test ===" -ForegroundColor Cyan

# Demo IDs
$customerId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
$productId = '66666666-6666-6666-6666-666666666601'
$unitId = '77777777-7777-7777-7777-777777777701'
$warehouseId = '22222222-2222-2222-2222-222222222201'

Test-Step 'Setup: admin + customer tokens' {
    $script:admin = Invoke-RestMethod "$base/api/auth/login" -Method POST -ContentType 'application/json' `
        -Body '{"username":"admin","password":"Admin@123"}'
    $script:adminH = @{ Authorization = "Bearer $($script:admin.accessToken)" }
    Invoke-RestMethod "$base/api/customer-app/auth/request-otp" -Method POST -ContentType 'application/json' `
        -Body '{"phone":"0909123456","tenantCode":"DEMO_PHARMACY"}' | Out-Null
    $script:cust = Invoke-RestMethod "$base/api/customer-app/auth/verify-otp" -Method POST -ContentType 'application/json' `
        -Body '{"phone":"0909123456","code":"000000","tenantCode":"DEMO_PHARMACY"}'
    $script:custH = @{ Authorization = "Bearer $($script:cust.accessToken)" }
}

Test-Step 'Chat: customer sends message' {
    $script:chatText = "E2E chat $(Get-Date -Format 'HH:mm:ss')"
    Invoke-RestMethod "$base/api/customer-app/chat/messages" -Method POST -ContentType 'application/json' `
        -Headers $script:custH -Body (@{ body = $script:chatText } | ConvertTo-Json) | Out-Null
}

Test-Step 'Chat: admin sees thread + replies' {
    $threads = Invoke-RestMethod "$base/api/sales/customer-chat/threads" -Headers $script:adminH
    if ($threads.items.Count -lt 1) { throw 'no chat threads' }
    $reply = "E2E reply $(Get-Date -Format 'HH:mm:ss')"
    Invoke-RestMethod "$base/api/sales/customer-chat/threads/$customerId/messages" -Method POST `
        -ContentType 'application/json' -Headers $script:adminH -Body (@{ body = $reply } | ConvertTo-Json) | Out-Null
    $script:adminReply = $reply
}

Test-Step 'Chat: customer reads admin reply' {
    Start-Sleep -Seconds 1
    $msgs = Invoke-RestMethod "$base/api/customer-app/chat/messages" -Headers $script:custH
    $found = $msgs.items | Where-Object { $_.body -eq $script:adminReply }
    if (-not $found) { throw 'admin reply not in customer messages' }
}

Test-Step 'Draft order: admin creates + sends' {
    $body = @{
        customerId = $customerId
        warehouseId = $warehouseId
        priceType = 1
        items = @(@{
            productId = $productId
            productUnitId = $unitId
            quantity = 2
            dosageNote = 'E2E draft test'
        })
        notes = 'E2E automated draft'
    } | ConvertTo-Json -Depth 5
    $script:draft = Invoke-RestMethod "$base/api/sales/customer-draft-orders" -Method POST `
        -ContentType 'application/json' -Headers $script:adminH -Body $body
    $script:draft = Invoke-RestMethod "$base/api/sales/customer-draft-orders/$($script:draft.id)/send" -Method POST `
        -Headers $script:adminH
    if ($script:draft.status -ne 2) { throw "expected Sent(2), got $($script:draft.status)" }
}

Test-Step 'Draft order: customer confirms' {
    $script:draft = Invoke-RestMethod "$base/api/customer-app/draft-orders/$($script:draft.id)/confirm" -Method POST `
        -Headers $script:custH
    if ($script:draft.status -ne 3) { throw "expected Confirmed(3), got $($script:draft.status)" }
}

Test-Step 'Draft order: POS load' {
    $pl = Invoke-RestMethod "$base/api/sales/customer-draft-orders/$($script:draft.id)/pos-load" -Headers $script:adminH
    if ($pl.lines.Count -lt 1) { throw 'pos-load empty' }
    if ($pl.customerId -ne $customerId) { throw 'wrong customer on pos-load' }
}

Test-Step 'Reservation: customer creates pickup' {
    $body = @{
        fulfillmentType = 1
        addressId = $null
        notes = 'E2E reservation test'
        items = @(@{
            productId = $productId
            quantity = 1
            customerNote = 'E2E'
        })
    } | ConvertTo-Json -Depth 5
    $script:res = Invoke-RestMethod "$base/api/customer-app/reservations" -Method POST `
        -ContentType 'application/json' -Headers $script:custH -Body $body
    if ($script:res.status -ne 1) { throw "expected Pending(1), got $($script:res.status)" }
}

Test-Step 'Reservation: admin confirm + ready' {
    $id = $script:res.id
    Invoke-RestMethod "$base/api/sales/customer-reservations/$id/confirm" -Method POST -Headers $script:adminH | Out-Null
    $script:res = Invoke-RestMethod "$base/api/sales/customer-reservations/$id/ready" -Method POST -Headers $script:adminH
    if ($script:res.status -ne 3) { throw "expected Ready(3), got $($script:res.status)" }
}

Test-Step 'Reservation: POS load' {
    $pl = Invoke-RestMethod "$base/api/sales/customer-reservations/$($script:res.id)/pos-load" -Headers $script:adminH
    if ($pl.lines.Count -lt 1) { throw 'pos-load empty' }
}

Test-Step 'POS: open shift (if needed)' {
    try {
        Invoke-RestMethod "$base/api/sales/shifts/current?warehouseId=$warehouseId" -Headers $script:adminH | Out-Null
        $script:notes += 'POS shift already open'
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
        $openBody = @{ warehouseId = $warehouseId; openingCash = 500000 } | ConvertTo-Json
        $script:shift = Invoke-RestMethod "$base/api/sales/shifts/open" -Method POST `
            -ContentType 'application/json' -Headers $script:adminH -Body $openBody
        $script:notes += "Opened shift $($script:shift.shiftNumber)"
    }
}

Test-Step 'POS: barcode lookup' {
    $lookup = Invoke-RestMethod "$base/api/sales/pos/lookup?barcode=8934567890012" -Headers $script:adminH
    if ($lookup.productId -ne $productId) { throw 'barcode lookup mismatch' }
}

Test-Step 'POS: preview allocation + complete sale' {
    $previewBody = @{
        warehouseId = $warehouseId
        items = @(@{
            productId = $productId
            productUnitId = $unitId
            quantity = 1
        })
    } | ConvertTo-Json -Depth 5
    $preview = Invoke-RestMethod "$base/api/sales/pos/preview-allocation" -Method POST `
        -ContentType 'application/json' -Headers $script:adminH -Body $previewBody
    if ($preview.lines.Count -lt 1) { throw 'preview has no lines' }
    if ($preview.lines[0].allocations.Count -lt 1) { throw 'no batch allocation' }

    $lookup = Invoke-RestMethod "$base/api/sales/pos/lookup?barcode=8934567890012" -Headers $script:adminH
    $total = [decimal]$lookup.unitPrice
    if ($total -le 0) { $total = 5000 }

    $saleBody = @{
        warehouseId = $warehouseId
        customerId = $customerId
        priceType = 1
        items = @(@{
            productId = $productId
            productUnitId = $unitId
            quantity = 1
        })
        payments = @(@{ paymentMethod = 1; amount = $total })
        notes = 'E2E POS sale'
    } | ConvertTo-Json -Depth 5
    $script:sale = Invoke-RestMethod "$base/api/sales/orders" -Method POST `
        -ContentType 'application/json' -Headers $script:adminH -Body $saleBody
    if ([decimal]$script:sale.totalAmount -le 0) { throw 'sale total is zero' }
    $script:notes += "Created SO $($script:sale.orderNumber) total=$($script:sale.totalAmount)"
}

Test-Step 'Purchase history includes new sale' {
    Start-Sleep -Seconds 1
    $pur = Invoke-RestMethod "$base/api/customer-app/purchases" -Headers $script:custH
    $found = $pur.items | Where-Object { $_.id -eq $script:sale.id }
    if (-not $found) { throw 'new sale not in customer purchases' }
}

Test-Step 'Loyalty earn after sale' {
    $l = Invoke-RestMethod "$base/api/customer-app/loyalty/summary" -Headers $script:custH
    if ($l.programs[0].pointsBalance -le 0) { throw 'expected points > 0 after sale' }
}

Write-Host "`n--- E2E Summary ---" -ForegroundColor Cyan
Write-Host "Passed: $passed"
if ($notes.Count -gt 0) {
    Write-Host "Notes:"
    $notes | ForEach-Object { Write-Host "  - $_" }
}
if ($failed.Count -gt 0) {
    Write-Host "Failed ($($failed.Count)):" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "`nAll E2E checks passed." -ForegroundColor Green

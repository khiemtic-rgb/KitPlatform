# Rx-2 Phase A — prescriber portal smoke (local dev)
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
        $script:failed += $Name
    }
}

Write-Host "`n=== Rx-2 Prescriber Portal smoke ===" -ForegroundColor Cyan

. (Join-Path $PSScriptRoot 'api-dev.ps1')
$ensure = Ensure-ApiDev -Quiet -SkipWatcher -SkipBuild
if ($ensure -ne 0) { throw 'API not ready' }

Test-Step 'API health/db' {
    try {
        $h = Invoke-RestMethod "$base/api/health/db" -TimeoutSec 15
    }
    catch {
        throw "DB unavailable (503). Chay: .\scripts\setup-and-migrate.ps1 -PostgresPassword <postgres_superuser>"
    }
    if ($h.status -ne 'ok') { throw "status=$($h.status)" }
}

# Resolve pilot tenant + prescriber from DB
$psql = @(
    'C:\Program Files\PostgreSQL\18\bin\psql.exe',
    'C:\Program Files\PostgreSQL\17\bin\psql.exe',
    'C:\Program Files\PostgreSQL\16\bin\psql.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $psql) { throw 'psql not found' }
$env:PGPASSWORD = 'kitplatform_dev_2026'

$ctxQuery = @"
SELECT
  t.tenant_code,
  t.id::text AS tenant_id,
  p.phone,
  p.id::text AS prescriber_id,
  l.link_status
FROM pack_pharmacy.prescriber_tenant_links l
JOIN tenants t ON t.id = l.tenant_id
JOIN pack_pharmacy.prescribers p ON p.id = l.prescriber_id
WHERE l.link_status = 'active'
  AND p.status = 'active'
  AND p.deleted_at IS NULL
ORDER BY CASE WHEN t.tenant_code = 'NT_XUANHOA' THEN 0 WHEN t.tenant_code = 'DEMO_PHARMACY' THEN 1 ELSE 2 END
LIMIT 1;
"@

$ctxRaw = & $psql -h localhost -p 5432 -U kitplatform -d kitplatform -t -A -F '|' -c $ctxQuery 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "psql failed: $ctxRaw"
}
$ctxLine = ($ctxRaw | Out-String).Trim()
if (-not $ctxLine) {
    throw 'No active prescriber link in DB — apply migration 100 + seed or invite a prescriber'
}
$parts = $ctxLine -split '\|'
$tenantCode = $parts[0]
$tenantId = $parts[1]
$prescriberPhone = $parts[2]
$prescriberId = $parts[3]
$notes += "tenant=$tenantCode prescriberPhone=$prescriberPhone"

$customerQuery = "SELECT id::text FROM customers WHERE tenant_id = '$tenantId'::uuid AND deleted_at IS NULL ORDER BY created_at LIMIT 1;"
$customerId = (& $psql -h localhost -p 5432 -U kitplatform -d kitplatform -t -A -c $customerQuery).Trim()
if (-not $customerId) { throw "No customer for tenant $tenantCode" }

$productQuery = @"
SELECT p.id::text
FROM products p
WHERE p.tenant_id = '$tenantId'::uuid
  AND p.deleted_at IS NULL
  AND COALESCE(p.dispensing_class, CASE p.drug_type WHEN 2 THEN 'prescription' WHEN 3 THEN 'controlled' ELSE 'otc' END) <> 'controlled'
ORDER BY p.product_name
LIMIT 1;
"@
$productId = (& $psql -h localhost -p 5432 -U kitplatform -d kitplatform -t -A -c $productQuery).Trim()
if (-not $productId) { throw "No non-controlled product for tenant $tenantCode" }

$controlledQuery = @"
SELECT p.id::text
FROM products p
WHERE p.tenant_id = '$tenantId'::uuid
  AND p.deleted_at IS NULL
  AND COALESCE(p.dispensing_class, CASE p.drug_type WHEN 3 THEN 'controlled' WHEN 2 THEN 'prescription' ELSE 'otc' END) = 'controlled'
LIMIT 1;
"@
$controlledProductId = (& $psql -h localhost -p 5432 -U kitplatform -d kitplatform -t -A -c $controlledQuery).Trim()

$linkIdQuery = "SELECT id::text FROM pack_pharmacy.prescriber_tenant_links WHERE prescriber_id = '$prescriberId'::uuid AND tenant_id = '$tenantId'::uuid LIMIT 1;"
$linkId = (& $psql -h localhost -p 5432 -U kitplatform -d kitplatform -t -A -c $linkIdQuery).Trim()
if (-not $linkId) { throw 'No prescriber link id' }

Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

Test-Step 'Admin login' {
    $script:admin = Invoke-RestMethod "$base/api/auth/login" -Method POST -ContentType 'application/json' `
        -Body (@{ username = 'admin'; password = 'Admin@123'; tenantCode = $tenantCode } | ConvertTo-Json)
    if (-not $script:admin.accessToken) { throw 'no token' }
    $script:adminH = @{ Authorization = "Bearer $($script:admin.accessToken)" }
}

Test-Step 'Prescriber OTP request' {
    $script:otp = Invoke-RestMethod "$base/api/prescriber-portal/auth/otp-request" -Method POST -ContentType 'application/json' `
        -Body (@{ phone = $prescriberPhone } | ConvertTo-Json)
    $script:otpCode = if ($script:otp.pilotCode) { $script:otp.pilotCode } else { '000000' }
    if (-not $script:otpCode) { throw 'no otp code' }
}

Test-Step 'Prescriber OTP verify + JWT' {
    $script:bs = Invoke-RestMethod "$base/api/prescriber-portal/auth/otp-verify" -Method POST -ContentType 'application/json' `
        -Body (@{ phone = $prescriberPhone; code = $script:otpCode } | ConvertTo-Json)
    if (-not $script:bs.accessToken) { throw 'no prescriber token' }
    $script:bsH = @{ Authorization = "Bearer $($script:bs.accessToken)" }
}

Test-Step 'Prescriber /auth/me' {
    $me = Invoke-RestMethod "$base/api/prescriber-portal/auth/me" -Headers $script:bsH
    if ($me.id -ne $prescriberId) { throw "profile id mismatch: $($me.id)" }
}

Test-Step 'List linked pharmacies' {
    $links = Invoke-RestMethod "$base/api/prescriber-portal/pharmacies?activeOnly=true" -Headers $script:bsH
    if ($links.Count -lt 1) { throw 'no active pharmacy links' }
    if ($links[0].id -ne $linkId) { throw "link id mismatch: $($links[0].id)" }
}

Test-Step 'Search customers (D14)' {
    $customers = Invoke-RestMethod "$base/api/prescriber-portal/customers?tenantId=$tenantId&q=" -Headers $script:bsH
    if ($customers.Count -lt 1) { throw 'no customers' }
}

Test-Step 'Search products' {
    $products = Invoke-RestMethod "$base/api/prescriber-portal/products?tenantId=$tenantId&q=" -Headers $script:bsH
    if ($products.Count -lt 1) { throw 'no products' }
}

Test-Step 'Create signed prescription (portal)' {
    $body = @{
        tenantId = $tenantId
        customerId = $customerId
        notes = 'Rx-2 smoke test'
        lines = @(
            @{
                productId = $productId
                qtyPrescribed = 1
                dosageInstruction = '1 viên x 2 lan/ngay'
            }
        )
    } | ConvertTo-Json -Depth 5
    $script:rx = Invoke-RestMethod "$base/api/prescriber-portal/prescriptions" -Method POST -ContentType 'application/json' `
        -Headers $script:bsH -Body $body
    if ($script:rx.status -ne 'signed') { throw "status=$($script:rx.status)" }
    if ($script:rx.source -ne 'prescriber_portal') { throw "source=$($script:rx.source)" }
    $notes += "rx=$($script:rx.prescriptionCode) id=$($script:rx.id)"
}

Test-Step 'Admin sees signed Rx (no verify needed)' {
    $list = Invoke-RestMethod "$base/api/pharmacy/prescriptions?status=signed&pageSize=20" -Headers $script:adminH
    $items = @($list.items)
    if ($items.Count -lt 1) { throw 'empty prescription list' }
    $hit = $items | Where-Object { $_.id -eq $script:rx.id -or $_.prescriptionCode -eq $script:rx.prescriptionCode }
    if (-not $hit) { throw 'signed rx not visible in admin list' }
}

Test-Step 'Blocked: controlled product (D16)' {
    if (-not $controlledProductId) { throw 'no controlled product in tenant — skip seed migration 099' }
    $body = @{
        tenantId = $tenantId
        customerId = $customerId
        lines = @(@{ productId = $controlledProductId; qtyPrescribed = 1 })
    } | ConvertTo-Json -Depth 5
    try {
        Invoke-RestMethod "$base/api/prescriber-portal/prescriptions" -Method POST -ContentType 'application/json' `
            -Headers $script:bsH -Body $body | Out-Null
        throw 'expected 400'
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 400) { throw $_ }
    }
}

Test-Step 'Blocked: prescribe without active link (fake tenant)' {
    $fakeTenant = '00000000-0000-4000-8000-000000000099'
    $body = @{
        tenantId = $fakeTenant
        customerId = $customerId
        lines = @(@{ productId = $productId; qtyPrescribed = 1 })
    } | ConvertTo-Json -Depth 5
    try {
        Invoke-RestMethod "$base/api/prescriber-portal/prescriptions" -Method POST -ContentType 'application/json' `
            -Headers $script:bsH -Body $body | Out-Null
        throw 'expected 400'
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 400) { throw $_ }
    }
}

Test-Step 'Admin prescriber links API' {
    $links = Invoke-RestMethod "$base/api/pharmacy/prescribers/links" -Headers $script:adminH
    if ($null -eq $links) { throw 'null links' }
}

Test-Step 'E2E: BS request link -> NT approve' {
    $reqPhone = '0909999011'
    $reqLicense = 'CCHN-SMOKE-REQ-011'
    $env:PGPASSWORD = 'kitplatform_dev_2026'
    $seedSql = @"
UPDATE pack_pharmacy.prescribers
SET full_name = 'Smoke Request BS',
    license_number = '$reqLicense',
    specialty = 'Noi',
    status = 'active',
    verified_at = COALESCE(verified_at, NOW()),
    deleted_at = NULL,
    updated_at = NOW()
WHERE phone = '$reqPhone';
INSERT INTO pack_pharmacy.prescribers (full_name, license_number, phone, specialty, status, verified_at)
SELECT 'Smoke Request BS', '$reqLicense', '$reqPhone', 'Noi', 'active', NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM pack_pharmacy.prescribers WHERE phone = '$reqPhone' AND deleted_at IS NULL
);
DELETE FROM pack_pharmacy.prescriber_tenant_links
WHERE prescriber_id = (SELECT id FROM pack_pharmacy.prescribers WHERE phone = '$reqPhone' AND deleted_at IS NULL LIMIT 1)
  AND tenant_id = '$tenantId'::uuid;
"@
    $seedOut = & $psql -h localhost -p 5432 -U kitplatform -d kitplatform -v ON_ERROR_STOP=1 -c $seedSql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "seed request BS failed: $seedOut" }
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

    $otpReq = Invoke-RestMethod "$base/api/prescriber-portal/auth/otp-request" -Method POST -ContentType 'application/json' `
        -Body (@{ phone = $reqPhone } | ConvertTo-Json)
    $code = if ($otpReq.pilotCode) { $otpReq.pilotCode } else { '000000' }
    $bsLogin = Invoke-RestMethod "$base/api/prescriber-portal/auth/otp-verify" -Method POST -ContentType 'application/json' `
        -Body (@{ phone = $reqPhone; code = $code } | ConvertTo-Json)
    if (-not $bsLogin.accessToken) { throw 'request-BS login failed' }
    $reqH = @{ Authorization = "Bearer $($bsLogin.accessToken)" }

    $requested = Invoke-RestMethod "$base/api/prescriber-portal/links/request" -Method POST -ContentType 'application/json' `
        -Headers $reqH -Body (@{ tenantCode = $tenantCode } | ConvertTo-Json)
    if ($requested.linkStatus -ne 'pending_nt_approval') {
        throw "expected pending_nt_approval, got $($requested.linkStatus)"
    }
    $reqLinkId = $requested.id

    $approved = Invoke-RestMethod "$base/api/pharmacy/prescribers/links/$reqLinkId/approve" -Method POST -Headers $script:adminH
    if ($approved.linkStatus -ne 'active') {
        throw "approve status=$($approved.linkStatus)"
    }

    $pharmacies = Invoke-RestMethod "$base/api/prescriber-portal/pharmacies?activeOnly=true" -Headers $reqH
    $hit = @($pharmacies) | Where-Object { $_.id -eq $reqLinkId -or $_.tenantCode -eq $tenantCode }
    if (-not $hit) { throw 'approved link not visible to BS' }
    $notes += "approve-link ok phone=$reqPhone link=$reqLinkId"
}

Test-Step 'Revoked link blocks new Rx' {
    $revoked = Invoke-RestMethod "$base/api/pharmacy/prescribers/links/$linkId/revoke" -Method POST -Headers $script:adminH
    if ($revoked.linkStatus -ne 'revoked') { throw "revoke status=$($revoked.linkStatus)" }
    $body = @{
        tenantId = $tenantId
        customerId = $customerId
        lines = @(@{ productId = $productId; qtyPrescribed = 1 })
    } | ConvertTo-Json -Depth 5
    try {
        Invoke-RestMethod "$base/api/prescriber-portal/prescriptions" -Method POST -ContentType 'application/json' `
            -Headers $script:bsH -Body $body | Out-Null
        throw 'expected 400 after revoke'
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 400) { throw $_ }
    }
}

Test-Step 'Old Rx still readable after revoke' {
    $old = Invoke-RestMethod "$base/api/prescriber-portal/prescriptions/$($script:rx.id)" -Headers $script:bsH
    if ($old.status -ne 'signed') { throw "old rx status=$($old.status)" }
}

# Restore link for next smoke run
$env:PGPASSWORD = 'kitplatform_dev_2026'
& $psql -h localhost -p 5432 -U kitplatform -d kitplatform -c "UPDATE pack_pharmacy.prescriber_tenant_links SET link_status='active', revoked_at=NULL, revoked_by=NULL, updated_at=NOW() WHERE id='$linkId'::uuid;" | Out-Null
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

Write-Host "`n--- Notes ---" -ForegroundColor DarkGray
$notes | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

Write-Host "`n=== Ket qua: $passed passed, $($failed.Count) failed ===" -ForegroundColor $(if ($failed.Count -eq 0) { 'Green' } else { 'Red' })
if ($failed.Count -gt 0) {
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
exit 0

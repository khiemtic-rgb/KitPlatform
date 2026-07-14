# Lab UAT human-assist probes for U2-U9 (API-level evidence)
# Complements browser checklist in success-ep03-ac135-lab-uat-pack-v1.md
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$Tenant = "NT_XUANHOA",
    [string]$AdminUser = "admin",
    [string]$AdminPass = "Admin@123"
)

$ErrorActionPreference = "Stop"
$results = New-Object System.Collections.Generic.List[object]

function Add-Result([string]$Id, [string]$Pass, [string]$Note) {
    $results.Add([pscustomobject]@{ Id = $Id; Pass = $Pass; Note = $Note }) | Out-Null
    $color = if ($Pass -eq "PASS") { "Green" } elseif ($Pass -eq "SKIP") { "Yellow" } else { "Red" }
    Write-Host "[$Pass] $Id - $Note" -ForegroundColor $color
}

function Login([string]$User, [string]$Pass) {
    return Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
        -Body (@{ username = $User; password = $Pass; tenantCode = $Tenant } | ConvertTo-Json)
}

Write-Host "`n=== EP03 human-assist API UAT @ $BaseUrl ===" -ForegroundColor Cyan
$admin = Login $AdminUser $AdminPass
$ah = @{ Authorization = "Bearer $($admin.accessToken)" }

# U1 audit feed readable
$feed = Invoke-RestMethod "$BaseUrl/api/success/loss/audit-feed?pageSize=20" -Headers $ah
if (($feed.items.Count -ge 1) -and $feed.attributionNotes) {
    Add-Result "U1" "PASS" "audit items=$($feed.items.Count) sample=$($feed.items[0].eventType)/$($feed.items[0].summary)"
} else {
    Add-Result "U1" "FAIL" "empty feed or missing notes"
}

# U10 AC2/AC4 regression
$cash = Invoke-RestMethod "$BaseUrl/api/success/loss/cash-variance" -Headers $ah
$emp = Invoke-RestMethod "$BaseUrl/api/success/loss/reports/by-employee" -Headers $ah
$cockpit = Invoke-RestMethod "$BaseUrl/api/success/owner-cockpit" -Headers $ah
if ($cash.threshold -and $emp.attributionNotes -and $cockpit.riskStrip) {
    Add-Result "U10" "PASS" "cash+byEmployee+riskStrip ok; cycle=$($cockpit.riskStrip.cycleCountStatusToday)"
} else {
    Add-Result "U10" "FAIL" "regression fields missing"
}

# Warehouses for cycle / sales
$warehouses = Invoke-RestMethod "$BaseUrl/api/inventory/warehouses" -Headers $ah
$wh = @($warehouses) | Select-Object -First 1
$wid = if ($wh.id) { $wh.id } else { $wh.Id }
if (-not $wid) { throw "No warehouse" }

# U8 suggestions + create session
$sug = Invoke-RestMethod "$BaseUrl/api/success/loss/cycle-count/suggestions?warehouseId=$wid&limit=15" -Headers $ah
if ($sug.items.Count -ge 10 -and $sug.items.Count -le 20) {
    Add-Result "U8a" "PASS" "suggestions=$($sug.items.Count)"
} else {
    Add-Result "U8a" "FAIL" "suggestions count=$($sug.items.Count) (expect 10-20 or less if thin catalog)"
}

# Avoid blocking if active counting exists - use status first
$status = Invoke-RestMethod "$BaseUrl/api/success/loss/cycle-count/status" -Headers $ah
$sessionId = $null
if ($status.status -eq "in_progress" -and $status.adjustmentId) {
    $sessionId = $status.adjustmentId
    Add-Result "U8b" "PASS" "reuse in_progress session $($status.adjustmentNumber)"
} else {
    try {
        $session = Invoke-RestMethod "$BaseUrl/api/success/loss/cycle-count/sessions" -Method POST -Headers $ah `
            -ContentType "application/json" -Body (@{ warehouseId = $wid; limit = 15; note = "UAT lab" } | ConvertTo-Json)
        $sessionId = $session.adjustmentId
        $reasonOk = [string]$session.reason -match '^\[cycle_count\]'
        if ($reasonOk -and $session.countHref) {
            Add-Result "U8b" "PASS" "created $($session.adjustmentNumber) reason=$($session.reason)"
        } else {
            Add-Result "U8b" "FAIL" "session missing tag/href reason=$($session.reason)"
        }
    } catch {
        Add-Result "U8b" "FAIL" $_.Exception.Message
    }
}

# U9: mark a count entry if possible then check status (do not force approve if no stock lines)
if ($sessionId) {
    $st2 = Invoke-RestMethod "$BaseUrl/api/success/loss/cycle-count/status" -Headers $ah
    if ($st2.status -eq "in_progress") {
        Add-Result "U9" "PASS" "status in_progress after session; count via UI $($st2.countHref)"
    } elseif ($st2.status -in @("done", "has_variance", "not_done")) {
        Add-Result "U9" "PASS" "status=$($st2.status) (no open session needed)"
    } else {
        Add-Result "U9" "FAIL" "unexpected status $($st2.status)"
    }
} else {
    Add-Result "U9" "SKIP" "no session id"
}

# U3: try create limited staff user OR detect existing - prefer probe with sales.write only if we can create temp user
# Soft check: confirm Cancel policy is not SalesWrite (OpenAPI introspection via unauthorized random cancel as admin already tested)
# Create temp cashier if identity APIs allow
$cashierPass = "UatCashier@123"
$cashierUser = "uat_loss_cashier"
$createdCashier = $false
try {
    $roles = Invoke-RestMethod "$BaseUrl/api/system/roles" -Headers $ah
    $adminRole = @($roles.items + $roles) | Where-Object { ($_.roleCode -eq "ADMIN") -or ($_.RoleCode -eq "ADMIN") } | Select-Object -First 1
    # list permissions / roles structure varies - attempt create user with sales.write only via known role PHARMACIST/STAFF
    $roleList = @()
    if ($roles.items) { $roleList = @($roles.items) } elseif ($roles -is [System.Array]) { $roleList = @($roles) }
    $staffRole = $roleList | Where-Object {
        $c = if ($_.roleCode) { $_.roleCode } else { $_.RoleCode }
        $c -in @("STAFF", "PHARMACIST", "CASHIER", "SELLER")
    } | Select-Object -First 1

    if ($staffRole) {
        $roleId = if ($staffRole.id) { $staffRole.id } else { $staffRole.Id }
        try {
            Invoke-RestMethod "$BaseUrl/api/system/users" -Method POST -Headers $ah -ContentType "application/json" -Body (@{
                username = $cashierUser
                password = $cashierPass
                email = "$cashierUser@uat.local"
                roleIds = @($roleId)
                status = 1
            } | ConvertTo-Json)
            $createdCashier = $true
        } catch {
            # maybe exists - try login
        }
        try {
            $cashier = Login $cashierUser $cashierPass
            $ch = @{ Authorization = "Bearer $($cashier.accessToken)" }
            $fake = [guid]::NewGuid()
            try {
                Invoke-WebRequest "$BaseUrl/api/sales/orders/$fake/cancel" -Method POST -Headers $ch -UseBasicParsing -ErrorAction Stop | Out-Null
                Add-Result "U3" "FAIL" "cashier cancel unexpectedly succeeded"
            } catch {
                $code = [int]$_.Exception.Response.StatusCode
                if ($code -eq 403) { Add-Result "U3" "PASS" "cashier cancel blocked 403" }
                else { Add-Result "U3" "SKIP" "cashier cancel status=$code (need sales.write without sales.cancel)" }
            }
            try {
                Invoke-WebRequest "$BaseUrl/api/inventory/adjustments/$fake/approve" -Method POST -Headers $ch -UseBasicParsing -ErrorAction Stop | Out-Null
                Add-Result "U7" "FAIL" "cashier approve unexpectedly succeeded"
            } catch {
                $code = [int]$_.Exception.Response.StatusCode
                if ($code -eq 403) { Add-Result "U7" "PASS" "cashier approve blocked 403" }
                else { Add-Result "U7" "SKIP" "cashier approve status=$code" }
            }
        } catch {
            Add-Result "U3" "SKIP" "no non-admin cashier login: $($_.Exception.Message)"
            Add-Result "U7" "SKIP" "no non-admin cashier login"
        }
    } else {
        Add-Result "U3" "SKIP" "no STAFF/PHARMACIST role to probe"
        Add-Result "U7" "SKIP" "no STAFF/PHARMACIST role to probe"
    }
} catch {
    Add-Result "U3" "SKIP" "roles API: $($_.Exception.Message)"
    Add-Result "U7" "SKIP" "roles API: $($_.Exception.Message)"
}

# U4 admin cancel missing -> not 403
try {
    Invoke-WebRequest "$BaseUrl/api/sales/orders/$([guid]::NewGuid())/cancel" -Method POST -Headers $ah -UseBasicParsing -ErrorAction Stop | Out-Null
    Add-Result "U4" "FAIL" "admin cancel missing order succeeded"
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    if ($code -eq 403) { Add-Result "U4" "FAIL" "admin blocked on cancel" }
    elseif ($code -in 404, 400) { Add-Result "U4" "PASS" "admin allowed cancel path (status=$code)" }
    else { Add-Result "U4" "FAIL" "unexpected $code" }
}

# U5/U6 discount override: request high discount as admin with only sales.discount in theory - ADMIN bypasses.
# Verify pending list endpoint + 409 shape by calling repository path if we can use Unlimited-less path.
# Document SKIP if ADMIN has unlimited.
Add-Result "U5" "SKIP" "ADMIN often has discount unlimited - verify 409 with limited staff in browser/POS"
Add-Result "U6" "SKIP" "decide path reachable: /api/system/workflow/pos-discount/pending (manual approve if task exists)"
try {
    $pending = Invoke-RestMethod "$BaseUrl/api/system/workflow/pos-discount/pending" -Headers $ah
    Add-Result "U5b" "PASS" "pending endpoint ok count=$(@($pending).Count)"
} catch {
    Add-Result "U5b" "FAIL" $_.Exception.Message
}

# U2: historical discounts / feed discount events
$disc = @($feed.items) | Where-Object { $_.eventType -eq "discount" }
$empDisc = @($emp.discounts)
if ($disc.Count -gt 0 -or $empDisc.Count -gt 0) {
    Add-Result "U2" "PASS" "discount evidence feed=$($disc.Count) ac4Rows=$($empDisc.Count)"
} else {
    Add-Result "U2" "SKIP" "no discount rows yet - create POS discount order in browser then re-check feed"
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize
$fail = @($results | Where-Object { $_.Pass -eq "FAIL" })
$pass = @($results | Where-Object { $_.Pass -eq "PASS" })
$skip = @($results | Where-Object { $_.Pass -eq "SKIP" })
Write-Host "PASS=$($pass.Count) SKIP=$($skip.Count) FAIL=$($fail.Count)"
if ($fail.Count -gt 0) { exit 1 }
exit 0

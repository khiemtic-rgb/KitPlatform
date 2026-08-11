# Mode A smoke: counter PIN + staff-read OTP (no code on customer app) + profile APIs.
# Usage: .\scripts\smoke-mode-a-customer-app.ps1
param(
    [string]$Base = 'http://localhost:5290',
    [string]$TenantCode = 'DEMO_PHARMACY',
    [string]$Phone = '0909123456',
    [string]$AdminUser = 'admin',
    [string]$AdminPass = 'Admin@123',
    [string]$CounterPin = '482917'
)

$ErrorActionPreference = 'Stop'
$passed = 0
$failed = @()

function Ok([string]$Name, [string]$Detail = '') {
    $script:passed++
    if ($Detail) { Write-Host "[OK] $Name - $Detail" -ForegroundColor Green }
    else { Write-Host "[OK] $Name" -ForegroundColor Green }
}

function Fail([string]$Name, [string]$Detail) {
    $script:failed += $Name
    Write-Host "[FAIL] $Name - $Detail" -ForegroundColor Red
}

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Path,
        $Body = $null,
        $Headers = $null
    )
    $params = @{
        Uri             = "$Base$Path"
        Method          = $Method
        ContentType     = 'application/json'
        TimeoutSec      = 30
        UseBasicParsing = $true
    }
    if ($Headers) { $params.Headers = $Headers }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 6 -Compress) }
    return Invoke-RestMethod @params
}

function Try-Json {
    param(
        [string]$Method,
        [string]$Path,
        $Body = $null,
        $Headers = $null
    )
    try {
        return @{ ok = $true; data = (Invoke-Json -Method $Method -Path $Path -Body $Body -Headers $Headers) }
    }
    catch {
        $status = $null
        $msg = $_.Exception.Message
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $raw = $reader.ReadToEnd()
                    if ($raw) {
                        $parsed = $raw | ConvertFrom-Json
                        if ($parsed.message) { $msg = [string]$parsed.message }
                    }
                }
            }
            catch { }
        }
        return @{ ok = $false; status = $status; message = $msg }
    }
}

Write-Host ""
Write-Host "=== Mode A customer-app smoke ===" -ForegroundColor Cyan
Write-Host "Base=$Base Tenant=$TenantCode Phone=$Phone"
Write-Host ""

# 1) Health
try {
    $h = Invoke-Json -Method GET -Path '/api/health'
    if ($h.status -ne 'ok') { throw "status=$($h.status)" }
    Ok 'API health'
}
catch {
    Fail 'API health' $_.Exception.Message
    throw
}

# 2) Admin login
try {
    $admin = Invoke-Json -Method POST -Path '/api/auth/login' -Body @{
        username   = $AdminUser
        password   = $AdminPass
        tenantCode = $TenantCode
    }
    if (-not $admin.accessToken) { throw 'no accessToken' }
    $adminH = @{ Authorization = "Bearer $($admin.accessToken)" }
    Ok 'Admin login' $TenantCode
}
catch {
    Fail 'Admin login' $_.Exception.Message
    throw
}

# 3) Ensure counter PIN
try {
    $settings = Invoke-Json -Method PUT -Path '/api/customers/app-auth-settings' -Body @{
        counterPin = $CounterPin
    } -Headers $adminH
    if (-not $settings.hasCounterPin) { throw 'hasCounterPin=false after PUT' }
    Ok 'Set counter PIN' "hasCounterPin=$($settings.hasCounterPin)"
}
catch { Fail 'Set counter PIN' $_.Exception.Message }

# 4) Counter OTP without PIN -> 400
$r = Try-Json -Method POST -Path '/api/customer-app/auth/request-otp' -Body @{
    phone      = $Phone
    tenantCode = $TenantCode
    channel    = 'counter'
}
if ((-not $r.ok) -and (($r.status -eq 400) -or ($r.message -match 'PIN|pin|quay'))) {
    Ok 'Counter OTP rejects empty PIN' $r.message
}
else {
    Fail 'Counter OTP rejects empty PIN' "expected 400, got ok=$($r.ok) status=$($r.status) msg=$($r.message)"
}

# 5) Wrong PIN -> 400
$r = Try-Json -Method POST -Path '/api/customer-app/auth/request-otp' -Body @{
    phone      = $Phone
    tenantCode = $TenantCode
    channel    = 'counter'
    counterPin = '000000'
}
if ((-not $r.ok) -and (($r.status -eq 400) -or ($r.message -match 'PIN|pin|dung|wrong|quay'))) {
    Ok 'Counter OTP rejects wrong PIN' $r.message
}
else {
    Fail 'Counter OTP rejects wrong PIN' "ok=$($r.ok) status=$($r.status) msg=$($r.message)"
}

Start-Sleep -Seconds 1

# 6) Valid counter OTP - no pilotCode on app (Mode A)
$r = Try-Json -Method POST -Path '/api/customer-app/auth/request-otp' -Body @{
    phone      = $Phone
    tenantCode = $TenantCode
    channel    = 'counter'
    counterPin = $CounterPin
}
$otp = $null
if (-not $r.ok) {
    Fail 'Counter OTP with PIN' $r.message
}
else {
    $otp = $r.data
    $statusOk = ($otp.status -eq 'otp_sent') -or [string]::IsNullOrWhiteSpace([string]$otp.status)
    $noPilot = [string]::IsNullOrWhiteSpace([string]$otp.pilotCode)
    if ($statusOk -and $noPilot) {
        Ok 'Counter OTP hides code on app' "msg=$($otp.message)"
    }
    elseif (-not $noPilot) {
        Fail 'Counter OTP hides code on app' "pilotCode unexpectedly returned: $($otp.pilotCode)"
    }
    else {
        Fail 'Counter OTP with PIN' "unexpected status=$($otp.status)"
    }
}

# 7) Staff reads OTP via Admin
$code = $null
$customerId = $null
$pilotOk = $false
try {
    $list = Invoke-Json -Method GET -Path "/api/customers?search=$Phone&pageSize=5" -Headers $adminH
    $items = @($list.items)
    if ($items.Count -lt 1) { throw "customer $Phone not found" }
    $customerId = [string]$items[0].id
    if ([string]::IsNullOrWhiteSpace($customerId)) { $customerId = [string]$items[0].customerId }
    $pilot = Invoke-Json -Method GET -Path "/api/customers/$customerId/pilot-otp" -Headers $adminH
    $codeCandidate = $pilot.code
    if ([string]::IsNullOrWhiteSpace([string]$codeCandidate)) { $codeCandidate = $pilot.Code }
    if ([string]::IsNullOrWhiteSpace([string]$codeCandidate)) { $codeCandidate = $pilot.pilotCode }
    if (-not [string]::IsNullOrWhiteSpace([string]$codeCandidate)) {
        $code = [string]$codeCandidate
        Ok 'Admin pilot-otp readable' "code=$code expires=$($pilot.expiresAt)"
        $pilotOk = $true
    }
    else {
        throw "pilot-otp empty: $($pilot | ConvertTo-Json -Compress)"
    }
}
catch {
    Write-Host "  (pilot-otp path: $($_.Exception.Message)) - trying issue-counter-otp..." -ForegroundColor DarkYellow
}

if (-not $pilotOk) {
    try {
        $issued = Invoke-Json -Method POST -Path '/api/customers/issue-counter-otp' -Body @{
            phone    = $Phone
            fullName = 'Smoke Mode A'
        } -Headers $adminH
        $customerId = [string]$issued.customerId
        $code = [string]$issued.pilotCode
        if ([string]::IsNullOrWhiteSpace($code)) { throw 'issue-counter-otp returned no pilotCode (ExposePilotOtpInAdmin?)' }
        Ok 'Admin issue-counter-otp' "code=$code"
    }
    catch { Fail 'Staff can read OTP' $_.Exception.Message }
}

# 8) Verify OTP
$session = $null
if ($code) {
    try {
        $session = Invoke-Json -Method POST -Path '/api/customer-app/auth/verify-otp' -Body @{
            phone      = $Phone
            code       = $code
            tenantCode = $TenantCode
        }
        if (-not $session.accessToken) { throw 'no accessToken' }
        Ok 'Verify OTP login' $session.profile.fullName
    }
    catch { Fail 'Verify OTP login' $_.Exception.Message }
}

$custH = $null
if ($session) { $custH = @{ Authorization = "Bearer $($session.accessToken)" } }

# 9) /me - member
if ($custH) {
    try {
        $me = Invoke-Json -Method GET -Path '/api/customer-app/auth/me' -Headers $custH
        $rel = ([string]$me.pharmacyRelation).ToLowerInvariant()
        if ($rel -ne 'member') { throw "pharmacyRelation=$rel (want member)" }
        Ok '/me pharmacyRelation=member' "tenant=$($me.tenantCode)"
    }
    catch { Fail '/me pharmacyRelation=member' $_.Exception.Message }
}

# 10) PATCH profile
if ($custH) {
    try {
        $name = "Smoke A $([DateTime]::UtcNow.ToString('HHmmss'))"
        $updated = Invoke-Json -Method PATCH -Path '/api/customer-app/auth/profile' -Body @{ fullName = $name } -Headers $custH
        if ($updated.fullName -ne $name) { throw "fullName=$($updated.fullName)" }
        Ok 'PATCH /auth/profile' $name
    }
    catch { Fail 'PATCH /auth/profile' $_.Exception.Message }
}

# 11) pharmacy-link idempotent
if ($custH) {
    try {
        $linked = Invoke-Json -Method POST -Path '/api/customer-app/auth/pharmacy-link' -Body @{
            verifiedVia = 'qr_scan'
            tenantCode  = $TenantCode
        } -Headers $custH
        if (([string]$linked.pharmacyRelation).ToLowerInvariant() -ne 'member') {
            throw "relation=$($linked.pharmacyRelation)"
        }
        Ok 'POST /auth/pharmacy-link (member)' $linked.pharmacyRelation
    }
    catch { Fail 'POST /auth/pharmacy-link' $_.Exception.Message }
}

# 12) Second verify of same code -> 401
if ($code) {
    $r = Try-Json -Method POST -Path '/api/customer-app/auth/verify-otp' -Body @{
        phone      = $Phone
        code       = $code
        tenantCode = $TenantCode
    }
    if ((-not $r.ok) -and ($r.status -eq 401)) {
        Ok 'OTP single-use (second verify 401)'
    }
    else {
        Fail 'OTP single-use' "ok=$($r.ok) status=$($r.status)"
    }
}

Write-Host ""
Write-Host "=== Result: $passed passed, $($failed.Count) failed ===" -ForegroundColor $(if ($failed.Count) { 'Red' } else { 'Green' })
if ($failed.Count) {
    Write-Host ("Failed: " + ($failed -join ', ')) -ForegroundColor Red
    exit 1
}
exit 0

# Smoke Novixa Connect C5 (status sync ready → Pharmacy consume)
# Usage: .\scripts\smoke-connect-c5-local.ps1
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$PharmacyTenant = "NT_XUANHOA",
    [string]$ClinicTenant = "DEMO_CLINIC",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"

function Login([string]$tenant) {
    $auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
        -Body (@{ username = $User; password = $Pass; tenantCode = $tenant } | ConvertTo-Json)
    if (-not $auth.accessToken) { throw "login failed for $tenant" }
    return @{ Authorization = "Bearer $($auth.accessToken)" }
}

Write-Host "`n=== Connect C5 smoke ($ClinicTenant → $PharmacyTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$hPharm = Login $PharmacyTenant
$hClinic = Login $ClinicTenant
Write-Host "[OK] Login both tenants" -ForegroundColor Green

$ov = Invoke-RestMethod "$BaseUrl/api/connect/overview" -Headers $hClinic
if ($ov.phase -ne "C5_status_sync") { throw "unexpected phase=$($ov.phase)" }
if ($ov.enabledCapabilities -notcontains "status_sync") { throw "missing status_sync" }
Write-Host "[OK] overview phase=$($ov.phase)" -ForegroundColor Green

# Ensure active org link
$rawLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hPharm
$activeList = @($rawLinks | Where-Object {
    $_.partnerTenantCode -eq $ClinicTenant -and $_.partnerOrgRole -eq "clinic"
})
$active = if ($activeList.Length -gt 0) { $activeList[0] } else { $null }
if (-not $active) {
    $inviteLink = (@{
        partnerTenantCode = $ClinicTenant
        ourOrgRole = "pharmacy"
        partnerOrgRole = "clinic"
        notes = "C5 smoke ensure link"
    } | ConvertTo-Json -Compress)
    $created = Invoke-RestMethod "$BaseUrl/api/connect/org-links/invite" -Method POST -Headers $hPharm `
        -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($inviteLink))
    $null = Invoke-RestMethod "$BaseUrl/api/connect/org-links/$($created.id)/accept" -Method POST -Headers $hClinic
    $rawLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hPharm
    $activeList = @($rawLinks | Where-Object {
        $_.partnerTenantCode -eq $ClinicTenant -and $_.partnerOrgRole -eq "clinic"
    })
    $active = if ($activeList.Length -gt 0) { $activeList[0] } else { $null }
}
if (-not $active) { throw "no active org link" }
$clinicTenantId = [string]$active.partnerTenantId

# Pharmacy tenant id from clinic POV
$clinicLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hClinic
$pharmLinkList = @($clinicLinks | Where-Object {
    $_.partnerTenantCode -eq $PharmacyTenant -and $_.partnerOrgRole -eq "pharmacy"
})
$pharmLink = if ($pharmLinkList.Length -gt 0) { $pharmLinkList[0] } else { $null }
if (-not $pharmLink) { throw "clinic cannot see pharmacy partner" }
$pharmacyTenantId = [string]$pharmLink.partnerTenantId

$custPage = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5" -Headers $hPharm
$pharmacyCustomerId = $null
if ($custPage.items -and @($custPage.items).Length -gt 0) {
    $pharmacyCustomerId = [string]$custPage.items[0].id
}
if (-not $pharmacyCustomerId) {
    $createdCust = Invoke-RestMethod "$BaseUrl/api/customers" -Method POST -Headers $hPharm `
        -ContentType "application/json; charset=utf-8" `
        -Body ([System.Text.Encoding]::UTF8.GetBytes((@{
            fullName = "BN Smoke C5"
            phone = "0912666777"
        } | ConvertTo-Json -Compress)))
    $pharmacyCustomerId = [string]$createdCust.id
}

# Referral → accept → complete emits ready
$refBody = (@{
    clinicTenantId = $clinicTenantId
    pharmacyCustomerId = $pharmacyCustomerId
    reason = "C5 status smoke"
} | ConvertTo-Json -Compress)
$ref = Invoke-RestMethod "$BaseUrl/api/connect/referrals" -Method POST -Headers $hPharm `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($refBody))
$null = Invoke-RestMethod "$BaseUrl/api/connect/referrals/$($ref.id)/accept" -Method POST -Headers $hClinic
$null = Invoke-RestMethod "$BaseUrl/api/connect/referrals/$($ref.id)/complete" -Method POST -Headers $hClinic
Write-Host "[OK] referral completed id=$($ref.id)" -ForegroundColor Green

function Get-ConnectProp($obj, [string]$camel) {
    if ($null -eq $obj) { return $null }
    $pascal = $camel.Substring(0, 1).ToUpperInvariant() + $camel.Substring(1)
    foreach ($name in @($camel, $pascal)) {
        $p = $obj.PSObject.Properties[$name]
        if ($null -ne $p -and $null -ne $p.Value -and "$($p.Value)" -ne '') { return $p.Value }
    }
    return $null
}

$refId = [string]$ref.id
$evtMatches = @()
for ($attempt = 1; $attempt -le 5; $attempt++) {
    $pendingRaw = Invoke-RestMethod "$BaseUrl/api/connect/status-events/pending" -Headers $hPharm
    $pending = @($pendingRaw)
    $evtMatches = @($pending | Where-Object {
        [string](Get-ConnectProp $_ 'sourceId') -eq $refId -and
        [string](Get-ConnectProp $_ 'sourceType') -eq 'referral'
    })
    if ($evtMatches.Count -ge 1) { break }
    Start-Sleep -Milliseconds 400
}
if ($evtMatches.Count -lt 1) {
    $allRaw = Invoke-RestMethod "$BaseUrl/api/connect/status-events" -Headers $hPharm
    $all = @($allRaw)
    $fromAll = @($all | Where-Object {
        [string](Get-ConnectProp $_ 'sourceId') -eq $refId -and
        [string](Get-ConnectProp $_ 'sourceType') -eq 'referral'
    })
    throw ("pharmacy pending missing referral-ready event for {0}; pendingCount={1}; allMatch={2} status={3}" -f `
        $refId, $pending.Count, $fromAll.Count, ($(if ($fromAll.Count -gt 0) { Get-ConnectProp $fromAll[0] 'eventStatus' } else { 'n/a' })))
}
$evt = $evtMatches[0]
$evtId = [string](Get-ConnectProp $evt 'id')
if ([string]::IsNullOrWhiteSpace($evtId) -or $evtId.Length -lt 32) {
    throw ("invalid event id '{0}'" -f $evtId)
}
$evtStatus = [string](Get-ConnectProp $evt 'eventStatus')
if ($evtStatus -ne "pending_pharmacy") { throw "expected pending_pharmacy got $evtStatus" }
Write-Host ('[OK] pharmacy pending event id={0}' -f $evtId) -ForegroundColor Green

# Clinic cannot consume
try {
    Invoke-RestMethod ("{0}/api/connect/status-events/{1}/consume" -f $BaseUrl, $evtId) -Method POST -Headers $hClinic
    throw "clinic consume should fail"
} catch {
    if ($_.Exception.Message -match "clinic consume should fail") { throw }
    Write-Host "[OK] clinic consume rejected" -ForegroundColor Green
}

$consumed = Invoke-RestMethod ("{0}/api/connect/status-events/{1}/consume" -f $BaseUrl, $evtId) -Method POST -Headers $hPharm
if ($consumed.eventStatus -ne "consumed") { throw "expected consumed" }
Write-Host "[OK] pharmacy consume" -ForegroundColor Green

# Manual ready from clinic
$manualBody = (@{
    pharmacyTenantId = $pharmacyTenantId
    patientDisplayName = "BN Manual C5"
    patientPhone = "0912888999"
    summary = "C5 manual ready smoke"
} | ConvertTo-Json -Compress)
$manual = Invoke-RestMethod "$BaseUrl/api/connect/status-events" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($manualBody))
if ($manual.eventStatus -ne "pending_pharmacy") { throw "manual expected pending" }
Write-Host "[OK] clinic manual ready id=$($manual.id)" -ForegroundColor Green

$dismissed = Invoke-RestMethod "$BaseUrl/api/connect/status-events/$($manual.id)/dismiss" -Method POST -Headers $hPharm
if ($dismissed.eventStatus -ne "dismissed") { throw "expected dismissed" }
Write-Host "[OK] pharmacy dismiss manual" -ForegroundColor Green

# Booking complete also emits (via referral — cannot attach pharmacy without referral)
$ref2Body = (@{
    clinicTenantId = $clinicTenantId
    pharmacyCustomerId = $pharmacyCustomerId
    reason = "C5 booking emit"
} | ConvertTo-Json -Compress)
$ref2 = Invoke-RestMethod "$BaseUrl/api/connect/referrals" -Method POST -Headers $hPharm `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($ref2Body))
$null = Invoke-RestMethod "$BaseUrl/api/connect/referrals/$($ref2.id)/accept" -Method POST -Headers $hClinic

$when = (Get-Date).ToUniversalTime().AddDays(3).ToString("o")
$bookBody = (@{
    scheduledAt = $when
    patientDisplayName = "BN Smoke C5 Book"
    patientPhone = "0912000111"
    referralId = $ref2.id
    durationMinutes = 20
    notes = "C5 booking emit"
} | ConvertTo-Json -Compress)
$booking = Invoke-RestMethod "$BaseUrl/api/connect/bookings" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($bookBody))
$null = Invoke-RestMethod "$BaseUrl/api/connect/bookings/$($booking.id)/confirm" -Method POST -Headers $hClinic
$null = Invoke-RestMethod "$BaseUrl/api/connect/bookings/$($booking.id)/complete" -Method POST -Headers $hClinic

$pending2 = @(Invoke-RestMethod "$BaseUrl/api/connect/status-events/pending" -Headers $hPharm)
$bookEvt = $pending2 | Where-Object { $_.sourceId -eq $booking.id -and $_.sourceType -eq "booking" } | Select-Object -First 1
if (-not $bookEvt) { throw "missing booking-ready event" }
Write-Host "[OK] booking complete emitted ready id=$($bookEvt.id)" -ForegroundColor Green

Write-Host "`n=== Connect C5 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/connect/status (NT_XUANHOA inbox / DEMO_CLINIC outbound)"

# Smoke Novixa Connect C4 (booking stub + notify)
# Usage: .\scripts\smoke-connect-c4-local.ps1
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

Write-Host "`n=== Connect C4 smoke ($ClinicTenant booking @ $BaseUrl) ===" -ForegroundColor Cyan

$hPharm = Login $PharmacyTenant
$hClinic = Login $ClinicTenant
Write-Host "[OK] Login both tenants" -ForegroundColor Green

$ov = Invoke-RestMethod "$BaseUrl/api/connect/overview" -Headers $hClinic
if ($ov.phase -notin @("C4_booking", "C5_status_sync")) { throw "unexpected phase=$($ov.phase)" }
if ($ov.enabledCapabilities -notcontains "booking_stub") { throw "missing booking_stub" }
Write-Host "[OK] overview phase=$($ov.phase)" -ForegroundColor Green

# Ensure link + accepted referral
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
        notes = "C4 smoke ensure link"
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
$clinicTenantId = [string]$active.partnerTenantId

$custPage = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5" -Headers $hPharm
$pharmacyCustomerId = $null
if ($custPage.items -and @($custPage.items).Length -gt 0) {
    $pharmacyCustomerId = [string]$custPage.items[0].id
}
if (-not $pharmacyCustomerId) {
    $createdCust = Invoke-RestMethod "$BaseUrl/api/customers" -Method POST -Headers $hPharm `
        -ContentType "application/json; charset=utf-8" `
        -Body ([System.Text.Encoding]::UTF8.GetBytes((@{
            fullName = "BN Smoke C4"
            phone = "0912555666"
        } | ConvertTo-Json -Compress)))
    $pharmacyCustomerId = [string]$createdCust.id
}

$refBody = (@{
    clinicTenantId = $clinicTenantId
    pharmacyCustomerId = $pharmacyCustomerId
    reason = "C4 booking smoke"
} | ConvertTo-Json -Compress)
$ref = Invoke-RestMethod "$BaseUrl/api/connect/referrals" -Method POST -Headers $hPharm `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($refBody))
$null = Invoke-RestMethod "$BaseUrl/api/connect/referrals/$($ref.id)/accept" -Method POST -Headers $hClinic
Write-Host "[OK] referral accepted id=$($ref.id)" -ForegroundColor Green

# Pharmacy cannot create booking
try {
    $bad = (@{
        scheduledAt = (Get-Date).ToUniversalTime().AddDays(1).ToString("o")
        patientDisplayName = "Should Fail"
        referralId = $ref.id
    } | ConvertTo-Json -Compress)
    Invoke-RestMethod "$BaseUrl/api/connect/bookings" -Method POST -Headers $hPharm `
        -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($bad))
    throw "pharmacy create should fail"
} catch {
    if ($_.Exception.Message -match "pharmacy create should fail") { throw }
    Write-Host "[OK] pharmacy create booking rejected" -ForegroundColor Green
}

$when = (Get-Date).ToUniversalTime().AddDays(2).ToString("o")
$bookBody = (@{
    scheduledAt = $when
    patientDisplayName = "BN Smoke C4"
    patientPhone = "0912555666"
    referralId = $ref.id
    durationMinutes = 30
    notes = "C4 smoke"
} | ConvertTo-Json -Compress)

$booking = Invoke-RestMethod "$BaseUrl/api/connect/bookings" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($bookBody))
if ($booking.bookingStatus -ne "proposed") { throw "expected proposed, got $($booking.bookingStatus)" }
if (-not $booking.notifiedAt) { Write-Host "[WARN] notifiedAt empty (notify still may have logged)" -ForegroundColor Yellow }
Write-Host "[OK] create booking id=$($booking.id) status=proposed" -ForegroundColor Green

$confirmed = Invoke-RestMethod "$BaseUrl/api/connect/bookings/$($booking.id)/confirm" -Method POST -Headers $hClinic
if ($confirmed.bookingStatus -ne "confirmed") { throw "expected confirmed" }
Write-Host "[OK] confirm" -ForegroundColor Green

$pharmList = @(Invoke-RestMethod "$BaseUrl/api/connect/bookings" -Headers $hPharm)
$seen = $pharmList | Where-Object { $_.id -eq $booking.id } | Select-Object -First 1
if (-not $seen) { throw "pharmacy cannot see booking linked to them" }
Write-Host "[OK] pharmacy sees booking" -ForegroundColor Green

$completed = Invoke-RestMethod "$BaseUrl/api/connect/bookings/$($booking.id)/complete" -Method POST -Headers $hClinic
if ($completed.bookingStatus -ne "completed") { throw "expected completed" }
Write-Host "[OK] complete" -ForegroundColor Green

Write-Host "`n=== Connect C4 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/connect/bookings (DEMO_CLINIC)"

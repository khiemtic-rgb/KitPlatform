# Smoke Novixa Connect C2 (doctor membership) against local API
# Usage: .\scripts\smoke-connect-c2-local.ps1
# Prerequisite: migration 108 DEMO_CLINIC; C1 Pharmacy <-> DEMO_CLINIC active
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

Write-Host "`n=== Connect C2 smoke (clinic=$ClinicTenant, pharmacy=$PharmacyTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$hClinic = Login $ClinicTenant
Write-Host "[OK] Login $ClinicTenant" -ForegroundColor Green

$ov = Invoke-RestMethod "$BaseUrl/api/connect/overview" -Headers $hClinic
if ($ov.phase -notin @("C4_booking", "C5_status_sync")) { throw "unexpected phase=$($ov.phase)" }
if ($ov.enabledCapabilities -notcontains "clinic_membership") { throw "missing clinic_membership" }
if ($ov.enabledCapabilities -notcontains "org_profile") { throw "missing org_profile" }
Write-Host "[OK] overview phase=$($ov.phase)" -ForegroundColor Green

$clinicProfile = Invoke-RestMethod "$BaseUrl/api/connect/org-profile" -Headers $hClinic
if ($clinicProfile.orgKind -ne "clinic") { throw "expected clinic org profile" }

$hPharm = Login $PharmacyTenant
Write-Host "[OK] Login $PharmacyTenant" -ForegroundColor Green

# Pharmacy must NOT invite doctors
try {
    $denyBody = (@{
        fullName = "Should Fail"
        phone = "0901111222"
        membershipRole = "attending"
    } | ConvertTo-Json -Compress)
    Invoke-RestMethod "$BaseUrl/api/connect/clinics/memberships/invite" -Method POST -Headers $hPharm `
        -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($denyBody))
    throw "pharmacy invite should fail"
} catch {
    if ($_.Exception.Message -match "pharmacy invite should fail") { throw }
    Write-Host "[OK] pharmacy membership invite rejected" -ForegroundColor Green
}

$rawLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hPharm
$activeList = @($rawLinks | Where-Object {
    $_.partnerTenantCode -eq $ClinicTenant -and $_.partnerOrgRole -eq "clinic"
})
$active = if ($activeList.Length -gt 0) { $activeList[0] } else { $null }
if (-not $active) {
    Write-Host "[..] creating C1 org link $PharmacyTenant -> $ClinicTenant" -ForegroundColor Yellow
    $inviteLink = (@{
        partnerTenantCode = $ClinicTenant
        ourOrgRole = "pharmacy"
        partnerOrgRole = "clinic"
        notes = "C2 smoke ensure link"
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
if (-not $active) { throw "no active org link to clinic" }
$clinicTenantId = [string]$active.partnerTenantId
if ([string]::IsNullOrWhiteSpace($clinicTenantId) -or $clinicTenantId -match '\s') {
    throw "bad clinicTenantId='$clinicTenantId'"
}
Write-Host "[OK] active org link clinicTenantId=$clinicTenantId" -ForegroundColor Green

$phone = "090" + (Get-Random -Minimum 1000000 -Maximum 9999999)
$inviteBody = (@{
    fullName = "BS Smoke C2"
    phone = $phone
    licenseNumber = "CCHN-C2-$phone"
    specialty = "Nội tổng quát"
    membershipRole = "attending"
    notes = "C2 smoke"
} | ConvertTo-Json -Compress)

try {
    $mem = Invoke-RestMethod "$BaseUrl/api/connect/clinics/memberships/invite" -Method POST -Headers $hClinic `
        -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($inviteBody))
} catch {
    throw "clinic invite failed: $($_.ErrorDetails.Message)"
}
if ($mem.membershipStatus -ne "pending_doctor_accept") {
    throw "expected pending_doctor_accept, got $($mem.membershipStatus)"
}
Write-Host "[OK] invite membership id=$($mem.id) phone=$phone" -ForegroundColor Green

$confirmed = Invoke-RestMethod "$BaseUrl/api/connect/clinics/memberships/$($mem.id)/confirm" -Method POST -Headers $hClinic
if ($confirmed.membershipStatus -ne "active") { throw "expected active after confirm, got $($confirmed.membershipStatus)" }
Write-Host "[OK] clinic confirm -> active" -ForegroundColor Green

$docs = @(Invoke-RestMethod "$BaseUrl/api/connect/partners/$clinicTenantId/doctors" -Headers $hPharm)
$found = $docs | Where-Object {
    $_.phone -eq $phone -or $_.Phone -eq $phone -or $_.id -eq $confirmed.doctorId -or $_.Id -eq $confirmed.doctorId
} | Select-Object -First 1
if (-not $found) { throw "pharmacy cannot see clinic doctor after active membership" }
Write-Host "[OK] pharmacy sees partner clinic doctor" -ForegroundColor Green

try {
    Invoke-RestMethod "$BaseUrl/api/connect/partners/00000000-0000-0000-0000-000000000099/doctors" -Headers $hPharm
    throw "expected failure for unlinked partner"
} catch {
    if ($_.Exception.Message -match "expected failure") { throw }
    Write-Host "[OK] unlinked partner doctors rejected" -ForegroundColor Green
}

Write-Host "`n=== Connect C2 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin UI clinic: http://localhost:5173/connect/team (login DEMO_CLINIC)"

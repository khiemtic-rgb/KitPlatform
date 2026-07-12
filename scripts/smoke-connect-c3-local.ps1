# Smoke Novixa Connect C3 (Pharmacy → Clinic referral)
# Usage: .\scripts\smoke-connect-c3-local.ps1
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

Write-Host "`n=== Connect C3 smoke ($PharmacyTenant -> $ClinicTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$hPharm = Login $PharmacyTenant
$hClinic = Login $ClinicTenant
Write-Host "[OK] Login both tenants" -ForegroundColor Green

$ov = Invoke-RestMethod "$BaseUrl/api/connect/overview" -Headers $hPharm
if ($ov.phase -notin @("C4_booking", "C5_status_sync")) { throw "unexpected phase=$($ov.phase)" }
if ($ov.enabledCapabilities -notcontains "referral") { throw "missing referral capability" }
Write-Host "[OK] overview phase=$($ov.phase)" -ForegroundColor Green

# Ensure active Pharmacy ↔ Clinic link
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
        notes = "C3 smoke ensure link"
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
Write-Host "[OK] clinicTenantId=$clinicTenantId" -ForegroundColor Green

# Resolve pharmacy CRM customer (required for referral)
$custPage = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5" -Headers $hPharm
$pharmacyCustomerId = $null
if ($custPage.items -and @($custPage.items).Length -gt 0) {
    $pharmacyCustomerId = [string]$custPage.items[0].id
}
if (-not $pharmacyCustomerId) {
    $createdCust = Invoke-RestMethod "$BaseUrl/api/customers" -Method POST -Headers $hPharm `
        -ContentType "application/json; charset=utf-8" `
        -Body ([System.Text.Encoding]::UTF8.GetBytes((@{
            fullName = "BN Smoke C3"
            phone = "0912345678"
        } | ConvertTo-Json -Compress)))
    $pharmacyCustomerId = [string]$createdCust.id
}
if (-not $pharmacyCustomerId) { throw "no pharmacy customer for referral" }
Write-Host "[OK] pharmacyCustomerId=$pharmacyCustomerId" -ForegroundColor Green

# Clinic cannot create referral
try {
    $bad = (@{
        clinicTenantId = $clinicTenantId
        pharmacyCustomerId = $pharmacyCustomerId
        patientDisplayName = "Should Fail"
    } | ConvertTo-Json -Compress)
    Invoke-RestMethod "$BaseUrl/api/connect/referrals" -Method POST -Headers $hClinic `
        -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($bad))
    throw "clinic create should fail"
} catch {
    if ($_.Exception.Message -match "clinic create should fail") { throw }
    Write-Host "[OK] clinic create referral rejected" -ForegroundColor Green
}

$body = (@{
    clinicTenantId = $clinicTenantId
    pharmacyCustomerId = $pharmacyCustomerId
    reason = "Theo doi huyet ap"
    notes = "C3 smoke"
} | ConvertTo-Json -Compress)

$ref = Invoke-RestMethod "$BaseUrl/api/connect/referrals" -Method POST -Headers $hPharm `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
if ($ref.referralStatus -ne "pending_clinic_accept") {
    throw "expected pending_clinic_accept, got $($ref.referralStatus)"
}
Write-Host "[OK] create referral id=$($ref.id)" -ForegroundColor Green

$inbox = @(Invoke-RestMethod "$BaseUrl/api/connect/referrals/inbox" -Headers $hClinic)
$hit = $inbox | Where-Object { $_.id -eq $ref.id } | Select-Object -First 1
if (-not $hit) { throw "clinic inbox missing referral" }
Write-Host "[OK] clinic inbox sees referral" -ForegroundColor Green

$accepted = Invoke-RestMethod "$BaseUrl/api/connect/referrals/$($ref.id)/accept" -Method POST -Headers $hClinic
if ($accepted.referralStatus -ne "accepted") { throw "expected accepted" }
Write-Host "[OK] accept" -ForegroundColor Green

$completed = Invoke-RestMethod "$BaseUrl/api/connect/referrals/$($ref.id)/complete" -Method POST -Headers $hClinic
if ($completed.referralStatus -ne "completed") { throw "expected completed" }
Write-Host "[OK] complete" -ForegroundColor Green

Write-Host "`n=== Connect C3 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/connect/referrals"

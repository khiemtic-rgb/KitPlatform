# Smoke ClinicOS GĐ1 CL1.4 (day-summary + booking→appointment bridge)
# Usage: .\scripts\smoke-clinic-gd1-cl14-local.ps1
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$ClinicTenant = "DEMO_CLINIC",
    [string]$PharmacyTenant = "NT_XUANHOA",
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

Write-Host "`n=== Clinic GĐ1 CL1.4 smoke ($ClinicTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$hClinic = Login $ClinicTenant
$hPharm = Login $PharmacyTenant
Write-Host "[OK] Login" -ForegroundColor Green

$summary = Invoke-RestMethod "$BaseUrl/api/clinic/day-summary" -Headers $hClinic
Write-Host "[OK] day-summary appointments=$($summary.appointmentsToday) visitsOpen=$($summary.visitsOpen) rxSent=$($summary.prescriptionsSentToPharmacy)" -ForegroundColor Green

# Ensure org link
$rawLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hPharm
$active = @($rawLinks) | Where-Object { $_.partnerTenantCode -eq $ClinicTenant } | Select-Object -First 1
if (-not $active) {
    $created = Invoke-RestMethod "$BaseUrl/api/connect/org-links/invite" -Method POST -Headers $hPharm `
        -ContentType "application/json" -Body (@{
            partnerTenantCode = $ClinicTenant
            ourOrgRole = "pharmacy"
            partnerOrgRole = "clinic"
        } | ConvertTo-Json)
    $null = Invoke-RestMethod "$BaseUrl/api/connect/org-links/$($created.id)/accept" -Method POST -Headers $hClinic
}

$customers = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5&search=BN01" -Headers $hClinic
$phone = "0902000099"
if ($customers.items) {
    $hit = @($customers.items | Where-Object { $_.customerCode -eq "BN01" }) | Select-Object -First 1
    if ($hit -and $hit.phone) { $phone = [string]$hit.phone }
}

$when = (Get-Date).ToUniversalTime().AddHours(3).ToString("o")
$bookingBody = (@{
    scheduledAt = $when
    patientDisplayName = "BN Demo Clinic"
    patientPhone = $phone
    durationMinutes = 30
    notes = "CL14 bridge smoke"
} | ConvertTo-Json)
$booking = Invoke-RestMethod "$BaseUrl/api/connect/bookings" -Method POST -Headers $hClinic `
    -ContentType "application/json" -Body ([System.Text.Encoding]::UTF8.GetBytes($bookingBody))
Write-Host "[OK] booking proposed id=$($booking.id)" -ForegroundColor Green

$confirmed = Invoke-RestMethod "$BaseUrl/api/connect/bookings/$($booking.id)/confirm" -Method POST -Headers $hClinic
if ($confirmed.bookingStatus -ne "confirmed") { throw "expected confirmed" }
Write-Host "[OK] booking confirmed" -ForegroundColor Green

Start-Sleep -Milliseconds 500
$from = (Get-Date).ToUniversalTime().AddDays(-1).ToString("o")
$to = (Get-Date).ToUniversalTime().AddDays(2).ToString("o")
$rawAppts = Invoke-RestMethod "$BaseUrl/api/clinic/appointments?from=$from&to=$to" -Headers $hClinic
$apptList = [System.Collections.Generic.List[object]]::new()
foreach ($item in @($rawAppts)) { [void]$apptList.Add($item) }

$bridged = $null
foreach ($a in $apptList) {
    $notes = [string]($(if ($null -ne $a.notes) { $a.notes } else { $a.Notes }))
    if ($notes -eq "CL14 bridge smoke") {
        $bridged = $a
        # keep last match (newest smoke run) — do not break
    }
}
if ($null -eq $bridged) { throw "expected clinic appointment from booking confirm bridge (notes CL14)" }

# PowerShell may expand properties across arrays — always take first scalar
$apptId = [string](@($(if ($null -ne $bridged.id) { $bridged.id } else { $bridged.Id }))[0])
$apptStatus = [string](@($(if ($null -ne $bridged.appointmentStatus) { $bridged.appointmentStatus } else { $bridged.AppointmentStatus }))[0])
$customerId = [string](@($(if ($null -ne $bridged.customerId) { $bridged.customerId } else { $bridged.CustomerId }))[0])
if ([string]::IsNullOrWhiteSpace($customerId) -or $customerId.Contains(" ")) {
    throw "bad customerId from appointment: '$customerId' (apptId=$apptId)"
}
Write-Host "[OK] bridged appointment id=$apptId status=$apptStatus" -ForegroundColor Green

$visits = @(Invoke-RestMethod "$BaseUrl/api/clinic/visits?customerId=$customerId" -Headers $hClinic)
Write-Host "[OK] patient history visits=$(@($visits).Length) for customer=$customerId" -ForegroundColor Green

Write-Host "`nCL1.4 smoke PASSED" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/clinic/overview (day summary)"
Write-Host "Connect bookings confirm → Clinic appointments"

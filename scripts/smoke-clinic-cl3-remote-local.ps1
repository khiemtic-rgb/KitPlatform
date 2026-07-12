# Smoke Clinic CL3-A — remote consult (modality + encounter_session stub)
# Usage: .\scripts\smoke-clinic-cl3-remote-local.ps1
# Requires: API running, migration 120 applied
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$ClinicTenant = "DEMO_CLINIC",
    [string]$PharmacyTenant = "NT_XUANHOA",
    [string]$User = "admin",
    [string]$Pass = "Admin@123",
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$DbName = "kitplatform",
    [string]$DbUser = "kitplatform",
    [string]$DbPassword = "kitplatform_dev_2026"
)

$ErrorActionPreference = "Stop"

function Login([string]$tenant) {
    $auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
        -Body (@{ username = $User; password = $Pass; tenantCode = $tenant } | ConvertTo-Json)
    if (-not $auth.accessToken) { throw "login failed for $tenant" }
    return @{ Authorization = "Bearer $($auth.accessToken)" }
}

function Get-Psql {
    $candidates = @(
        'C:\Program Files\PostgreSQL\18\bin\psql.exe',
        'C:\Program Files\PostgreSQL\17\bin\psql.exe',
        'C:\Program Files\PostgreSQL\16\bin\psql.exe'
    )
    $psql = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $psql) {
        $cmd = Get-Command psql -ErrorAction SilentlyContinue
        if ($cmd) { $psql = $cmd.Source }
    }
    return $psql
}

Write-Host "`n=== Clinic CL3-A remote smoke ($ClinicTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$hClinic = Login $ClinicTenant
$hPharm = Login $PharmacyTenant
Write-Host "[OK] Login both tenants" -ForegroundColor Green

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
    $rawLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hPharm
    $active = @($rawLinks) | Where-Object { $_.partnerTenantCode -eq $ClinicTenant } | Select-Object -First 1
}
if (-not $active) { throw "no active org link" }
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
            fullName = "BN Smoke CL3 Remote"
            phone = "0912333444"
        } | ConvertTo-Json -Compress)))
    $pharmacyCustomerId = [string]$createdCust.id
}

$refBody = (@{
    clinicTenantId = $clinicTenantId
    pharmacyCustomerId = $pharmacyCustomerId
    reason = "CL3 remote smoke"
} | ConvertTo-Json -Compress)
$ref = Invoke-RestMethod "$BaseUrl/api/connect/referrals" -Method POST -Headers $hPharm `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($refBody))
$null = Invoke-RestMethod "$BaseUrl/api/connect/referrals/$($ref.id)/accept" -Method POST -Headers $hClinic
Write-Host "[OK] referral accepted id=$($ref.id)" -ForegroundColor Green

$when = (Get-Date).ToUniversalTime().AddHours(2).ToString("o")
$bookBody = (@{
    scheduledAt = $when
    patientDisplayName = "BN Smoke CL3 Remote"
    patientPhone = "0912333444"
    referralId = $ref.id
    durationMinutes = 30
    notes = "CL3-A remote_async smoke"
    encounterModality = "remote_async"
} | ConvertTo-Json -Compress)

$booking = Invoke-RestMethod "$BaseUrl/api/connect/bookings" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($bookBody))
$bookModality = if ($booking.encounterModality) { $booking.encounterModality } else { $booking.EncounterModality }
if ($bookModality -ne "remote_async") { throw "booking modality expected remote_async, got $bookModality" }
Write-Host "[OK] booking proposed id=$($booking.id) modality=$bookModality" -ForegroundColor Green

$confirmed = Invoke-RestMethod "$BaseUrl/api/connect/bookings/$($booking.id)/confirm" -Method POST -Headers $hClinic
if ($confirmed.bookingStatus -ne "confirmed" -and $confirmed.BookingStatus -ne "confirmed") {
    throw "expected confirmed"
}
Write-Host "[OK] booking confirmed" -ForegroundColor Green

Start-Sleep -Milliseconds 400
$from = (Get-Date).ToUniversalTime().AddDays(-1).ToString("o")
$to = (Get-Date).ToUniversalTime().AddDays(2).ToString("o")
$rawAppts = Invoke-RestMethod "$BaseUrl/api/clinic/appointments?from=$from&to=$to" -Headers $hClinic
$bridged = $null
foreach ($a in @($rawAppts)) {
    $notes = [string]($(if ($null -ne $a.notes) { $a.notes } else { $a.Notes }))
    if ($notes -eq "CL3-A remote_async smoke") { $bridged = $a }
}
if ($null -eq $bridged) { throw "expected clinic appointment from bridge (notes CL3-A remote_async smoke)" }

$apptId = [string](@($(if ($null -ne $bridged.id) { $bridged.id } else { $bridged.Id }))[0])
$apptModality = [string](@($(if ($null -ne $bridged.encounterModality) { $bridged.encounterModality } else { $bridged.EncounterModality }))[0])
if ($apptModality -ne "remote_async") { throw "appointment modality expected remote_async, got $apptModality" }
Write-Host "[OK] bridged appointment id=$apptId modality=$apptModality" -ForegroundColor Green

$visit = Invoke-RestMethod "$BaseUrl/api/clinic/appointments/$apptId/check-in" -Method POST -Headers $hClinic
$visitId = [string]($(if ($visit.id) { $visit.id } else { $visit.Id }))
$visitModality = [string]($(if ($visit.encounterModality) { $visit.encounterModality } else { $visit.EncounterModality }))
if ($visitModality -ne "remote_async") { throw "visit modality expected remote_async, got $visitModality" }
Write-Host "[OK] check-in (start remote) visit=$visitId modality=$visitModality" -ForegroundColor Green

$patchBody = (@{ diagnosisSummary = "CL3-A remote smoke diagnosis" } | ConvertTo-Json -Compress)
$patched = Invoke-RestMethod "$BaseUrl/api/clinic/visits/$visitId" -Method PATCH -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($patchBody))
$diag = if ($patched.diagnosisSummary) { $patched.diagnosisSummary } else { $patched.DiagnosisSummary }
if ($diag -notmatch "CL3-A") { throw "diagnosis patch failed" }
Write-Host "[OK] PATCH diagnosis" -ForegroundColor Green

$providersResp = Invoke-RestMethod "$BaseUrl/api/clinic/providers" -Headers $hClinic
$providerId = $null
$firstProvider = @($providersResp) | Select-Object -First 1
if ($null -ne $firstProvider -and $firstProvider.id) {
    $providerId = [string]$firstProvider.id
}

$rxBody = @{
    visitId = $visitId
    diagnosisText = "CL3-A"
    lines = @(
        @{ drugName = "Paracetamol"; strength = "500mg"; quantity = 10; unit = "vien"; dosageInstruction = "1x3" }
    )
}
if ($providerId) { $rxBody.providerId = $providerId }
$rxJson = ($rxBody | ConvertTo-Json -Compress -Depth 5)
$rx = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($rxJson))
$rxStatus = if ($rx.prescriptionStatus) { $rx.prescriptionStatus } else { $rx.PrescriptionStatus }
if ($rxStatus -ne "draft") { throw "expected draft Rx, got $rxStatus" }
Write-Host "[OK] draft Rx id=$($rx.id)" -ForegroundColor Green

$psql = Get-Psql
if (-not $psql) { throw "psql not found - cannot assert encounter_session stub" }
$env:PGPASSWORD = $DbPassword
$sessionSql = "SELECT session_status || '|' || COALESCE(media_provider, '') FROM pack_clinic.encounter_session WHERE visit_id = '$visitId'::uuid LIMIT 1;"
$sessionRow = & $psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -c $sessionSql
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
$sessionRow = [string]$sessionRow
if ([string]::IsNullOrWhiteSpace($sessionRow)) { throw "encounter_session row missing for visit $visitId" }
$parts = $sessionRow.Trim().Split('|')
$status = $parts[0]
$media = if ($parts.Length -gt 1) { $parts[1] } else { '' }
if ($status -ne "none") { throw "session_status expected none, got $status" }
if (-not [string]::IsNullOrWhiteSpace($media)) { throw "media_provider expected null/empty, got $media" }
Write-Host "[OK] encounter_session stub status=none media_provider=null" -ForegroundColor Green

$summary = Invoke-RestMethod "$BaseUrl/api/clinic/day-summary" -Headers $hClinic
$remoteToday = if ($null -ne $summary.appointmentsRemoteToday) {
    [int]$summary.appointmentsRemoteToday
} else {
    [int]$summary.AppointmentsRemoteToday
}
Write-Host "[OK] day-summary appointmentsRemoteToday=$remoteToday" -ForegroundColor Green

Write-Host "`n=== Clinic CL3-A remote smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/clinic/appointments (DEMO_CLINIC) - filter modality = remote"
Write-Host "Brief: docs/novixa/03-solution/novixa-clinic-cl3-remote-consult-a.md"

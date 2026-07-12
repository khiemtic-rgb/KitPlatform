# Smoke ClinicOS GĐ1 CL1.1 (appointments + check-in + visit)
# Usage: .\scripts\smoke-clinic-gd1-cl11-local.ps1
param(
    [string]$BaseUrl = "http://localhost:5290",
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

Write-Host "`n=== Clinic GĐ1 CL1.1 smoke ($ClinicTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$h = Login $ClinicTenant
Write-Host "[OK] Login" -ForegroundColor Green

# Ensure patient (seed BN01 via migration 113 - avoid customers POST party_identifier bug)
$customers = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5&search=BN01" -Headers $h
$customerId = $null
if ($customers.items) {
    $hit = @($customers.items | Where-Object { $_.customerCode -eq "BN01" })
    if ($hit.Length -gt 0) { $customerId = [string]$hit[0].id }
}
if (-not $customerId -and $customers.total -gt 0 -and $customers.items -and $customers.items.Count -gt 0) {
    $customerId = [string]$customers.items[0].id
}
if (-not $customerId) {
    throw "No customer for DEMO_CLINIC - apply migration 113_clinic_gd1_demo_customer.sql"
}
Write-Host "[OK] customerId=$customerId" -ForegroundColor Green


$providersResp = Invoke-RestMethod "$BaseUrl/api/clinic/providers" -Headers $h
$providerId = $null
$firstProvider = @($providersResp) | Select-Object -First 1
if ($null -ne $firstProvider -and $firstProvider.id) {
    $providerId = [string](@($firstProvider.id) | Select-Object -First 1)
}

$when = (Get-Date).ToUniversalTime().AddHours(2).ToString("o")
$apptBody = @{
    customerId = $customerId
    appointmentAt = $when
    durationMinutes = 20
    reason = "CL11 smoke"
}
if ($providerId) { $apptBody.providerId = $providerId }
$apptJson = ($apptBody | ConvertTo-Json -Compress)
$appt = Invoke-RestMethod "$BaseUrl/api/clinic/appointments" -Method POST -Headers $h `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($apptJson))
if ($appt.appointmentStatus -ne "scheduled") { throw "expected scheduled" }
Write-Host "[OK] create appointment id=$($appt.id)" -ForegroundColor Green

$visit = Invoke-RestMethod "$BaseUrl/api/clinic/appointments/$($appt.id)/check-in" -Method POST -Headers $h
if ($visit.visitStatus -ne "open") { throw "expected open visit" }
Write-Host "[OK] check-in visit id=$($visit.id)" -ForegroundColor Green

# Idempotent check-in
$visit2 = Invoke-RestMethod "$BaseUrl/api/clinic/appointments/$($appt.id)/check-in" -Method POST -Headers $h
if ($visit2.id -ne $visit.id) { throw "check-in should reuse open visit" }
Write-Host "[OK] check-in idempotent" -ForegroundColor Green

$noteBody = (@{ noteBody = "CL11 clinical note"; noteType = "clinical" } | ConvertTo-Json -Compress)
$null = Invoke-RestMethod "$BaseUrl/api/clinic/visits/$($visit.id)/notes" -Method POST -Headers $h `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($noteBody))
Write-Host "[OK] add note" -ForegroundColor Green

$patchBody = (@{
    chiefComplaint = "Dau dau"
    diagnosisSummary = "Theo doi"
} | ConvertTo-Json -Compress)
$updated = Invoke-RestMethod "$BaseUrl/api/clinic/visits/$($visit.id)" -Method PATCH -Headers $h `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($patchBody))
if (-not $updated.diagnosisSummary) { throw "patch visit failed" }
Write-Host "[OK] update visit" -ForegroundColor Green

$closed = Invoke-RestMethod "$BaseUrl/api/clinic/visits/$($visit.id)" -Method PATCH -Headers $h `
    -ContentType "application/json; charset=utf-8" `
    -Body ([System.Text.Encoding]::UTF8.GetBytes((@{ visitStatus = "closed" } | ConvertTo-Json -Compress)))
if ($closed.visitStatus -ne "closed") { throw "expected closed" }
Write-Host "[OK] close visit" -ForegroundColor Green

$null = Invoke-RestMethod "$BaseUrl/api/clinic/appointments/$($appt.id)/status" -Method POST -Headers $h `
    -ContentType "application/json; charset=utf-8" `
    -Body ([System.Text.Encoding]::UTF8.GetBytes((@{ appointmentStatus = "completed" } | ConvertTo-Json -Compress)))
Write-Host "[OK] complete appointment" -ForegroundColor Green

# Walk-in visit
$walkBody = (@{
    customerId = $customerId
    providerId = $providerId
    chiefComplaint = "Walk-in smoke"
} | ConvertTo-Json -Compress)
$walk = Invoke-RestMethod "$BaseUrl/api/clinic/visits" -Method POST -Headers $h `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($walkBody))
if ($walk.visitStatus -ne "open") { throw "walk-in expected open" }
Write-Host "[OK] walk-in visit id=$($walk.id)" -ForegroundColor Green

Write-Host "`n=== Clinic GĐ1 CL1.1 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/clinic/appointments (DEMO_CLINIC)"

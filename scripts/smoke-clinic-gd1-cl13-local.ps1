# Smoke ClinicOS GĐ1 CL1.3 (send Rx → Pharmacy via Connect)
# Usage: .\scripts\smoke-clinic-gd1-cl13-local.ps1
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

Write-Host "`n=== Clinic GĐ1 CL1.3 smoke ($ClinicTenant -> $PharmacyTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$hPharm = Login $PharmacyTenant
$hClinic = Login $ClinicTenant
Write-Host "[OK] Login both tenants" -ForegroundColor Green

# Ensure active org link (same as C5)
$rawLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hPharm
$active = @($rawLinks) | Where-Object {
    ($_.partnerTenantCode -eq $ClinicTenant) -or ($_.partnerTenantCode -eq $PharmacyTenant)
} | Select-Object -First 1
if (-not $active) {
    Write-Host "[..] creating org link $PharmacyTenant <-> $ClinicTenant" -ForegroundColor Yellow
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

$clinicLinks = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $hClinic
$pharmacyPartner = @($clinicLinks) | Where-Object { $_.partnerOrgRole -eq "pharmacy" } | Select-Object -First 1
if (-not $pharmacyPartner) { throw "clinic has no active pharmacy partner" }
$pharmacyTenantId = [string]$pharmacyPartner.partnerTenantId
Write-Host "[OK] pharmacyTenantId=$pharmacyTenantId" -ForegroundColor Green

$customers = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5&search=BN01" -Headers $hClinic
$customerId = $null
if ($customers.items) {
    $hit = @($customers.items | Where-Object { $_.customerCode -eq "BN01" })
    if ($hit.Length -gt 0) { $customerId = [string]$hit[0].id }
}
if (-not $customerId) { throw "BN01 missing - apply migration 113" }

$providersResp = Invoke-RestMethod "$BaseUrl/api/clinic/providers" -Headers $hClinic
$providerId = $null
$firstProvider = @($providersResp) | Select-Object -First 1
if ($null -ne $firstProvider -and $firstProvider.id) {
    $providerId = [string]$firstProvider.id
}

$walkBody = @{ customerId = $customerId; chiefComplaint = "CL13 smoke" }
if ($providerId) { $walkBody.providerId = $providerId }
$visit = Invoke-RestMethod "$BaseUrl/api/clinic/visits" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" `
    -Body ([System.Text.Encoding]::UTF8.GetBytes(($walkBody | ConvertTo-Json -Compress)))
Write-Host "[OK] visit id=$($visit.id)" -ForegroundColor Green

$rxBody = (@{
    visitId = $visit.id
    providerId = $providerId
    diagnosisText = "CL13"
    lines = @(
        @{ drugName = "Paracetamol"; strength = "500mg"; quantity = 10; unit = "vien"; dosageInstruction = "1x3" }
    )
} | ConvertTo-Json -Compress -Depth 5)
$rx = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($rxBody))
$final = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions/$($rx.id)/finalize" -Method POST -Headers $hClinic
if ($final.prescriptionStatus -ne "finalized") { throw "expected finalized" }
Write-Host "[OK] finalized $($final.prescriptionCode)" -ForegroundColor Green

$sent = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions/$($rx.id)/send-to-pharmacy" -Method POST -Headers $hClinic `
    -ContentType "application/json" -Body (@{ pharmacyTenantId = $pharmacyTenantId } | ConvertTo-Json)
$handoffId = [string]$sent.connectHandoffId
if (-not $handoffId) { $handoffId = [string]$sent.ConnectHandoffId }
if (-not $handoffId) { throw "expected connectHandoffId on send response" }
Write-Host "[OK] sent handoff=$handoffId" -ForegroundColor Green

$handoff = Invoke-RestMethod "$BaseUrl/api/connect/rx-handoffs/$handoffId" -Headers $hPharm
$code = if ($handoff.prescriptionCode) { $handoff.prescriptionCode } else { $handoff.PrescriptionCode }
if ($code -ne $final.prescriptionCode -and $code -ne $final.PrescriptionCode) { throw "handoff code mismatch: $code" }
$lines = @($handoff.lines)
if ($lines.Length -eq 0) { $lines = @($handoff.Lines) }
if ($lines.Length -lt 1) { throw "handoff lines empty" }
Write-Host "[OK] handoff lines=$($lines.Length) code=$code" -ForegroundColor Green

$eventId = [string]$handoff.statusEventId
if (-not $eventId) { $eventId = [string]$handoff.StatusEventId }
if (-not $eventId) {
    $pending = @(Invoke-RestMethod "$BaseUrl/api/connect/status-events/pending" -Headers $hPharm)
    $evt = $pending | Where-Object {
        $st = if ($null -ne $_.sourceType) { [string]$_.sourceType } else { [string]$_.SourceType }
        $sid = if ($null -ne $_.sourceId) { [string]$_.sourceId } else { [string]$_.SourceId }
        $st -eq "clinic_rx" -and $sid -eq $handoffId
    } | Select-Object -First 1
    if ($evt) {
        $eventId = if ($evt.id) { [string]$evt.id } else { [string]$evt.Id }
    }
}
if (-not $eventId) { throw "pharmacy did not see status event for handoff $handoffId" }
Write-Host "[OK] status event id=$eventId" -ForegroundColor Green

$consumed = Invoke-RestMethod "$BaseUrl/api/connect/status-events/$eventId/consume" -Method POST -Headers $hPharm
$cStatus = if ($consumed.eventStatus) { $consumed.eventStatus } else { $consumed.EventStatus }
if ($cStatus -ne "consumed") { throw "expected consumed, got $cStatus" }
$handoff2 = Invoke-RestMethod "$BaseUrl/api/connect/rx-handoffs/$handoffId" -Headers $hPharm
$hStatus = if ($handoff2.handoffStatus) { $handoff2.handoffStatus } else { $handoff2.HandoffStatus }
if ($hStatus -ne "consumed") { throw "expected handoff consumed, got $hStatus" }
Write-Host "[OK] pharmacy consumed event + handoff" -ForegroundColor Green

Write-Host "`nCL1.3 smoke PASSED" -ForegroundColor Green
Write-Host "Admin clinic: http://localhost:5173/clinic/visits (DEMO_CLINIC) — Gửi NT"
Write-Host "Admin pharmacy: http://localhost:5173/connect/status ($PharmacyTenant) — Xem đơn / Consume"

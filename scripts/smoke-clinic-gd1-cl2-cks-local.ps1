# Smoke ClinicOS CL2.0 Soft-CKS (finalize → sign mock → send-to-pharmacy)
# Usage: .\scripts\smoke-clinic-gd1-cl2-cks-local.ps1
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

Write-Host "`n=== Clinic CL2 Soft-CKS smoke ($ClinicTenant -> $PharmacyTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$hPharm = Login $PharmacyTenant
$hClinic = Login $ClinicTenant
Write-Host "[OK] Login both tenants" -ForegroundColor Green

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

$walkBody = @{ customerId = $customerId; chiefComplaint = "CL2 Soft-CKS smoke" }
if ($providerId) { $walkBody.providerId = $providerId }
$visit = Invoke-RestMethod "$BaseUrl/api/clinic/visits" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" `
    -Body ([System.Text.Encoding]::UTF8.GetBytes(($walkBody | ConvertTo-Json -Compress)))
Write-Host "[OK] visit id=$($visit.id)" -ForegroundColor Green

$rxBody = (@{
    visitId = $visit.id
    providerId = $providerId
    diagnosisText = "CL2 Soft-CKS"
    lines = @(
        @{ drugName = "Paracetamol"; strength = "500mg"; quantity = 10; unit = "vien"; dosageInstruction = "1x3" }
    )
} | ConvertTo-Json -Compress -Depth 5)
$rx = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions" -Method POST -Headers $hClinic `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($rxBody))
$final = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions/$($rx.id)/finalize" -Method POST -Headers $hClinic
$statusFinal = if ($final.prescriptionStatus) { $final.prescriptionStatus } else { $final.PrescriptionStatus }
if ($statusFinal -ne "finalized") { throw "expected finalized, got $statusFinal" }
Write-Host "[OK] finalized $($final.prescriptionCode)" -ForegroundColor Green

$signed = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions/$($rx.id)/sign" -Method POST -Headers $hClinic
$statusSigned = if ($signed.prescriptionStatus) { $signed.prescriptionStatus } else { $signed.PrescriptionStatus }
$provider = if ($signed.signatureProvider) { $signed.signatureProvider } else { $signed.SignatureProvider }
$signedAt = if ($signed.signedAt) { $signed.signedAt } else { $signed.SignedAt }
if ($statusSigned -ne "signed") { throw "expected signed, got $statusSigned" }
if ($provider -ne "mock") { throw "expected signatureProvider=mock, got $provider" }
if (-not $signedAt) { throw "expected signedAt" }
Write-Host "[OK] signed provider=$provider at=$signedAt" -ForegroundColor Green

$sent = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions/$($rx.id)/send-to-pharmacy" -Method POST -Headers $hClinic `
    -ContentType "application/json" -Body (@{ pharmacyTenantId = $pharmacyTenantId } | ConvertTo-Json)
$handoffId = [string]$sent.connectHandoffId
if (-not $handoffId) { $handoffId = [string]$sent.ConnectHandoffId }
if (-not $handoffId) { throw "expected connectHandoffId on send after signed" }
Write-Host "[OK] send-to-pharmacy after signed handoff=$handoffId" -ForegroundColor Green

$handoff = Invoke-RestMethod "$BaseUrl/api/connect/rx-handoffs/$handoffId" -Headers $hPharm
$code = if ($handoff.prescriptionCode) { $handoff.prescriptionCode } else { $handoff.PrescriptionCode }
$rxCode = if ($signed.prescriptionCode) { $signed.prescriptionCode } else { $signed.PrescriptionCode }
if ($code -ne $rxCode) { throw "handoff code mismatch: $code vs $rxCode" }
Write-Host "[OK] pharmacy sees handoff code=$code" -ForegroundColor Green

Write-Host "`nCL2 Soft-CKS smoke PASSED" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/clinic/visits (DEMO_CLINIC) - Sign (trial) / Signed (mock)"

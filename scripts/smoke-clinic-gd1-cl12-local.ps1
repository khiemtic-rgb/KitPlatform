# Smoke ClinicOS GĐ1 CL1.2 (internal Rx + PDF)
# Usage: .\scripts\smoke-clinic-gd1-cl12-local.ps1
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

Write-Host "`n=== Clinic GĐ1 CL1.2 smoke ($ClinicTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$h = Login $ClinicTenant
Write-Host "[OK] Login" -ForegroundColor Green

$customers = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5&search=BN01" -Headers $h
$customerId = $null
if ($customers.items) {
    $hit = @($customers.items | Where-Object { $_.customerCode -eq "BN01" })
    if ($hit.Length -gt 0) { $customerId = [string]$hit[0].id }
}
if (-not $customerId) { throw "BN01 missing - apply migration 113" }
Write-Host "[OK] customerId=$customerId" -ForegroundColor Green

$providersResp = Invoke-RestMethod "$BaseUrl/api/clinic/providers" -Headers $h
$providerId = $null
$firstProvider = @($providersResp) | Select-Object -First 1
if ($null -ne $firstProvider -and $firstProvider.id) {
    $providerId = [string](@($firstProvider.id) | Select-Object -First 1)
}

$walkBody = @{
    customerId = $customerId
    chiefComplaint = "CL12 smoke"
}
if ($providerId) { $walkBody.providerId = $providerId }
$visit = Invoke-RestMethod "$BaseUrl/api/clinic/visits" -Method POST -Headers $h `
    -ContentType "application/json; charset=utf-8" `
    -Body ([System.Text.Encoding]::UTF8.GetBytes(($walkBody | ConvertTo-Json -Compress)))
Write-Host "[OK] visit id=$($visit.id)" -ForegroundColor Green

$rxBody = (@{
    visitId = $visit.id
    providerId = $providerId
    diagnosisText = "Cam cum"
    notes = "CL12"
    lines = @(
        @{ drugName = "Paracetamol"; strength = "500mg"; quantity = 20; unit = "vien"; dosageInstruction = "1 vien x 3 lan/ngay" },
        @{ drugName = "Vitamin C"; strength = "1000mg"; quantity = 10; unit = "vien"; dosageInstruction = "1 vien/ngay" }
    )
} | ConvertTo-Json -Compress -Depth 5)
$rx = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions" -Method POST -Headers $h `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($rxBody))
if ($rx.prescriptionStatus -ne "draft") { throw "expected draft" }
if (@($rx.lines).Length -lt 2) { throw "expected 2 lines" }
Write-Host "[OK] create draft $($rx.prescriptionCode) lines=$(@($rx.lines).Length)" -ForegroundColor Green

$final = Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions/$($rx.id)/finalize" -Method POST -Headers $h
if ($final.prescriptionStatus -ne "finalized") { throw "expected finalized" }
if (-not $final.pdfSha256) { throw "expected pdfSha256" }
Write-Host "[OK] finalize sha=$($final.pdfSha256.Substring(0,12))..." -ForegroundColor Green

$tmp = Join-Path $env:TEMP "clinic-rx-$($rx.prescriptionCode).pdf"
Invoke-WebRequest -Uri "$BaseUrl/api/clinic/prescriptions/$($rx.id)/pdf" -Headers $h -OutFile $tmp
$info = Get-Item $tmp
if ($info.Length -lt 200) { throw "pdf too small" }
Write-Host "[OK] pdf bytes=$($info.Length) path=$tmp" -ForegroundColor Green

$list = @(Invoke-RestMethod "$BaseUrl/api/clinic/prescriptions?visitId=$($visit.id)" -Headers $h)
if ($list.Length -lt 1) { throw "list empty" }
Write-Host "[OK] list by visit count=$($list.Length)" -ForegroundColor Green

Write-Host "`n=== Clinic GĐ1 CL1.2 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/clinic/visits (DEMO_CLINIC) - open visit - Prescribe"

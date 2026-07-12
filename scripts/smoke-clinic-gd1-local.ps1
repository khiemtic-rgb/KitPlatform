# Smoke ClinicOS GĐ1 CL1.0 (shell + providers + patients API)
# Usage: .\scripts\smoke-clinic-gd1-local.ps1
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

Write-Host "`n=== Clinic GĐ1 CL1.0 smoke ($ClinicTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$h = Login $ClinicTenant
Write-Host "[OK] Login $ClinicTenant" -ForegroundColor Green

$providers = @(Invoke-RestMethod "$BaseUrl/api/clinic/providers?includeInactive=true" -Headers $h)
if ($providers.Length -lt 1) { throw "expected seeded provider BS01" }
$seed = $providers | Where-Object { $_.providerCode -eq "BS01" } | Select-Object -First 1
if (-not $seed) { throw "BS01 missing" }
Write-Host "[OK] providers list count=$($providers.Length) seed=$($seed.displayName)" -ForegroundColor Green

$code = "BS" + (Get-Random -Maximum 9999)
$body = (@{
    providerCode = $code
    displayName = "BS Smoke $code"
    specialty = "Nội tổng quát"
    licenseNo = "CCHN-SMOKE-$code"
    status = 1
} | ConvertTo-Json -Compress)
$created = Invoke-RestMethod "$BaseUrl/api/clinic/providers" -Method POST -Headers $h `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
if ($created.providerCode -ne $code) { throw "create mismatch" }
Write-Host "[OK] create provider id=$($created.id)" -ForegroundColor Green

$patched = Invoke-RestMethod "$BaseUrl/api/clinic/providers/$($created.id)" -Method PATCH -Headers $h `
    -ContentType "application/json; charset=utf-8" `
    -Body ([System.Text.Encoding]::UTF8.GetBytes((@{ displayName = "BS Smoke Updated"; status = 1 } | ConvertTo-Json -Compress)))
if ($patched.displayName -ne "BS Smoke Updated") { throw "patch failed" }
Write-Host "[OK] patch provider" -ForegroundColor Green

$customers = Invoke-RestMethod "$BaseUrl/api/customers?page=1&pageSize=5" -Headers $h
Write-Host "[OK] customers API reachable total=$($customers.total)" -ForegroundColor Green

Write-Host "`n=== Clinic GĐ1 CL1.0 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin: http://localhost:5173/clinic/overview (DEMO_CLINIC)"

# Smoke ClinicOS chain CL1.0→CL1.4 + CL2 Soft-CKS + CL3-A
# Usage:
#   .\scripts\smoke-clinic-chain-local.ps1
#   .\scripts\smoke-clinic-chain-local.ps1 -SkipCl3A
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$PharmacyTenant = "NT_XUANHOA",
    [string]$ClinicTenant = "DEMO_CLINIC",
    [string]$User = "admin",
    [string]$Pass = "Admin@123",
    [switch]$SkipCl3A
)

$ErrorActionPreference = "Stop"
$scriptsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$common = @{
    BaseUrl        = $BaseUrl
    PharmacyTenant = $PharmacyTenant
    ClinicTenant   = $ClinicTenant
    User           = $User
    Pass           = $Pass
}

$steps = @(
    @{ Name = "CL1.0 shell"; Script = "smoke-clinic-gd1-local.ps1" },
    @{ Name = "CL1.1 schedule+visit"; Script = "smoke-clinic-gd1-cl11-local.ps1" },
    @{ Name = "CL1.2 Rx+PDF"; Script = "smoke-clinic-gd1-cl12-local.ps1" },
    @{ Name = "CL1.3 handoff NT"; Script = "smoke-clinic-gd1-cl13-local.ps1" },
    @{ Name = "CL1.4 day+bridge"; Script = "smoke-clinic-gd1-cl14-local.ps1" },
    @{ Name = "CL2 Soft-CKS"; Script = "smoke-clinic-gd1-cl2-cks-local.ps1" }
)

if (-not $SkipCl3A) {
    $steps += @{ Name = "CL3-A remote"; Script = "smoke-clinic-cl3-remote-local.ps1" }
}

Write-Host "`n=== Clinic smoke chain ($ClinicTenant @ $BaseUrl) ===" -ForegroundColor Cyan

foreach ($step in $steps) {
    $path = Join-Path $scriptsDir $step.Script
    if (-not (Test-Path $path)) {
        throw "Missing script: $path"
    }
    Write-Host "`n--- $($step.Name) ($($step.Script)) ---" -ForegroundColor Yellow
    & $path @common
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Smoke failed: $($step.Name) (exit $LASTEXITCODE)"
    }
}

# S12 settings smoke (thin)
Write-Host "`n--- S12 clinic settings ---" -ForegroundColor Yellow
$auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ username = $User; password = $Pass; tenantCode = $ClinicTenant } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($auth.accessToken)" }
$put = Invoke-RestMethod "$BaseUrl/api/clinic/settings" -Method PUT -Headers $h -ContentType "application/json" `
    -Body (@{
        name = "PK Demo Smoke"
        address = "1 Duong Smoke"
        phone = "0241111222"
        workingHours = "T2-T7 08:00-17:00"
    } | ConvertTo-Json)
if ($put.name -ne "PK Demo Smoke") { throw "settings put failed" }
$get = Invoke-RestMethod "$BaseUrl/api/clinic/settings" -Headers $h
if ($get.workingHours -notmatch "08:00") { throw "settings get mismatch" }
Write-Host "[OK] clinic settings name=$($get.name)" -ForegroundColor Green

Write-Host "`n=== Clinic smoke chain OK ===" -ForegroundColor Green

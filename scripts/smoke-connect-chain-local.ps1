# Smoke Novixa Connect chain C1→C5 (+ optional CL3-A)
# Usage:
#   .\scripts\smoke-connect-chain-local.ps1
#   .\scripts\smoke-connect-chain-local.ps1 -IncludeCl3A
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$PharmacyTenant = "NT_XUANHOA",
    [string]$ClinicTenant = "DEMO_CLINIC",
    [string]$User = "admin",
    [string]$Pass = "Admin@123",
    [switch]$IncludeCl3A
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
    @{ Name = "C1 org links"; Script = "smoke-connect-c1-local.ps1" },
    @{ Name = "C2 doctor membership"; Script = "smoke-connect-c2-local.ps1" },
    @{ Name = "C3 referral"; Script = "smoke-connect-c3-local.ps1" },
    @{ Name = "C4 booking"; Script = "smoke-connect-c4-local.ps1" },
    @{ Name = "C5 status sync"; Script = "smoke-connect-c5-local.ps1" }
)

if ($IncludeCl3A) {
    $steps += @{ Name = "CL3-A remote"; Script = "smoke-clinic-cl3-remote-local.ps1" }
}

Write-Host "`n=== Connect smoke chain ($ClinicTenant ↔ $PharmacyTenant @ $BaseUrl) ===" -ForegroundColor Cyan

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

Write-Host "`n=== Connect smoke chain OK ===" -ForegroundColor Green

# Lab UAT gate - EP03 AC1 + AC5 + AC3 (plus AC2/AC4 regression)
# Pack: docs/novixa/07-customer/success-ep03-ac135-lab-uat-pack-v1.md
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$Tenant = "NT_XUANHOA",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host ""
Write-Host "=== EP03 Lab UAT gate AC1/AC5/AC3 (+ AC2/AC4) @ $BaseUrl ===" -ForegroundColor Cyan

try {
    $health = Invoke-WebRequest "$BaseUrl/api/health" -UseBasicParsing -TimeoutSec 5
    if ($health.StatusCode -ne 200) { throw "health status $($health.StatusCode)" }
} catch {
    throw "API not reachable at $BaseUrl - start KitPlatform.Api first. $_"
}

$common = @{
    BaseUrl = $BaseUrl
    Tenant  = $Tenant
    User    = $User
    Pass    = $Pass
}

$scripts = @(
    @{ Name = "AC2 cash-variance"; File = "smoke-success-loss-cash-variance-local.ps1" },
    @{ Name = "AC4 by-employee";   File = "smoke-success-loss-employee-reports-local.ps1" },
    @{ Name = "AC1 audit-feed";    File = "smoke-success-loss-audit-feed-local.ps1" },
    @{ Name = "AC5 gates";         File = "smoke-success-loss-gates-local.ps1" },
    @{ Name = "AC3 cycle-count";   File = "smoke-success-loss-cycle-count-local.ps1" }
)

$failed = New-Object System.Collections.Generic.List[string]
foreach ($s in $scripts) {
    $path = Join-Path $Root "scripts\$($s.File)"
    if (-not (Test-Path $path)) { throw "Missing $path" }
    Write-Host ""
    Write-Host "--- $($s.Name) ---" -ForegroundColor Yellow
    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $path @common
        if ($LASTEXITCODE -ne 0) { throw "exit $LASTEXITCODE" }
        Write-Host "[PASS] $($s.Name)" -ForegroundColor Green
    } catch {
        Write-Host "[FAIL] $($s.Name): $_" -ForegroundColor Red
        $failed.Add($s.Name) | Out-Null
    }
}

Write-Host ""
Write-Host "--- Extra AC5 probes ---" -ForegroundColor Yellow
$auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ username = $User; password = $Pass; tenantCode = $Tenant } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($auth.accessToken)" }

$feed = Invoke-RestMethod "$BaseUrl/api/success/loss/audit-feed?pageSize=5" -Headers $h
if ($null -eq $feed.attributionNotes) { $failed.Add("audit-feed notes") | Out-Null }

$cycle = Invoke-RestMethod "$BaseUrl/api/success/loss/cycle-count/status" -Headers $h
$allowed = @("not_done", "in_progress", "done", "has_variance")
if ($allowed -notcontains $cycle.status) { $failed.Add("cycle status $($cycle.status)") | Out-Null }

Write-Host "[OK] audit total=$($feed.total) cycle.status=$($cycle.status)" -ForegroundColor Green

Write-Host ""
Write-Host "=== Lab UAT gate summary ===" -ForegroundColor Cyan
if ($failed.Count -gt 0) {
    Write-Host "FAIL: $($failed -join ', ')" -ForegroundColor Red
    Write-Host "Hold deploy. Fix then re-run."
    Write-Host "Pack: docs/novixa/07-customer/success-ep03-ac135-lab-uat-pack-v1.md"
    exit 1
}

Write-Host "PASS machine gate - continue human checklist U1-U10 in pack." -ForegroundColor Green
Write-Host "Admin lab: http://localhost:5173/success/loss ($Tenant)"
Write-Host "Pack: docs/novixa/07-customer/success-ep03-ac135-lab-uat-pack-v1.md"
exit 0

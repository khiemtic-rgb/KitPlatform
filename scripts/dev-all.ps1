# Mot lenh dev: PostgreSQL + API (nen + watchdog) + Customer App + Admin.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "=== PharmaCore dev (all-in-one) ===" -ForegroundColor Cyan
Write-Host "API:      http://localhost:5290"
Write-Host "Admin:    http://localhost:5173"
Write-Host "Customer: http://localhost:5174"
Write-Host ""

& (Join-Path $PSScriptRoot "check-postgres.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

. (Join-Path $PSScriptRoot "api-dev.ps1")
$code = Ensure-ApiDev
if ($code -ne 0) { exit $code }

$customerDir = Join-Path $Root "client\customer-app"
$adminDir = Join-Path $Root "client\admin"

foreach ($dir in @($customerDir, $adminDir)) {
    if (-not (Test-Path (Join-Path $dir "node_modules"))) {
        Write-Host "[npm install] $dir" -ForegroundColor Yellow
        Push-Location $dir
        npm install | Out-Host
        if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
        Pop-Location
    }
}

& (Join-Path $PSScriptRoot "stop-dev-frontends.ps1")

foreach ($port in @(5173, 5174)) {
    $listeners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($listeners) {
        Write-Host "[CANH BAO] Port $port dang duoc dung - dong cua so Vite cu hoac chay scripts\stop-dev-frontends.ps1" -ForegroundColor Yellow
    }
}

Write-Host "[OK] Mo Customer App (5174)..." -ForegroundColor Green
Start-Process -FilePath "cmd.exe" -ArgumentList "/k", "npm run dev:vite" -WorkingDirectory $customerDir

Start-Sleep -Seconds 2

Write-Host "[OK] Admin (5173) - Ctrl+C de dung ca session" -ForegroundColor Green
Push-Location $adminDir
try {
    npm run dev:vite
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

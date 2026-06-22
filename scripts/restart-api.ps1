$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$ApiProject = Join-Path $Root "src\PharmaCore.Api\PharmaCore.Api.csproj"

Write-Host "=== Restart PharmaCore API ===" -ForegroundColor Cyan

Write-Host ">> Dung PharmaCore.Api cu..." -ForegroundColor Yellow
Get-Process -Name "PharmaCore.Api" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

Write-Host ">> Kiem tra PostgreSQL..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "check-postgres.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ">> Build API..." -ForegroundColor Yellow
dotnet build $ApiProject --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "[LOI] Build that bai. Dong cua so API cu roi chay lai." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ">> Khoi dong API (http://localhost:5290)..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList @(
    "run", "--project", $ApiProject, "--no-build", "--urls", "http://localhost:5290"
) -WorkingDirectory $Root -WindowStyle Normal

Start-Sleep -Seconds 4
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5290/api/health" -TimeoutSec 10
    Write-Host "[OK] API: $($health.status)" -ForegroundColor Green
}
catch {
    Write-Host "[LOI] API chua phan hoi. Xem cua so API de biet loi." -ForegroundColor Red
    exit 1
}

Write-Host "Swagger: http://localhost:5290/swagger" -ForegroundColor DarkGray
Write-Host "Admin:   http://localhost:5173 (chay npm run dev trong client\admin neu chua co)" -ForegroundColor DarkGray

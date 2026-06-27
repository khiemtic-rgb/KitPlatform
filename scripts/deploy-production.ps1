<#
.SYNOPSIS
  Build artifacts cho triển khai Production (API + admin SPA + customer SPA).

.PARAMETER ApiBaseUrl
  URL gốc API, ví dụ https://api.yourpharmacy.vn (không có /api).

.PARAMETER OutputRoot
  Thư mục output, mặc định .\publish

.PARAMETER UseExistingNodeModules
  Bỏ qua npm ci — dùng node_modules hiện có (dry-run trên máy dev khi run-dev.bat đang chạy).

.EXAMPLE
  .\scripts\deploy-production.ps1 -ApiBaseUrl "https://api.demo.vn"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl,

    [string]$OutputRoot = "publish",

    [switch]$UseExistingNodeModules
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$apiBase = $ApiBaseUrl.TrimEnd("/")
Write-Host "=== PharmaCore production build ===" -ForegroundColor Cyan
Write-Host "API base: $apiBase"

$out = Join-Path $root $OutputRoot
$apiOut = Join-Path $out "api"
$adminOut = Join-Path $out "admin"
$customerOut = Join-Path $out "customer-app"

if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}
New-Item -ItemType Directory -Force -Path $apiOut, $adminOut, $customerOut | Out-Null

Write-Host "`n[1/4] dotnet publish API (Release)..." -ForegroundColor Yellow
dotnet publish "src\PharmaCore.Api\PharmaCore.Api.csproj" `
    -c Release `
    -o $apiOut `
    --no-self-contained
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Invoke-NpmBuild([string]$ClientDir, [string]$DestDir) {
    Push-Location $ClientDir
    $env:VITE_API_BASE_URL = $apiBase
    if (-not $UseExistingNodeModules) {
        npm ci
        if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
    Copy-Item -Recurse -Force "dist\*" $DestDir
    Remove-Item Env:VITE_API_BASE_URL -ErrorAction SilentlyContinue
    Pop-Location
}

Write-Host "`n[2/4] npm build admin..." -ForegroundColor Yellow
Invoke-NpmBuild "client\admin" $adminOut

Write-Host "`n[3/4] npm build customer-app..." -ForegroundColor Yellow
Invoke-NpmBuild "client\customer-app" $customerOut

Write-Host "`n[4/4] Ghi deploy notes..." -ForegroundColor Yellow
$notes = @"
PharmaCore production artifacts
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

Thư mục:
  api/           - ASP.NET Core (ASPNETCORE_ENVIRONMENT=Production)
  admin/         - Static SPA quản trị
  customer-app/  - Static SPA khách hàng

Biến môi trường API (bắt buộc):
  ConnectionStrings__Default
  Jwt__Secret                    (>= 32 ký tự, không dùng dev-secret)
  CustomerAppSms__HttpUrl        (gateway SMS)
  CustomerAppSms__ApiKey         (tuỳ chọn)
  Cors__AllowedOrigins__0        https://admin.domain
  Cors__AllowedOrigins__1        https://app.domain
  CustomerAppPush__PublicKey     (nếu bật push)
  CustomerAppPush__PrivateKey

Migration DB (Production — khong seed demo):
  .\scripts\run-migrations-prod.ps1 -ConnectionString "<prod connection>"

Tao nha thuoc (form — khuyen nghi Novixa):
  https://admin.novixa.vn/setup
  Hoac script: .\scripts\bootstrap-first-tenant.ps1 ...

Huong dan Novixa: docs\novixa-deploy.md

Chạy API:
  cd api
  `$env:ASPNETCORE_ENVIRONMENT='Production'
  dotnet PharmaCore.Api.dll

Frontend build dùng VITE_API_BASE_URL=$apiBase
"@
Set-Content -Path (Join-Path $out "DEPLOY.txt") -Value $notes -Encoding UTF8

Write-Host "`nDone → $out" -ForegroundColor Green
Write-Host "Đọc DEPLOY.txt trước khi triển khai." -ForegroundColor Green

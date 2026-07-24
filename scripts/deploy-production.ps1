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

$apiBase = $ApiBaseUrl.Trim().TrimEnd("/")
Write-Host "=== KitPlatform production build ===" -ForegroundColor Cyan
Write-Host "API base: [$apiBase]"
if ($apiBase -notmatch '^https?://') {
    throw "ApiBaseUrl must be absolute http(s) URL, got: [$ApiBaseUrl]"
}
if ($apiBase -match '\s') {
    throw "ApiBaseUrl must not contain whitespace: [$apiBase]"
}

$out = Join-Path $root $OutputRoot
$apiOut = Join-Path $out "api"
$adminOut = Join-Path $out "admin"
$customerOut = Join-Path $out "customer-app"
$staffOut = Join-Path $out "staff-app"
$prescriberOut = Join-Path $out "prescriber-portal"
$partnerOut = Join-Path $out "partner-portal"
$assessmentOut = Join-Path $out "assessment-web"
$familyOut = Join-Path $out "family-app"

if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}
New-Item -ItemType Directory -Force -Path $apiOut, $adminOut, $customerOut, $staffOut, $prescriberOut, $partnerOut, $assessmentOut, $familyOut | Out-Null

Write-Host "`n[1/9] dotnet publish API (Release)..." -ForegroundColor Yellow
dotnet publish "src\KitPlatform.Api\KitPlatform.Api.csproj" `
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

Write-Host "`n[2/7] npm build admin..." -ForegroundColor Yellow
Invoke-NpmBuild "client\admin" $adminOut

Write-Host "`n[3/7] npm build customer-app..." -ForegroundColor Yellow
Invoke-NpmBuild "client\customer-app" $customerOut

Write-Host "`n[4/7] npm build staff-app (POS mobile)..." -ForegroundColor Yellow
Invoke-NpmBuild "client\staff-app" $staffOut

Write-Host "`n[5/8] npm build prescriber-portal..." -ForegroundColor Yellow
Invoke-NpmBuild "client\prescriber-portal" $prescriberOut

Write-Host "`n[6/8] npm build partner-portal..." -ForegroundColor Yellow
Invoke-NpmBuild "client\partner-portal" $partnerOut

Write-Host "`n[7/9] npm build assessment-web (KAP public)..." -ForegroundColor Yellow
Invoke-NpmBuild "client\assessment-web" $assessmentOut

Write-Host "`n[8/9] npm build family-app (FamilyOS)..." -ForegroundColor Yellow
Invoke-NpmBuild "client\family-app" $familyOut

Write-Host "`n[9/9] Ghi deploy notes..." -ForegroundColor Yellow
$notes = @"
KitPlatform production artifacts
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

Thư mục:
  api/           - ASP.NET Core (ASPNETCORE_ENVIRONMENT=Production)
  admin/         - Static SPA quản trị
  customer-app/  - Static SPA khách hàng
  staff-app/     - Static SPA quầy bán (POS mobile)
  prescriber-portal/ - Portal bác sĩ kê đơn (Rx-2)
  partner-portal/ - KAP Partner / CTV portal
  assessment-web/ - KAP public survey (survey.domain)
  family-app/    - FamilyOS consumer (family.kittech.vn)

Biến môi trường API (bắt buộc):
  ConnectionStrings__Default
  Jwt__Secret                    (>= 32 ký tự, không dùng dev-secret)
  CustomerAppSms__HttpUrl        (gateway SMS)
  CustomerAppSms__ApiKey         (tuỳ chọn)
  Cors__AllowedOrigins__0        https://admin.domain
  Cors__AllowedOrigins__1        https://app.domain
  Cors__AllowedOrigins__2        https://pos.domain
  Cors__AllowedOrigins__3        https://survey.domain
  Cors__AllowedOrigins__4        https://prescriber.domain
  Cors__AllowedOrigins__5        https://partner.domain
  Cors__AllowedOrigins__6        https://family.kittech.vn
  (env array REPLACES appsettings — list ALL SPAs; deploy runs ensure-novixa-cors-env.sh)
  CustomerAppPush__PublicKey     (nếu bật push)
  CustomerAppPush__PrivateKey

FamilyOS schema (KHÔNG nằm trong migration-files.prod.txt mặc định):
  Chỉ apply off-hours khi chốt pilot — xem deploy/ubuntu/migration-files.family-os.txt
  + seed tenant riêng (không dùng DEMO_FAMILY trên prod pharmacy DB nếu chưa tách).

Migration DB (Production — khong seed demo):
  .\scripts\run-migrations-prod.ps1 -ConnectionString "<prod connection>"

Tao nha thuoc (form — khuyen nghi Novixa):
  https://admin.novixa.vn/setup
  Hoac script: .\scripts\bootstrap-first-tenant.ps1 ...

Huong dan Novixa: docs\novixa-deploy.md
Huong dan push app khach (pilot NT): docs\customer-app-push-pilot.md

Sinh VAPID (Production):
  .\scripts\generate-vapid-keys.ps1 -Subject "mailto:care@domain.vn"
  .\scripts\verify-push-config.ps1 -BaseUrl https://api.domain.vn

Chạy API:
  cd api
  `$env:ASPNETCORE_ENVIRONMENT='Production'
  dotnet KitPlatform.Api.dll

Frontend build dùng VITE_API_BASE_URL=$apiBase (family-app dùng same-origin /api qua nginx)
"@
Set-Content -Path (Join-Path $out "DEPLOY.txt") -Value $notes -Encoding UTF8

Write-Host "`nDone → $out" -ForegroundColor Green
Write-Host "Đọc DEPLOY.txt trước khi triển khai." -ForegroundColor Green


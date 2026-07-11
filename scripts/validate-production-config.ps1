<#
.SYNOPSIS
  Kiểm tra biến môi trường tối thiểu trước khi chạy API Production.

.EXAMPLE
  $env:ConnectionStrings__Default = "Host=..."
  $env:Jwt__Secret = "..."
  .\scripts\validate-production-config.ps1
#>
$ErrorActionPreference = "Stop"

function Require-Env([string]$Name, [int]$MinLength = 1) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value) -or $value.Length -lt $MinLength) {
        throw "Thiếu hoặc quá ngắn: $Name"
    }
    return $value
}

Write-Host "Kiểm tra cấu hình Production..." -ForegroundColor Cyan

$conn = Require-Env "ConnectionStrings__Default" 10
if ($conn -match "KitPlatform_dev") {
    throw "ConnectionStrings__Default vẫn là chuỗi dev."
}

$jwt = Require-Env "Jwt__Secret" 32
if ($jwt -match "dev-secret|change-in-production") {
    throw "Jwt__Secret vẫn là placeholder dev."
}

$platformKey = [Environment]::GetEnvironmentVariable("Platform__ProvisioningKey")
if ([string]::IsNullOrWhiteSpace($platformKey) -or $platformKey.Length -lt 16) {
    Write-Host "  Cảnh báo: Platform__ProvisioningKey chưa đặt (≥16 ký tự) — cần khi thêm nhà thuốc thứ 2 qua /setup" -ForegroundColor Yellow
} else {
    Write-Host "  Platform__ProvisioningKey OK" -ForegroundColor Green
}

if ([Environment]::GetEnvironmentVariable("CustomerAppAuth__DevBypassCode")) {
    throw "Xóa CustomerAppAuth__DevBypassCode trong Production."
}

$smsUrl = Require-Env "CustomerAppSms__HttpUrl" 8
Write-Host "  ConnectionStrings__Default OK" -ForegroundColor Green
Write-Host "  Jwt__Secret OK" -ForegroundColor Green
Write-Host "  CustomerAppSms__HttpUrl = $smsUrl" -ForegroundColor Green

$requiredCors = @(
    'https://admin.novixa.vn',
    'https://app.novixa.vn',
    'https://pos.novixa.vn',
    'https://survey.novixa.vn',
    'https://prescriber.novixa.vn',
    'https://partner.novixa.vn'
)
$corsFromEnv = @()
for ($i = 0; $i -lt 16; $i++) {
    $v = [Environment]::GetEnvironmentVariable("Cors__AllowedOrigins__$i")
    if (-not [string]::IsNullOrWhiteSpace($v)) { $corsFromEnv += $v.Trim() }
}
if ($corsFromEnv.Count -eq 0) {
    Write-Host "  Cảnh báo: chưa đặt Cors__AllowedOrigins__* (API Production sẽ union RequiredNovixaSpaOrigins)" -ForegroundColor Yellow
} else {
    $missing = $requiredCors | Where-Object { $corsFromEnv -notcontains $_ }
    if ($missing.Count -gt 0) {
        throw "Cors__AllowedOrigins__* thiếu: $($missing -join ', '). Env array thay thế appsettings — phải đủ 6 SPA."
    }
    Write-Host "  Cors__AllowedOrigins__* OK ($($corsFromEnv.Count) origins)" -ForegroundColor Green
}

Write-Host "`nCấu hình tối thiểu OK." -ForegroundColor Green


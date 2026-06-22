# Chạy website Novixa (Astro) — giữ cửa sổ này mở
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$site = Join-Path $root "novixa-site"

if (-not (Test-Path $site)) {
    Write-Host "[LOI] Khong tim thay: $site" -ForegroundColor Red
    exit 1
}

Set-Location $site
if (-not (Test-Path "node_modules")) {
    Write-Host ">> npm install..."
    npm install
}

Write-Host ""
Write-Host "=== Novixa site ===" -ForegroundColor Cyan
Write-Host "Mo trinh duyet: http://localhost:4321/vi"
Write-Host "Dung server: Ctrl+C"
Write-Host ""

npm run dev -- --host localhost --port 4321

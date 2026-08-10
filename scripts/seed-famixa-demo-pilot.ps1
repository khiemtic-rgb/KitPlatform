<#
.SYNOPSIS
  Upload + run Famixa GTM demo seed on the FamilyOS pilot VPS (explicit confirm).

.DESCRIPTION
  Does NOT run with normal family-os deploy. Requires -ConfirmSeed.
  Seeds DEMO_FAMILY (2 kids, summer/school, viewer `demo`) for public /demo browse.

.EXAMPLE
  # Dry print only
  .\scripts\seed-famixa-demo-pilot.ps1

.EXAMPLE
  .\scripts\seed-famixa-demo-pilot.ps1 -ConfirmSeed
#>
param(
    [switch]$ConfirmSeed,
    [string]$SshTarget = "root@103.200.23.229",
    [string]$CredentialsFile = "E:\Maychu_VPS\tk.txt",
    [string]$Root = "E:\KitPlatform"
)

$ErrorActionPreference = "Stop"
Set-Location $Root

Write-Host "=== Famixa GTM demo seed (pilot) ===" -ForegroundColor Cyan
Write-Host "  Target : $SshTarget"
Write-Host "  Effect : DEMO_FAMILY + demo viewer on pilot DB"
Write-Host "  Entry  : https://home.famixa.vn/demo"
Write-Host ""

if (-not $ConfirmSeed) {
    Write-Host "DRY RUN — chua seed." -ForegroundColor Yellow
    Write-Host "Chay lai voi -ConfirmSeed khi da deploy SPA/API co /demo + viewer gate."
    Write-Host ""
    Write-Host "Tren VPS (thu cong):"
    Write-Host '  CONFIRM=YES_SEED_FAMIXA_GTM bash /opt/kit-platform/seed-famixa-gtm-demo.sh'
    Write-Host ""
    Write-Host "Luu y an toan:"
    Write-Host "  - Khong share mat khau admin"
    Write-Host "  - demo chi role viewer (server chan mutate)"
    Write-Host "  - Khong gan script nay vao apply-family-os-pilot.sh"
    exit 0
}

$plink = "C:\Program Files\PuTTY\plink.exe"
$pscp = "C:\Program Files\PuTTY\pscp.exe"
if (-not (Test-Path $plink)) { throw "plink.exe not found" }
if (-not (Test-Path $pscp)) { throw "pscp.exe not found" }
if (-not (Test-Path $CredentialsFile)) { throw "Missing credentials file: $CredentialsFile" }

$passLine = Get-Content $CredentialsFile | Where-Object { $_ -match '^Pass' } | Select-Object -First 1
if (-not $passLine) { throw "No Pass line in credentials file" }
$pass = ($passLine -replace '^Pass[^:]*:\s*', '').Trim()
if (-not $pass) { throw "Empty password in credentials file" }

$remote = "/tmp/kit-platform-upload"
$seedSh = Join-Path $Root "deploy\ubuntu\seed-famixa-gtm-demo.sh"
if (-not (Test-Path $seedSh)) { throw "Missing $seedSh" }

Write-Host "Upload migrations + seed script ..." -ForegroundColor Yellow
& $plink -batch -pw $pass $SshTarget "mkdir -p $remote/deploy/ubuntu $remote/migrations"
& $pscp -batch -pw $pass -r "$Root\migrations" "${SshTarget}:${remote}/"
& $pscp -batch -pw $pass $seedSh "${SshTarget}:${remote}/deploy/ubuntu/seed-famixa-gtm-demo.sh"

$remoteCmd = @"
set -e
sed -i 's/\r$//' $remote/deploy/ubuntu/seed-famixa-gtm-demo.sh
chmod +x $remote/deploy/ubuntu/seed-famixa-gtm-demo.sh
cp $remote/deploy/ubuntu/seed-famixa-gtm-demo.sh /opt/kit-platform/seed-famixa-gtm-demo.sh
rsync -a $remote/migrations/ /opt/kit-platform/migrations/
CONFIRM=YES_SEED_FAMIXA_GTM MIGRATIONS=/opt/kit-platform/migrations bash /opt/kit-platform/seed-famixa-gtm-demo.sh
"@

$tmpSh = Join-Path $env:TEMP "seed-famixa-gtm-remote.sh"
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($tmpSh, ($remoteCmd -replace "`r`n", "`n"), $utf8)
& $pscp -batch -pw $pass $tmpSh "${SshTarget}:/tmp/seed-famixa-gtm-remote.sh"
& $plink -batch -pw $pass $SshTarget "sed -i 's/\r$//' /tmp/seed-famixa-gtm-remote.sh; bash /tmp/seed-famixa-gtm-remote.sh"
if ($LASTEXITCODE -ne 0) {
    throw "Remote GTM seed failed: $LASTEXITCODE"
}

Write-Host ""
Write-Host "GTM seed done." -ForegroundColor Green
Write-Host "  https://home.famixa.vn/demo" -ForegroundColor Yellow
Write-Host "Smoke login demo tren API sau do harden: doi mat khau admin neu can." -ForegroundColor DarkYellow

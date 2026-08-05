# Deploy FamilyOS pilot: API + admin + family-app + migrations to Novixa VPS
# Usage: .\scripts\deploy-family-os-pilot-vps.ps1
# Requires: publish artifacts from deploy-production.ps1
param(
    [string]$SshTarget = "root@103.200.23.229",
    [string]$CredentialsFile = "E:\Maychu_VPS\tk.txt",
    [string]$Root = "E:\KitPlatform"
)

$ErrorActionPreference = "Stop"
Set-Location $Root

$plink = "C:\Program Files\PuTTY\plink.exe"
$pscp = "C:\Program Files\PuTTY\pscp.exe"
if (-not (Test-Path $plink)) { throw "plink.exe not found" }
if (-not (Test-Path $pscp)) { throw "pscp.exe not found" }
if (-not (Test-Path $CredentialsFile)) { throw "Missing credentials file: $CredentialsFile" }

$passLine = Get-Content $CredentialsFile | Where-Object { $_ -match '^Pass' } | Select-Object -First 1
if (-not $passLine) { throw "No Pass line in credentials file" }
$pass = ($passLine -replace '^Pass[^:]*:\s*', '').Trim()
if (-not $pass) { throw "Empty password in credentials file" }

$required = @(
    "publish\api\KitPlatform.Api.dll",
    "publish\admin\index.html",
    "publish\family-app\index.html",
    "deploy\ubuntu\apply-family-os-pilot.sh",
    "deploy\ubuntu\migration-files.family-os.txt"
)
foreach ($f in $required) {
    if (-not (Test-Path $f)) { throw "Missing $f - run deploy-production.ps1 first." }
}

$remote = "/tmp/kit-platform-upload"
Write-Host "=== FamilyOS pilot deploy -> $SshTarget ===" -ForegroundColor Cyan

& $plink -batch -pw $pass $SshTarget "rm -rf $remote; mkdir -p $remote"

Write-Host "  upload api ..."
& $pscp -batch -pw $pass -r "$Root\publish\api" "${SshTarget}:${remote}/"
Write-Host "  upload admin ..."
& $pscp -batch -pw $pass -r "$Root\publish\admin" "${SshTarget}:${remote}/"
Write-Host "  upload family-app ..."
& $pscp -batch -pw $pass -r "$Root\publish\family-app" "${SshTarget}:${remote}/"
Write-Host "  upload migrations ..."
& $pscp -batch -pw $pass -r "$Root\migrations" "${SshTarget}:${remote}/"
Write-Host "  upload deploy scripts ..."
& $pscp -batch -pw $pass -r "$Root\deploy" "${SshTarget}:${remote}/"

# Normalize CRLF on the apply script before running
$remoteCmd = @"
set -e
cd $remote
find deploy/ubuntu -type f -name '*.sh' -exec sed -i 's/\r$//' {} +
chmod +x deploy/ubuntu/*.sh
bash deploy/ubuntu/apply-family-os-pilot.sh
"@

$tmpSh = Join-Path $env:TEMP "apply-family-os-pilot-remote.sh"
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($tmpSh, ($remoteCmd -replace "`r`n", "`n"), $utf8)
& $pscp -batch -pw $pass $tmpSh "${SshTarget}:/tmp/apply-family-os-pilot-remote.sh"
& $plink -batch -pw $pass $SshTarget "sed -i 's/\r$//' /tmp/apply-family-os-pilot-remote.sh; bash /tmp/apply-family-os-pilot-remote.sh"
if ($LASTEXITCODE -ne 0) {
    throw "Remote apply-family-os-pilot failed: $LASTEXITCODE"
}

Write-Host "`nFamilyOS pilot deploy done." -ForegroundColor Green
Write-Host "  https://family.kittech.vn" -ForegroundColor Yellow
Write-Host "  Admin FamilyOS Overview also refreshed." -ForegroundColor Yellow

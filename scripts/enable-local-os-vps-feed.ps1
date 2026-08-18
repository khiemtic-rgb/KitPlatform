# Set LocalOs homepage feed keys on VPS and restart API. No Pharmacy migs.
# Usage: .\scripts\enable-local-os-vps-feed.ps1
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

$sh = Join-Path $Root "deploy\ubuntu\ensure-local-os-feed-env.sh"
if (-not (Test-Path $sh)) { throw "Missing $sh" }

Write-Host "=== Enable Local OS homepage feed on VPS ===" -ForegroundColor Cyan
& $pscp -batch -pw $pass $sh "${SshTarget}:/tmp/ensure-local-os-feed-env.sh"
& $plink -batch -pw $pass $SshTarget "sed -i 's/\r$//' /tmp/ensure-local-os-feed-env.sh; bash /tmp/ensure-local-os-feed-env.sh; systemctl restart kit-platform-api"
if ($LASTEXITCODE -ne 0) {
    throw "enable-local-os-vps-feed failed: $LASTEXITCODE"
}
Write-Host "VPS API restarted with LocalOs feed keys." -ForegroundColor Green

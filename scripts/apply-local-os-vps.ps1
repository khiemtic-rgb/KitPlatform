# Apply KIT_LOCAL / Local OS migrations on Novixa VPS. No API restart. No Pharmacy migs.
# Usage: .\scripts\apply-local-os-vps.ps1
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

$manifest = Join-Path $Root "deploy\ubuntu\migration-files.local-os.txt"
$applySh = Join-Path $Root "deploy\ubuntu\apply-local-os-pilot.sh"
if (-not (Test-Path $manifest)) { throw "Missing $manifest" }
if (-not (Test-Path $applySh)) { throw "Missing $applySh" }

$files = Get-Content $manifest | ForEach-Object {
    ($_ -replace '#.*', '').Trim()
} | Where-Object { $_ }
foreach ($f in $files) {
    $p = Join-Path $Root "migrations\$f"
    if (-not (Test-Path $p)) { throw "Missing $p" }
}

$remote = "/tmp/kit-local-os"
Write-Host "=== Local OS DB apply -> $SshTarget (no API restart) ===" -ForegroundColor Cyan

& $plink -batch -pw $pass $SshTarget "rm -rf $remote; mkdir -p $remote/migrations"

Write-Host "  upload manifest + apply script ..."
& $pscp -batch -pw $pass $manifest "${SshTarget}:${remote}/migration-files.local-os.txt"
& $pscp -batch -pw $pass $applySh "${SshTarget}:${remote}/apply-local-os-pilot.sh"

Write-Host "  upload Local OS migrations only ..."
foreach ($f in $files) {
    & $pscp -batch -pw $pass (Join-Path $Root "migrations\$f") "${SshTarget}:${remote}/migrations/"
}

$remoteCmd = @"
set -e
sed -i 's/\r$//' $remote/apply-local-os-pilot.sh $remote/migration-files.local-os.txt
chmod +x $remote/apply-local-os-pilot.sh
UPLOAD=$remote bash $remote/apply-local-os-pilot.sh
"@

$tmpSh = Join-Path $env:TEMP "apply-local-os-remote.sh"
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($tmpSh, ($remoteCmd -replace "`r`n", "`n"), $utf8)
& $pscp -batch -pw $pass $tmpSh "${SshTarget}:/tmp/apply-local-os-remote.sh"
& $plink -batch -pw $pass $SshTarget "sed -i 's/\r$//' /tmp/apply-local-os-remote.sh; bash /tmp/apply-local-os-remote.sh"
if ($LASTEXITCODE -ne 0) {
    throw "Remote apply-local-os-pilot failed: $LASTEXITCODE"
}

Write-Host "`nKIT_LOCAL applied on VPS DB. API was not restarted." -ForegroundColor Green

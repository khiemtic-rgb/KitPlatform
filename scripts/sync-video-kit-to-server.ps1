# KHONG CHAY SCRIPT NAY NUA. Du an da chot 09/11/2026: dung phim o may local,
# may chu B chi dang bai. Thu muc video-repo tren B da duoc do. Dung day lai.
#
# Dong bo bo dung Video Studio (Python) len VPS B — khong dong bo khoa, .dev/, output cu.
# API Famixa doc thu muc nay qua Content__VideoRepoRoot trong /etc/kit-famixa/api.env.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-video-kit-to-server.ps1
param(
    [string]$SshTarget = "root@157.10.53.182",
    [string]$HostKey = "SHA256:8+jHoSMzJS+BC1U0N18HfCPVVAzroBdtzZH6DdeO+28",
    [string]$CredentialsFile = "E:\Maychu_VPS\may_chu2.txt",
    [string]$Root = "E:\KitPlatform",
    [string]$RemoteRepo = "/var/lib/kit-famixa/video-repo"
)

$ErrorActionPreference = "Stop"
$plink = "C:\Program Files\PuTTY\plink.exe"
$pscp = "C:\Program Files\PuTTY\pscp.exe"

$passLine = Get-Content $CredentialsFile | Where-Object { $_ -match '^\s*Pass' } | Select-Object -First 1
if (-not $passLine) { throw "password not found in credentials file" }
$pass = ($passLine -split '[:=]', 2)[1].Trim()

$stage = Join-Path $Root ".tmp\video-kit-sync"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory $stage | Out-Null

function Copy-Tree([string]$From, [string]$To, [string[]]$ExcludeDirs = @(), [string[]]$ExcludeFiles = @()) {
    $src = Join-Path $Root $From
    if (-not (Test-Path $src)) { throw "Missing $From" }
    $dst = Join-Path $stage $To
    $xd = @('__pycache__', '.dev') + $ExcludeDirs
    $null = robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /XD $xd /XF (@('*.pyc') + $ExcludeFiles)
    if ($LASTEXITCODE -ge 8) { throw "robocopy $From failed ($LASTEXITCODE)" }
}

$pain = "proof\kit-video-production-v1\outputs\novixa-pain-stock"
New-Item -ItemType Directory (Join-Path $stage $pain) -Force | Out-Null
Get-ChildItem (Join-Path $Root $pain) -File -Filter *.py | Where-Object { $_.Name -notlike '*_test.py' } |
    ForEach-Object { Copy-Item $_.FullName (Join-Path $stage $pain) }
Copy-Tree "$pain\screens" "$pain\screens"
Copy-Tree "tools\kit-video-library" "tools\kit-video-library" -ExcludeDirs @('voice')
Copy-Tree "tools\kit-video-production\kit_video" "tools\kit-video-production\kit_video"
Copy-Tree "tools\kit-video-production\schemas" "tools\kit-video-production\schemas"
Copy-Item (Join-Path $Root "tools\kit-video-production\kv.py") (Join-Path $stage "tools\kit-video-production\")
Get-ChildItem (Join-Path $Root "docs\marketing") -Directory | Where-Object { Test-Path (Join-Path $_.FullName "kich-ban") } |
    ForEach-Object { Copy-Tree "docs\marketing\$($_.Name)\kich-ban" "docs\marketing\$($_.Name)\kich-ban" }
Set-Content (Join-Path $stage "KitPlatform.slnx") "" -Encoding ascii

$bad = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Name -match '(^|[-_.])(secret|credential|\.env)|elevenlab\.txt|appsettings' }
if ($bad) { throw "Refusing to upload secret-looking files: $($bad.Name -join ', ')" }

$tar = Join-Path $Root ".tmp\video-kit-sync.tar"
tar -cf $tar -C $stage .
$count = (Get-ChildItem $stage -Recurse -File).Count
Write-Host "Staged $count files ($([math]::Round((Get-Item $tar).Length / 1MB, 1)) MB)"

& $pscp -batch -q -hostkey $HostKey -pw $pass $tar "${SshTarget}:/tmp/video-kit-sync.tar"
if ($LASTEXITCODE -ne 0) { throw "upload failed" }
$remote = "set -e; mkdir -p $RemoteRepo; tar -xf /tmp/video-kit-sync.tar -C $RemoteRepo; rm -f /tmp/video-kit-sync.tar; " +
          "mkdir -p $RemoteRepo/proof/kit-video-production-v1/outputs/novixa-studio; chown -R www-data:www-data $RemoteRepo; " +
          "echo synced: `$(find $RemoteRepo -type f -not -path '*/novixa-studio/*' | wc -l) files"
& $plink -batch -hostkey $HostKey -pw $pass $SshTarget $remote
if ($LASTEXITCODE -ne 0) { throw "remote extract failed" }

Remove-Item -Recurse -Force $stage
Remove-Item -Force $tar

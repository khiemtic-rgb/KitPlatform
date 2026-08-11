# Quản lý KitPlatform API cho môi trường dev local (chạy nền, không cần giữ cửa sổ CMD).
$ErrorActionPreference = "Stop"

$script:Root = Split-Path -Parent $PSScriptRoot
$script:ApiProject = Join-Path $script:Root "src\KitPlatform.Api\KitPlatform.Api.csproj"
$script:DevDir = Join-Path $script:Root ".dev"
$script:PidFile = Join-Path $script:DevDir "api.pid"
$script:WatchPidFile = Join-Path $script:DevDir "watch.pid"
$script:LogFile = Join-Path $script:DevDir "api.log"
$script:LogErrFile = Join-Path $script:DevDir "api.err.log"
$script:LockFile = Join-Path $env:TEMP "kit-platform-api-dev.lock"

function Test-ApiDevHealth {
    try {
        $health = Invoke-RestMethod -Uri "http://localhost:5290/api/health" -TimeoutSec 4
        return $health.status -eq "ok"
    }
    catch {
        return $false
    }
}

function Wait-ApiDevHealth {
    param([int]$TimeoutSec = 45)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-ApiDevHealth) { return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}

function Wait-ApiDevLock {
    param([int]$TimeoutSec = 90)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-Path $script:LockFile)) { return $true }
        $lockAge = (Get-Date) - (Get-Item $script:LockFile).LastWriteTime
        if ($lockAge.TotalSeconds -gt 120) {
            Remove-Item $script:LockFile -Force -ErrorAction SilentlyContinue
            return $true
        }
        Start-Sleep -Seconds 1
    }
    return $false
}

function Stop-ApiDevProcess {
    if (Test-Path $script:PidFile) {
        $raw = Get-Content $script:PidFile -ErrorAction SilentlyContinue
        if ($raw -match '^\d+$') {
            Stop-Process -Id ([int]$raw) -Force -ErrorAction SilentlyContinue
        }
        Remove-Item $script:PidFile -Force -ErrorAction SilentlyContinue
    }

    Get-Process -Name "KitPlatform.Api" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    foreach ($port in 5290, 7224) {
        try {
            Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop |
                ForEach-Object {
                    $proc = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
                    if ($proc -and ($proc.ProcessName -eq "KitPlatform.Api" -or $proc.ProcessName -eq "dotnet")) {
                        Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
                    }
                }
        } catch {
            # NetTCPIP module unavailable in some sandboxes — pid file + process name above is enough.
        }
    }

    Start-Sleep -Seconds 1
}

function Start-ApiDevBackground {
    param([switch]$SkipBuild)

    New-Item -ItemType Directory -Force -Path $script:DevDir | Out-Null

    if (-not $SkipBuild) {
        $env:NUGET_PACKAGES = "$env:USERPROFILE\.nuget\packages"
        dotnet build $script:ApiProject --verbosity minimal | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Build API that bai."
        }
    }

    $argList = @(
        "run",
        "--project", $script:ApiProject,
        "--no-build",
        "--launch-profile", "https"
    )

    $proc = Start-Process -FilePath "dotnet" `
        -ArgumentList $argList `
        -WorkingDirectory $script:Root `
        -WindowStyle Hidden `
        -PassThru `
        -RedirectStandardOutput $script:LogFile `
        -RedirectStandardError $script:LogErrFile

    $proc.Id | Set-Content -Path $script:PidFile -Encoding ascii

    if (-not (Wait-ApiDevHealth)) {
        Write-Host "[LOI] API khong len sau khi khoi dong. Log:" -ForegroundColor Red
        if (Test-Path $script:LogErrFile) { Get-Content $script:LogErrFile -Tail 20 | Write-Host }
        if (Test-Path $script:LogFile) { Get-Content $script:LogFile -Tail 20 | Write-Host }
        throw "API chua san sang tren http://localhost:5290"
    }
}

function Start-ApiDevWatcher {
    if (Test-Path $script:WatchPidFile) {
        $raw = Get-Content $script:WatchPidFile -ErrorAction SilentlyContinue
        if ($raw -match '^\d+$') {
            $existing = Get-Process -Id ([int]$raw) -ErrorAction SilentlyContinue
            if ($existing) { return }
        }
    }

    $watchScript = Join-Path $PSScriptRoot "watch-api-dev.ps1"
    $proc = Start-Process -FilePath "powershell.exe" `
        -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", $watchScript) `
        -WorkingDirectory $script:Root `
        -WindowStyle Hidden `
        -PassThru

    $proc.Id | Set-Content -Path $script:WatchPidFile -Encoding ascii
}

function Ensure-ApiDev {
    param(
        [switch]$Quiet,
        [switch]$SkipWatcher,
        [switch]$SkipBuild
    )

    if (Test-ApiDevHealth) {
        if (-not $Quiet) {
            Write-Host "[OK] API dang chay (http://localhost:5290)" -ForegroundColor Green
        }
        if (-not $SkipWatcher) { Start-ApiDevWatcher }
        return 0
    }

    if (-not (Wait-ApiDevLock)) {
        Write-Host "[LOI] API dang duoc restart boi process khac." -ForegroundColor Red
        return 1
    }

    New-Item -ItemType File -Path $script:LockFile -Force | Out-Null

    try {
        if (-not $Quiet) {
            Write-Host "[!] API chua phan hoi - khoi dong nen (port 5290)..." -ForegroundColor Yellow
        }

        Stop-ApiDevProcess

        & (Join-Path $PSScriptRoot "check-postgres.ps1")
        if ($LASTEXITCODE -ne 0) { return $LASTEXITCODE }

        Start-ApiDevBackground -SkipBuild:$SkipBuild

        if (-not $Quiet) {
            Write-Host "[OK] API san sang: http://localhost:5290/swagger" -ForegroundColor Green
        }

        if (-not $SkipWatcher) { Start-ApiDevWatcher }
        return 0
    }
    finally {
        Remove-Item $script:LockFile -Force -ErrorAction SilentlyContinue
    }
}

function Stop-ApiDevAll {
    if (Test-Path $script:WatchPidFile) {
        $raw = Get-Content $script:WatchPidFile -ErrorAction SilentlyContinue
        if ($raw -match '^\d+$') {
            Stop-Process -Id ([int]$raw) -Force -ErrorAction SilentlyContinue
        }
        Remove-Item $script:WatchPidFile -Force -ErrorAction SilentlyContinue
    }
    Stop-ApiDevProcess
    Write-Host "[OK] Da dung API dev + watcher." -ForegroundColor Green
}


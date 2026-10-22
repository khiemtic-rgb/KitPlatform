# Stops only the Second Brain validation API and admin UI started by
# second-brain-trading-ui-start.ps1. Never stops other dotnet or node processes.
$ErrorActionPreference = 'Stop'
$Root = 'E:\KitPlatform'
$Dev = Join-Path $Root '.dev\sb-trading-ui'
$PidFile = Join-Path $Dev 'pids.json'
$ProtectedPid = 11404

if (-not (Test-Path -LiteralPath $PidFile)) {
    Write-Host 'No Second Brain local process file. Nothing to stop.'
    exit 0
}

$recorded = Get-Content -LiteralPath $PidFile -Raw | ConvertFrom-Json
$stopped = @()

function Stop-RecordedProcess {
    param([int]$ProcessId, [string]$Marker)
    if ($ProcessId -le 0) { return }
    if ($ProcessId -eq $ProtectedPid) {
        throw "Refusing to stop protected API process $ProtectedPid."
    }
    $proc = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction SilentlyContinue
    if (-not $proc) { return }
    $cmd = [string]$proc.CommandLine
    if ($cmd -notlike "*$Marker*") {
        throw "PID $ProcessId does not match marker '$Marker'. Not stopping."
    }
    Stop-Process -Id $ProcessId -Force
    $script:stopped += $ProcessId
}

Stop-RecordedProcess -ProcessId ([int]$recorded.apiPid) -Marker '.dev\sb-trading-ui\api\KitPlatform.Api.dll'
Stop-RecordedProcess -ProcessId ([int]$recorded.uiPid) -Marker 'vite.second-brain.config.ts'
Remove-Item -LiteralPath $PidFile -Force
if ($stopped.Count -eq 0) {
    Write-Host 'Recorded Second Brain processes were already gone.'
} else {
    Write-Host ("Stopped Second Brain local PID: " + ($stopped -join ', '))
}

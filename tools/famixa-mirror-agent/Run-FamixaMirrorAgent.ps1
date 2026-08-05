# Famixa Mirror Agent (Windows) — M1
# Samples foreground process and reports heartbeat + usage to FamilyOS API.
#
# Setup:
#   1. Copy config.example.json → config.json and fill token / ids
#   2. powershell -ExecutionPolicy Bypass -File .\Run-FamixaMirrorAgent.ps1
#
# Optional: register Task Scheduler to run at logon (see Register-ScheduledTask.ps1)

param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot "config.json"),
    [int]$SampleSeconds = 15,
    [int]$FlushSeconds = 60
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not (Test-Path $ConfigPath)) {
    Write-Host "Missing $ConfigPath — copy config.example.json and fill values." -ForegroundColor Red
    exit 1
}

$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$apiBase = ($config.apiBaseUrl).TrimEnd('/')
$familyId = $config.familyId
$memberId = $config.childMemberId
$token = $config.accessToken
$deviceId = if ($config.deviceId) { $config.deviceId } else { $env:COMPUTERNAME }
$deviceLabel = if ($config.deviceLabel) { $config.deviceLabel } else { $env:COMPUTERNAME }
$agentVersion = "0.1.0-m1"

if (-not $apiBase -or -not $familyId -or -not $memberId -or -not $token) {
    throw "config.json needs apiBaseUrl, familyId, childMemberId, accessToken"
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class FamixaFg {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
}
"@

function Get-ForegroundApp {
    try {
        $hwnd = [FamixaFg]::GetForegroundWindow()
        if ($hwnd -eq [IntPtr]::Zero) { return $null }
        $pid = 0
        [void][FamixaFg]::GetWindowThreadProcessId($hwnd, [ref]$pid)
        if ($pid -eq 0) { return $null }
        $p = Get-Process -Id $pid -ErrorAction SilentlyContinue
        if (-not $p) { return $null }
        $name = $p.ProcessName
        if ($name -match '^(Idle|dwm|explorer|SearchHost|ShellExperienceHost|TextInputHost|ApplicationFrameHost)$') {
            return $null
        }
        return @{
            key   = ($name.ToLowerInvariant())
            label = $name
        }
    } catch {
        return $null
    }
}

function Invoke-MirrorApi {
    param([string]$Method, [string]$Path, [object]$Body)
    $uri = "$apiBase/api/family-os/families/$familyId/mirror/$Path"
    $headers = @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    }
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 6 -Compress } else { $null }
    try {
        if ($Method -eq "POST") {
            Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $json -TimeoutSec 20 | Out-Null
        } else {
            Invoke-RestMethod -Method Get -Uri $uri -Headers $headers -TimeoutSec 20
        }
        return $true
    } catch {
        Write-Host ("API error {0}: {1}" -f $Path, $_.Exception.Message) -ForegroundColor DarkYellow
        return $false
    }
}

Write-Host "=== Famixa Mirror Agent M1 ===" -ForegroundColor Cyan
Write-Host "API: $apiBase"
Write-Host "Device: $deviceId · child=$memberId"
Write-Host "Sample ${SampleSeconds}s · flush ${FlushSeconds}s · Ctrl+C to stop"

$bucket = @{}  # key -> @{ label; seconds }
$lastFlush = Get-Date
$lastApp = $null

while ($true) {
    $fg = Get-ForegroundApp
    if ($fg) {
        $lastApp = $fg.label
        if (-not $bucket.ContainsKey($fg.key)) {
            $bucket[$fg.key] = @{ label = $fg.label; seconds = 0 }
        }
        $bucket[$fg.key].seconds += $SampleSeconds
        if (-not $bucket[$fg.key].label) { $bucket[$fg.key].label = $fg.label }
    }

    $null = Invoke-MirrorApi -Method POST -Path "heartbeat" -Body @{
        deviceId           = $deviceId
        memberId           = $memberId
        deviceLabel        = $deviceLabel
        agentVersion       = $agentVersion
        lastForegroundApp  = $lastApp
    }

    $elapsed = ((Get-Date) - $lastFlush).TotalSeconds
    if ($elapsed -ge $FlushSeconds -and $bucket.Count -gt 0) {
        $items = @()
        foreach ($k in @($bucket.Keys)) {
            $v = $bucket[$k]
            if ($v.seconds -le 0) { continue }
            $items += @{
                appKey   = $k
                appLabel = $v.label
                kind     = "app"
                seconds  = [int]$v.seconds
            }
        }
        if ($items.Count -gt 0) {
            $ok = Invoke-MirrorApi -Method POST -Path "usage" -Body @{
                memberId = $memberId
                items    = $items
            }
            if ($ok) {
                Write-Host ("Flushed {0} app(s) · foreground={1}" -f $items.Count, $lastApp) -ForegroundColor Green
                $bucket = @{}
                $lastFlush = Get-Date
            }
        } else {
            $lastFlush = Get-Date
        }
    }

    Start-Sleep -Seconds $SampleSeconds
}

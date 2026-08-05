# Register Famixa Mirror Agent to run at user logon (current user).
# Run once in elevated PowerShell if needed; default runs as current user.
param(
    [string]$TaskName = "FamixaMirrorAgent"
)

$script = Join-Path $PSScriptRoot "Run-FamixaMirrorAgent.ps1"
if (-not (Test-Path $script)) { throw "Missing $script" }

$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`""
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
Write-Host "Registered scheduled task: $TaskName" -ForegroundColor Green
Write-Host "Ensure config.json exists beside the script." -ForegroundColor Yellow

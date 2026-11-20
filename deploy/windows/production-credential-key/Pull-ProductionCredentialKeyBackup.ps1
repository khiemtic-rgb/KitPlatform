# Pull a staged key file from the VPS and seal it with DPAPI.
# Default mode is precheck only when -PrecheckOnly is set.
# Without -AllowInteractivePassword the SSH client cannot prompt, and no password is stored.
param(
    [Parameter(Mandatory = $true)][string]$SshUser,
    [Parameter(Mandatory = $true)][string]$SshHost,
    [Parameter(Mandatory = $true)][string]$RemotePath,
    [string]$IdentityFile,
    [switch]$AllowInteractivePassword,
    [switch]$PrecheckOnly,
    [string]$VaultRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $here 'ProductionCredentialKeyBackup.psm1') -Force

function Write-Known {
    param([string]$Message)
    $known = @(
        'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED',
        'PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE',
        'PRODUCTION_CREDENTIAL_KEY_PULL_HOSTKEY_UNVERIFIED',
        'PRODUCTION_CREDENTIAL_KEY_PULL_AUTH_REQUIRED',
        'PRODUCTION_CREDENTIAL_KEY_PULL_FAILED',
        'PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS',
        'PRODUCTION_CREDENTIAL_KEY_MISSING',
        'PRODUCTION_CREDENTIAL_KEY_INVALID',
        'PRODUCTION_CREDENTIAL_KEY_PLAINTEXT_REMAINS',
        'PRODUCTION_CREDENTIAL_KEY_PULL_REMOTE_REMAINS',
        'WINDOWS_KEY_PULL_PRECHECK_OK',
        'PRODUCTION_CREDENTIAL_KEY_BACKUP_SEALED'
    )
    if ($known -contains $Message) { Write-Output $Message } else { Write-Output 'PRODUCTION_CREDENTIAL_KEY_PULL_FAILED' }
}

try {
    $precheck = Test-ProductionCredentialPullPrecheck -SshUser $SshUser -SshHost $SshHost -RemotePath $RemotePath
} catch {
    Write-Known $_.Exception.Message
    exit 1
}

if ($PrecheckOnly) {
    Write-Output $precheck
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($IdentityFile) -and -not (Test-Path -LiteralPath $IdentityFile)) {
    Write-Output 'PRODUCTION_CREDENTIAL_KEY_PULL_AUTH_REQUIRED'
    exit 1
}

$local = Join-Path $env:TEMP ('kit-credential-pull-' + [guid]::NewGuid().ToString('n') + '.env')
$remoteSpec = '{0}@{1}:{2}' -f $SshUser, $SshHost, $RemotePath
$sshTarget = '{0}@{1}' -f $SshUser, $SshHost
$pulled = $false
$seal = $null
$sealError = $null
$scpFailure = $null
try {
    $scpArgs = @('-q', '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=15')
    if (-not $AllowInteractivePassword) {
        $scpArgs += @('-o', 'BatchMode=yes', '-o', 'NumberOfPasswordPrompts=0')
    }
    if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
        $scpArgs += @('-i', $IdentityFile)
    }
    $scpArgs += @($remoteSpec, $local)
    $scpOut = & scp.exe @scpArgs 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $local)) {
        if ($scpOut -match 'Host key verification failed|REMOTE HOST IDENTIFICATION') {
            $scpFailure = 'PRODUCTION_CREDENTIAL_KEY_PULL_HOSTKEY_UNVERIFIED'
        } elseif ($scpOut -match 'Permission denied|Authentication|Auth fail') {
            $scpFailure = 'PRODUCTION_CREDENTIAL_KEY_PULL_AUTH_REQUIRED'
        } else {
            $scpFailure = 'PRODUCTION_CREDENTIAL_KEY_PULL_FAILED'
        }
    } else {
        $pulled = $true
        if ([string]::IsNullOrWhiteSpace($VaultRoot)) {
            $seal = Protect-ProductionCredentialKeyMaterial -PlaintextPath $local
        } else {
            $seal = Protect-ProductionCredentialKeyMaterial -PlaintextPath $local -VaultRoot $VaultRoot
        }
    }
} catch {
    $sealError = $_.Exception.Message
} finally {
    if (Test-Path -LiteralPath $local) {
        $zeros = $null
        $info = Get-Item -LiteralPath $local -ErrorAction SilentlyContinue
        if ($null -ne $info -and $info.Length -le 1MB) {
            $zeros = New-Object byte[] $info.Length
            [System.IO.File]::WriteAllBytes($local, $zeros)
        }
        Remove-Item -LiteralPath $local -Force -ErrorAction SilentlyContinue
        if ($null -ne $zeros) { [Array]::Clear($zeros, 0, $zeros.Length) }
    }
}

if (-not $pulled) {
    if ($sealError) { Write-Known $sealError }
    elseif ([string]::IsNullOrWhiteSpace($scpFailure)) { Write-Output 'PRODUCTION_CREDENTIAL_KEY_PULL_FAILED' }
    else { Write-Output $scpFailure }
    exit 1
}

$sshArgs = @('-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=15')
if (-not $AllowInteractivePassword) {
    $sshArgs += @('-o', 'BatchMode=yes', '-o', 'NumberOfPasswordPrompts=0')
}
if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
    $sshArgs += @('-i', $IdentityFile)
}
$remoteCommand = "rm -f -- '$RemotePath'"
$sshArgs += @($sshTarget, $remoteCommand)
$sshOut = & ssh.exe @sshArgs 2>&1 | Out-String
$discarded = ($LASTEXITCODE -eq 0) -and ($sshOut -notmatch 'Permission denied|Authentication')

if ($sealError) { Write-Known $sealError }
elseif ($seal) { Write-Output $seal }

if (-not $discarded) {
    Write-Output 'PRODUCTION_CREDENTIAL_KEY_PULL_REMOTE_REMAINS'
    exit 1
}
if ($sealError -or $seal -ne 'PRODUCTION_CREDENTIAL_KEY_BACKUP_SEALED') { exit 1 }
Write-Output 'PRODUCTION_CREDENTIAL_KEY_PULL_REMOTE_DISCARDED'
exit 0

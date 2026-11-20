# Local readiness for the Windows off-machine key vault.
# Does not create a production key, does not connect to the VPS, and does not print key material.
param(
    [string]$ExpectedHost = '103.200.23.229'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $here 'ProductionCredentialKeyBackup.psm1') -Force

$failed = $false
function Write-Code {
    param([string]$Code)
    Write-Output $Code
}
function Fail {
    param([string]$Code)
    Write-Output $Code
    $script:failed = $true
}

$os = Get-CimInstance Win32_OperatingSystem
Write-Code ('OS=' + $os.Caption)
Write-Code ('BUILD=' + $os.BuildNumber)
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
Write-Code ('USER=' + $identity.Name)
Write-Code ('ELEVATED=' + $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator))

$ssh = Get-Command ssh.exe -ErrorAction SilentlyContinue
$scp = Get-Command scp.exe -ErrorAction SilentlyContinue
if ($ssh -and $scp) { Write-Code 'SSH_CLIENT_PRESENT' } else { Fail 'SSH_CLIENT_MISSING' }

$sshd = Get-Service -Name sshd -ErrorAction SilentlyContinue
if ($null -eq $sshd) { Write-Code 'SSHD_ABSENT' } else { Write-Code ('SSHD_STATUS=' + $sshd.Status) }

$sshDir = Join-Path $env:USERPROFILE '.ssh'
$identityFiles = @()
if (Test-Path -LiteralPath $sshDir) {
    $identityFiles = @(Get-ChildItem -LiteralPath $sshDir -Force -File | Where-Object { $_.Name -like 'id_*' -and $_.Name -notlike '*.pub' })
}
if ($identityFiles.Count -eq 0) { Write-Code 'SSH_IDENTITY_ABSENT' } else { Write-Code 'SSH_IDENTITY_PRESENT' }

$known = Join-Path $env:USERPROFILE '.ssh\known_hosts'
$hostKnown = $false
if (Test-Path -LiteralPath $known) {
    foreach ($line in [System.IO.File]::ReadLines($known)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $field = ($line -split '\s+', 2)[0]
        foreach ($name in ($field -split ',')) {
            if ($name -eq $ExpectedHost) { $hostKnown = $true }
        }
    }
}
if ($hostKnown) { Write-Code 'SSH_HOSTKEY_PRESENT' } else { Fail 'SSH_HOSTKEY_ABSENT' }

$probe = Join-Path $env:TEMP ('efs-probe-' + [guid]::NewGuid().ToString('n') + '.txt')
[System.IO.File]::WriteAllText($probe, 'not-a-secret')
$efs = & cmd.exe /c "cipher /e `"$probe`" 2>&1"
if ($efs -match 'not supported') {
    Write-Code 'EFS_UNSUPPORTED'
} else {
    & cmd.exe /c "cipher /d `"$probe`" >nul 2>&1" | Out-Null
    Write-Code 'EFS_AVAILABLE'
}
Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue

$plain = $null
$back = $null
try {
    $plain = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($plain) } finally { $rng.Dispose() }
    $blob = [System.Security.Cryptography.ProtectedData]::Protect($plain, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $back = [System.Security.Cryptography.ProtectedData]::Unprotect($blob, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $same = $true
    for ($i = 0; $i -lt $plain.Length; $i++) { if ($plain[$i] -ne $back[$i]) { $same = $false } }
    if ($same -and $plain.Length -eq 32) { Write-Code 'DPAPI_ROUNDTRIP_OK' } else { Fail 'DPAPI_ROUNDTRIP_FAILED' }
} catch {
    Fail 'DPAPI_ROUNDTRIP_FAILED'
} finally {
    if ($null -ne $plain) { [Array]::Clear($plain, 0, $plain.Length) }
    if ($null -ne $back) { [Array]::Clear($back, 0, $back.Length) }
}

$fixture = @(Invoke-ProductionCredentialKeyBackupFixture)
foreach ($code in $fixture) { Write-Code $code }
foreach ($required in @('FIXTURE_RESTORE_OK', 'FIXTURE_TAMPER_REJECTED', 'FIXTURE_SHORT_KEY_REJECTED', 'FIXTURE_REPO_PATH_REJECTED')) {
    if ($fixture -notcontains $required) { Fail 'FIXTURE_INCOMPLETE' }
}

$vault = Initialize-ProductionCredentialKeyVault
Write-Code ('VAULT=' + $vault)
if (Test-ProductionCredentialVaultAcl -VaultRoot $vault) { Write-Code 'VAULT_ACL_OK' } else { Fail 'VAULT_ACL_FAILED' }
$sealed = Join-Path $vault 'production-credential-key.dpapi'
if (Test-Path -LiteralPath $sealed) { Fail 'PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS' } else { Write-Code 'VAULT_EMPTY' }

$badPath = Test-ProductionCredentialRemoteStagePath -RemotePath '/etc/kit-platform/api.env'
$goodPath = Test-ProductionCredentialRemoteStagePath -RemotePath '/run/kit-production-credential-pull/key-0123456789abcdef0123456789abcdef.env'
if ($badPath -or -not $goodPath) { Fail 'REMOTE_PATH_GUARD_FAILED' } else { Write-Code 'REMOTE_PATH_GUARD_OK' }

try {
    $precheck = Test-ProductionCredentialPullPrecheck -SshUser 'operator' -SshHost $ExpectedHost -RemotePath '/run/kit-production-credential-pull/key-0123456789abcdef0123456789abcdef.env'
    Write-Code $precheck
} catch {
    Fail $_.Exception.Message
}

try {
    $null = Test-ProductionCredentialPullPrecheck -SshUser 'operator' -SshHost 'localhost' -RemotePath '/run/kit-production-credential-pull/key-0123456789abcdef0123456789abcdef.env'
    Fail 'SAME_MACHINE_GUARD_FAILED'
} catch {
    if ($_.Exception.Message -eq 'PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE') {
        Write-Code 'SAME_MACHINE_GUARD_OK'
    } else {
        Fail 'SAME_MACHINE_GUARD_FAILED'
    }
}

if ($failed) {
    Write-Code 'WINDOWS_KEY_BACKUP_NOT_READY'
    exit 1
}
Write-Code 'WINDOWS_KEY_BACKUP_READY'
exit 0

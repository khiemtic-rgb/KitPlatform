# Seal a two-line key file into the Windows DPAPI vault. Does not print the key.
# Refuses to run until a production key file exists outside this repository.
param(
    [Parameter(Mandatory = $true)][string]$PlaintextPath,
    [string]$VaultRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $here 'ProductionCredentialKeyBackup.psm1') -Force

try {
    if ([string]::IsNullOrWhiteSpace($VaultRoot)) {
        $code = Protect-ProductionCredentialKeyMaterial -PlaintextPath $PlaintextPath
    } else {
        $code = Protect-ProductionCredentialKeyMaterial -PlaintextPath $PlaintextPath -VaultRoot $VaultRoot
    }
    Write-Output $code
    exit 0
} catch {
    $message = $_.Exception.Message
    $known = @(
        'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED',
        'PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS',
        'PRODUCTION_CREDENTIAL_KEY_MISSING',
        'PRODUCTION_CREDENTIAL_KEY_INVALID',
        'PRODUCTION_CREDENTIAL_KEY_PLAINTEXT_REMAINS'
    )
    if ($known -contains $message) { Write-Output $message } else { Write-Output 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED' }
    exit 1
}

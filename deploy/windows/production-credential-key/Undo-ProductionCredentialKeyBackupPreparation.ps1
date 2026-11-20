# Remove an empty Windows key vault. Refuses when a sealed key file is present.
param(
    [string]$VaultRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $here 'ProductionCredentialKeyBackup.psm1') -Force

try {
    if ([string]::IsNullOrWhiteSpace($VaultRoot)) {
        $code = Undo-ProductionCredentialKeyVault
    } else {
        $code = Undo-ProductionCredentialKeyVault -VaultRoot $VaultRoot
    }
    Write-Output $code
    exit 0
} catch {
    if ($_.Exception.Message -eq 'PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS') {
        Write-Output 'PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS'
    } else {
        Write-Output 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'
    }
    exit 1
}

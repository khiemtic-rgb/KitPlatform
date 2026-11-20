# Unseal the Windows vault and compare it, or export a temp copy for the VPS checker.
# Prints a status code only. Does not print the key.
param(
    [string]$ComparePlaintextPath,
    [string]$ExportTo,
    [string]$VaultRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $here 'ProductionCredentialKeyBackup.psm1') -Force

$known = @(
    'PRODUCTION_CREDENTIAL_KEY_OK',
    'PRODUCTION_CREDENTIAL_KEY_MISSING',
    'PRODUCTION_CREDENTIAL_KEY_INVALID',
    'PRODUCTION_CREDENTIAL_KEY_RESTORE_OK',
    'PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH',
    'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED',
    'PRODUCTION_CREDENTIAL_KEY_EXPORT_WRITTEN'
)

try {
    $openArgs = @{}
    if (-not [string]::IsNullOrWhiteSpace($VaultRoot)) { $openArgs.VaultRoot = $VaultRoot }
    if (-not [string]::IsNullOrWhiteSpace($ComparePlaintextPath)) { $openArgs.ComparePlaintextPath = $ComparePlaintextPath }
    $code = Open-ProductionCredentialKeyMaterial @openArgs
    Write-Output $code
    if (-not [string]::IsNullOrWhiteSpace($ExportTo)) {
        $exportArgs = @{ DestinationPath = $ExportTo }
        if (-not [string]::IsNullOrWhiteSpace($VaultRoot)) { $exportArgs.VaultRoot = $VaultRoot }
        $exportCode = Export-ProductionCredentialKeyPlaintext @exportArgs
        Write-Output $exportCode
        Write-Output ('EXPORT_PATH=' + [System.IO.Path]::GetFullPath($ExportTo))
    }
    if ($code -eq 'PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH' -or $code -eq 'PRODUCTION_CREDENTIAL_KEY_INVALID' -or $code -eq 'PRODUCTION_CREDENTIAL_KEY_MISSING') {
        exit 1
    }
    exit 0
} catch {
    $message = $_.Exception.Message
    if ($known -contains $message) { Write-Output $message } else { Write-Output 'PRODUCTION_CREDENTIAL_KEY_INVALID' }
    exit 1
}

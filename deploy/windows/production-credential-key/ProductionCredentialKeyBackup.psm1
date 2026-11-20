Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Security

$script:KeyIdPrefix = 'ProductionCredentials__DataEncryptionKeyId='
$script:KeyPrefix = 'ProductionCredentials__DataEncryptionKey='
$script:SealedFileName = 'production-credential-key.dpapi'
$script:ManifestFileName = 'production-credential-key.manifest.txt'
$script:MarkerFileName = 'README.txt'

function Get-ProductionCredentialKeyVaultRoot {
    Join-Path $env:LOCALAPPDATA 'KitPlatform\production-credential-backup'
}

function Get-ProductionCredentialRepoRoot {
    $dir = Split-Path -Parent $PSScriptRoot
    $dir = Split-Path -Parent $dir
    Split-Path -Parent $dir
}

function Clear-Bytes {
    param([byte[]]$Bytes)
    if ($null -eq $Bytes) { return }
    [Array]::Clear($Bytes, 0, $Bytes.Length)
}

function Set-CurrentUserOnlyAcl {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ((Get-Item -LiteralPath $Path).PSIsContainer) {
        $acl = New-Object System.Security.AccessControl.DirectorySecurity
        $inherit = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    } else {
        $acl = New-Object System.Security.AccessControl.FileSecurity
        $inherit = [System.Security.AccessControl.InheritanceFlags]::None
    }
    $acl.SetAccessRuleProtection($true, $false)
    $me = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $me,
        'FullControl',
        $inherit,
        [System.Security.AccessControl.PropagationFlags]::None,
        'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Initialize-ProductionCredentialKeyVault {
    param([string]$VaultRoot = (Get-ProductionCredentialKeyVaultRoot))
    $parent = Split-Path -Parent $VaultRoot
    if (-not (Test-Path -LiteralPath $parent)) {
        $null = New-Item -ItemType Directory -Path $parent
    }
    if (-not (Test-Path -LiteralPath $VaultRoot)) {
        $null = New-Item -ItemType Directory -Path $VaultRoot
    }
    Set-CurrentUserOnlyAcl -Path $VaultRoot
    $marker = Join-Path $VaultRoot $script:MarkerFileName
    if (-not (Test-Path -LiteralPath $marker)) {
        $text = @(
            'Off-machine vault for the production credential data-encryption key.',
            'The sealed file is DPAPI CurrentUser ciphertext.',
            'Do not copy this directory into a git repository.',
            'An empty vault is not a key backup.'
        ) -join "`r`n"
        [System.IO.File]::WriteAllText($marker, $text)
        Set-CurrentUserOnlyAcl -Path $marker
    }
    $VaultRoot
}

function Get-IcaclsAccounts {
    param([Parameter(Mandatory = $true)][string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    $lines = @( & icacls.exe $full )
    $accounts = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -match 'Successfully processed') { continue }
        $ace = $line
        if ($ace.StartsWith($full)) {
            $ace = $ace.Substring($full.Length).Trim()
        } else {
            $ace = $ace.Trim()
        }
        if ($ace -match '^(?<account>.+?):\(') {
            $accounts.Add($Matches.account.Trim())
        }
    }
    return @($accounts)
}

function Test-ProductionCredentialVaultAcl {
    param([string]$VaultRoot = (Get-ProductionCredentialKeyVaultRoot))
    if (-not (Test-Path -LiteralPath $VaultRoot)) { return $false }
    $accounts = @(Get-IcaclsAccounts -Path $VaultRoot)
    $me = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    if ($accounts.Count -ne 1) { return $false }
    return ($accounts[0] -eq $me)
}

function Remove-PlaintextFile {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $info = Get-Item -LiteralPath $Path
    if ($info.Length -le 1MB) {
        $zeros = New-Object byte[] $info.Length
        [System.IO.File]::WriteAllBytes($Path, $zeros)
        Clear-Bytes $zeros
    }
    Remove-Item -LiteralPath $Path -Force
}

function Read-ProductionCredentialKeyFile {
    param([Parameter(Mandatory = $true)][string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    $repo = (Get-ProductionCredentialRepoRoot).TrimEnd('\') + '\'
    if ($full.StartsWith($repo, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'
    }
    $raw = [System.IO.File]::ReadAllBytes($full)
    try {
        if ($raw.Length -eq 0 -or $raw.Length -gt 4096) {
            throw 'PRODUCTION_CREDENTIAL_KEY_INVALID'
        }
        $text = [System.Text.Encoding]::UTF8.GetString($raw)
        if ($text.Contains([char]0xFEFF)) {
            throw 'PRODUCTION_CREDENTIAL_KEY_INVALID'
        }
        $lines = @($text.Split([string[]]@("`n"), [System.StringSplitOptions]::None) | ForEach-Object { $_.TrimEnd("`r") })
        if ($lines.Count -gt 0 -and $lines[-1] -eq '') {
            $lines = @($lines[0..($lines.Count - 2)])
        }
        if ($lines.Count -ne 2) {
            throw 'PRODUCTION_CREDENTIAL_KEY_INVALID'
        }
        if (-not $lines[0].StartsWith($script:KeyIdPrefix) -or -not $lines[1].StartsWith($script:KeyPrefix)) {
            throw 'PRODUCTION_CREDENTIAL_KEY_MISSING'
        }
        $keyId = $lines[0].Substring($script:KeyIdPrefix.Length)
        $b64 = $lines[1].Substring($script:KeyPrefix.Length)
        if ([string]::IsNullOrWhiteSpace($keyId) -or $keyId -match '[\s\p{C}]') {
            throw 'PRODUCTION_CREDENTIAL_KEY_INVALID'
        }
        try {
            $material = [Convert]::FromBase64String($b64.Trim())
        } catch {
            throw 'PRODUCTION_CREDENTIAL_KEY_INVALID'
        }
        if ($material.Length -ne 32) {
            Clear-Bytes $material
            throw 'PRODUCTION_CREDENTIAL_KEY_INVALID'
        }
        $payload = [System.Text.Encoding]::UTF8.GetBytes(($lines[0] + "`n" + $lines[1] + "`n"))
        return [pscustomobject]@{
            KeyId = $keyId
            Material = $material
            Payload = $payload
        }
    } finally {
        Clear-Bytes $raw
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($Bytes)
        try {
            return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
        } finally {
            Clear-Bytes $hash
        }
    } finally {
        $sha.Dispose()
    }
}

function Protect-ProductionCredentialKeyMaterial {
    param(
        [Parameter(Mandatory = $true)][string]$PlaintextPath,
        [string]$VaultRoot = (Get-ProductionCredentialKeyVaultRoot)
    )
    $parsed = $null
    $sealed = $null
    try {
        $parsed = Read-ProductionCredentialKeyFile -Path $PlaintextPath
        $null = Initialize-ProductionCredentialKeyVault -VaultRoot $VaultRoot
        $sealedPath = Join-Path $VaultRoot $script:SealedFileName
        if (Test-Path -LiteralPath $sealedPath) {
            throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS'
        }
        $sealed = [System.Security.Cryptography.ProtectedData]::Protect(
            $parsed.Payload,
            $null,
            [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
        [System.IO.File]::WriteAllBytes($sealedPath, $sealed)
        Set-CurrentUserOnlyAcl -Path $sealedPath
        $manifestPath = Join-Path $VaultRoot $script:ManifestFileName
        $manifest = @(
            'format=production-credential-key-dpapi-v1'
            ('keyId=' + $parsed.KeyId)
            'materialBytes=32'
            ('sealedBytes=' + $sealed.Length)
            ('sealedSha256=' + (Get-Sha256Hex -Bytes $sealed))
            'scope=CurrentUser'
            ('user=' + [System.Security.Principal.WindowsIdentity]::GetCurrent().Name)
            ('machine=' + $env:COMPUTERNAME)
            ('sealedAtUtc=' + [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))
        ) -join "`n"
        [System.IO.File]::WriteAllText($manifestPath, $manifest + "`n")
        Set-CurrentUserOnlyAcl -Path $manifestPath
        Remove-PlaintextFile -Path $PlaintextPath
        if (Test-Path -LiteralPath $PlaintextPath) {
            throw 'PRODUCTION_CREDENTIAL_KEY_PLAINTEXT_REMAINS'
        }
        return 'PRODUCTION_CREDENTIAL_KEY_BACKUP_SEALED'
    } finally {
        if ($null -ne $parsed) {
            Clear-Bytes $parsed.Material
            Clear-Bytes $parsed.Payload
        }
        Clear-Bytes $sealed
    }
}

function Open-SealedPayload {
    param([Parameter(Mandatory = $true)][string]$VaultRoot)
    $sealedPath = Join-Path $VaultRoot $script:SealedFileName
    if (-not (Test-Path -LiteralPath $sealedPath)) {
        throw 'PRODUCTION_CREDENTIAL_KEY_MISSING'
    }
    $blob = [System.IO.File]::ReadAllBytes($sealedPath)
    try {
        return [System.Security.Cryptography.ProtectedData]::Unprotect(
            $blob,
            $null,
            [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    } catch [System.Security.Cryptography.CryptographicException] {
        throw 'PRODUCTION_CREDENTIAL_KEY_INVALID'
    } finally {
        Clear-Bytes $blob
    }
}

function Test-MaterialEqual {
    param([byte[]]$Left, [byte[]]$Right)
    if ($null -eq $Left -or $null -eq $Right -or $Left.Length -ne $Right.Length) { return $false }
    $diff = 0
    for ($i = 0; $i -lt $Left.Length; $i++) {
        $diff = $diff -bor ($Left[$i] -bxor $Right[$i])
    }
    return ($diff -eq 0)
}

function Open-ProductionCredentialKeyMaterial {
    param(
        [string]$VaultRoot = (Get-ProductionCredentialKeyVaultRoot),
        [string]$ComparePlaintextPath
    )
    $payload = $null
    $opened = $null
    $other = $null
    try {
        $payload = Open-SealedPayload -VaultRoot $VaultRoot
        $temp = Join-Path $env:TEMP ('kit-credential-open-' + [guid]::NewGuid().ToString('n') + '.env')
        try {
            [System.IO.File]::WriteAllBytes($temp, $payload)
            Set-CurrentUserOnlyAcl -Path $temp
            $opened = Read-ProductionCredentialKeyFile -Path $temp
        } finally {
            Remove-PlaintextFile -Path $temp
        }
        $manifestPath = Join-Path $VaultRoot $script:ManifestFileName
        if (Test-Path -LiteralPath $manifestPath) {
            $manifestId = $null
            foreach ($line in [System.IO.File]::ReadLines($manifestPath)) {
                if ($line.StartsWith('keyId=')) { $manifestId = $line.Substring(6) }
            }
            if ($manifestId -and $manifestId -ne $opened.KeyId) {
                return 'PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH'
            }
        }
        if ([string]::IsNullOrWhiteSpace($ComparePlaintextPath)) {
            return 'PRODUCTION_CREDENTIAL_KEY_OK'
        }
        $other = Read-ProductionCredentialKeyFile -Path $ComparePlaintextPath
        if ($other.KeyId -eq $opened.KeyId -and (Test-MaterialEqual $other.Material $opened.Material)) {
            return 'PRODUCTION_CREDENTIAL_KEY_RESTORE_OK'
        }
        return 'PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH'
    } finally {
        Clear-Bytes $payload
        if ($null -ne $opened) {
            Clear-Bytes $opened.Material
            Clear-Bytes $opened.Payload
        }
        if ($null -ne $other) {
            Clear-Bytes $other.Material
            Clear-Bytes $other.Payload
        }
    }
}

function Export-ProductionCredentialKeyPlaintext {
    param(
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [string]$VaultRoot = (Get-ProductionCredentialKeyVaultRoot)
    )
    $destFull = [System.IO.Path]::GetFullPath($DestinationPath)
    $tempRoot = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    if (-not $destFull.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'
    }
    $repo = (Get-ProductionCredentialRepoRoot).TrimEnd('\') + '\'
    if ($destFull.StartsWith($repo, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'
    }
    $payload = Open-SealedPayload -VaultRoot $VaultRoot
    try {
        [System.IO.File]::WriteAllBytes($destFull, $payload)
        Set-CurrentUserOnlyAcl -Path $destFull
        return 'PRODUCTION_CREDENTIAL_KEY_EXPORT_WRITTEN'
    } finally {
        Clear-Bytes $payload
    }
}

function Test-ProductionCredentialRemoteStagePath {
    param([Parameter(Mandatory = $true)][string]$RemotePath)
    return $RemotePath -match '^/run/kit-production-credential-pull/key-[0-9a-f]{32}\.env$'
}

function Test-KnownSshHost {
    param([Parameter(Mandatory = $true)][string]$HostName)
    $path = Join-Path $env:USERPROFILE '.ssh\known_hosts'
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    foreach ($line in [System.IO.File]::ReadLines($path)) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { continue }
        $field = ($line -split '\s+', 2)[0]
        foreach ($name in ($field -split ',')) {
            if ($name -eq $HostName) { return $true }
        }
    }
    return $false
}

function Test-ProductionCredentialPullPrecheck {
    param(
        [Parameter(Mandatory = $true)][string]$SshUser,
        [Parameter(Mandatory = $true)][string]$SshHost,
        [Parameter(Mandatory = $true)][string]$RemotePath
    )
    if ($SshUser -notmatch '^[A-Za-z0-9._-]+$') {
        throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'
    }
    if ($SshHost -notmatch '^[A-Za-z0-9._-]+$') {
        throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'
    }
    $blocked = @('localhost', '127.0.0.1', '::1')
    if (-not [string]::IsNullOrWhiteSpace($env:COMPUTERNAME)) {
        $blocked += $env:COMPUTERNAME
    }
    try {
        $addresses = @(Get-NetIPAddress -ErrorAction Stop | Select-Object -ExpandProperty IPAddress)
        foreach ($address in $addresses) {
            if (-not [string]::IsNullOrWhiteSpace($address)) { $blocked += $address }
        }
    } catch {
        # Address lookup is optional. The named local hosts above still fail closed.
    }
    foreach ($item in $blocked) {
        if ($item -eq $SshHost) {
            throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE'
        }
    }
    if (-not (Test-ProductionCredentialRemoteStagePath -RemotePath $RemotePath)) {
        throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'
    }
    if (-not (Test-KnownSshHost -HostName $SshHost)) {
        throw 'PRODUCTION_CREDENTIAL_KEY_PULL_HOSTKEY_UNVERIFIED'
    }
    return 'WINDOWS_KEY_PULL_PRECHECK_OK'
}

function Invoke-ProductionCredentialKeyBackupFixture {
    $vault = Join-Path $env:TEMP ('kit-credential-fixture-' + [guid]::NewGuid().ToString('n'))
    $plainA = Join-Path $env:TEMP ('kit-credential-fixture-a-' + [guid]::NewGuid().ToString('n') + '.env')
    $plainB = Join-Path $env:TEMP ('kit-credential-fixture-b-' + [guid]::NewGuid().ToString('n') + '.env')
    $short = Join-Path $env:TEMP ('kit-credential-fixture-short-' + [guid]::NewGuid().ToString('n') + '.env')
    $repoFile = Join-Path (Get-ProductionCredentialRepoRoot) ('fixture-key-' + [guid]::NewGuid().ToString('n') + '.env')
    $material = $null
    $codes = New-Object System.Collections.Generic.List[string]
    try {
        $material = New-Object byte[] 32
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($material) } finally { $rng.Dispose() }
        $b64 = [Convert]::ToBase64String($material)
        $body = "ProductionCredentials__DataEncryptionKeyId=fixture-not-production`nProductionCredentials__DataEncryptionKey=$b64`n"
        $payload = [System.Text.Encoding]::UTF8.GetBytes($body)
        [System.IO.File]::WriteAllBytes($plainA, $payload)
        [System.IO.File]::WriteAllBytes($plainB, $payload)
        Set-CurrentUserOnlyAcl -Path $plainA
        Set-CurrentUserOnlyAcl -Path $plainB
        $sealedCode = Protect-ProductionCredentialKeyMaterial -PlaintextPath $plainA -VaultRoot $vault
        if ($sealedCode -ne 'PRODUCTION_CREDENTIAL_KEY_BACKUP_SEALED') {
            throw 'FIXTURE_SEAL_FAILED'
        }
        if (Test-Path -LiteralPath $plainA) { throw 'FIXTURE_PLAINTEXT_REMAINS' }
        $compareCode = Open-ProductionCredentialKeyMaterial -VaultRoot $vault -ComparePlaintextPath $plainB
        if ($compareCode -ne 'PRODUCTION_CREDENTIAL_KEY_RESTORE_OK') {
            throw 'FIXTURE_RESTORE_FAILED'
        }
        $codes.Add('FIXTURE_RESTORE_OK')

        $sealedPath = Join-Path $vault $script:SealedFileName
        $blob = [System.IO.File]::ReadAllBytes($sealedPath)
        try {
            $blob[$blob.Length - 1] = $blob[$blob.Length - 1] -bxor 0x01
            $tampered = $false
            try {
                $null = [System.Security.Cryptography.ProtectedData]::Unprotect(
                    $blob,
                    $null,
                    [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
            } catch [System.Security.Cryptography.CryptographicException] {
                $tampered = $true
            }
            if (-not $tampered) { throw 'FIXTURE_TAMPER_ACCEPTED' }
            $codes.Add('FIXTURE_TAMPER_REJECTED')
        } finally {
            Clear-Bytes $blob
        }

        $shortMaterial = New-Object byte[] 16
        try {
            [System.IO.File]::WriteAllBytes($short, [System.Text.Encoding]::UTF8.GetBytes(
                "ProductionCredentials__DataEncryptionKeyId=fixture-not-production`nProductionCredentials__DataEncryptionKey=$([Convert]::ToBase64String($shortMaterial))`n"))
            $rejected = $false
            try { $null = Read-ProductionCredentialKeyFile -Path $short } catch { if ($_.Exception.Message -eq 'PRODUCTION_CREDENTIAL_KEY_INVALID') { $rejected = $true } }
            if (-not $rejected) { throw 'FIXTURE_SHORT_KEY_ACCEPTED' }
            $codes.Add('FIXTURE_SHORT_KEY_REJECTED')
        } finally {
            Clear-Bytes $shortMaterial
        }

        $repoRejected = $false
        try { $null = Read-ProductionCredentialKeyFile -Path $repoFile } catch { if ($_.Exception.Message -eq 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED') { $repoRejected = $true } }
        if (-not $repoRejected) { throw 'FIXTURE_REPO_PATH_ACCEPTED' }
        $codes.Add('FIXTURE_REPO_PATH_REJECTED')
        if (Test-Path -LiteralPath $repoFile) { throw 'FIXTURE_REPO_FILE_CREATED' }

        return @($codes)
    } finally {
        Clear-Bytes $material
        foreach ($path in @($plainA, $plainB, $short)) {
            if (Test-Path -LiteralPath $path) { Remove-PlaintextFile -Path $path }
        }
        if (Test-Path -LiteralPath $vault) { Remove-Item -LiteralPath $vault -Recurse -Force }
        if (Test-Path -LiteralPath $repoFile) { Remove-Item -LiteralPath $repoFile -Force }
    }
}

function Undo-ProductionCredentialKeyVault {
    param([string]$VaultRoot = (Get-ProductionCredentialKeyVaultRoot))
    if (-not (Test-Path -LiteralPath $VaultRoot)) {
        return 'WINDOWS_KEY_VAULT_ABSENT'
    }
    $sealedPath = Join-Path $VaultRoot $script:SealedFileName
    if (Test-Path -LiteralPath $sealedPath) {
        throw 'PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS'
    }
    Remove-Item -LiteralPath $VaultRoot -Recurse -Force
    return 'WINDOWS_KEY_VAULT_REMOVED'
}

Export-ModuleMember -Function @(
    'Get-ProductionCredentialKeyVaultRoot',
    'Initialize-ProductionCredentialKeyVault',
    'Test-ProductionCredentialVaultAcl',
    'Protect-ProductionCredentialKeyMaterial',
    'Open-ProductionCredentialKeyMaterial',
    'Export-ProductionCredentialKeyPlaintext',
    'Test-ProductionCredentialRemoteStagePath',
    'Test-ProductionCredentialPullPrecheck',
    'Invoke-ProductionCredentialKeyBackupFixture',
    'Undo-ProductionCredentialKeyVault'
)

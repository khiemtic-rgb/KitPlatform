# Starts a loopback validation API and a separate admin UI for Second Brain Trading.
# Does not change the default Vite proxy, appsettings, or any process other than
# one previously started by this script.
$ErrorActionPreference = 'Stop'
$Root = 'E:\KitPlatform'
$Dev = Join-Path $Root '.dev\sb-trading-ui'
$ApiOut = Join-Path $Dev 'api'
$PidFile = Join-Path $Dev 'pids.json'
$AccessFile = Join-Path $Dev 'local-access.txt'
$Psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$ProtectedPid = 11404

function Restore-Env([hashtable]$Saved) {
    foreach ($name in $Saved.Keys) {
        [Environment]::SetEnvironmentVariable($name, $Saved[$name], 'Process')
    }
}

Set-Location $Root
$ignored = git -C $Root check-ignore -- '.dev/sb-trading-ui/local-access.txt'
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ignored)) {
    throw 'Refusing to write credentials because .dev/sb-trading-ui/local-access.txt is not gitignored.'
}

if (Get-Process -Id $ProtectedPid -ErrorAction SilentlyContinue) {
    Write-Host "Protected API PID $ProtectedPid is running and will be left untouched."
}

& (Join-Path $PSScriptRoot 'second-brain-trading-ui-stop.ps1')

New-Item -ItemType Directory -Force -Path $Dev | Out-Null
if (-not (Test-Path -LiteralPath $Psql)) { throw "psql was not found at $Psql" }
$viteJs = Join-Path $Root 'client\admin\node_modules\vite\bin\vite.js'
if (-not (Test-Path -LiteralPath $viteJs)) { throw 'client/admin/node_modules/vite is missing. Run npm install in client/admin first.' }

function Get-FreeLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Test-PortFree([int]$Port) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
    try {
        $listener.Start()
        return $true
    } catch {
        return $false
    } finally {
        if ($listener.Server.IsBound) { $listener.Stop() }
    }
}

$apiPort = 0
for ($i = 0; $i -lt 20 -and $apiPort -eq 0; $i++) {
    $candidate = Get-FreeLoopbackPort
    if ($candidate -notin 5290, 7224, 5173, 5174, 5432, 5433) { $apiPort = $candidate }
}
if ($apiPort -eq 0) { throw 'No free loopback port for the validation API.' }

$uiPort = 5174
if (-not (Test-PortFree $uiPort)) {
    $uiPort = Get-FreeLoopbackPort
    if ($uiPort -in 5290, 7224, 5432, 5433) { throw 'No free UI port away from the shared API and database.' }
}

if (-not (Test-Path -LiteralPath $AccessFile)) {
    $alphabet = (48..57) + (65..90) + (97..122)
    $password = -join ($alphabet | Get-Random -Count 24 | ForEach-Object { [char]$_ })
    $access = @"
tenant=PI_A
approve_username=pi-ui-approve
run_username=pi-ui-run
module_off_tenant=PI_B
module_off_username=pi-ui-b
password=$password
"@
    [System.IO.File]::WriteAllText($AccessFile, $access.Trim() + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}
$accessMap = @{}
foreach ($line in Get-Content -LiteralPath $AccessFile) {
    $pair = $line.Split('=', 2)
    if ($pair.Count -eq 2) { $accessMap[$pair[0]] = $pair[1] }
}
$password = [string]$accessMap['password']
if ([string]::IsNullOrWhiteSpace($password) -or $password.Length -lt 16) {
    throw 'local-access.txt does not contain a usable password.'
}

$sqlPath = Join-Path $Dev 'ensure-accounts.sql'
$sql = @"
DO `$`$
BEGIN
    IF current_database() <> 'kit_pi_validation' THEN
        RAISE EXCEPTION 'refusing database %', current_database();
    END IF;
    IF inet_server_port() <> 5433 THEN
        RAISE EXCEPTION 'refusing port %', inet_server_port();
    END IF;
END
`$`$;

WITH desired(username, email, tenant_code) AS (
    VALUES
        ('pi-ui-approve', 'pi-ui-approve@validation.local', 'PI_A'),
        ('pi-ui-run', 'pi-ui-run@validation.local', 'PI_A'),
        ('pi-ui-b', 'pi-ui-b@validation.local', 'PI_B')
),
hashed AS (
    SELECT crypt('$($password.Replace("'", "''"))', gen_salt('bf', 11)) AS password_hash
)
INSERT INTO users (tenant_id, username, email, password_hash, status)
SELECT t.id, d.username, d.email, h.password_hash, 1
FROM desired d
JOIN tenants t ON t.tenant_code = d.tenant_code AND t.deleted_at IS NULL AND t.status = 1
CROSS JOIN hashed h
ON CONFLICT (tenant_id, username) DO UPDATE
    SET password_hash = EXCLUDED.password_hash,
        email = EXCLUDED.email,
        status = 1,
        deleted_at = NULL;

INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u
JOIN tenants t ON t.id = u.tenant_id
JOIN roles r ON r.tenant_id = t.id
WHERE (t.tenant_code = 'PI_A' AND u.username = 'pi-ui-approve' AND r.role_code IN ('PI_TRADING', 'PI_APPROVE'))
   OR (t.tenant_code = 'PI_A' AND u.username = 'pi-ui-run' AND r.role_code = 'PI_TRADING')
   OR (t.tenant_code = 'PI_B' AND u.username = 'pi-ui-b' AND r.role_code = 'PI_TRADING')
ON CONFLICT ON CONSTRAINT uq_user_roles DO NOTHING;

INSERT INTO public.permissions (id, permission_code, permission_name, module_name)
VALUES
    ('e1111111-1111-1111-1111-111111111101', 'personal_intelligence.exchange.connect', 'Quản lý kết nối API sàn', 'personal_intelligence'),
    ('e1111111-1111-1111-1111-111111111102', 'personal_intelligence.exchange.read', 'Xem tài khoản sàn đã kết nối', 'personal_intelligence')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO public.roles (id, tenant_id, role_code, role_name)
SELECT 'e2222222-2222-2222-2222-222222222201'::uuid, t.id, 'PI_EXCHANGE', 'PI exchange connection'
FROM public.tenants t
WHERE t.tenant_code = 'PI_A' AND t.deleted_at IS NULL
ON CONFLICT (tenant_id, role_code) DO NOTHING;

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
JOIN public.tenants t ON t.id = r.tenant_id AND t.tenant_code = 'PI_A'
JOIN public.permissions p ON p.permission_code IN (
    'personal_intelligence.exchange.connect',
    'personal_intelligence.exchange.read')
WHERE r.role_code = 'PI_EXCHANGE'
ON CONFLICT DO NOTHING;

INSERT INTO public.user_roles (user_id, role_id)
SELECT u.id, r.id
FROM public.users u
JOIN public.tenants t ON t.id = u.tenant_id AND t.tenant_code = 'PI_A'
JOIN public.roles r ON r.tenant_id = t.id AND r.role_code = 'PI_EXCHANGE'
WHERE u.username = 'pi-ui-approve'
ON CONFLICT ON CONSTRAINT uq_user_roles DO NOTHING;

SELECT current_database() AS db, inet_server_port() AS port, u.username, t.tenant_code,
       COALESCE(array_agg(DISTINCT r.role_code) FILTER (WHERE r.role_code IS NOT NULL), '{}') AS roles,
       COALESCE(array_agg(DISTINCT p.permission_code) FILTER (WHERE p.permission_code IS NOT NULL), '{}') AS permissions
FROM users u
JOIN tenants t ON t.id = u.tenant_id
LEFT JOIN user_roles ur ON ur.user_id = u.id
LEFT JOIN roles r ON r.id = ur.role_id
LEFT JOIN role_permissions rp ON rp.role_id = r.id
LEFT JOIN permissions p ON p.id = rp.permission_id
WHERE u.username IN ('pi-ui-approve', 'pi-ui-run', 'pi-ui-b')
GROUP BY u.username, t.tenant_code
ORDER BY t.tenant_code, u.username;
"@
[System.IO.File]::WriteAllText($sqlPath, $sql, [System.Text.UTF8Encoding]::new($false))
try {
    & $Psql -h 127.0.0.1 -p 5433 -U pi_validation -d kit_pi_validation -v ON_ERROR_STOP=1 -f $sqlPath
    if ($LASTEXITCODE -ne 0) { throw 'Account setup failed.' }
} finally {
    Remove-Item -LiteralPath $sqlPath -Force -ErrorAction SilentlyContinue
}

Write-Host 'Building validation API into .dev/sb-trading-ui/api'
dotnet build (Join-Path $Root 'src\KitPlatform.Api\KitPlatform.Api.csproj') -c Debug -o $ApiOut --nologo
if ($LASTEXITCODE -ne 0) { throw 'Validation API build failed.' }
$dll = Join-Path $ApiOut 'KitPlatform.Api.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Validation API output is missing.' }

$secretBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($secretBytes)
$jwtSecret = [Convert]::ToBase64String($secretBytes)
$keyFile = Join-Path $Dev 'exchange-key.local'
$ignoredKey = git -C $Root check-ignore -- '.dev/sb-trading-ui/exchange-key.local'
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ignoredKey)) {
    throw 'Refusing to write the exchange encryption key because it is not gitignored.'
}
if (-not (Test-Path -LiteralPath $keyFile)) {
    $keyBytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($keyBytes)
    $keyText = "pi-exchange-v1`n" + [Convert]::ToBase64String($keyBytes) + "`n"
    [System.IO.File]::WriteAllText($keyFile, $keyText, [System.Text.UTF8Encoding]::new($false))
}
$keyLines = [System.IO.File]::ReadAllLines($keyFile)
if ($keyLines.Length -lt 2 -or [string]::IsNullOrWhiteSpace($keyLines[0]) -or [string]::IsNullOrWhiteSpace($keyLines[1])) {
    throw 'Exchange encryption key file is incomplete. Not regenerating it.'
}
$exchangeKeyId = $keyLines[0].Trim()
$exchangeKey = $keyLines[1].Trim()
$savedEnv = @{
    ASPNETCORE_ENVIRONMENT = [Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Process')
    ASPNETCORE_URLS = [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS', 'Process')
    'ConnectionStrings__Default' = [Environment]::GetEnvironmentVariable('ConnectionStrings__Default', 'Process')
    'Jwt__Secret' = [Environment]::GetEnvironmentVariable('Jwt__Secret', 'Process')
    'Jwt__Issuer' = [Environment]::GetEnvironmentVariable('Jwt__Issuer', 'Process')
    'Jwt__Audience' = [Environment]::GetEnvironmentVariable('Jwt__Audience', 'Process')
    'PersonalIntelligence__ExchangeCredentialKeyId' = [Environment]::GetEnvironmentVariable('PersonalIntelligence__ExchangeCredentialKeyId', 'Process')
    'PersonalIntelligence__ExchangeCredentialKey' = [Environment]::GetEnvironmentVariable('PersonalIntelligence__ExchangeCredentialKey', 'Process')
    SB_TRADING_API_URL = [Environment]::GetEnvironmentVariable('SB_TRADING_API_URL', 'Process')
    SB_TRADING_UI_PORT = [Environment]::GetEnvironmentVariable('SB_TRADING_UI_PORT', 'Process')
}
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$apiPort"
    $env:ConnectionStrings__Default = 'Host=127.0.0.1;Port=5433;Database=kit_pi_validation;Username=pi_validation;Application Name=sb-trading-ui'
    $env:Jwt__Secret = $jwtSecret
    $env:Jwt__Issuer = 'PiValidationLocal'
    $env:Jwt__Audience = 'KitPlatform.Client'
    $env:PersonalIntelligence__ExchangeCredentialKeyId = $exchangeKeyId
    $env:PersonalIntelligence__ExchangeCredentialKey = $exchangeKey
    $apiProc = Start-Process -FilePath 'dotnet' -ArgumentList "`"$dll`"" -WorkingDirectory $ApiOut -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $Dev 'api.log') -RedirectStandardError (Join-Path $Dev 'api.err.log')
} finally {
    Restore-Env $savedEnv
}

$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    if ($apiProc.HasExited) { break }
    try {
        $health = Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:$apiPort/api/health" -TimeoutSec 2
        $dbHealth = Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:$apiPort/api/health/db" -TimeoutSec 2
        if ($health.StatusCode -eq 200 -and $dbHealth.StatusCode -eq 200) { $ready = $true; break }
    } catch { }
}
if (-not $ready) {
    if (-not $apiProc.HasExited) { Stop-Process -Id $apiProc.Id -Force }
    throw "Validation API did not become ready. See $Dev\api.err.log"
}

$activity = & $Psql -h 127.0.0.1 -p 5433 -U pi_validation -d kit_pi_validation -tA -c "SELECT datname || '|' || COALESCE(client_addr::text, '') FROM pg_stat_activity WHERE application_name = 'sb-trading-ui' AND datname = 'kit_pi_validation' LIMIT 1;"
if ($LASTEXITCODE -ne 0 -or [string]$activity -notlike 'kit_pi_validation|*') {
    Stop-Process -Id $apiProc.Id -Force
    throw 'Validation API is not connected to kit_pi_validation. Stopped that process.'
}

$env:SB_TRADING_API_URL = "http://127.0.0.1:$apiPort"
$env:SB_TRADING_UI_PORT = "$uiPort"
try {
    $uiProc = Start-Process -FilePath 'node' -ArgumentList "`"$viteJs`" --config vite.second-brain.config.ts --host 127.0.0.1 --port $uiPort" `
        -WorkingDirectory (Join-Path $Root 'client\admin') -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $Dev 'ui.log') -RedirectStandardError (Join-Path $Dev 'ui.err.log')
} finally {
    Remove-Item Env:SB_TRADING_API_URL -ErrorAction SilentlyContinue
    Remove-Item Env:SB_TRADING_UI_PORT -ErrorAction SilentlyContinue
}

$uiReady = $false
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    if ($uiProc.HasExited) { break }
    try {
        $page = Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:$uiPort/login" -TimeoutSec 2
        if ($page.StatusCode -eq 200) { $uiReady = $true; break }
    } catch { }
}
if (-not $uiReady) {
    Stop-Process -Id $apiProc.Id -Force -ErrorAction SilentlyContinue
    if (-not $uiProc.HasExited) { Stop-Process -Id $uiProc.Id -Force }
    throw "Admin UI did not become ready. See $Dev\ui.err.log"
}

$state = @{
    apiPid = $apiProc.Id
    uiPid = $uiProc.Id
    apiPort = $apiPort
    uiPort = $uiPort
    apiUrl = "http://127.0.0.1:$apiPort"
    uiUrl = "http://127.0.0.1:$uiPort"
} | ConvertTo-Json
[System.IO.File]::WriteAllText($PidFile, $state, [System.Text.UTF8Encoding]::new($false))

Write-Host "UI: http://127.0.0.1:$uiPort/login"
Write-Host "Validation API: http://127.0.0.1:$apiPort"
Write-Host "Tenant PI_A users: pi-ui-approve (run, approve, exchange connect), pi-ui-run (run only)"
Write-Host "Tenant PI_B user: pi-ui-b (run only, module off)"
Write-Host "Password file: $AccessFile"
Write-Host "Logs: $Dev\api.log and $Dev\ui.log"
Write-Host "Stop: powershell -File $Root\scripts\second-brain-trading-ui-stop.ps1"

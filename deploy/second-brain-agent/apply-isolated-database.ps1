# Apply the isolated Second Brain schema.
# Refuses every database except second_brain_agent_v1 on 127.0.0.1:5433.
param(
    [string]$Psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe",
    [string]$HostName = "127.0.0.1",
    [int]$Port = 5433,
    [string]$User = "pi_validation",
    [string]$Database = "second_brain_agent_v1"
)

$ErrorActionPreference = "Stop"
if ($Database -ne "second_brain_agent_v1") { throw "Refusing database $Database" }
if ($Port -eq 5432) { throw "Refusing port 5432" }
if ($HostName -ne "127.0.0.1" -and $HostName -ne "localhost") { throw "Refusing host $HostName" }

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$files = @(
    (Join-Path $PSScriptRoot "000_identity_minimum.sql"),
    (Join-Path $root "migrations\385_pack_personal_intelligence_trading_ledger_v1.sql"),
    (Join-Path $root "migrations\387_pack_personal_intelligence_loop_v1.sql"),
    (Join-Path $root "migrations\388_pack_personal_intelligence_loop_integrity_v1.sql"),
    (Join-Path $root "migrations\389_pack_personal_intelligence_operation_v1.sql"),
    (Join-Path $root "migrations\390_platform_module_personal_intelligence.sql"),
    (Join-Path $root "migrations\391_pack_personal_intelligence_exchange_connection_v1.sql"),
    (Join-Path $root "migrations\392_pack_personal_intelligence_exchange_execution_sync_v1.sql"),
    (Join-Path $root "migrations\393_pack_personal_intelligence_exchange_backfill_v1.sql"),
    (Join-Path $root "migrations\399_pack_personal_intelligence_task_v1.sql")
)
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing $file" }
}

$probe = & $Psql -h $HostName -p $Port -U $User -d postgres -tA -c "SELECT current_database() || '|' || inet_server_port()"
if ($LASTEXITCODE -ne 0) { throw "Maintenance connection failed" }
if ($probe.Trim() -ne "postgres|$Port") { throw "Unexpected maintenance target $probe" }

$exists = & $Psql -h $HostName -p $Port -U $User -d postgres -tA -c "SELECT 1 FROM pg_database WHERE datname = 'second_brain_agent_v1'"
$existsText = "$(if ($null -eq $exists) { '' } else { $exists })".Trim()
if ($existsText -ne "1") {
    & $Psql -h $HostName -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE second_brain_agent_v1"
    if ($LASTEXITCODE -ne 0) { throw "CREATE DATABASE failed" }
}

$check = & $Psql -h $HostName -p $Port -U $User -d second_brain_agent_v1 -tA -c "SELECT current_database() || '|' || inet_server_port()"
if ($check.Trim() -ne "second_brain_agent_v1|$Port") { throw "Refusing to apply schema on $check" }
$ready = & $Psql -h $HostName -p $Port -U $User -d second_brain_agent_v1 -tA -c "SELECT to_regclass('pack_personal_intelligence.pi_task') IS NOT NULL"
if ("$ready".Trim() -eq "t") {
    Write-Host "schema already present on second_brain_agent_v1"
    exit 0
}

foreach ($file in $files) {
    Write-Host "apply $(Split-Path -Leaf $file)"
    & $Psql -h $HostName -p $Port -U $User -d second_brain_agent_v1 -v ON_ERROR_STOP=1 -f $file
    if ($LASTEXITCODE -ne 0) { throw "Failed $(Split-Path -Leaf $file)" }
}

Write-Host "schema ready on second_brain_agent_v1"

param(
    [Parameter(Mandatory)] [int]$Port,
    [Parameter(Mandatory)] [string]$Source,
    [Parameter(Mandatory)] [string]$Database,
    [string]$DbHost = "127.0.0.1",
    [string]$User = "pgadmin",
    [string]$Role = "novixa_iso",
    [string]$Psql = "psql",
    [string]$PgDump = "pg_dump"
)
# Tenant-free database for KitPlatform.Platform.Tests isolated suites (OTC gate, W5 knowledge).
# Source = a database built by build-pharmacy-test-db.ps1 -NoSeed. Copies the schema plus the global
# pharmacy_* reference tables (no tenant_id column); no tenant rows are copied. Then grants $Role.
$ErrorActionPreference = "Continue"
$env:PGOPTIONS = "-c client_min_messages=warning"
if ($Port -eq 5432) { throw "Refusing port 5432 (dev cluster)." }
foreach ($db in @($Source, $Database)) {
    if ($db -in @("kitplatform", "pharmacore", "postgres") -or $db -match "prod") { throw "Refusing database '$db'." }
}

$conn = @("-h", $DbHost, "-p", "$Port", "-U", $User)
$work = Join-Path ([IO.Path]::GetTempPath()) "kit-platform-iso-$Database"
New-Item -ItemType Directory -Force -Path $work | Out-Null
$schemaFile = Join-Path $work "schema.sql"
$dataFile = Join-Path $work "reference-data.sql"

& $Psql @conn -d postgres -q -v ON_ERROR_STOP=1 `
    -c "DROP DATABASE IF EXISTS $Database" `
    -c "CREATE DATABASE $Database"
if ($LASTEXITCODE -ne 0) { throw "Could not create $Database." }

& $PgDump @conn -d $Source --schema-only --no-owner --no-privileges -f $schemaFile
if ($LASTEXITCODE -ne 0) { throw "Schema dump failed." }
& $Psql @conn -d $Database -q -v ON_ERROR_STOP=1 -f $schemaFile | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Schema restore failed." }

$tables = & $Psql @conn -d $Source -tA -c @"
SELECT c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relname LIKE 'pharmacy\_%'
  AND NOT EXISTS (
      SELECT 1 FROM information_schema.columns k
      WHERE k.table_schema = 'public' AND k.table_name = c.relname AND k.column_name = 'tenant_id')
ORDER BY 1
"@
if ($LASTEXITCODE -ne 0) { throw "Could not list reference tables." }
$tableArgs = @($tables | Where-Object { $_ } | ForEach-Object { "--table=public.$_" })

& $PgDump @conn -d $Source --data-only --no-owner --no-privileges --disable-triggers @tableArgs -f $dataFile
if ($LASTEXITCODE -ne 0) { throw "Reference data dump failed." }
& $Psql @conn -d $Database -q -v ON_ERROR_STOP=1 -f $dataFile | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Reference data restore failed." }

$tenants = & $Psql @conn -d $Database -tA -c "SELECT COUNT(*) FROM tenants"
if ("$tenants".Trim() -ne "0") { throw "Isolated database must not contain tenants (found $tenants)." }

& (Join-Path $PSScriptRoot "grant-isolated-test-role.ps1") -Port $Port -Database $Database -DbHost $DbHost -User $User -Role $Role -Psql $Psql
if ($LASTEXITCODE -ne 0) { throw "Grant failed." }
"built {0} from {1}: {2} reference tables, 0 tenants" -f $Database, $Source, $tableArgs.Count

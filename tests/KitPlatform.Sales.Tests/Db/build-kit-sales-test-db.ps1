param(
    [Parameter(Mandatory)] [int]$Port,
    [string]$Database = "kit_sales_ci",
    [string]$DbHost = "127.0.0.1",
    [string]$User = "pgadmin",
    [string]$Psql = "psql"
)
# Disposable KIT SALES test database: platform base (prod manifest, no seed) + kit-sales manifest.
# The integration tests write freely here; they refuse any database that is not named kit_sales_ci*.
$ErrorActionPreference = "Continue"
$env:PGOPTIONS = "-c client_min_messages=warning"
if ($Port -eq 5432) { throw "Refusing port 5432 (dev cluster)." }
if ($Database -notmatch '^kit_sales_ci[a-z0-9_]*$') { throw "Refusing database '$Database'." }

$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$conn = @("-h", $DbHost, "-p", "$Port", "-U", $User)

# The prod manifest does not replay from an empty database for these files (same list as the Pharmacy CI script).
$expectedFailures = @(
    "090_kap_pharmacy_intelligence_seed.sql",
    "102_kap_pharmacy_v1_1_telemed.sql",
    "230_pack_family_parent_emotion_push.sql",
    "231_pack_family_memory.sql"
)

& $Psql @conn -d postgres -q -v ON_ERROR_STOP=1 `
    -c "DROP DATABASE IF EXISTS $Database" `
    -c "CREATE DATABASE $Database" `
    -c "DO `$`$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN CREATE ROLE kitplatform NOLOGIN; END IF; END `$`$;"
if ($LASTEXITCODE -ne 0) { throw "Could not create $Database." }

function Read-Manifest([string]$name) {
    Get-Content (Join-Path $root "deploy/ubuntu/$name") |
        Where-Object { $_ -and $_ -notmatch '^\s*#' } | ForEach-Object { $_.Trim() }
}

$base = Read-Manifest "migration-files.prod.txt"
$sales = Read-Manifest "migration-files.kit-sales.txt"
$unexpected = @()
foreach ($f in @($base) + @($sales)) {
    $out = & $Psql @conn -d $Database -q -v ON_ERROR_STOP=1 -f (Join-Path $root "migrations/$f") 2>&1 | ForEach-Object { "$_" }
    $failed = $LASTEXITCODE -ne 0
    if ($failed -and ($f -notin $expectedFailures -or $f -in $sales)) {
        $unexpected += $f
        "FAIL {0}: {1}" -f $f, (($out | Where-Object { $_ -match "ERROR" } | Select-Object -First 1))
    }
}

$tenant = & $Psql @conn -d $Database -tA -c "SELECT COUNT(*) FROM tenants WHERE id = '11111111-1111-1111-1111-111111111107'"
"applied {0} base + {1} kit-sales files into {2}; KIT_SALES tenant rows: {3}; unexpected failures: {4}" -f `
    $base.Count, $sales.Count, $Database, $tenant, $unexpected.Count
if ($unexpected.Count -gt 0 -or "$tenant".Trim() -ne "1") { exit 1 }

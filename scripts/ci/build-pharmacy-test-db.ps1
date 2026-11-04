param(
    [Parameter(Mandatory)] [int]$Port,
    [Parameter(Mandatory)] [string]$Database,
    [string]$DbHost = "127.0.0.1",
    [string]$User = "pgadmin",
    [string]$Psql = "psql",
    [switch]$NoSeed
)
# Disposable Pharmacy test database: prod manifest, then the night release manifests in release order.
# Without -NoSeed it adds the demo seed (DEMO_PHARMACY) that KitPlatform.Api.Tests log in with.
# Like run-incremental-migrations-prod.sh, a file with a body in deploy/ubuntu/greenfield-compat runs that body.
$ErrorActionPreference = "Continue"
$env:PGOPTIONS = "-c client_min_messages=warning"
if ($Port -eq 5432) { throw "Refusing port 5432 (dev cluster)." }
if ($Database -in @("kitplatform", "pharmacore", "postgres") -or $Database -match "prod") {
    throw "Refusing database '$Database'."
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$deploy = Join-Path $root "deploy/ubuntu"
$conn = @("-h", $DbHost, "-p", "$Port", "-U", $User)

# The prod manifest does not replay from an empty database for these files (accepted, tracked in the release report).
$expectedFailures = @(
    "090_kap_pharmacy_intelligence_seed.sql",
    "102_kap_pharmacy_v1_1_telemed.sql",
    "230_pack_family_parent_emotion_push.sql",
    "231_pack_family_memory.sql"
)

# Night release order. A manifest that is not in this tree is skipped.
$releaseManifests = @(
    "migration-files.pharmacy-ops-v2.txt",
    "migration-files.pharmacy-knowledge-v1.txt",
    "migration-files.pharmacy-perf-v1.txt",
    "migration-files.staff-enablement-v1.txt"
)

function Read-Manifest([string]$name) {
    Get-Content (Join-Path $deploy $name) | Where-Object { $_ -and $_ -notmatch '^\s*#' } | ForEach-Object { $_.Trim() }
}

& $Psql @conn -d postgres -q -v ON_ERROR_STOP=1 `
    -c "DROP DATABASE IF EXISTS $Database" `
    -c "CREATE DATABASE $Database" `
    -c "DO `$`$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN CREATE ROLE kitplatform NOLOGIN; END IF; END `$`$;"
if ($LASTEXITCODE -ne 0) { throw "Could not create $Database." }

$files = [System.Collections.Generic.List[string]]::new()
foreach ($f in Read-Manifest "migration-files.prod.txt") {
    $files.Add($f)
    if ($NoSeed) { continue }
    if ($f -eq "010_supplier_payment_status.sql") { $files.Add("seed/001_demo_data.sql") }
    if ($f -eq "023_sales_loyalty_redeem.sql") {
        "seed/002_admin_password.sql", "seed/003_more_customers.sql", "seed/004_deck_rich_demo.sql" | ForEach-Object { $files.Add($_) }
    }
}
foreach ($m in $releaseManifests) {
    if (-not (Test-Path (Join-Path $deploy $m))) { "SKIP manifest {0} (not in this tree)" -f $m; continue }
    foreach ($f in Read-Manifest $m) {
        if (-not $files.Contains($f)) { $files.Add($f) }
    }
}

$unexpected = @()
foreach ($f in $files) {
    $path = Join-Path $root "migrations/$f"
    $compat = Join-Path $deploy "greenfield-compat/$f"
    if (Test-Path $compat) { $path = $compat }
    $out = & $Psql @conn -d $Database -q -v ON_ERROR_STOP=1 -f $path 2>&1 | ForEach-Object { "$_" }
    $failed = $LASTEXITCODE -ne 0
    if ($failed -and $f -notin $expectedFailures) {
        $unexpected += $f
        "FAIL {0}: {1}" -f $f, (($out | Where-Object { $_ -match "ERROR" } | Select-Object -First 1))
    }
    elseif (-not $failed -and $f -in $expectedFailures) {
        "NOTE {0} now applies; remove it from the expected failures." -f $f
    }
}

"applied {0} files into {1}; unexpected failures: {2}" -f $files.Count, $Database, $unexpected.Count
if ($unexpected.Count -gt 0) { exit 1 }

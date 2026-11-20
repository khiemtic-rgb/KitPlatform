param(
    [Parameter(Mandatory)] [int]$Port,
    [Parameter(Mandatory)] [string]$Database,
    [string]$DbHost = "127.0.0.1",
    [string]$User = "pgadmin",
    [string]$Psql = "psql",
    [switch]$NoSeed
)
# Disposable Pharmacy test database: prod manifest + Pharmacy migrations not yet in a manifest.
# Without -NoSeed it adds the demo seed (DEMO_PHARMACY) that KitPlatform.Api.Tests log in with.
$ErrorActionPreference = "Continue"
$env:PGOPTIONS = "-c client_min_messages=warning"
if ($Port -eq 5432) { throw "Refusing port 5432 (dev cluster)." }
if ($Database -in @("kitplatform", "pharmacore", "postgres") -or $Database -match "prod") {
    throw "Refusing database '$Database'."
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$conn = @("-h", $DbHost, "-p", "$Port", "-U", $User)

# The prod manifest does not replay from an empty database for these files (accepted, tracked in the release report).
$expectedFailures = @(
    "090_kap_pharmacy_intelligence_seed.sql",
    "102_kap_pharmacy_v1_1_telemed.sql",
    "230_pack_family_parent_emotion_push.sql",
    "231_pack_family_memory.sql"
)

$unlisted = @(
    "411_sales_return_client_return_id.sql", "412_success_checklist_pos_shift.sql", "413_learning_operational_task.sql",
    "414_pharmacy_knowledge_approval.sql", "415_pharmacy_knowledge_approval_history.sql",
    "416_pharmacy_knowledge_tenant_governance.sql", "417_pharmacy_knowledge_publication_event.sql",
    "423_pharmacy_knowledge_source_verification.sql", "424_pharmacy_owned_knowledge.sql",
    "425_pharmacy_owned_knowledge_event_snapshot.sql", "426_pharmacy_owned_knowledge_event_fidelity.sql",
    "427_pharmacy_owned_knowledge_governance.sql", "428_pharmacy_owned_knowledge_one_open.sql",
    "429_pharmacy_customer_and_local_regimen.sql", "431_pharmacy_report_fk_indexes.sql",
    "432_pharmacy_knowledge_permissions.sql", "433_pharmacy_owned_knowledge_review_due.sql",
    "434_employee_job_title.sql", "435_learning_observation_history.sql",
    "436_pharmacy_consultation_pharmacist_permission.sql", "437_learning_quiz_attempt_history.sql"
)

& $Psql @conn -d postgres -q -v ON_ERROR_STOP=1 `
    -c "DROP DATABASE IF EXISTS $Database" `
    -c "CREATE DATABASE $Database" `
    -c "DO `$`$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN CREATE ROLE kitplatform NOLOGIN; END IF; END `$`$;"
if ($LASTEXITCODE -ne 0) { throw "Could not create $Database." }

$manifest = Get-Content (Join-Path $root "deploy/ubuntu/migration-files.prod.txt") |
    Where-Object { $_ -and $_ -notmatch '^\s*#' } | ForEach-Object { $_.Trim() }
$files = [System.Collections.Generic.List[string]]::new()
foreach ($f in $manifest) {
    $files.Add($f)
    if ($NoSeed) { continue }
    if ($f -eq "010_supplier_payment_status.sql") { $files.Add("seed/001_demo_data.sql") }
    if ($f -eq "023_sales_loyalty_redeem.sql") {
        "seed/002_admin_password.sql", "seed/003_more_customers.sql", "seed/004_deck_rich_demo.sql" | ForEach-Object { $files.Add($_) }
    }
}
$unlisted | ForEach-Object { $files.Add($_) }

$unexpected = @()
foreach ($f in $files) {
    $out = & $Psql @conn -d $Database -q -v ON_ERROR_STOP=1 -f (Join-Path $root "migrations/$f") 2>&1 | ForEach-Object { "$_" }
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

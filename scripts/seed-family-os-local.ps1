<#
.SYNOPSIS
  Local-only: apply FamilyOS schema (192) + DEMO_FAMILY seed (Ngay di hoc).
  Does NOT touch production/VPS.

.EXAMPLE
  .\scripts\seed-family-os-local.ps1

.EXAMPLE
  .\scripts\seed-family-os-local.ps1 -PostgresPassword "<postgres>" -ApplyFamilyVertical -SmokeApi
#>
param(
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$Database = "kitplatform",
    [string]$AppUser = "kitplatform",
    [string]$AppPassword = "kitplatform_dev_2026",
    [string]$PostgresUser = "postgres",
    [string]$PostgresPassword = "",
    [switch]$ApplyFamilyVertical,
    [switch]$SmokeApi,
    [string]$ApiBaseUrl = "http://localhost:5290"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

$psqlCandidates = @(
    "C:\Program Files\PostgreSQL\18\bin\psql.exe",
    "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    "C:\Program Files\PostgreSQL\16\bin\psql.exe"
)
$psql = $psqlCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $psql) {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { $psql = $cmd.Source }
}
if (-not $psql) {
    throw "psql not found. Install PostgreSQL client or set PATH."
}

function Invoke-PsqlFile {
    param(
        [string]$User,
        [string]$Password,
        [string]$RelativePath
    )
    $path = Join-Path $Root $RelativePath
    if (-not (Test-Path $path)) { throw "Missing file: $path" }
    Write-Host (">> [" + $User + "] " + $RelativePath) -ForegroundColor Yellow
    $env:PGPASSWORD = $Password
    # psql writes NOTICE to stderr — don't treat as terminating error in PowerShell
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $psql -h $DbHost -p $DbPort -U $User -d $Database -v ON_ERROR_STOP=1 -f $path 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($code -ne 0) {
        throw ("SQL failed: " + $RelativePath + " (exit " + $code + ")")
    }
}

Write-Host "=== FamilyOS local seed (DEV ONLY) ===" -ForegroundColor Cyan
Write-Host ("DB: " + $DbHost + ":" + $DbPort + "/" + $Database)
Write-Host "NOTE: Do not run this against production." -ForegroundColor DarkYellow

# Schema + module registry as app user (no ALTER on tenants)
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\192_pack_family_os.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\193_pack_family_agreement_f2.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\194_pack_family_accountability_options.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\195_pack_family_consequence_event.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\196_pack_family_parent_push.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\197_pack_family_routine_graph.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\198_pack_family_agreement_taxonomy.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\199_pack_family_commitment_evidence.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\200_pack_family_value_persistence.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\201_pack_family_team_unlock.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\214_pack_family_star_ledger.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\215_pack_family_star_settings.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\216_pack_family_commitment_timing.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\217_pack_family_pending_stars.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\218_pack_family_reward_catalog.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\seed\006_family_os_demo.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\seed\007_family_constitution_v1.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\seed\008_family_team_siblings.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\seed\009_family_weekend_commitments.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\221_pack_family_calendar_period.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\seed\010_family_calendar_periods.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\seed\011_family_summer_schedule_2026.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\222_pack_family_commercial_foundation.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\249_pack_family_blueprint.sql"
Invoke-PsqlFile -User $AppUser -Password $AppPassword -RelativePath "migrations\seed\012_family_demo_gtm.sql"

if ($ApplyFamilyVertical) {
    if ([string]::IsNullOrWhiteSpace($PostgresPassword)) {
        throw "ApplyFamilyVertical requires -PostgresPassword (tenants table owner)."
    }
    Invoke-PsqlFile -User $PostgresUser -Password $PostgresPassword -RelativePath "migrations\192a_pack_family_os_vertical_owner.sql"
}

$env:PGPASSWORD = $AppPassword
$checkSql = @"
SELECT
  'tenant=' || t.tenant_code
  || ' vertical=' || t.business_vertical
  || ' modules=' || COALESCE(t.settings->'platform'->>'enabled_modules', '[]')
FROM public.tenants t
WHERE t.tenant_code = 'DEMO_FAMILY' AND t.deleted_at IS NULL;
SELECT
  'family=' || f.display_name
  || ' members=' || (SELECT COUNT(*)::text FROM pack_family.membership m WHERE m.family_id = f.id AND m.deleted_at IS NULL)
  || ' routines=' || (SELECT COUNT(*)::text FROM pack_family.routine r WHERE r.family_id = f.id AND r.deleted_at IS NULL)
  || ' templates=' || (
        SELECT COUNT(*)::text
        FROM pack_family.commitment_template ct
        INNER JOIN pack_family.routine r ON r.id = ct.routine_id
        WHERE r.family_id = f.id AND ct.deleted_at IS NULL AND ct.is_active
     )
FROM pack_family.family f
WHERE f.id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'::uuid;
SELECT 'routine=' || display_name || ' weekdays=' || weekdays::text
FROM pack_family.routine
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01'::uuid;
SELECT
  'constitution_agreements=' || COALESCE(SUM(cnt), 0)::text
  || ' by_category=' || COALESCE(string_agg(target_type || ':' || cnt::text, ', ' ORDER BY target_type), '')
FROM (
  SELECT target_type, COUNT(*)::int AS cnt
  FROM pack_family.agreement
  WHERE family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'::uuid
    AND deleted_at IS NULL
    AND COALESCE(terms->>'constitution', '') = 'v1.0'
  GROUP BY target_type
) s;
"@

$summary = & $psql -h $DbHost -p $DbPort -U $AppUser -d $Database -t -A -c $checkSql

Write-Host "=== Seed OK ===" -ForegroundColor Green
$summary | ForEach-Object { if ($_ -and $_.Trim()) { Write-Host ("  " + $_) } }

Write-Host ""
Write-Host "Login local admin (full write):" -ForegroundColor Cyan
Write-Host "  TenantCode : DEMO_FAMILY"
Write-Host "  Username   : admin"
Write-Host "  Password   : Admin@123"
Write-Host "  FamilyId   : aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"
Write-Host "  Kids       : Bao Nhi + Duc Huy (summer/school calendar)"
Write-Host ""
Write-Host "GTM demo viewer (read-only):" -ForegroundColor Cyan
Write-Host "  SPA        : http://localhost:5178/demo"
Write-Host "  TenantCode : DEMO_FAMILY"
Write-Host "  Username   : demo"
Write-Host "  Password   : Admin@123"
Write-Host "  Invite     : FAMIXADEM (viewer)"

if (-not $SmokeApi) {
    return
}

Write-Host ""
Write-Host ("=== Smoke API (" + $ApiBaseUrl + ") ===") -ForegroundColor Cyan
try {
    $loginBody = '{"tenantCode":"DEMO_FAMILY","username":"admin","password":"Admin@123"}'
    $login = Invoke-RestMethod -Method Post -Uri ($ApiBaseUrl + "/api/auth/login") -ContentType "application/json" -Body $loginBody
} catch {
    Write-Host "[WARN] API login failed - start API locally then re-run with -SmokeApi" -ForegroundColor DarkYellow
    Write-Host ("  " + $_)
    exit 0
}

$token = $login.accessToken
if (-not $token) { $token = $login.AccessToken }
if (-not $token) { throw "Login response missing accessToken" }

$headers = @{ Authorization = ("Bearer " + $token) }
$overview = Invoke-RestMethod -Headers $headers -Uri ($ApiBaseUrl + "/api/family-os/overview")
$families = Invoke-RestMethod -Headers $headers -Uri ($ApiBaseUrl + "/api/family-os/families")
$familyId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"
$routines = Invoke-RestMethod -Headers $headers -Uri ($ApiBaseUrl + "/api/family-os/families/" + $familyId + "/routines")
$flow = Invoke-RestMethod -Method Post -Headers $headers -ContentType "application/json" `
    -Uri ($ApiBaseUrl + "/api/family-os/families/" + $familyId + "/day-flows/ensure") `
    -Body "{}"

$agreements = Invoke-RestMethod -Headers $headers -Uri ($ApiBaseUrl + "/api/family-os/families/" + $familyId + "/agreements")
$library = Invoke-RestMethod -Headers $headers -Uri ($ApiBaseUrl + "/api/family-os/consequence-library")
$options = Invoke-RestMethod -Headers $headers -Uri ($ApiBaseUrl + "/api/family-os/families/" + $familyId + "/accountability-options")

Write-Host ("  overview.phase = " + $overview.phase)
Write-Host ("  families       = " + $families.Count)
$firstRoutine = if ($routines.Count -gt 0) { $routines[0].displayName } else { "(none)" }
Write-Host ("  routines       = " + $routines.Count + " (first=" + $firstRoutine + ")")
Write-Host ("  dayFlow.date   = " + $flow.flowDate + " commitments=" + $flow.totalCommitments + " pending=" + $flow.pendingCount)
Write-Host ("  agreements     = " + $agreements.Count)
Write-Host ("  consequenceLib = " + $library.Count)
Write-Host ("  acctOptions    = " + $options.Count)
Write-Host "=== Smoke API OK ===" -ForegroundColor Green

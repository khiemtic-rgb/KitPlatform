<#
.SYNOPSIS
  Verify KIT-BP-ASBUILT stays wired and aligned with shipped stack/packs.
#>
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$failed = New-Object System.Collections.Generic.List[string]

function Assert-True([bool]$cond, [string]$msg) {
    if (-not $cond) { [void]$script:failed.Add($msg) }
}

function Assert-File([string]$rel) {
    Assert-True (Test-Path -LiteralPath (Join-Path $root $rel)) "Missing file: $rel"
}

Write-Host '== KIT-BP-ASBUILT check =='

Assert-File 'docs/novixa/03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md'
Assert-File 'docs/novixa/03-solution/kitplatform-blueprint-asbuilt-v2.1.yaml'

$md = Get-Content -Raw (Join-Path $root 'docs/novixa/03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md')
$yaml = Get-Content -Raw (Join-Path $root 'docs/novixa/03-solution/kitplatform-blueprint-asbuilt-v2.1.yaml')
$master = Get-Content -Raw (Join-Path $root 'docs/novixa/DOC-MASTER-INDEX.md')
$readme = Get-Content -Raw (Join-Path $root 'docs/novixa/README.md')
$plt = Get-Content -Raw (Join-Path $root 'docs/novixa/03-solution/platform-kernel-and-solution-packs-v1.md')

Assert-True ($master -match 'kitplatform-enterprise-blueprint-asbuilt-v2\.1\.md') 'DOC-MASTER-INDEX missing as-built MD link'
Assert-True ($master -match 'KIT-BP-ASBUILT') 'DOC-MASTER-INDEX missing KIT-BP-ASBUILT'
Assert-True ($readme -match 'kitplatform-enterprise-blueprint-asbuilt-v2\.1\.md') 'README missing as-built entry'
Assert-True ($plt -match 'kitplatform-enterprise-blueprint-asbuilt-v2\.1\.md') 'KIT-PLT-01 missing as-built link'
Assert-True ($md -match '2\.1\.1') 'MD version should be 2.1.1 after sync'
Assert-True ($yaml -match 'version:\s*"2\.1\.1"') 'YAML version should be 2.1.1'

$apiCsproj = Get-Content -Raw (Join-Path $root 'src/KitPlatform.Api/KitPlatform.Api.csproj')
Assert-True ($apiCsproj -match 'TargetFramework>net10\.0<') 'Api TargetFramework is not net10.0'

$efCount = 0
Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter '*.csproj' | ForEach-Object {
    if ((Get-Content -Raw $_.FullName) -match 'EntityFramework|Microsoft\.EntityFrameworkCore') {
        $efCount++
    }
}
Assert-True ($efCount -eq 0) 'EF Core PackageReference found under src/'

@(
    'client/admin/vite.config.ts'
    'client/staff-app/vite.config.ts'
    'client/customer-app/vite.config.ts'
    'client/assessment-web/vite.config.ts'
    'client/prescriber-portal/vite.config.ts'
    'client/partner-portal/vite.config.ts'
) | ForEach-Object { Assert-File $_ }

$pharmacy = Get-Content -Raw (Join-Path $root 'src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/PharmacyPackDefinition.cs')
$clinic = Get-Content -Raw (Join-Path $root 'src/Packs/Clinic/KitPlatform.Packs.Clinic.Application/ClinicPackDefinition.cs')
$connect = Get-Content -Raw (Join-Path $root 'src/Packs/Connect/KitPlatform.Packs.Connect.Application/ConnectPackDefinition.cs')
$survey = Get-Content -Raw (Join-Path $root 'src/Packs/Survey/KitPlatform.Packs.Survey.Application/SurveyPackDefinition.cs')
$modules = Get-Content -Raw (Join-Path $root 'src/KitPlatform.Application/Core/PlatformModuleCodes.cs')
$provisioner = Get-Content -Raw (Join-Path $root 'src/KitPlatform.Infrastructure/Kernel/Workspace/WorkspacePackProvisioner.cs')

foreach ($code in @('e_rx', 'prescriber_network', 'prescriber_portal')) {
    Assert-True ($pharmacy -match [regex]::Escape($code)) "PharmacyPackDefinition missing $code"
    Assert-True ($yaml -match [regex]::Escape($code)) "YAML missing pharmacy module $code"
    Assert-True ($md -match [regex]::Escape($code)) "MD missing pharmacy module $code"
}

Assert-True ($clinic -match 'crm_leads') 'Clinic PackModuleCodes should include crm_leads'
Assert-True ($clinic -match 'novixa_connect') 'Clinic defaults should include novixa_connect'
Assert-True ($survey -match 'reports') 'Survey DefaultEnabledModules should include reports'
Assert-True ($connect -match 'novixa_connect') 'Connect pack code missing'
Assert-True ($yaml -match 'crm_leads') 'YAML should document clinic crm_leads'
Assert-True ($yaml -match 'not_in_runtime_provisioner') 'YAML should document Survey provisioner gap'
Assert-True ($md -match 'WorkspacePackProvisioner') 'MD should document WorkspacePackProvisioner'
Assert-True ($provisioner -match 'novixa_pharmacy') 'Provisioner must always provision pharmacy'
Assert-True ($provisioner -notmatch 'pharmacy_survey') 'Provisioner must NOT auto-provision pharmacy_survey (as-built)'

foreach ($code in @(
        'inventory', 'assessment', 'pharmacy_survey', 'clinic_appointments',
        'novixa_connect', 'e_rx', 'telehealth'
    )) {
    Assert-True ($modules -match [regex]::Escape($code)) "PlatformModuleCodes missing $code"
}

Assert-True ($yaml -match 'ef_core') 'YAML should forbid ef_core in generate_never'
Assert-True ($yaml -match 'blazor') 'YAML should forbid blazor in generate_never'

if ($failed.Count -gt 0) {
    Write-Host ("FAIL ({0}):" -f $failed.Count)
    foreach ($f in $failed) { Write-Host (" - {0}" -f $f) }
    exit 1
}

Write-Host 'OK - blueprint as-built synced with hub + pack definitions.'
exit 0

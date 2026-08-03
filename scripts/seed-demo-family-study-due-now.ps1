# Seed DEMO_FAMILY: one study_focus ad-hoc due_now for kid smoke (local API).
param(
    [string]$ApiBase = "http://localhost:5290",
    [string]$TenantCode = "DEMO_FAMILY",
    [string]$Username = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Stop"

function Get-Prop($obj, [string[]]$names) {
    foreach ($n in $names) {
        if ($null -ne $obj -and $obj.PSObject.Properties.Name -contains $n) {
            return $obj.$n
        }
    }
    return $null
}

function Invoke-ApiJson {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers,
        [object]$Body = $null
    )
    $params = @{
        Method      = $Method
        Uri         = $Uri
        Headers     = $Headers
        ContentType = "application/json"
        TimeoutSec  = 30
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Compress -Depth 6)
    }
    return Invoke-RestMethod @params
}

Write-Host "=== Seed study due_now @ $ApiBase tenant=$TenantCode ===" -ForegroundColor Cyan

$health = Invoke-RestMethod -Uri "$ApiBase/api/health" -TimeoutSec 5
if ((Get-Prop $health @("status", "Status")) -ne "ok") {
    throw "API health not ok"
}

$login = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/auth/login" -Headers @{} -Body @{
    tenantCode = $TenantCode
    username   = $Username
    password   = $Password
}
$token = Get-Prop $login @("accessToken", "AccessToken")
if (-not $token) { throw "login failed" }
$headers = @{ Authorization = "Bearer $token" }

$families = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families" -Headers $headers
$family = @($families)[0]
$familyId = Get-Prop $family @("id", "Id")
if (-not $familyId) { throw "no family" }

$members = @(Get-Prop $family @("members", "Members"))
$child = $null
foreach ($m in $members) {
    $role = Get-Prop $m @("roleCode", "RoleCode")
    if ("$role" -eq "child") { $child = $m; break }
}
$childId = if ($child) { Get-Prop $child @("id", "Id") } else { $null }
if (-not $childId) { throw "no child member" }

$flow = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/day-flows/ensure" -Headers $headers -Body @{}
$flowDate = Get-Prop $flow @("flowDate", "FlowDate")

$now = Get-Date
$start = $now.AddMinutes(-15).ToString("HH:mm")
$end = $now.AddMinutes(90).ToString("HH:mm")
$stamp = Get-Date -Format "HHmmss"
$title = "Hoc toan due-now $stamp"

$created = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/ad-hoc" -Headers $headers -Body @{
    flowDate                = "$flowDate"
    memberId                = "$childId"
    title                   = $title
    windowStart             = $start
    windowEnd               = $end
    expectedDurationMinutes = 25
}

$id = Get-Prop $created @("id", "Id")
$kind = Get-Prop $created @("commitmentKind", "CommitmentKind")
$policy = Get-Prop $created @("evidencePolicy", "EvidencePolicy")
$reminder = Get-Prop $created @("reminderState", "ReminderState")
$satisfied = Get-Prop $created @("evidenceSatisfied", "EvidenceSatisfied")

Write-Host "OK id=$id title=$title"
Write-Host "    kind=$kind policy=$policy reminder=$reminder evidenceSatisfied=$satisfied"
Write-Host "    window=$start-$end flowDate=$flowDate child=$childId"
Write-Host "Open family-app as kid -> Nhiem vu: Can lam ngay + Gui anh da hoc"
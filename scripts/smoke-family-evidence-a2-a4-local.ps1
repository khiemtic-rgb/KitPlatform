# DEPRECATED for photo-satisfies assumptions.
# Use: scripts/smoke-family-evidence-p05-local.ps1 (P0.5 photo!=stars + checklist + P0.6).
# Keep this file only for historical A4 hard-gate reference; prefer P05 smoke for daily CI.
﻿<#
.SYNOPSIS
  Local API smoke: Evidence P0 A2 (photo) and A4 (required_hard).
#>
param(
    [string]$ApiBase = "http://localhost:5290",
    [string]$TenantCode = "DEMO_FAMILY",
    [string]$Username = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Stop"
$script:FailCount = 0

function Ok([string]$msg) { Write-Host "  PASS  $msg" -ForegroundColor Green }
function Bad([string]$msg) {
    Write-Host "  FAIL  $msg" -ForegroundColor Red
    $script:FailCount++
}
function Info([string]$msg) { Write-Host "  --    $msg" -ForegroundColor DarkGray }

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

Write-Host "=== Evidence P0 smoke A2/A3/A4/A5 + metrics ===" -ForegroundColor Cyan
Write-Host "API: $ApiBase  tenant: $TenantCode"

try {
    $health = Invoke-RestMethod -Uri "$ApiBase/api/health" -TimeoutSec 5
    if ((Get-Prop $health @("status", "Status")) -eq "ok") { Ok "health" } else { Bad "health" ; exit 1 }
}
catch {
    Bad "health unreachable"; exit 1
}

$login = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/auth/login" -Headers @{} -Body @{
    tenantCode = $TenantCode
    username   = $Username
    password   = $Password
}
$token = Get-Prop $login @("accessToken", "AccessToken")
if (-not $token) { Bad "login"; exit 1 }
Ok "login"
$headers = @{ Authorization = "Bearer $token" }

$families = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families" -Headers $headers
$family = @($families)[0]
$familyId = Get-Prop $family @("id", "Id")
if (-not $familyId) { Bad "no family"; exit 1 }
Ok "family=$familyId"

$members = @(Get-Prop $family @("members", "Members"))
$child = $null
foreach ($m in $members) {
    $role = Get-Prop $m @("roleCode", "RoleCode")
    if ("$role" -eq "child") { $child = $m; break }
}
$childId = if ($child) { Get-Prop $child @("id", "Id") } else { $null }
if (-not $childId) { Bad "no child member for star post"; exit 1 }
Info "child=$childId"

$flow = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/day-flows/ensure" -Headers $headers -Body @{}
$flowDate = Get-Prop $flow @("flowDate", "FlowDate")
Ok "day-flow date=$flowDate"

$now = Get-Date
$start = $now.AddMinutes(-30).ToString("HH:mm")
$end = $now.AddMinutes(90).ToString("HH:mm")
$stamp = Get-Date -Format "HHmmss"
$evidenceUrl = $null

Write-Host "`n-- A2 photo -> evidenceSatisfied + starPosted --" -ForegroundColor Yellow
$a2Title = "Smoke Hoc toan A2 $stamp"
$a2 = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/ad-hoc" -Headers $headers -Body @{
    flowDate                = "$flowDate"
    memberId                = "$childId"
    title                   = $a2Title
    windowStart             = $start
    windowEnd               = $end
    expectedDurationMinutes = 20
}
$a2Id = Get-Prop $a2 @("id", "Id")
$a2Kind = Get-Prop $a2 @("commitmentKind", "CommitmentKind")
$a2Policy = Get-Prop $a2 @("evidencePolicy", "EvidencePolicy")
Info "a2 id=$a2Id kind=$a2Kind policy=$a2Policy"
if ("$a2Kind" -ne "study_focus") { Bad "A2 infer kind expected study_focus got $a2Kind" } else { Ok "A2 kind=study_focus" }

$soft = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a2Id" -Headers $headers -Body @{
    status         = "done"
    skipReason     = $null
    evidenceUrl    = $null
    parentOverride = $false
}
$softSat = [bool](Get-Prop $soft @("evidenceSatisfied", "EvidenceSatisfied"))
$softPosted = [bool](Get-Prop $soft @("starPosted", "StarPosted"))
$softStatus = Get-Prop $soft @("status", "Status")
if ("$softStatus" -eq "done" -and -not $softSat -and -not $softPosted) {
    Ok "A1-lite soft tick: done, evidenceSatisfied=false, starPosted=false"
} else {
    Bad "A1-lite soft tick unexpected status=$softStatus sat=$softSat posted=$softPosted"
}

$null = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a2Id" -Headers $headers -Body @{
    status         = "pending"
    skipReason     = $null
    parentOverride = $false
}

$pngPath = Join-Path $env:TEMP "famixa-smoke-a2.png"
[IO.File]::WriteAllBytes($pngPath, [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="))

if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) { Bad "curl.exe missing"; exit 1 }
$uploadOut = & curl.exe -s -S -X POST -H "Authorization: Bearer $token" -F "file=@$pngPath;type=image/png" "$ApiBase/api/family-os/families/$familyId/evidence"
$uploadJson = $uploadOut | ConvertFrom-Json
$evidenceUrl = Get-Prop $uploadJson @("url", "Url")
if (-not $evidenceUrl) {
    Bad "A2 upload no url: $uploadOut"
} else {
    Ok "A2 upload url=$evidenceUrl"
    $withPhoto = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a2Id" -Headers $headers -Body @{
        status         = "done"
        skipReason     = $null
        evidenceUrl    = "$evidenceUrl"
        parentOverride = $false
    }
    $sat = [bool](Get-Prop $withPhoto @("evidenceSatisfied", "EvidenceSatisfied"))
    $by = Get-Prop $withPhoto @("evidenceSatisfiedBy", "EvidenceSatisfiedBy")
    $posted = [bool](Get-Prop $withPhoto @("starPosted", "StarPosted"))
    $status = Get-Prop $withPhoto @("status", "Status")
    Info "A2 after photo: status=$status sat=$sat by=$by posted=$posted"
    if ("$status" -eq "done" -and $sat) { Ok "A2 evidenceSatisfied=true" } else { Bad "A2 not satisfied" }
    if ("$by" -eq "photo" -or $sat) { Ok "A2 satisfiedBy=$by" } else { Bad "A2 satisfiedBy=$by" }
    if ($posted) { Ok "A2 starPosted=true" } else { Bad "A2 starPosted=false (expected post after photo)" }
}

Write-Host "`n-- A4 required_hard blocks tick without evidence --" -ForegroundColor Yellow
$a4Title = "Smoke Hoc toan A4 $stamp"
$a4 = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/ad-hoc" -Headers $headers -Body @{
    flowDate                = "$flowDate"
    memberId                = "$childId"
    title                   = $a4Title
    windowStart             = $start
    windowEnd               = $end
    expectedDurationMinutes = 20
}
$a4Id = Get-Prop $a4 @("id", "Id")
$a4Kind = Get-Prop $a4 @("commitmentKind", "CommitmentKind")
if ("$a4Kind" -ne "study_focus") { Bad "A4 kind=$a4Kind" } else { Ok "A4 kind=study_focus" }

$policed = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a4Id/evidence-policy" -Headers $headers -Body @{
    evidencePolicy = "required_hard"
}
$pol = Get-Prop $policed @("evidencePolicy", "EvidencePolicy")
if ("$pol" -eq "required_hard") { Ok "A4 policy=required_hard" } else { Bad "A4 policy=$pol" }

try {
    Invoke-WebRequest -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a4Id" -Headers $headers -ContentType "application/json" -Body (@{
        status = "done"; skipReason = $null; evidenceUrl = $null; parentOverride = $false
    } | ConvertTo-Json -Compress) -UseBasicParsing | Out-Null
    Bad "A4 expected 400 but succeeded"
} catch {
    $resp = $_.Exception.Response
    $statusCode = if ($resp) { [int]$resp.StatusCode } else { 0 }
    $bodyText = ""
    if ($resp) {
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $bodyText = $reader.ReadToEnd()
        $reader.Close()
    }
    Info "A4 block HTTP=$statusCode body=$bodyText"
    $code = $null
    try { $code = Get-Prop ($bodyText | ConvertFrom-Json) @("code", "Code") } catch {}
    if ($statusCode -eq 400 -and "$code" -eq "evidence_required") {
        Ok "A4 HTTP 400 code=evidence_required"
    } else {
        Bad "A4 unexpected status=$statusCode code=$code"
    }
}

$flow2 = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/day-flows/$flowDate" -Headers $headers
$commitments = @(Get-Prop $flow2 @("commitments", "Commitments"))
$a4Row = $null
foreach ($c in $commitments) {
    if ((Get-Prop $c @("id", "Id")) -eq $a4Id) { $a4Row = $c; break }
}
$a4Status = if ($a4Row) { Get-Prop $a4Row @("status", "Status") } else { "missing" }
if ("$a4Status" -eq "pending" -or "$a4Status" -eq "in_progress") {
    Ok "A4 status still $a4Status (not done)"
} else {
    Bad "A4 status=$a4Status after blocked tick"
}

if ($evidenceUrl) {
    $hardPhoto = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a4Id" -Headers $headers -Body @{
        status = "done"; skipReason = $null; evidenceUrl = "$evidenceUrl"; parentOverride = $false
    }
    $hStatus = Get-Prop $hardPhoto @("status", "Status")
    $hSat = [bool](Get-Prop $hardPhoto @("evidenceSatisfied", "EvidenceSatisfied"))
    $hPosted = [bool](Get-Prop $hardPhoto @("starPosted", "StarPosted"))
    if ("$hStatus" -eq "done" -and $hSat) { Ok "A4+photo done+satisfied" } else { Bad "A4+photo status=$hStatus sat=$hSat" }
    if ($hPosted) { Ok "A4+photo starPosted=true" } else { Bad "A4+photo starPosted=false" }
}


Write-Host "`n-- A3 verify-evidence -> parent_verify + stars --" -ForegroundColor Yellow
$a3Title = "Smoke Hoc toan A3 $stamp"
$a3 = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/ad-hoc" -Headers $headers -Body @{
    flowDate = "$flowDate"; memberId = "$childId"; title = $a3Title
    windowStart = $start; windowEnd = $end; expectedDurationMinutes = 20
}
$a3Id = Get-Prop $a3 @("id", "Id")
if ((Get-Prop $a3 @("commitmentKind","CommitmentKind")) -ne "study_focus") { Bad "A3 kind" } else { Ok "A3 kind=study_focus" }
$null = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a3Id" -Headers $headers -Body @{
    status = "done"; skipReason = $null; evidenceUrl = $null; parentOverride = $false
}
$verified = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a3Id/verify-evidence" -Headers $headers -Body @{}
$vSat = [bool](Get-Prop $verified @("evidenceSatisfied","EvidenceSatisfied"))
$vBy = Get-Prop $verified @("evidenceSatisfiedBy","EvidenceSatisfiedBy")
$vPosted = [bool](Get-Prop $verified @("starPosted","StarPosted"))
Info "A3 after verify: sat=$vSat by=$vBy posted=$vPosted"
if ($vSat -and "$vBy" -eq "parent_verify") { Ok "A3 evidenceSatisfiedBy=parent_verify" } else { Bad "A3 by=$vBy sat=$vSat" }
if ($vPosted) { Ok "A3 starPosted=true" } else { Bad "A3 starPosted=false" }

Write-Host "`n-- A5 chore tick without photo (regression) --" -ForegroundColor Yellow
$a5Title = "Smoke rua tay A5 $stamp"
$a5 = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/ad-hoc" -Headers $headers -Body @{
    flowDate = "$flowDate"; memberId = "$childId"; title = "Rua tay smoke $stamp"
    windowStart = $start; windowEnd = $end; expectedDurationMinutes = 10
}
# Force chore title that InferKind won't map to study — "Danh rang" has no hoc/bai
$a5Id = Get-Prop $a5 @("id", "Id")
$a5Kind = Get-Prop $a5 @("commitmentKind","CommitmentKind")
Info "a5 kind=$a5Kind title=$(Get-Prop $a5 @('title','Title'))"
if ("$a5Kind" -eq "chore") { Ok "A5 kind=chore" } else { Bad "A5 kind=$a5Kind (want chore)" }
$choreDone = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$a5Id" -Headers $headers -Body @{
    status = "done"; skipReason = $null; evidenceUrl = $null; parentOverride = $false
}
$cStatus = Get-Prop $choreDone @("status","Status")
$cPosted = [bool](Get-Prop $choreDone @("starPosted","StarPosted"))
$cSat = [bool](Get-Prop $choreDone @("evidenceSatisfied","EvidenceSatisfied"))
Info "A5 after tick: status=$cStatus posted=$cPosted sat=$cSat"
if ("$cStatus" -eq "done" -and $cPosted) { Ok "A5 chore done + starPosted" } else { Bad "A5 status=$cStatus posted=$cPosted" }
if ($cSat) { Ok "A5 chore evidenceSatisfied=true (optional)" } else { Bad "A5 chore not satisfied" }

Write-Host "`n-- Metrics weekly insight --" -ForegroundColor Yellow
$insight = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/insight/weekly?asOf=$flowDate&days=7" -Headers $headers
$rate = Get-Prop $insight @("studyEvidenceRate","StudyEvidenceRate")
$sDone = Get-Prop $insight @("studyFocusDoneCount","StudyFocusDoneCount")
$sEv = Get-Prop $insight @("studyFocusDoneWithEvidenceCount","StudyFocusDoneWithEvidenceCount")
$tickOnly = Get-Prop $insight @("studyTickOnlyStarPosts","StudyTickOnlyStarPosts")
$blocked = Get-Prop $insight @("evidenceGateBlockedCount","EvidenceGateBlockedCount")
Info "study done=$sDone withEv=$sEv rate=$rate tickOnlyStars=$tickOnly gateBlocked=$blocked"
if ([int]$sDone -ge 1) { Ok "metrics studyFocusDoneCount=$sDone" } else { Bad "metrics studyFocusDoneCount=$sDone" }
if ($null -ne $rate) { Ok "metrics studyEvidenceRate=$rate" } else { Bad "metrics studyEvidenceRate missing" }
if ([int]$tickOnly -eq 0) { Ok "metrics studyTickOnlyStarPosts=0" } else { Bad "metrics studyTickOnlyStarPosts=$tickOnly (must be 0)" }
if ([int]$blocked -ge 1) { Ok "metrics evidenceGateBlockedCount=$blocked" } else { Bad "metrics gateBlocked=$blocked (want >=1 after A4)" }

Write-Host "`n=== Result: $($script:FailCount) fail(s) ===" -ForegroundColor $(if ($script:FailCount -eq 0) { "Green" } else { "Red" })
if ($script:FailCount -gt 0) { exit 1 }
exit 0




<#
.SYNOPSIS
  Local smoke: FamilyOS value persistence + Team Unlock APIs.
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
    $script:FailCount = $script:FailCount + 1
}
function Info([string]$msg) { Write-Host "  --    $msg" -ForegroundColor DarkGray }

Write-Host "=== FamilyOS local smoke ===" -ForegroundColor Cyan
Write-Host "API: $ApiBase"

try {
    $health = Invoke-RestMethod -Uri "$ApiBase/api/health" -TimeoutSec 5
    if ($health.status -eq "ok") { Ok "health" } else { Bad "health status=$($health.status)" }
}
catch {
    Bad "health unreachable - restart API first"
    exit 1
}

$loginBody = @{
    tenantCode = $TenantCode
    username   = $Username
    password   = $Password
} | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/auth/login" -ContentType "application/json" -Body $loginBody
$token = $login.accessToken
if (-not $token) { Bad "login no token"; exit 1 }
Ok "login"

$headers = @{ Authorization = "Bearer $token" }

$families = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families" -Headers $headers
$family = $families[0]
$familyId = $family.id
if (-not $familyId) { $familyId = $family.Id }
Ok "family=$familyId"

$members = @($family.members)
if ($family.Members) { $members = @($family.Members) }
$parent = $null
foreach ($m in $members) {
    $role = $m.roleCode
    if (-not $role) { $role = $m.RoleCode }
    if ($role -ne "child") {
        $parent = $m
        break
    }
}
$parentId = $null
if ($parent) {
    $parentId = $parent.id
    if (-not $parentId) { $parentId = $parent.Id }
}
Info "parentMembership=$parentId"

$flow = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/day-flows/ensure" -Headers $headers -ContentType "application/json" -Body "{}"
$flowDate = $flow.flowDate
if (-not $flowDate) { $flowDate = $flow.FlowDate }
Ok "day-flow ensure date=$flowDate"

try {
    $null = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/value/state" -Headers $headers
    Ok "value/state"
}
catch {
    Bad "value/state: $($_.Exception.Message)"
}

$nudgeBody = @{ nudgeDate = "$flowDate"; increment = 1 } | ConvertTo-Json
try {
    $nudge = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/value/nudges/increment" -Headers $headers -ContentType "application/json" -Body $nudgeBody
    $nCount = $nudge.count
    if ($null -eq $nCount) { $nCount = $nudge.Count }
    if ($nCount -ge 1) { Ok "nudge increment count=$nCount" } else { Bad "nudge count=$nCount" }
}
catch {
    Bad "nudge increment: $($_.Exception.Message)"
}

$scoreBody = @{ scoreDate = "$flowDate"; score = 72; breakdownJson = $null } | ConvertTo-Json
try {
    $null = Invoke-RestMethod -Method Put -Uri "$ApiBase/api/family-os/families/$familyId/value/health-score" -Headers $headers -ContentType "application/json" -Body $scoreBody
    Ok "health-score put"
}
catch {
    Bad "health-score: $($_.Exception.Message)"
}

$profile = @{
    childId       = "smoke"
    childName     = "Smoke"
    ageBand       = "7-9"
    struggles     = @("morning_forget")
    goal          = "fewer_nudges"
    completedAt   = (Get-Date).ToUniversalTime().ToString("o")
    missionTitles = @("Smoke Mission")
} | ConvertTo-Json -Compress
$onboardBody = @{
    payloadJson = $profile
    completedAt = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json
try {
    $null = Invoke-RestMethod -Method Put -Uri "$ApiBase/api/family-os/families/$familyId/value/onboarding" -Headers $headers -ContentType "application/json" -Body $onboardBody
    $state2 = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/value/state" -Headers $headers
    $ob = $state2.onboarding
    if (-not $ob) { $ob = $state2.Onboarding }
    if ($ob) { Ok "onboarding persisted" } else { Bad "onboarding missing after put" }
}
catch {
    Bad "onboarding: $($_.Exception.Message)"
}

try {
    $team = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/team-day?flowDate=$flowDate" -Headers $headers
    $pct = $team.teamPercent
    if ($null -eq $pct) { $pct = $team.TeamPercent }
    $complete = $team.teamComplete
    if ($null -eq $complete) { $complete = $team.TeamComplete }
    Ok "team-day percent=$pct complete=$complete"
}
catch {
    Bad "team-day: $($_.Exception.Message)"
}

$commitments = @()
if ($flow.commitments) { $commitments = @($flow.commitments) }
elseif ($flow.Commitments) { $commitments = @($flow.Commitments) }
$doneN = 0
foreach ($c in $commitments) {
    $st = $c.status
    if (-not $st) { $st = $c.Status }
    $cid = $c.id
    if (-not $cid) { $cid = $c.Id }
    if ($st -eq "done") { continue }
    if ($st -eq "skipped") { continue }
    $patchBody = @{ status = "done" } | ConvertTo-Json
    try {
        $null = Invoke-RestMethod -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$cid" -Headers $headers -ContentType "application/json" -Body $patchBody
        $doneN = $doneN + 1
    }
    catch {
        Info "skip mark $cid"
    }
}
Info "marked done=$doneN"

try {
    $ensureRes = Invoke-WebRequest -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/team-unlocks/ensure?flowDate=$flowDate" -Headers $headers -UseBasicParsing
    if ($ensureRes.StatusCode -eq 204) {
        Info "ensure returned 204 (team not complete)"
    }
    elseif ($ensureRes.StatusCode -eq 200) {
        $unlock = $ensureRes.Content | ConvertFrom-Json
        $uid = $unlock.id
        if (-not $uid) { $uid = $unlock.Id }
        $ust = $unlock.status
        if (-not $ust) { $ust = $unlock.Status }
        Ok "team-unlock ensure id=$uid status=$ust"
        if ($parentId -and $ust -eq "pending_confirm") {
            $confirmBody = @{
                status      = "confirmed"
                confirmedBy = $parentId
            } | ConvertTo-Json
            $dec = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/team-unlocks/$uid/confirm" -Headers $headers -ContentType "application/json" -Body $confirmBody
            $dst = $dec.status
            if (-not $dst) { $dst = $dec.Status }
            if ($dst -eq "confirmed") { Ok "team-unlock confirmed" } else { Bad "confirm status=$dst" }
        }
        elseif ($ust -eq "confirmed") {
            Ok "team-unlock already confirmed"
        }
    }
    else {
        Bad "ensure status=$($ensureRes.StatusCode)"
    }
}
catch {
    Bad "team-unlock: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "=== Payer path (Brief → Coach acted → ROP → checkout) ===" -ForegroundColor Cyan

try {
    $null = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/decision-inbox" -Headers $headers
    Ok "decision-inbox"
}
catch {
    Bad "decision-inbox: $($_.Exception.Message)"
}

try {
    if ($parentId) {
        $modeBody = @{
            mode                = "normal"
            activatedByMemberId = $parentId
            confirmNow          = $true
        } | ConvertTo-Json
        $null = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/family-modes/activate" -Headers $headers -ContentType "application/json" -Body $modeBody
        Ok "family-mode activate"
    }
    else {
        Info "skip family-mode (no parent)"
    }
}
catch {
    Bad "family-mode: $($_.Exception.Message)"
}

try {
    $null = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/behavior/coach" -Headers $headers
    Ok "behavior/coach"
}
catch {
    Info "behavior/coach skipped: $($_.Exception.Message)"
}

if ($parentId) {
    $actedBody = @{
        memberId = $parentId
        tipId    = "smoke_tip"
        slot     = "brief"
        titleVi  = "Smoke Đã thử"
    } | ConvertTo-Json
    try {
        $null = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/parent-success/coach-acted" -Headers $headers -ContentType "application/json" -Body $actedBody
        Ok "coach-acted"
    }
    catch {
        Bad "coach-acted: $($_.Exception.Message)"
    }
}

try {
    $null = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/parent-success/rop?days=30" -Headers $headers
    Ok "parent-success/rop"
}
catch {
    $code = $null
    try { $code = [int]$_.Exception.Response.StatusCode } catch { }
    if ($code -eq 402 -or $code -eq 403 -or $code -eq 400) {
        Ok "parent-success/rop gated/validation ($code)"
    }
    else {
        Bad "parent-success/rop: $($_.Exception.Message)"
    }
}

try {
    $sub = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/subscription" -Headers $headers
    $plan = $sub.planCode
    if (-not $plan) { $plan = $sub.PlanCode }
    Ok "subscription plan=$plan"
}
catch {
    Bad "subscription: $($_.Exception.Message)"
}

if ($parentId) {
    try {
        $checkoutBody = @{
            planCode            = "pro"
            initiatedByMemberId = $parentId
        } | ConvertTo-Json
        $co = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/billing/checkout" -Headers $headers -ContentType "application/json" -Body $checkoutBody
        $url = $co.checkoutUrl
        if (-not $url) { $url = $co.CheckoutUrl }
        $order = $co.orderCode
        if (-not $order) { $order = $co.OrderCode }
        if ($url -or $order) {
            Ok "billing/checkout created"
        }
        else {
            Ok "billing/checkout accepted"
        }
    }
    catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        if ($code -eq 400 -or $code -eq 409 -or $code -eq 402) {
            Ok "billing/checkout reachable ($code)"
        }
        else {
            Bad "billing/checkout: $($_.Exception.Message)"
        }
    }
}

Write-Host ""
if ($script:FailCount -eq 0) {
    Write-Host "SMOKE OK" -ForegroundColor Green
    Write-Host "Checklist: docs/novixa/03-solution/family-os-smoke-checklist-v1.md"
    exit 0
}

Write-Host "SMOKE FAILED ($($script:FailCount))" -ForegroundColor Red
exit 1

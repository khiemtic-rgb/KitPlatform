# Family OS evidence smoke (canonical): P0.5 photo!=stars + checklist + duration + P0.6 tiny reject.
# Supersedes assumptions in smoke-family-evidence-a2-a4-local.ps1 (photo no longer satisfies alone).
param(
    [string]$ApiBase = "http://localhost:5290",
    [string]$TenantCode = "DEMO_FAMILY",
    [string]$Username = "admin",
    [string]$Password = "Admin@123"
)
$ErrorActionPreference = "Stop"
$script:FailCount = 0
function Ok([string]$m) { Write-Host "  PASS  $m" -ForegroundColor Green }
function Bad([string]$m) { Write-Host "  FAIL  $m" -ForegroundColor Red; $script:FailCount++ }
function Info([string]$m) { Write-Host "  --  $m" -ForegroundColor DarkGray }
function Get-Prop($obj, [string[]]$names) {
    foreach ($n in $names) {
        if ($null -ne $obj -and $obj.PSObject.Properties.Name -contains $n) { return $obj.$n }
    }
    return $null
}
function Invoke-ApiJson {
    param([string]$Method,[string]$Uri,[hashtable]$Headers,[object]$Body=$null)
    $p = @{ Method=$Method; Uri=$Uri; Headers=$Headers; ContentType="application/json"; TimeoutSec=30 }
    if ($null -ne $Body) { $p.Body = ($Body | ConvertTo-Json -Compress -Depth 6) }
    return Invoke-RestMethod @p
}

Write-Host "=== Family evidence smoke (P0.5/0.6) @ $ApiBase ===" -ForegroundColor Cyan
$health = Invoke-RestMethod -Uri "$ApiBase/api/health" -TimeoutSec 5
if ((Get-Prop $health @("status","Status")) -ne "ok") { Bad "health"; exit 1 }
Ok "health"

$login = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/auth/login" -Headers @{} -Body @{
    tenantCode=$TenantCode; username=$Username; password=$Password
}
$token = Get-Prop $login @("accessToken","AccessToken")
if (-not $token) { Bad "login"; exit 1 }
Ok "login"
$headers = @{ Authorization = "Bearer $token" }

$famRaw = Invoke-WebRequest -Uri "$ApiBase/api/family-os/families" -Headers $headers -UseBasicParsing
$famJson = $famRaw.Content | ConvertFrom-Json
if ($famJson -is [System.Array]) { $family = $famJson[0] } else { $family = $famJson }
$familyId = $null
$members = @()
if ($null -ne $family) {
    if ($family.PSObject.Properties.Name -contains "id") { $familyId = [string]$family.id }
    elseif ($family.PSObject.Properties.Name -contains "Id") { $familyId = [string]$family.Id }
    if ($family.PSObject.Properties.Name -contains "members") { $members = @($family.members) }
    elseif ($family.PSObject.Properties.Name -contains "Members") { $members = @($family.Members) }
}
$child = $null
foreach ($m in $members) {
    $role = $null
    if ($m.PSObject.Properties.Name -contains "roleCode") { $role = [string]$m.roleCode }
    elseif ($m.PSObject.Properties.Name -contains "RoleCode") { $role = [string]$m.RoleCode }
    if ($role -eq "child") { $child = $m; break }
}
$childId = $null
if ($null -ne $child) {
    if ($child.PSObject.Properties.Name -contains "id") { $childId = [string]$child.id }
    elseif ($child.PSObject.Properties.Name -contains "Id") { $childId = [string]$child.Id }
}
if (-not $familyId -or -not $childId) {
    Bad "family/child members=$($members.Count) famType=$($family.GetType().Name) props=$((@($family.PSObject.Properties.Name) -join '|'))"
    exit 1
}
Ok "family=$familyId child=$childId"

$flow = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/day-flows/ensure" -Headers $headers -Body @{}
$flowDate = "$(Get-Prop $flow @('flowDate','FlowDate'))"
$now = Get-Date
$start = $now.AddMinutes(-20).ToString("HH:mm")
$end = $now.AddMinutes(90).ToString("HH:mm")
$stamp = Get-Date -Format "HHmmss"

# P0.6 tiny reject (curl.exe does not throw into catch on WinPS 5.1)
$tiny = Join-Path $env:TEMP "famixa-tiny-$stamp.png"
[IO.File]::WriteAllBytes($tiny, [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="))
$tinyOut = [string](curl.exe -s -w "HTTP:%{http_code}" -X POST -H "Authorization: Bearer $token" -F "file=@$tiny;type=image/png" -F "memberId=$childId" "$ApiBase/api/family-os/families/$familyId/evidence")
$tinyCode = 0
if ($tinyOut -match "HTTP:(\d{3})\s*$") { $tinyCode = [int]$Matches[1] }
if ($tinyCode -ge 400) { Ok "P0.6 tiny image rejected HTTP=$tinyCode" } else { Bad "P0.6 tiny upload should fail (HTTP=$tinyCode body=$tinyOut)" }

# Create study ad-hoc
$created = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/ad-hoc" -Headers $headers -Body @{
    flowDate=$flowDate; memberId="$childId"; title=("Hoc smoke P05 $stamp")
    windowStart=$start; windowEnd=$end; expectedDurationMinutes=25
}
$id = Get-Prop $created @("id","Id")
$kind = Get-Prop $created @("commitmentKind","CommitmentKind")
if ("$kind" -ne "study_focus") { Bad "kind=$kind" } else { Ok "ad-hoc study_focus id=$id" }

$null = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id" -Headers $headers -Body @{ status="in_progress" }

# Soft tick without photo
$soft = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id" -Headers $headers -Body @{
    status="done"; evidenceUrl=$null; parentOverride=$false
}
$sat = [bool](Get-Prop $soft @("evidenceSatisfied","EvidenceSatisfied"))
$posted = [bool](Get-Prop $soft @("starPosted","StarPosted"))
if (-not $sat -and -not $posted) { Ok "soft tick: unsatisfied, no starPosted" } else { Bad "soft tick sat=$sat posted=$posted" }

# Reset + photo via real upload (needs non-tiny image: reuse pad)
$pad = Join-Path $env:TEMP "famixa-pad-$stamp.png"
# 200x200 light PNG via System.Drawing if available, else skip photo path
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 200,200
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$g.DrawString("HOC $stamp", (New-Object System.Drawing.Font "Arial",14), [System.Drawing.Brushes]::Black, 10, 80)
$g.Dispose(); $bmp.Save($pad, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()

$null = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id" -Headers $headers -Body @{ status="pending" }
$null = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id" -Headers $headers -Body @{ status="in_progress" }

$uploadRaw = curl.exe -s -X POST -H "Authorization: Bearer $token" -F "file=@$pad;type=image/png" -F "memberId=$childId" "$ApiBase/api/family-os/families/$familyId/evidence"
$upload = $uploadRaw | ConvertFrom-Json
$url = Get-Prop $upload @("url","Url")
$warn = Get-Prop $upload @("warningMessageVi","WarningMessageVi")
if (-not $url) { Bad "upload url missing: $uploadRaw" } else { Ok "upload url ok"; if ($warn) { Info "P0.7 soft warn: $warn" } }

$withPhoto = Invoke-ApiJson -Method Patch -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id" -Headers $headers -Body @{
    status="done"; evidenceUrl=$url; parentOverride=$false
}
$sat2 = [bool](Get-Prop $withPhoto @("evidenceSatisfied","EvidenceSatisfied"))
$posted2 = [bool](Get-Prop $withPhoto @("starPosted","StarPosted"))
$submitted = [bool](Get-Prop $withPhoto @("evidenceSubmitted","EvidenceSubmitted"))
if (-not $sat2 -and -not $posted2) { Ok "P0.5 photo alone: unsatisfied submitted=$submitted" } else { Bad "photo alone sat=$sat2 posted=$posted2" }

# Incomplete checklist
try {
    Invoke-WebRequest -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id/verify-evidence" -Headers $headers -ContentType "application/json" -Body '{"isTodaysWork":true,"withinCommitmentWindow":false,"matchesCommitment":true,"overrideDuration":true}' -UseBasicParsing | Out-Null
    Bad "incomplete checklist should 400"
} catch {
    if ([int]$_.Exception.Response.StatusCode -eq 400) { Ok "incomplete checklist => 400" } else { Bad "incomplete HTTP" }
}

# Duration without override
try {
    Invoke-WebRequest -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id/verify-evidence" -Headers $headers -ContentType "application/json" -Body '{"isTodaysWork":true,"withinCommitmentWindow":true,"matchesCommitment":true,"overrideDuration":false}' -UseBasicParsing | Out-Null
    Info "duration allowed (started long enough or no expected)"
} catch {
    if ([int]$_.Exception.Response.StatusCode -eq 400) { Ok "duration gate => 400 without override" } else { Bad "duration unexpected" }
}

# Full verify + override
$ok = Invoke-ApiJson -Method Post -Uri "$ApiBase/api/family-os/families/$familyId/commitments/$id/verify-evidence" -Headers $headers -Body @{
    isTodaysWork=$true; withinCommitmentWindow=$true; matchesCommitment=$true; overrideDuration=$true; note="smoke"
}
$sat3 = [bool](Get-Prop $ok @("evidenceSatisfied","EvidenceSatisfied"))
$by = "$(Get-Prop $ok @('evidenceSatisfiedBy','EvidenceSatisfiedBy'))"
$posted3 = [bool](Get-Prop $ok @("starPosted","StarPosted"))
if ($sat3 -and $by -eq "parent_verify") { Ok "checklist+override => by=$by posted=$posted3" } else { Bad "verify sat=$sat3 by=$by posted=$posted3" }

# Morning note
$note = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/morning-note?memberId=$childId" -Headers $headers
$body = "$(Get-Prop $note @('bodyVi','BodyVi'))"
if ($body.Length -gt 20) { Ok "morning-note len=$($body.Length)" } else { Bad "morning-note empty" }

# Weekly metrics fields present
$insight = Invoke-RestMethod -Uri "$ApiBase/api/family-os/families/$familyId/insight/weekly?asOf=$flowDate&days=7" -Headers $headers
$rate = Get-Prop $insight @("studyEvidenceRate","StudyEvidenceRate")
$tick = Get-Prop $insight @("studyTickOnlyStarPosts","StudyTickOnlyStarPosts")
$blocked = Get-Prop $insight @("evidenceGateBlockedCount","EvidenceGateBlockedCount")
Info "metrics studyEvidenceRate=$rate tickOnly=$tick gateBlocked=$blocked"
if ($null -ne $rate) { Ok "metrics studyEvidenceRate present" } else { Bad "metrics studyEvidenceRate missing" }

Write-Host "`n=== Result: $($script:FailCount) fail(s) ===" -ForegroundColor $(if ($script:FailCount -eq 0) {'Green'} else {'Red'})
if ($script:FailCount -gt 0) { exit 1 }
exit 0
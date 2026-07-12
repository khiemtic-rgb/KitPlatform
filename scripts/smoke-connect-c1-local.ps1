# Smoke Novixa Connect C1 (org links) against local API
# Usage: .\scripts\smoke-connect-c1-local.ps1
# Requires: DEMO_CLINIC (migration 108) + pharmacy with org_profiles
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$InitiatorTenant = "NT_XUANHOA",
    [string]$PartnerTenant = "DEMO_CLINIC",
    [string]$User = "admin",
    [string]$Pass = "Admin@123"
)

$ErrorActionPreference = "Stop"

function Login([string]$tenant) {
    $auth = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" `
        -Body (@{ username = $User; password = $Pass; tenantCode = $tenant } | ConvertTo-Json)
    if (-not $auth.accessToken) { throw "login failed for $tenant" }
    return @{ Authorization = "Bearer $($auth.accessToken)" }
}

Write-Host "`n=== Connect C1 smoke ($InitiatorTenant -> $PartnerTenant @ $BaseUrl) ===" -ForegroundColor Cyan

$h1 = Login $InitiatorTenant
Write-Host "[OK] Login $InitiatorTenant" -ForegroundColor Green

$ov = Invoke-RestMethod "$BaseUrl/api/connect/overview" -Headers $h1
if ($ov.packCode -ne "novixa_connect") { throw "unexpected packCode=$($ov.packCode)" }
if ($ov.enabledCapabilities -notcontains "org_links") { throw "missing org_links capability" }
if ($ov.enabledCapabilities -notcontains "org_profile") { throw "missing org_profile capability" }
Write-Host "[OK] overview phase=$($ov.phase)" -ForegroundColor Green

$profile = Invoke-RestMethod "$BaseUrl/api/connect/org-profile" -Headers $h1
if ($profile.orgKind -ne "pharmacy") { throw "initiator orgKind expected pharmacy, got $($profile.orgKind)" }
Write-Host "[OK] initiator org profile pharmacy" -ForegroundColor Green

$dir = Invoke-RestMethod "$BaseUrl/api/connect/org-links/directory" -Headers $h1
$partner = @($dir) | Where-Object { $_.tenantCode -eq $PartnerTenant } | Select-Object -First 1
if (-not $partner) { throw "directory missing $PartnerTenant" }
if ($partner.orgKind -ne "clinic") { throw "partner orgKind expected clinic, got $($partner.orgKind)" }
Write-Host "[OK] directory contains $PartnerTenant as clinic" -ForegroundColor Green

$rawActive = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $h1
$existing = @($rawActive) | Where-Object {
    $_.partnerTenantCode -eq $PartnerTenant -and $_.linkStatus -eq "active"
} | Select-Object -First 1
if ($existing) {
    Write-Host ('[OK] active link already exists id={0} - skip invite/accept' -f $existing.id) -ForegroundColor Green
    Write-Host "`n=== Connect C1 smoke PASSED (idempotent) ===" -ForegroundColor Green
    exit 0
}

$inviteBody = (@{
    partnerTenantCode = $PartnerTenant
    ourOrgRole = "pharmacy"
    partnerOrgRole = "clinic"
    notes = "C1 smoke invite"
} | ConvertTo-Json -Compress)

$link = Invoke-RestMethod "$BaseUrl/api/connect/org-links/invite" -Method POST -Headers $h1 `
    -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($inviteBody))
if ($link.linkStatus -ne "pending_partner_accept") { throw "expected pending_partner_accept, got $($link.linkStatus)" }
Write-Host "[OK] invite id=$($link.id) status=$($link.linkStatus)" -ForegroundColor Green

# Reject fake role override
try {
    $bad = (@{
        partnerTenantCode = $PartnerTenant
        ourOrgRole = "clinic"
        partnerOrgRole = "pharmacy"
        notes = "should fail"
    } | ConvertTo-Json -Compress)
    Invoke-RestMethod "$BaseUrl/api/connect/org-links/invite" -Method POST -Headers $h1 `
        -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($bad))
    throw "expected role mismatch to fail"
} catch {
    if ($_.Exception.Message -match "expected role mismatch") { throw }
    Write-Host "[OK] role mismatch rejected" -ForegroundColor Green
}

$h2 = Login $PartnerTenant
Write-Host "[OK] Login $PartnerTenant" -ForegroundColor Green

$clinicProfile = Invoke-RestMethod "$BaseUrl/api/connect/org-profile" -Headers $h2
if ($clinicProfile.orgKind -ne "clinic") { throw "clinic orgKind expected clinic" }

$pending = Invoke-RestMethod "$BaseUrl/api/connect/org-links/pending" -Headers $h2
$incoming = @($pending) | Where-Object { $_.id -eq $link.id } | Select-Object -First 1
if (-not $incoming) { throw "partner pending queue missing invite $($link.id)" }
if ($incoming.linkStatus -ne "pending_our_approval") { throw "expected pending_our_approval POV, got $($incoming.linkStatus)" }
Write-Host "[OK] partner sees pending_our_approval" -ForegroundColor Green

$accepted = Invoke-RestMethod "$BaseUrl/api/connect/org-links/$($link.id)/accept" -Method POST -Headers $h2
if ($accepted.linkStatus -ne "active") { throw "expected active after accept, got $($accepted.linkStatus)" }
Write-Host "[OK] accept -> active" -ForegroundColor Green

$active = Invoke-RestMethod "$BaseUrl/api/connect/org-links?status=active" -Headers $h1
$found = @($active) | Where-Object { $_.id -eq $link.id } | Select-Object -First 1
if (-not $found) { throw "initiator active list missing $($link.id)" }
Write-Host "[OK] initiator sees active partner" -ForegroundColor Green

Write-Host "`n=== Connect C1 smoke PASS ===" -ForegroundColor Green
Write-Host "Admin UI: http://localhost:5173/connect/network"

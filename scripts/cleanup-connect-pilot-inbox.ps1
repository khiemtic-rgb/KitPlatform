# Cleanup Connect pilot inbox on pharmacy + fill demo clinic provider phone/title.
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$PharmacyTenant = "NT_XUANHOA",
    [string]$ClinicTenant = "DEMO_CLINIC",
    [string]$Username = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Stop"

function Login([string]$tenant) {
    $body = @{ tenantCode = $tenant; username = $Username; password = $Password } | ConvertTo-Json
    $r = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" -ContentType "application/json" -Body $body
    return $r.accessToken
}

function AuthHeaders([string]$token) {
    @{ Authorization = "Bearer $token" }
}

Write-Host "=== Connect ops cleanup ===" -ForegroundColor Cyan
$nt = Login $PharmacyTenant
Write-Host "[OK] Login $PharmacyTenant (+ will use $ClinicTenant for reference)"

$events = @(Invoke-RestMethod -Uri "$BaseUrl/api/connect/status-events?status=pending_pharmacy" -Headers (AuthHeaders $nt))
Write-Host "Pending signals: $($events.Count)"
$consumed = 0
$dismissed = 0
foreach ($e in $events) {
    $id = $e.id
    $src = $e.sourceType
    if ($src -eq "clinic_rx") { continue }
    try {
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/connect/status-events/$id/consume" -Headers (AuthHeaders $nt) | Out-Null
        $consumed++
    } catch {
        try {
            Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/connect/status-events/$id/dismiss" -Headers (AuthHeaders $nt) | Out-Null
            $dismissed++
        } catch {
            Write-Host "  skip event $id" -ForegroundColor Yellow
        }
    }
}
Write-Host "Consumed=$consumed dismissed=$dismissed (clinic_rx left for handoff list)"

$handoffs = @(Invoke-RestMethod -Uri "$BaseUrl/api/connect/rx-handoffs?status=pending_pharmacy" -Headers (AuthHeaders $nt))
Write-Host "Pending handoffs: $($handoffs.Count)"
$keep = @($handoffs | Sort-Object { $_.createdAt } -Descending | Select-Object -First 3)
$keepIds = @($keep | ForEach-Object { $_.id })
$old = @($handoffs | Where-Object { $keepIds -notcontains $_.id })
$hxDismiss = 0
foreach ($h in $old) {
    if (-not $h.statusEventId) { continue }
    try {
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/connect/status-events/$($h.statusEventId)/dismiss" -Headers (AuthHeaders $nt) | Out-Null
        $hxDismiss++
    } catch {
        Write-Host "  handoff dismiss skip $($h.prescriptionCode)" -ForegroundColor Yellow
    }
}
Write-Host "Old handoff signals dismissed=$hxDismiss (kept $($keepIds.Count) newest)"

$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
if (Test-Path $psql) {
    $env:PGPASSWORD = "pharmacore_dev_2026"
    & $psql -h localhost -U pharmacore -d kitplatform -v ON_ERROR_STOP=1 -c "UPDATE pack_clinic.clinic_provider SET phone = COALESCE(NULLIF(BTRIM(phone), ''), '0901000001'), title = COALESCE(NULLIF(BTRIM(title), ''), 'BS'), updated_at = NOW() WHERE tenant_id = '11111111-1111-1111-1111-111111111102' AND deleted_at IS NULL AND provider_code = 'BS01';"
    Write-Host "[OK] DEMO_CLINIC BS01 phone/title filled"
} else {
    Write-Host "[!] psql not found - skip BS profile SQL" -ForegroundColor Yellow
}

Write-Host "=== DONE ===" -ForegroundColor Green

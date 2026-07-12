# G1 monitoring snapshot (local/staging)
param(
    [string]$BaseUrl = "http://localhost:5290",
    [string]$Tenant = "DEMO_PHARMACY",
    [string]$Phone = "0909123456",
    [string]$Otp = "000000"
)

$ErrorActionPreference = "Stop"
Write-Host "=== P11b G1 monitoring snapshot ===" -ForegroundColor Cyan
Write-Host "Base=$BaseUrl"

$health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -TimeoutSec 5
Write-Host "[OK] health=$($health.status)"

$otpBody = @{ tenantCode = $Tenant; phone = $Phone } | ConvertTo-Json
$otpRes = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/customer-app/auth/request-otp" -ContentType "application/json" -Body $otpBody
$code = if ($otpRes.pilotCode) { $otpRes.pilotCode } else { $Otp }
$hasPilot = [bool]$otpRes.pilotCode
Write-Host "[OK] OTP request pilotCode=$hasPilot"

$verifyBody = @{ tenantCode = $Tenant; phone = $Phone; code = $code } | ConvertTo-Json
$auth = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/customer-app/auth/verify-otp" -ContentType "application/json" -Body $verifyBody
$token = $auth.accessToken
$headers = @{ Authorization = "Bearer $token" }

$push = Invoke-RestMethod -Uri "$BaseUrl/api/customer-app/push/status" -Headers $headers
Write-Host "[OK] push supported=$($push.supported) enabled=$($push.enabled) subscribed=$($push.subscribed)"

$homeSummary = Invoke-RestMethod -Uri "$BaseUrl/api/customer-app/overview/home-summary" -Headers $headers
$ci = $homeSummary.connectInbox
if ($null -ne $ci) {
    $n = 0
    if ($ci.items) { $n = @($ci.items).Count }
    Write-Host "[OK] connectInbox enabled=$($ci.connectEnabled) items=$n"
} else {
    Write-Host "[!] connectInbox missing on home-summary - rebuild API?" -ForegroundColor Yellow
}

Write-Host "=== Snapshot DONE ===" -ForegroundColor Green
Write-Host "Next: CORS HTTPS origins; UAT 6 scenarios on pilot pharmacy tenants."

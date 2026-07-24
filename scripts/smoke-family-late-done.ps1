# Read-only smoke: late-done flags + accountability glance
$ErrorActionPreference = 'Stop'
$base = if ($env:FAMILYOS_API) { $env:FAMILYOS_API.TrimEnd('/') } else { 'http://127.0.0.1:5290/api' }
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -ContentType 'application/json' -Body '{"tenantCode":"DEMO_FAMILY","username":"admin","password":"Admin@123"}'
$token = $login.accessToken
if (-not $token) { $token = $login.AccessToken }
$h = @{ Authorization = "Bearer $token" }
$fid = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
$flow = Invoke-RestMethod -Method Post -Headers $h -Uri "$base/family-os/families/$fid/day-flows/ensure" -ContentType 'application/json' -Body '{}'
$late = @($flow.commitments | Where-Object { $_.isLateDone }).Count
$g = Invoke-RestMethod -Headers $h -Uri "$base/family-os/families/$fid/accountability-glance"
$t = $g.days | Where-Object { $_.date -eq $g.today } | Select-Object -First 1
Write-Output "late=$late beautiful=$($g.todayIsBeautifulDay) streak=$($g.currentStreak) childLate=$($t.childLateDone) open=$($t.childOpen) dayBeautiful=$($t.isBeautifulDay) scored=$($t.isScored)"

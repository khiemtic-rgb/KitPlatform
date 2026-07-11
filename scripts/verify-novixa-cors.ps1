<#
.SYNOPSIS
  Verify browser CORS for every Novixa SPA origin against the live API.

.EXAMPLE
  .\scripts\verify-novixa-cors.ps1
  .\scripts\verify-novixa-cors.ps1 -ApiBaseUrl https://api.novixa.vn
#>
param(
    [string]$ApiBaseUrl = 'https://api.novixa.vn'
)

$ErrorActionPreference = 'Stop'
$api = $ApiBaseUrl.TrimEnd('/')
$origins = @(
    'https://admin.novixa.vn',
    'https://app.novixa.vn',
    'https://pos.novixa.vn',
    'https://survey.novixa.vn',
    'https://prescriber.novixa.vn',
    'https://partner.novixa.vn'
)

Write-Host "=== Novixa CORS verify → $api ===" -ForegroundColor Cyan
$failed = 0
foreach ($origin in $origins) {
    $tmp = [IO.Path]::GetTempFileName()
    try {
        $null = & curl.exe -sS -D $tmp -o NUL -H "Origin: $origin" "$api/api/health"
        $headers = Get-Content $tmp -Raw
        if ($headers -match [regex]::Escape("Access-Control-Allow-Origin: $origin")) {
            Write-Host "[OK] $origin" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] $origin — missing Access-Control-Allow-Origin" -ForegroundColor Red
            $failed++
        }
    } catch {
        Write-Host "[FAIL] $origin — $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    } finally {
        Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    }
}

if ($failed -gt 0) {
    Write-Host "`n$failed origin(s) failed. Fix /etc/kit-platform/api.env via ensure-novixa-cors-env.sh and restart API." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll SPA origins OK." -ForegroundColor Green

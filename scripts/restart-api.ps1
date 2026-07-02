$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "api-dev.ps1")



Write-Host "=== Restart PharmaCore API ===" -ForegroundColor Cyan



Stop-ApiDevAll



Write-Host ">> Kiem tra PostgreSQL..." -ForegroundColor Yellow

& (Join-Path $PSScriptRoot "check-postgres.ps1")

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }



Write-Host ">> Migration P10b (i18n / en-US)..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "apply-064-p10b-i18n.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ">> Migration 065 (Customer Engagement)..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "apply-065-customer-engagement.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }



Write-Host ">> Build + khoi dong API nen (5290/7224)..." -ForegroundColor Yellow

try {

    Start-ApiDevBackground

    Start-ApiDevWatcher

}

catch {

    Write-Host "[LOI] $($_.Exception.Message)" -ForegroundColor Red

    exit 1

}



$httpOk = Test-ApiDevHealth

$httpsOk = $false



if ($httpOk) {

    Write-Host "[OK] API (HTTP :5290): ok" -ForegroundColor Green

}

else {

    Write-Host "[LOI] API HTTP :5290 chua phan hoi." -ForegroundColor Red

}



try {

    $httpsCode = & curl.exe -k -s -o NUL -w "%{http_code}" "https://localhost:7224/api/health"

    if ($httpsCode -eq "200") {

        Write-Host "[OK] API (HTTPS :7224): ok" -ForegroundColor Green

        $httpsOk = $true

    }

    else {

        throw "HTTP $httpsCode"

    }

}

catch {

    Write-Host '[LOI] API HTTPS :7224 chua phan hoi.' -ForegroundColor Yellow

    Write-Host "      Thu: dotnet dev-certs https --trust" -ForegroundColor Yellow

}



if (-not $httpOk) {

    Write-Host '[LOI] Xem log: .dev\api.err.log' -ForegroundColor Red

    exit 1

}



Write-Host ""

Write-Host "Swagger HTTP:  http://localhost:5290/swagger" -ForegroundColor DarkGray

Write-Host "Swagger HTTPS: https://localhost:7224/swagger" -ForegroundColor DarkGray

Write-Host "Admin:         http://localhost:5173" -ForegroundColor DarkGray

Write-Host "Customer:      http://localhost:5174" -ForegroundColor DarkGray

Write-Host "Log API:       .dev\api.log" -ForegroundColor DarkGray



if (-not $httpsOk) { exit 1 }


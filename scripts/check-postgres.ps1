$ErrorActionPreference = "Stop"

$dbHost = "localhost"
$dbPort = 5432
$dbName = "pharmacore"
$dbUser = "pharmacore"
$dbPass = "pharmacore_dev_2026"

$pgIsready = @(
    "C:\Program Files\PostgreSQL\18\bin\pg_isready.exe",
    "C:\Program Files\PostgreSQL\17\bin\pg_isready.exe",
    "C:\Program Files\PostgreSQL\16\bin\pg_isready.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

$psql = @(
    "C:\Program Files\PostgreSQL\18\bin\psql.exe",
    "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    "C:\Program Files\PostgreSQL\16\bin\psql.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $pgIsready -or -not $psql) {
    Write-Host "[LOI] Khong tim thay PostgreSQL (psql/pg_isready)." -ForegroundColor Red
    Write-Host "      Cai PostgreSQL 16+ hoac them vao PATH." -ForegroundColor Yellow
    exit 1
}

$service = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($service -and $service.Status -ne "Running") {
    Write-Host ">> Dang bat service $($service.Name)..." -ForegroundColor Yellow
    try {
        Start-Service $service.Name
        Start-Sleep -Seconds 2
    }
    catch {
        Write-Host "[LOI] Khong bat duoc PostgreSQL. Chay PowerShell/CMD as Administrator." -ForegroundColor Red
        exit 1
    }
}

& $pgIsready -h $dbHost -p $dbPort | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[LOI] PostgreSQL chua lang nghe tren ${dbHost}:${dbPort}." -ForegroundColor Red
    exit 1
}

$env:PGPASSWORD = $dbPass
try {
    & $psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -t -A -c "SELECT 1" | Out-Null
}
catch {
    Write-Host "[LOI] Khong ket noi database '$dbName' (user: $dbUser)." -ForegroundColor Red
    Write-Host "      Chay scripts\setup-and-migrate.bat de tao DB + migration." -ForegroundColor Yellow
    exit 1
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "[OK] PostgreSQL + database '$dbName' san sang." -ForegroundColor Green
exit 0

$ErrorActionPreference = 'Stop'
$hba = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf'
$bak = 'C:\Program Files\PostgreSQL\18\data\pg_hba.conf.kitplatform.bak'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if (-not (Test-Path $bak)) { Copy-Item $hba $bak -Force }

$content = Get-Content $hba -Raw
$new = $content -replace '(?m)^host\s+all\s+all\s+127\.0\.0\.1/32\s+scram-sha-256\s*$', 'host    all             all             127.0.0.1/32            trust'
$new = $new -replace '(?m)^host\s+all\s+all\s+::1/128\s+scram-sha-256\s*$', 'host    all             all             ::1/128                 trust'
Set-Content -Path $hba -Value $new -NoNewline

Restart-Service postgresql-x64-18 -Force
Start-Sleep -Seconds 3

& $psql -U postgres -h 127.0.0.1 -d postgres -v ON_ERROR_STOP=1 -f (Join-Path $root 'migrations\000_setup_database.sql')

$pharma = & $psql -U postgres -h 127.0.0.1 -d postgres -t -A -c "SELECT 1 FROM pg_database WHERE datname='pharmacore'"
if ($pharma -eq '1') {
    & $psql -U postgres -h 127.0.0.1 -d postgres -v ON_ERROR_STOP=1 -c 'DROP DATABASE IF EXISTS kitplatform;'
    & $psql -U postgres -h 127.0.0.1 -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE kitplatform OWNER kitplatform TEMPLATE pharmacore;'
}

Copy-Item $bak $hba -Force
Restart-Service postgresql-x64-18 -Force
Start-Sleep -Seconds 2

$env:PGPASSWORD = 'kitplatform_dev_2026'
& $psql -U kitplatform -h 127.0.0.1 -d kitplatform -c "SELECT current_database(), current_user, (SELECT count(*) FROM information_schema.tables WHERE table_schema='public') AS public_tables;"
Write-Host 'KITPLATFORM_DB_OK'

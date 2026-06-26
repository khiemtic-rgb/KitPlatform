# Dung Vite dev tren port 5173 (admin) va 5174 (customer).
$ErrorActionPreference = "SilentlyContinue"

foreach ($port in 5173, 5174) {
    Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object {
            $proc = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
            if ($proc -and ($proc.ProcessName -eq "node" -or $proc.ProcessName -eq "cmd")) {
                Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
            }
        }
}

Write-Host "[OK] Da giai phong port 5173/5174 (neu co Vite cu)." -ForegroundColor Green

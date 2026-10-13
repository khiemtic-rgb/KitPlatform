# Xuất OpenAPI spec từ KitPlatform.Api (không cần API đang chạy).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $root "src\KitPlatform.Api\KitPlatform.Api.csproj"
$outDir = Join-Path $root "client\admin\openapi"
$outFile = Join-Path $outDir "swagger.json"
$tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("kitplatform-swagger-" + [guid]::NewGuid().ToString("n") + ".json")

function Assert-LowStockPagedContract([string]$Path) {
    $text = Get-Content -Raw -Path $Path
    $marker = '"/api/inventory/stock/low-stock"'
    $start = $text.IndexOf($marker)
    if ($start -lt 0) {
        throw "Refusing to replace swagger.json: missing GET /api/inventory/stock/low-stock."
    }

    $window = $text.Substring($start, [Math]::Min(4000, $text.Length - $start))
    if ($window -notmatch '"\$ref"\s*:\s*"#/components/schemas/PagedLowStockProductsResult"') {
        throw "Refusing to replace swagger.json: low-stock 200 is not PagedLowStockProductsResult."
    }

    $schemaKey = '"PagedLowStockProductsResult":'
    $schemaStart = $text.IndexOf($schemaKey)
    if ($schemaStart -lt 0) {
        throw "Refusing to replace swagger.json: schema PagedLowStockProductsResult is missing."
    }

    $schemaWindow = $text.Substring($schemaStart, [Math]::Min(1200, $text.Length - $schemaStart))
    foreach ($name in @("items", "total", "page", "pageSize")) {
        if ($schemaWindow -notmatch ('"' + $name + '"')) {
            throw "Refusing to replace swagger.json: PagedLowStockProductsResult is missing $name."
        }
    }
}

Push-Location $root
try {
    Write-Host "Restoring dotnet tools..."
    dotnet tool restore | Out-Null

    $dll = Join-Path $root "src\KitPlatform.Api\bin\Debug\net10.0\KitPlatform.Api.dll"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    Write-Host "Building API..."
    $buildOk = $true
    dotnet build $apiProject -c Debug 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { $buildOk = $false }

    if ($buildOk -and (Test-Path $dll)) {
        Write-Host "Exporting OpenAPI (offline) -> $tempFile"
        dotnet swagger tofile --output $tempFile $dll v1
        if ($LASTEXITCODE -eq 0) {
            Assert-LowStockPagedContract $tempFile
            Copy-Item $tempFile $outFile -Force
            Write-Host "OK: $(Get-Item $outFile | Select-Object -ExpandProperty Length) bytes"
            return
        }
        Write-Warning "swagger tofile failed; trying live API..."
    }
    else {
        Write-Warning "API build skipped (DLL locked?). Trying live API at http://localhost:5290 ..."
    }

    $liveUrl = "http://localhost:5290/swagger/v1/swagger.json"
    Invoke-WebRequest -Uri $liveUrl -UseBasicParsing -OutFile $tempFile
    Assert-LowStockPagedContract $tempFile
    Copy-Item $tempFile $outFile -Force
    Write-Host "OK (live): $(Get-Item $outFile | Select-Object -ExpandProperty Length) bytes from $liveUrl"
}
finally {
    if (Test-Path $tempFile) { Remove-Item $tempFile -Force }
    Pop-Location
}


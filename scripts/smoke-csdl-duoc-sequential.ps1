# Sequential CSDL duoc API smoke (API 1.1): login -> drugs -> stock-in OB -> stock-out.
# Credentials: .dev/national-drug.secrets.json (gitignored). Never prints password/token.
# Usage: powershell -File scripts/smoke-csdl-duoc-sequential.ps1
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$secretsPath = Join-Path $repo '.dev\national-drug.secrets.json'
$outDir = Join-Path $repo '.tmp'
$outLog = Join-Path $outDir 'csdl-sequential-smoke.json'

if (-not (Test-Path $secretsPath)) {
    throw "Missing secrets file: $secretsPath"
}

$secrets = Get-Content $secretsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$username = $null
$password = $null
$baseUrl = 'https://api-sandbox.csdlduoc.com.vn/v2'

if ($secrets.PSObject.Properties.Name -contains 'Username') { $username = [string]$secrets.Username }
if ($secrets.PSObject.Properties.Name -contains 'Password') { $password = [string]$secrets.Password }
if ($secrets.PSObject.Properties.Name -contains 'BaseUrl' -and $secrets.BaseUrl) {
    $baseUrl = [string]$secrets.BaseUrl.TrimEnd('/')
}
if ($secrets.PSObject.Properties.Name -contains 'NationalDrugCatalog') {
    $nd = $secrets.NationalDrugCatalog
    if (-not $username -and $nd.Username) { $username = [string]$nd.Username }
    if (-not $password -and $nd.Password) { $password = [string]$nd.Password }
    if ($nd.BaseUrl) { $baseUrl = [string]$nd.BaseUrl.TrimEnd('/') }
}

if (-not $username -and $env:CSDL_USERNAME) { $username = $env:CSDL_USERNAME }
if (-not $password -and $env:CSDL_PASSWORD) { $password = $env:CSDL_PASSWORD }

if ([string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrWhiteSpace($password)) {
    throw 'Username/Password not found in secrets or env CSDL_USERNAME/CSDL_PASSWORD'
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$pwdB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($password))
$password = $null
$secrets = $null

$report = [ordered]@{
    ran_at   = (Get-Date).ToString('o')
    base_url = $baseUrl
    username = $username
    steps    = New-Object System.Collections.ArrayList
}

function Add-Step([string]$id, [string]$endpoint, [int]$http, [hashtable]$body, [string]$note) {
    [void]$script:report.steps.Add([ordered]@{
        id       = $id
        endpoint = $endpoint
        http     = $http
        body     = $body
        note     = $note
    })
    Write-Host ("[{0}] HTTP {1} {2} - {3}" -f $id, $http, $endpoint, $note)
}

# A1 - login
$loginBody = "username=$([uri]::EscapeDataString($username))&password=$([uri]::EscapeDataString($pwdB64))"
$loginResp = Invoke-WebRequest -Uri "$baseUrl/auth/login" -Method POST `
    -ContentType 'application/x-www-form-urlencoded' -Body $loginBody -UseBasicParsing
$loginJson = $loginResp.Content | ConvertFrom-Json
$token = [string]$loginJson.access_token
if ([string]::IsNullOrWhiteSpace($token)) { throw 'No access_token in login response' }
Add-Step 'A1' '/auth/login' ([int]$loginResp.StatusCode) @{
    token_type  = [string]$loginJson.token_type
    expires_in  = $loginJson.expires_in
    has_refresh = [bool]$loginJson.refresh_token
} 'OK - token received'

$headers = @{ Authorization = "Bearer $token" }

# B1 - drugs page
$drugsResp = Invoke-WebRequest -Uri "$baseUrl/master/drugs?page=1&page_size=3" -Headers $headers -UseBasicParsing
$drugsJson = $drugsResp.Content | ConvertFrom-Json
Add-Step 'B1' '/master/drugs?page=1&page_size=3' ([int]$drugsResp.StatusCode) @{
    total = $drugsJson.total
    page  = $drugsJson.page
    count = @($drugsJson.data).Count
} ("OK - total={0}" -f $drugsJson.total)

# B2 - drug detail
$drugId = '893110130226'
$detailResp = Invoke-WebRequest -Uri "$baseUrl/master/drugs/$drugId" -Headers $headers -UseBasicParsing
$detail = $detailResp.Content | ConvertFrom-Json
$unitId = 'U31'
if ($detail.packagings -and $detail.packagings.Count -gt 0 -and $detail.packagings[0].unit_id) {
    $unitId = [string]$detail.packagings[0].unit_id
}
Add-Step 'B2' "/master/drugs/$drugId" ([int]$detailResp.StatusCode) @{
    id      = [string]$detail.id
    name    = [string]$detail.name
    unit_id = $unitId
} ("OK - unit_id={0}" -f $unitId)

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$txDate = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')

# C0 - stock-in opening-balance
$obRef = "NVX-OB-$stamp"
$obObj = @{
    transaction_date = $txDate
    reason           = 'opening-balance'
    reference_number = $obRef
    note             = 'Novixa sequential smoke - opening-balance API 1.1'
    items            = @(
        @{
            drug_id     = $drugId
            unit_id     = $unitId
            quantity    = 10
            batch_no    = "OB$stamp"
            expiry_date = '2027-12-31'
            price       = 1000.0
        }
    )
}
$obPayload = $obObj | ConvertTo-Json -Depth 6

$obTxn = $null
try {
    $obResp = Invoke-WebRequest -Uri "$baseUrl/transactions/stock-in" -Method POST `
        -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($obPayload)) -UseBasicParsing
    $obBody = $obResp.Content | ConvertFrom-Json
    $obTxn = [string]$obBody.transaction_id
    Add-Step 'C0' '/transactions/stock-in' ([int]$obResp.StatusCode) @{
        transaction_id   = $obTxn
        reference_number = $obRef
        reason           = 'opening-balance'
        status_field     = [string]$obBody.status
    } ("create OK - txn={0}" -f $obTxn)
}
catch {
    $raw = ''
    $code = 0
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $raw = $reader.ReadToEnd()
        } catch { $raw = $_.Exception.Message }
    }
    else { $raw = $_.ToString() }
    Add-Step 'C0' '/transactions/stock-in' $code @{ raw = $raw; reference_number = $obRef } 'FAIL'
}

# C0s - stock-in status poll
if ($obTxn) {
    Start-Sleep -Seconds 2
    $stIn = $null
    for ($i = 0; $i -lt 6; $i++) {
        $stResp = Invoke-WebRequest -Uri "$baseUrl/transactions/stock-in/$obTxn/status" -Headers $headers -UseBasicParsing
        $stIn = $stResp.Content | ConvertFrom-Json
        $stName = [string]$stIn.status
        if ($stName -and $stName -notin @('Pending', 'Processing', 'Received', 'Queued')) { break }
        Start-Sleep -Seconds 2
    }
    $msgs = (@($stIn.messages) | ForEach-Object { "$_" }) -join '; '
    Add-Step 'C0s' '/transactions/stock-in/{id}/status' 200 @{
        transaction_id = $obTxn
        status         = [string]$stIn.status
        messages       = @($stIn.messages)
    } ("status={0} {1}" -f $stIn.status, $msgs)
}

# C1 - stock-out sale-retail
$outRef = "NVX-SO-$stamp"
$outObj = @{
    transaction_date = $txDate
    reason           = 'sale-retail'
    reference_number = $outRef
    note             = 'Novixa sequential smoke - sale-retail after opening-balance'
    items            = @(
        @{
            drug_id     = $drugId
            unit_id     = $unitId
            quantity    = 1
            batch_no    = "OB$stamp"
            expiry_date = '2027-12-31'
            price       = 1500.0
        }
    )
}
$outPayload = $outObj | ConvertTo-Json -Depth 6

$outTxn = $null
try {
    $outResp = Invoke-WebRequest -Uri "$baseUrl/transactions/stock-out" -Method POST `
        -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($outPayload)) -UseBasicParsing
    $outBody = $outResp.Content | ConvertFrom-Json
    $outTxn = [string]$outBody.transaction_id
    Add-Step 'C1' '/transactions/stock-out' ([int]$outResp.StatusCode) @{
        transaction_id   = $outTxn
        reference_number = $outRef
        reason           = 'sale-retail'
        status_field     = [string]$outBody.status
    } ("create OK - txn={0}" -f $outTxn)
}
catch {
    $raw = ''
    $code = 0
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $raw = $reader.ReadToEnd()
        } catch { $raw = $_.Exception.Message }
    }
    else { $raw = $_.ToString() }
    Add-Step 'C1' '/transactions/stock-out' $code @{ raw = $raw; reference_number = $outRef } 'FAIL'
}

# C1s - stock-out status
if ($outTxn) {
    Start-Sleep -Seconds 2
    $stOut = $null
    for ($i = 0; $i -lt 6; $i++) {
        $stResp = Invoke-WebRequest -Uri "$baseUrl/transactions/stock-out/$outTxn/status" -Headers $headers -UseBasicParsing
        $stOut = $stResp.Content | ConvertFrom-Json
        $stName = [string]$stOut.status
        if ($stName -and $stName -notin @('Pending', 'Processing', 'Received', 'Queued')) { break }
        Start-Sleep -Seconds 2
    }
    $msgs = (@($stOut.messages) | ForEach-Object { "$_" }) -join '; '
    Add-Step 'C1s' '/transactions/stock-out/{id}/status' 200 @{
        transaction_id = $outTxn
        status         = [string]$stOut.status
        messages       = @($stOut.messages)
    } ("status={0} {1}" -f $stOut.status, $msgs)
}

$token = $null
$pwdB64 = $null

($report | ConvertTo-Json -Depth 10) | Set-Content -Path $outLog -Encoding UTF8
Write-Host ""
Write-Host "Report written: $outLog"
Write-Host "Done."

# KPA-PRODUCT-PROOF-008R30 — Customer Context Rollback Guard Verification
# Test script for endpoint-specific rollback capability

param(
    [string]$ApiBaseUrl = "http://localhost:5290",
    [string]$TenantCode = "DEMO_PHARMACY",
    [string]$Username = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Continue"
$testStartTime = Get-Date

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "KPA-PRODUCT-PROOF-008R30 Verification" -ForegroundColor Cyan
Write-Host "Customer Context Rollback Guard Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "Start Time: $testStartTime`n"

# Step 1: Login and get auth token
Write-Host "[1/8] Authenticating..." -ForegroundColor Yellow
$loginBody = @{
    tenantCode = $TenantCode
    username = $Username
    password = $Password
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/auth/login" -ContentType "application/json" -Body $loginBody
    $token = $loginResponse.accessToken
    Write-Host "  ✅ Authentication successful" -ForegroundColor Green
    Write-Host "  Token: $($token.Substring(0, 20))..." -ForegroundColor DarkGray
} catch {
    Write-Host "  ❌ Authentication failed: $_" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Step 2: Get a valid customer ID and warehouse ID
Write-Host "`n[2/8] Getting test customer and warehouse..." -ForegroundColor Yellow
try {
    $customers = Invoke-RestMethod -Method Get -Uri "$ApiBaseUrl/api/sales/customers?pageSize=1" -Headers $headers
    if ($customers.items.Count -gt 0) {
        $customerId = $customers.items[0].id
        Write-Host "  ✅ Test Customer ID: $customerId" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️ No customers found, using dummy GUID" -ForegroundColor DarkYellow
        $customerId = "00000000-0000-0000-0000-000000000001"
    }
} catch {
    Write-Host "  ⚠️ Could not fetch customers: $_" -ForegroundColor DarkYellow
    $customerId = "00000000-0000-0000-0000-000000000001"
}

try {
    $warehouses = Invoke-RestMethod -Method Get -Uri "$ApiBaseUrl/api/inventory/warehouses" -Headers $headers
    if ($warehouses.Count -gt 0) {
        $warehouseId = $warehouses[0].id
        Write-Host "  ✅ Test Warehouse ID: $warehouseId" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️ No warehouses found, using dummy GUID" -ForegroundColor DarkYellow
        $warehouseId = "00000000-0000-0000-0000-000000000001"
    }
} catch {
    Write-Host "  ⚠️ Could not fetch warehouses: $_" -ForegroundColor DarkYellow
    $warehouseId = "00000000-0000-0000-0000-000000000001"
}

# TEST 1: Normal operation (guard disabled/absent)
Write-Host "`n[3/8] TEST 1: Customer Context - Normal Operation" -ForegroundColor Yellow
Write-Host "  Configuration: Pharmacy:DisableCustomerContext = false (or absent)" -ForegroundColor DarkGray
try {
    $response = Invoke-WebRequest -Uri "$ApiBaseUrl/api/sales/pos/customer-context/$customerId?warehouseId=$warehouseId" -Headers $headers -Method Get -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "  ✅ PASS: Returns 200 OK" -ForegroundColor Green
        $data = $response.Content | ConvertFrom-Json
        Write-Host "  Customer Name: $($data.customerName)" -ForegroundColor DarkGray
    } elseif ($response.StatusCode -eq 404) {
        Write-Host "  ✅ PASS: Returns 404 Not Found (customer does not exist)" -ForegroundColor Green
    } else {
        Write-Host "  ❌ FAIL: Unexpected status $($response.StatusCode)" -ForegroundColor Red
    }
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 404) {
        Write-Host "  ✅ PASS: Returns 404 Not Found (customer does not exist)" -ForegroundColor Green
    } else {
        Write-Host "  ❌ FAIL: Error - $statusCode - $_" -ForegroundColor Red
    }
}

# TEST 2: Verify other endpoints work (baseline)
Write-Host "`n[4/8] TEST 2: Other Endpoints - Baseline Check" -ForegroundColor Yellow
Write-Host "  Testing unrelated endpoints to confirm they work before guard enabled..." -ForegroundColor DarkGray

$otherEndpoints = @(
    @{ Name = "GET /api/sales/customers"; Uri = "$ApiBaseUrl/api/sales/customers?pageSize=1" }
    @{ Name = "GET /api/sales/orders"; Uri = "$ApiBaseUrl/api/sales/orders?pageSize=1" }
)

$otherEndpointsPass = $true
foreach ($endpoint in $otherEndpoints) {
    try {
        $response = Invoke-WebRequest -Uri $endpoint.Uri -Headers $headers -Method Get -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "  ✅ $($endpoint.Name) - OK" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️ $($endpoint.Name) - Status $($response.StatusCode)" -ForegroundColor DarkYellow
        }
    } catch {
        Write-Host "  ❌ $($endpoint.Name) - FAILED: $_" -ForegroundColor Red
        $otherEndpointsPass = $false
    }
}

if ($otherEndpointsPass) {
    Write-Host "  ✅ PASS: All baseline endpoints working" -ForegroundColor Green
} else {
    Write-Host "  ❌ FAIL: Some baseline endpoints failed" -ForegroundColor Red
}

# TEST 3: Enable guard via environment variable and restart API
Write-Host "`n[5/8] TEST 3: Enabling Rollback Guard" -ForegroundColor Yellow
Write-Host "  ⚠️ MANUAL STEP REQUIRED:" -ForegroundColor Red
Write-Host "  1. Stop the API (Ctrl+C in API terminal)" -ForegroundColor White
Write-Host "  2. Set environment variable: `$env:Pharmacy__DisableCustomerContext = `"true`"" -ForegroundColor White
Write-Host "  3. Restart API: dotnet run --no-build" -ForegroundColor White
Write-Host "  4. Press Enter to continue testing..." -ForegroundColor White
Read-Host

# Wait for API to be ready
Write-Host "  Waiting for API to be ready..." -ForegroundColor DarkGray
Start-Sleep -Seconds 3

# Re-authenticate after API restart
try {
    $loginResponse = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/auth/login" -ContentType "application/json" -Body $loginBody
    $token = $loginResponse.accessToken
    $headers["Authorization"] = "Bearer $token"
    Write-Host "  ✅ Re-authenticated" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Re-authentication failed: $_" -ForegroundColor Red
    exit 1
}

# TEST 4: Customer Context should return 503
Write-Host "`n[6/8] TEST 4: Customer Context - Guard Enabled (Rollback Mode)" -ForegroundColor Yellow
Write-Host "  Configuration: Pharmacy:DisableCustomerContext = true" -ForegroundColor DarkGray
try {
    $response = Invoke-WebRequest -Uri "$ApiBaseUrl/api/sales/pos/customer-context/$customerId?warehouseId=$warehouseId" -Headers $headers -Method Get -UseBasicParsing
    Write-Host "  ❌ FAIL: Should return 503, got $($response.StatusCode)" -ForegroundColor Red
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 503) {
        Write-Host "  ✅ PASS: Returns 503 Service Unavailable" -ForegroundColor Green
        try {
            $errorStream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorStream)
            $errorBody = $reader.ReadToEnd()
            $reader.Close()
            $errorData = $errorBody | ConvertFrom-Json
            if ($errorData.message -like "*temporarily unavailable*") {
                Write-Host "  ✅ PASS: Response message correct: '$($errorData.message)'" -ForegroundColor Green
            } else {
                Write-Host "  ⚠️ WARNING: Unexpected message: '$($errorData.message)'" -ForegroundColor DarkYellow
            }
        } catch {
            Write-Host "  ⚠️ Could not parse error body" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "  ❌ FAIL: Expected 503, got $statusCode" -ForegroundColor Red
    }
}

# TEST 5: Other endpoints still work
Write-Host "`n[7/8] TEST 5: Other Endpoints - Still Working" -ForegroundColor Yellow
Write-Host "  Verifying other endpoints unaffected by guard..." -ForegroundColor DarkGray

$otherEndpointsPassAfter = $true
foreach ($endpoint in $otherEndpoints) {
    try {
        $response = Invoke-WebRequest -Uri $endpoint.Uri -Headers $headers -Method Get -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "  ✅ $($endpoint.Name) - OK" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️ $($endpoint.Name) - Status $($response.StatusCode)" -ForegroundColor DarkYellow
        }
    } catch {
        Write-Host "  ❌ $($endpoint.Name) - FAILED: $_" -ForegroundColor Red
        $otherEndpointsPassAfter = $false
    }
}

if ($otherEndpointsPassAfter) {
    Write-Host "  ✅ PASS: All other endpoints still working" -ForegroundColor Green
} else {
    Write-Host "  ❌ FAIL: Some endpoints failed" -ForegroundColor Red
}

# TEST 6: Disable guard and verify restoration
Write-Host "`n[8/8] TEST 6: Disabling Guard - Restoration" -ForegroundColor Yellow
Write-Host "  ⚠️ MANUAL STEP REQUIRED:" -ForegroundColor Red
Write-Host "  1. Stop the API (Ctrl+C in API terminal)" -ForegroundColor White
Write-Host "  2. Remove environment variable: `$env:Pharmacy__DisableCustomerContext = `"`"" -ForegroundColor White
Write-Host "  3. Restart API: dotnet run --no-build" -ForegroundColor White
Write-Host "  4. Press Enter to continue testing..." -ForegroundColor White
Read-Host

# Wait for API
Write-Host "  Waiting for API to be ready..." -ForegroundColor DarkGray
Start-Sleep -Seconds 3

# Re-authenticate
try {
    $loginResponse = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/auth/login" -ContentType "application/json" -Body $loginBody
    $token = $loginResponse.accessToken
    $headers["Authorization"] = "Bearer $token"
    Write-Host "  ✅ Re-authenticated" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Re-authentication failed: $_" -ForegroundColor Red
    exit 1
}

# Verify Customer Context works again
Write-Host "  Testing Customer Context endpoint restored..." -ForegroundColor DarkGray
try {
    $response = Invoke-WebRequest -Uri "$ApiBaseUrl/api/sales/pos/customer-context/$customerId?warehouseId=$warehouseId" -Headers $headers -Method Get -UseBasicParsing
    if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 404) {
        Write-Host "  ✅ PASS: Customer Context restored (Status $($response.StatusCode))" -ForegroundColor Green
    } else {
        Write-Host "  ❌ FAIL: Unexpected status $($response.StatusCode)" -ForegroundColor Red
    }
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 404) {
        Write-Host "  ✅ PASS: Customer Context restored (404 Not Found is valid)" -ForegroundColor Green
    } else {
        Write-Host "  ❌ FAIL: Error - $statusCode" -ForegroundColor Red
    }
}

# Summary
$testEndTime = Get-Date
$elapsed = $testEndTime - $testStartTime

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "End Time: $testEndTime"
Write-Host "Elapsed: $($elapsed.TotalSeconds) seconds`n"

Write-Host "Configuration Key: Pharmacy:DisableCustomerContext" -ForegroundColor White
Write-Host "Default Behavior: false (endpoint works normally)" -ForegroundColor White
Write-Host "Rollback Mode: true (endpoint returns 503)" -ForegroundColor White
Write-Host "`nReview the test output above for detailed results." -ForegroundColor White
Write-Host "`n⚠️ NOTE: Frontend and checkout tests blocked by existing Vite issue.`n" -ForegroundColor DarkYellow

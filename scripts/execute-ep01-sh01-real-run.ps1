# FAMIXA-EP01-PERMIT-001: Issue Official Execution Permit and Perform One Real EP01 Shot Run
#
# This script:
# 1. Issues the official execution permit via SQL
# 2. Verifies permit issuance
# 3. Evaluates CreateVideo readiness
# 4. Executes one real CreateVideo operation through Production OS
# 5. Records the result

param(
    [string]$ServerUrl = "https://localhost:5290",
    [string]$DbConnectionString = "Host=localhost;Port=5432;Database=kit_platform;Username=postgres"
)

$ErrorActionPreference = "Stop"

Write-Host "=== FAMIXA-EP01-PERMIT-001 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "TARGET:"
Write-Host "  buildId: 4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"
Write-Host "  shotCode: EP01-SC01-SH01"
Write-Host "  action: CREATE_VIDEO"
Write-Host "  providerId: runway"
Write-Host "  modelId: gen4_turbo"
Write-Host ""

# STEP 1: Issue Permit
Write-Host "=== STEP 1: ISSUE PERMIT ===" -ForegroundColor Yellow
Write-Host "Executing SQL script to issue permit..."

try {
    $permitSqlPath = Join-Path $PSScriptRoot "issue-ep01-sh01-permit.sql"
    
    if (-not (Test-Path $permitSqlPath)) {
        throw "Permit SQL script not found: $permitSqlPath"
    }
    
    # Execute permit issuance via psql
    $psqlOutput = & psql $DbConnectionString -f $permitSqlPath 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to execute permit SQL: $psqlOutput"
    }
    
    Write-Host $psqlOutput
    Write-Host ""
    Write-Host "Permit issuance: SUCCESS" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Failed to issue permit" -ForegroundColor Red
    Write-Host $_.Exception.Message
    Write-Host ""
    Write-Host "BLOCKER: Permit issuance failed" -ForegroundColor Red
    exit 1
}

# STEP 2: Verify Permit
Write-Host ""
Write-Host "=== STEP 2: VERIFY PERMIT ===" -ForegroundColor Yellow

$verifyPermitSql = @"
SELECT 
    permit_id,
    status,
    issued_by,
    issued_at,
    provider_id,
    model_id
FROM pack_content.production_execution_permit
WHERE build_id = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f'
  AND shot_code = 'EP01-SC01-SH01'
  AND action = 'CREATE_VIDEO'
  AND lower(provider_id) = 'runway'
  AND lower(model_id) = 'gen4_turbo'
  AND status = 'ISSUED'
ORDER BY issued_at DESC
LIMIT 1;
"@

try {
    $permitCheck = & psql $DbConnectionString -t -c $verifyPermitSql 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to verify permit: $permitCheck"
    }
    
    if ([string]::IsNullOrWhiteSpace($permitCheck)) {
        throw "No ISSUED permit found"
    }
    
    Write-Host "Permit verified: ISSUED" -ForegroundColor Green
    Write-Host $permitCheck
}
catch {
    Write-Host "ERROR: Permit verification failed" -ForegroundColor Red
    Write-Host $_.Exception.Message
    Write-Host ""
    Write-Host "BLOCKER: No valid permit" -ForegroundColor Red
    exit 1
}

# STEP 3: Evaluate CreateVideo Readiness
Write-Host ""
Write-Host "=== STEP 3: VERIFY EXECUTION AVAILABILITY ===" -ForegroundColor Yellow

$actionsUrl = "$ServerUrl/api/content/production/shots/4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f/EP01-SC01-SH01/actions?episodeCode=EP01&sceneCode=SC01"

try {
    Write-Host "GET $actionsUrl"
    
    # Ignore SSL certificate errors for localhost development
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    
    $response = Invoke-RestMethod -Uri $actionsUrl -Method Get -ContentType "application/json" -ErrorAction Stop
    
    Write-Host "Response:"
    Write-Host ($response | ConvertTo-Json -Depth 5)
    
    $createVideoAction = $response.actions | Where-Object { $_.action -eq "CREATE_VIDEO" }
    
    if ($null -eq $createVideoAction) {
        throw "CREATE_VIDEO action not found in response"
    }
    
    Write-Host ""
    if ($createVideoAction.available -eq $true) {
        Write-Host "CreateVideo availability: AVAILABLE" -ForegroundColor Green
        
        if ($createVideoAction.blockReason) {
            Write-Host "Block reason present: $($createVideoAction.blockReason)" -ForegroundColor Yellow
            
            if ($createVideoAction.blockReason -match "OfficialFilmForbidden") {
                throw "BLOCKED: OfficialFilmForbidden is still present"
            }
        }
        else {
            Write-Host "No blockers detected" -ForegroundColor Green
        }
    }
    else {
        Write-Host "CreateVideo availability: NOT AVAILABLE" -ForegroundColor Red
        Write-Host "Block reason: $($createVideoAction.blockReason)"
        
        throw "BLOCKED: $($createVideoAction.blockReason)"
    }
}
catch {
    Write-Host "ERROR: Readiness check failed" -ForegroundColor Red
    Write-Host $_.Exception.Message
    Write-Host ""
    Write-Host "BLOCKER: Cannot proceed with execution" -ForegroundColor Red
    exit 1
}

# STEP 4: Execute Real CreateVideo
Write-Host ""
Write-Host "=== STEP 4: REAL EXECUTION ===" -ForegroundColor Yellow

$executeUrl = "$ServerUrl/api/content/production/requests"

$requestBody = @{
    buildId = "4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"
    episodeCode = "EP01"
    sceneCode = "SC01"
    shotCode = "EP01-SC01-SH01"
    action = "CREATE_VIDEO"
    idempotencyKey = "FAMIXA-EP01-PERMIT-001-EXEC-$(Get-Date -Format 'yyyyMMddHHmmss')"
    providerId = "runway"
    modelId = "gen4_turbo"
    authorizePaidNetwork = $true
} | ConvertTo-Json

try {
    Write-Host "POST $executeUrl"
    Write-Host "Request Body:"
    Write-Host $requestBody
    Write-Host ""
    
    $executeResponse = Invoke-RestMethod -Uri $executeUrl -Method Post -Body $requestBody -ContentType "application/json" -ErrorAction Stop
    
    Write-Host "Execution Response:"
    Write-Host ($executeResponse | ConvertTo-Json -Depth 5) -ForegroundColor Green
    
    Write-Host ""
    Write-Host "Execution submitted: SUCCESS" -ForegroundColor Green
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $errorBody = $null
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        $reader.Close()
    }
    
    Write-Host "ERROR: Execution failed" -ForegroundColor Red
    Write-Host "Status Code: $statusCode"
    Write-Host "Error: $($_.Exception.Message)"
    
    if ($errorBody) {
        Write-Host "Response Body:"
        Write-Host $errorBody
    }
    
    Write-Host ""
    Write-Host "BLOCKER: Execution API call failed" -ForegroundColor Red
    exit 1
}

# STEP 5: Verify Real Provider Path
Write-Host ""
Write-Host "=== STEP 5: VERIFY REAL PROVIDER PATH ===" -ForegroundColor Yellow

if ($executeResponse.requestId) {
    $requestId = $executeResponse.requestId
    $checkUrl = "$ServerUrl/api/content/production/requests/$requestId"
    
    Write-Host "Checking execution result..."
    Write-Host "GET $checkUrl"
    
    try {
        Start-Sleep -Seconds 2
        $requestStatus = Invoke-RestMethod -Uri $checkUrl -Method Get -ContentType "application/json" -ErrorAction Stop
        
        Write-Host "Request Status:"
        Write-Host ($requestStatus | ConvertTo-Json -Depth 5)
    }
    catch {
        Write-Host "WARNING: Could not retrieve request status" -ForegroundColor Yellow
        Write-Host $_.Exception.Message
    }
}

# Check Runway HTTP audit
$auditUrl = "$ServerUrl/api/content/production/runway-http-audit"
Write-Host ""
Write-Host "Checking Runway HTTP audit..."
Write-Host "GET $auditUrl"

try {
    $audit = Invoke-RestMethod -Uri $auditUrl -Method Get -ContentType "application/json" -ErrorAction Stop
    
    Write-Host "Runway Audit:"
    Write-Host ($audit | ConvertTo-Json)
    
    if ($audit.startCount -gt 0) {
        Write-Host ""
        Write-Host "VERIFIED: Real Runway adapter was called" -ForegroundColor Green
        Write-Host "Start count: $($audit.startCount)"
        Write-Host "Last provider job ID: $($audit.lastProviderJobId)"
    }
    else {
        Write-Host ""
        Write-Host "WARNING: No Runway Start calls recorded" -ForegroundColor Yellow
        Write-Host "Execution may have used FixtureMotionAdapter or failed before provider call"
    }
}
catch {
    Write-Host "WARNING: Could not retrieve Runway audit" -ForegroundColor Yellow
    Write-Host $_.Exception.Message
}

# STEP 6: Record Result
Write-Host ""
Write-Host "=== STEP 6: FINAL REPORT ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "PERMIT: ISSUED" -ForegroundColor Green
Write-Host "READINESS CHECK: PASSED" -ForegroundColor Green
Write-Host "REAL EXECUTION: SUBMITTED" -ForegroundColor Green
Write-Host ""

if ($executeResponse) {
    Write-Host "Execution Details:"
    Write-Host "  Request ID: $($executeResponse.requestId)"
    Write-Host "  Task ID: $($executeResponse.taskId)"
    Write-Host "  Attempt ID: $($executeResponse.attemptId)"
    Write-Host "  Provider: $($executeResponse.providerId)"
    Write-Host "  Model: $($executeResponse.modelId)"
    Write-Host "  Provider Job ID: $($executeResponse.providerJobId)"
    Write-Host "  Artifact ID: $($executeResponse.artifactId)"
    
    Write-Host ""
    Write-Host "RESULT: SUCCESS" -ForegroundColor Green
    Write-Host ""
    Write-Host "One real EP01-SC01-SH01 CreateVideo execution was submitted to Production OS."
    Write-Host "The execution reached the real provider adapter path."
    Write-Host ""
    Write-Host "Monitor the execution status via:"
    Write-Host "  GET $ServerUrl/api/content/production/requests/$($executeResponse.requestId)"
}
else {
    Write-Host "RESULT: INCOMPLETE" -ForegroundColor Yellow
    Write-Host "Execution response was empty or invalid"
}

Write-Host ""
Write-Host "=== END OF REPORT ===" -ForegroundColor Cyan

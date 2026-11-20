# KPA-PRODUCT-PROOF-008R17 — Apply migrations to existing kitplatform_test
param(
    [string]$ConnectionString = "Host=localhost;Port=5432;Database=kitplatform_test;Username=kitplatform;Password=kitplatform_dev_2026"
)

$ErrorActionPreference = "Stop"

$migrationFiles = @(
    "001_extensions.sql",
    "002_identity.sql",
    "003_catalog.sql",
    "004_inventory.sql",
    "005_procurement.sql",
    "006_sales.sql"
)

Write-Host "=== Verify Database Connection ===" -ForegroundColor Cyan
try {
    $conn = New-Object Npgsql.NpgsqlConnection($ConnectionString)
    $conn.Open()
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT current_database()"
    $currentDb = $cmd.ExecuteScalar()
    
    Write-Host "Connected to: $currentDb" -ForegroundColor Green
    
    if ($currentDb -ne "kitplatform_test") {
        Write-Host "ERROR: Wrong database! Expected kitplatform_test, got $currentDb" -ForegroundColor Red
        exit 1
    }
    
    # Check if database is empty
    $cmd.CommandText = "SELECT COUNT(*) FROM pg_tables WHERE schemaname = 'public'"
    $tableCount = $cmd.ExecuteScalar()
    Write-Host "Existing tables in public schema: $tableCount" -ForegroundColor Yellow
    
    $conn.Close()
} catch {
    Write-Host "ERROR: Cannot connect to database" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Apply Migrations ===" -ForegroundColor Cyan

$migrationsDir = "e:\KitPlatform\migrations"

foreach ($migFile in $migrationFiles) {
    $migPath = Join-Path $migrationsDir $migFile
    
    if (-not (Test-Path $migPath)) {
        Write-Host "ERROR: Migration file not found: $migPath" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Applying: $migFile" -ForegroundColor White
    
    try {
        $sql = Get-Content $migPath -Raw -Encoding UTF8
        
        $conn = New-Object Npgsql.NpgsqlConnection($ConnectionString)
        $conn.Open()
        
        # Verify database context
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT current_database()"
        $currentDb = $cmd.ExecuteScalar()
        
        if ($currentDb -ne "kitplatform_test") {
            Write-Host "ERROR: Database context changed to: $currentDb" -ForegroundColor Red
            $conn.Close()
            exit 1
        }
        
        # Execute migration
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $cmd.CommandTimeout = 60
        $cmd.ExecuteNonQuery() | Out-Null
        
        $conn.Close()
        Write-Host "  ✓ $migFile completed" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ ERROR in $migFile" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "=== SUCCESS ===" -ForegroundColor Green
Write-Host "All migrations applied to kitplatform_test"

// KPA-PRODUCT-PROOF-008R17 — Apply migrations to existing kitplatform_test
using Npgsql;
using Dapper;

const string TestDb = "kitplatform_test";
const string TestConnStr = "Host=localhost;Port=5432;Database=kitplatform_test;Username=kitplatform;Password=kitplatform_dev_2026";

var migrationFiles = new[]
{
    "001_extensions.sql",
    "002_identity.sql",
    "003_catalog.sql",
    "004_inventory.sql",
    "005_procurement.sql",
    "006_sales.sql"
};

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== Step 1: Verify Database Connection ===");
Console.ResetColor();

try
{
    await using var conn = new NpgsqlConnection(TestConnStr);
    await conn.OpenAsync();
    
    var currentDb = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✓ Connected to: {currentDb}");
    Console.ResetColor();
    
    if (currentDb != TestDb)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: Wrong database! Expected {TestDb}, got {currentDb}");
        Console.ResetColor();
        return 1;
    }
    
    var tableCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pg_tables WHERE schemaname = 'public'");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"Existing tables in public schema: {tableCount}");
    Console.ResetColor();
    
    if (tableCount > 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("WARNING: Database is not empty. Will apply migrations anyway.");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: Cannot connect to database");
    Console.WriteLine(ex.Message);
    Console.ResetColor();
    return 1;
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== Step 2: Apply Migrations ===");
Console.ResetColor();

var migrationsDir = @"e:\KitPlatform\migrations";

foreach (var migFile in migrationFiles)
{
    var migPath = Path.Combine(migrationsDir, migFile);
    
    if (!File.Exists(migPath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: Migration file not found: {migPath}");
        Console.ResetColor();
        return 1;
    }
    
    Console.WriteLine($"Applying: {migFile}");
    
    try
    {
        var sql = await File.ReadAllTextAsync(migPath);
        
        await using var conn = new NpgsqlConnection(TestConnStr);
        await conn.OpenAsync();
        
        // Verify database context
        var currentDb = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
        if (currentDb != TestDb)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Database context changed to: {currentDb}");
            Console.ResetColor();
            return 1;
        }
        
        // Execute migration
        await conn.ExecuteAsync(sql);
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {migFile} completed");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ ERROR in {migFile}");
        Console.WriteLine($"  {ex.Message}");
        Console.ResetColor();
        return 1;
    }
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("=== SUCCESS ===");
Console.ResetColor();
Console.WriteLine($"All migrations applied to {TestDb}");

return 0;

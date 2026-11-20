param(
    [Parameter(Mandatory)] [int]$Port,
    [Parameter(Mandatory)] [string]$Database,
    [string]$DbHost = "127.0.0.1",
    [string]$User = "pgadmin",
    [string]$Role = "novixa_iso",
    [string]$Psql = "psql"
)
# Restricted login for KitPlatform.Platform.Tests isolated suites (NovixaValidationSession refuses superusers
# and roles that can reach postgres/template1). Data privileges only; no DDL, no other databases.
$ErrorActionPreference = "Stop"
if ($Port -eq 5432) { throw "Refusing port 5432 (dev cluster)." }
if ($Database -in @("kitplatform", "pharmacore", "postgres") -or $Database -match "prod") {
    throw "Refusing database '$Database'."
}
if ($Role -notmatch '^[a-z_][a-z0-9_]*$') { throw "Invalid role name '$Role'." }

$conn = @("-h", $DbHost, "-p", "$Port", "-U", $User)

& $Psql @conn -d postgres -q -v ON_ERROR_STOP=1 `
    -c "DO `$`$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$Role') THEN CREATE ROLE $Role LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE; END IF; END `$`$;" `
    -c "REVOKE CONNECT ON DATABASE postgres FROM PUBLIC" `
    -c "REVOKE CONNECT ON DATABASE template1 FROM PUBLIC" `
    -c "GRANT CONNECT ON DATABASE $Database TO $Role"
if ($LASTEXITCODE -ne 0) { throw "Could not prepare role $Role." }

$grant = @"
DO `$`$
DECLARE s text;
BEGIN
    FOR s IN
        SELECT nspname FROM pg_namespace
        WHERE nspname NOT IN ('pg_catalog', 'information_schema') AND nspname NOT LIKE 'pg_%'
    LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO $Role', s);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO $Role', s);
        EXECUTE format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA %I TO $Role', s);
        EXECUTE format('GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA %I TO $Role', s);
    END LOOP;
END `$`$;
"@
& $Psql @conn -d $Database -q -v ON_ERROR_STOP=1 -c $grant
if ($LASTEXITCODE -ne 0) { throw "Could not grant $Role on $Database." }
"granted $Role on $Database"

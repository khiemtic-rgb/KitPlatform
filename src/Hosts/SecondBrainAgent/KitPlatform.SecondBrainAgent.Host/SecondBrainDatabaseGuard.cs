using System.Net;
using Npgsql;

namespace KitPlatform.SecondBrainAgent.Host;

public interface IHostAddressResolver
{
    IReadOnlyList<IPAddress> Resolve(string host);
}

public static class SecondBrainDatabaseGuard
{
    public const string DatabaseName = "second_brain_agent_v1";
    public const int ForbiddenPort = 5432;
    public static readonly IPAddress ProductionApiAddress = IPAddress.Parse("103.200.23.229");

    static readonly string[] ForbiddenDatabases = ["novixa_prod", "trading_research", "kit_pi_validation"];

    public static void RequireWorkersDisabled(IConfiguration configuration)
    {
        var raw = configuration["PersonalIntelligence:ExchangeBackfill:WorkerEnabled"];
        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Second Brain agent host refuses PersonalIntelligence:ExchangeBackfill:WorkerEnabled=true.");
    }

    public static string RequireSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Trim().Length < 32)
            throw new InvalidOperationException("SecondBrain:JwtSecret must be configured and at least 32 characters.");
        return secret.Trim();
    }

    public static string RequireSystemIdentifier(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("SecondBrain:SystemIdentifier is required for the Second Brain agent host.");
        return configured.Trim();
    }

    public static void RequireLoopbackUrls(string? urls)
    {
        if (string.IsNullOrWhiteSpace(urls))
            throw new InvalidOperationException("The Second Brain agent host requires a loopback ASPNETCORE_URLS value.");
        foreach (var raw in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("The Second Brain agent host only listens on a loopback address.");
            var host = uri.Host.Trim().Trim('[', ']');
            if (!IsLoopbackLiteral(host))
                throw new InvalidOperationException("The Second Brain agent host only listens on a loopback address.");
        }
    }

    public static string RequireConnection(IConfiguration configuration, IHostAddressResolver? resolver = null)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Default is required for the Second Brain agent host.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        RequireHost((builder.Host ?? "").Trim(), resolver);
        if (builder.Port == ForbiddenPort)
            throw new InvalidOperationException("Second Brain agent host refuses PostgreSQL port 5432.");

        var database = (builder.Database ?? "").Trim();
        if (!string.Equals(database, DatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Second Brain agent host only opens database " + DatabaseName + ".");
        if (ForbiddenDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Second Brain agent host refuses database " + database + ".");

        return builder.ConnectionString;
    }

    public static void RequireHost(string host, IHostAddressResolver? resolver = null)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Second Brain agent host refuses an empty host.");
        if (host.Contains(',', StringComparison.Ordinal))
            throw new InvalidOperationException("Second Brain agent host refuses a host list.");
        if (host.Contains('/', StringComparison.Ordinal) || host.Contains('\\', StringComparison.Ordinal))
            throw new InvalidOperationException("Second Brain agent host refuses a socket path.");
        if (IsProductionAddress(host))
            throw new InvalidOperationException("Second Brain agent host refuses the production API address.");
        if (IsLoopbackLiteral(host))
            return;

        if (resolver is not null)
            RequireResolvedAddresses(resolver.Resolve(host));
        throw new InvalidOperationException("Second Brain agent host only accepts a loopback host.");
    }

    public static void RequireResolvedAddresses(IReadOnlyList<IPAddress> addresses)
    {
        if (addresses.Any(address => address.Equals(ProductionApiAddress)))
            throw new InvalidOperationException("Second Brain agent host refuses the production API address.");
        if (addresses.Count == 0 || addresses.Any(address => !IsStrictLoopback(address)))
            throw new InvalidOperationException("Second Brain agent host only accepts a loopback host.");
    }

    public static void RequireActualDatabase(string? actual)
    {
        if (!string.Equals(actual?.Trim(), DatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException("Connected database is not " + DatabaseName + ".");
    }

    public static void RequireActualEndpoint(
        string? database,
        int? port,
        string? systemIdentifier,
        int approvedPort,
        string expectedSystemIdentifier)
    {
        RequireActualDatabase(database);
        if (port is null || port == ForbiddenPort || port != approvedPort)
            throw new InvalidOperationException("Connected PostgreSQL port is not the approved isolated port.");
        if (!string.Equals(systemIdentifier?.Trim(), expectedSystemIdentifier, StringComparison.Ordinal))
            throw new InvalidOperationException("Connected PostgreSQL system identifier is not the approved cluster.");
    }

    static bool IsProductionAddress(string host)
    {
        if (string.Equals(host, ProductionApiAddress.ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        return IPAddress.TryParse(host, out var address) && address.Equals(ProductionApiAddress);
    }

    static bool IsLoopbackLiteral(string host)
    {
        return IPAddress.TryParse(host, out var address) && IsStrictLoopback(address);
    }

    static bool IsStrictLoopback(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        return address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback);
    }
}

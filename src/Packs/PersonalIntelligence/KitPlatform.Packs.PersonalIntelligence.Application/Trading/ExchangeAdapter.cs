namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed class ExchangeCredential
{
    public string Environment { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string? Passphrase { get; set; }
}

public sealed class ExchangeHistoryQuery
{
    public string ProductSubtype { get; set; } = "";
    public string Phase { get; set; } = "fills";
    public long WindowStartMs { get; set; }
    public long WindowEndMs { get; set; }
    public string? Cursor { get; set; }
    public string? Symbol { get; set; }
    public int Limit { get; set; } = 100;
}

public sealed class ExchangeHistoryPage
{
    public List<ExchangeExecutionItemDto> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public bool PageFull { get; set; }
    public long? OldestMs { get; set; }
    public List<string> DiscoveredSymbols { get; set; } = [];
}

public interface IExchangeAdapter
{
    string ExchangeCode { get; }
    bool RequiresPassphrase { get; }
    Task<ExchangeProbeDto> ProbeAsync(ExchangeCredential credential, CancellationToken cancellationToken);
    Task<ExchangeHistoryPage> ReadHistoryAsync(ExchangeCredential credential, ExchangeHistoryQuery query, CancellationToken cancellationToken);
}

public interface IExchangeAdapterRegistry
{
    IExchangeAdapter Require(string exchangeCode);
    IReadOnlyList<IExchangeAdapter> All { get; }
}

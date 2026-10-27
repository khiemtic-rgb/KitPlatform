namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Trading Ledger V1 constants — frozen values from schema design
/// </summary>
public static class TradingSide
{
    public const string Buy = "BUY";
    public const string Sell = "SELL";
}

public static class FeeType
{
    public const string Commission = "commission";
    public const string Spread = "spread";
    public const string Gas = "gas";
    public const string Withdrawal = "withdrawal";
    public const string Deposit = "deposit";
    public const string Funding = "funding";
    public const string Other = "other";
}

public static class SourceType
{
    public const string Import = "IMPORT";
    public const string Manual = "MANUAL";
    public const string Derived = "DERIVED";
    public const string Corrected = "CORRECTED";
    public const string Reconciled = "RECONCILED";
    public const string Exchange = "EXCHANGE";
}

public static class ExchangeStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Deprecated = "deprecated";
}

public static class TradingAccountStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Closed = "closed";
}

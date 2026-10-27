namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Query: GetPersonTradingAccounts — list TradingAccounts for Person
/// </summary>
public sealed record GetPersonTradingAccountsQuery(
    Guid PersonId,
    Guid TenantId);

public sealed record GetPersonTradingAccountsResult(
    IReadOnlyList<TradingAccount> TradingAccounts);

/// <summary>
/// Query: GetTradingAccount — get single TradingAccount by ID
/// </summary>
public sealed record GetTradingAccountQuery(
    Guid TradingAccountId,
    Guid TenantId);

public sealed record GetTradingAccountResult(
    TradingAccount? TradingAccount);

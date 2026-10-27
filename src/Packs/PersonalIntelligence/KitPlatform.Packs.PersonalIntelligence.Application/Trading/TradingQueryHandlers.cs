namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Query handler: GetPersonTradingAccounts
/// </summary>
public sealed class GetPersonTradingAccountsHandler
{
    private readonly ITradingAccountRepository _tradingAccountRepository;

    public GetPersonTradingAccountsHandler(ITradingAccountRepository tradingAccountRepository)
    {
        _tradingAccountRepository = tradingAccountRepository;
    }

    public async Task<GetPersonTradingAccountsResult> HandleAsync(
        GetPersonTradingAccountsQuery query,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _tradingAccountRepository.ListForPersonAsync(
            query.PersonId,
            query.TenantId,
            cancellationToken);

        return new GetPersonTradingAccountsResult(accounts);
    }
}

/// <summary>
/// Query handler: GetTradingAccount
/// </summary>
public sealed class GetTradingAccountHandler
{
    private readonly ITradingAccountRepository _tradingAccountRepository;

    public GetTradingAccountHandler(ITradingAccountRepository tradingAccountRepository)
    {
        _tradingAccountRepository = tradingAccountRepository;
    }

    public async Task<GetTradingAccountResult> HandleAsync(
        GetTradingAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        var account = await _tradingAccountRepository.GetByIdAsync(
            query.TradingAccountId,
            query.TenantId,
            cancellationToken);

        return new GetTradingAccountResult(account);
    }
}

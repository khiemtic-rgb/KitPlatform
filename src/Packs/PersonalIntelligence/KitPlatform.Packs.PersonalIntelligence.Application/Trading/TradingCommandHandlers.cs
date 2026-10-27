namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Command handler: RecordTrade
/// </summary>
public sealed class RecordTradeHandler
{
    private readonly ITradeRepository _tradeRepository;
    private readonly ITradingAccountRepository _tradingAccountRepository;

    public RecordTradeHandler(
        ITradeRepository tradeRepository,
        ITradingAccountRepository tradingAccountRepository)
    {
        _tradeRepository = tradeRepository;
        _tradingAccountRepository = tradingAccountRepository;
    }

    public async Task<RecordTradeResult> HandleAsync(
        RecordTradeCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate TradingAccount exists
        var account = await _tradingAccountRepository.GetByIdAsync(
            command.TradingAccountId,
            command.TenantId,
            cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException(
                $"TradingAccount {command.TradingAccountId} not found");
        }

        if (account.PersonId != command.PersonId)
        {
            throw new InvalidOperationException(
                $"TradingAccount {command.TradingAccountId} does not belong to person {command.PersonId}");
        }

        // Validate Side
        if (command.Side != TradingSide.Buy && command.Side != TradingSide.Sell)
        {
            throw new ArgumentException(
                $"Invalid trade side: {command.Side}. Must be {TradingSide.Buy} or {TradingSide.Sell}",
                nameof(command.Side));
        }

        // Validate SourceType
        if (command.SourceType != SourceType.Import &&
            command.SourceType != SourceType.Manual &&
            command.SourceType != SourceType.Derived &&
            command.SourceType != SourceType.Corrected &&
            command.SourceType != SourceType.Reconciled)
        {
            throw new ArgumentException(
                $"Invalid source type: {command.SourceType}",
                nameof(command.SourceType));
        }

        // Calculate total value
        var totalValue = command.Quantity * command.Price;

        // Create trade (repository handles idempotency on external_trade_id)
        var tradeId = await _tradeRepository.CreateAsync(
            personId: command.PersonId,
            tenantId: command.TenantId,
            tradingAccountId: command.TradingAccountId,
            externalTradeId: command.ExternalTradeId,
            symbol: command.Symbol,
            side: command.Side,
            quantity: command.Quantity,
            price: command.Price,
            totalValue: totalValue,
            feeAmount: command.FeeAmount,
            feeCurrency: command.FeeCurrency,
            executedAt: command.ExecutedAt,
            source: command.RecordedAt.ToString("O"),
            sourceType: command.SourceType,
            isObserved: command.IsObserved,
            correctsTradeId: command.CorrectsTradeId,
            orderId: null,
            createdBy: command.CreatedBy,
            cancellationToken: cancellationToken);

        return new RecordTradeResult(tradeId);
    }
}

/// <summary>
/// Command handler: RecordFee
/// </summary>
public sealed class RecordFeeHandler
{
    private readonly IFeeRepository _feeRepository;
    private readonly ITradingAccountRepository _tradingAccountRepository;

    public RecordFeeHandler(
        IFeeRepository feeRepository,
        ITradingAccountRepository tradingAccountRepository)
    {
        _feeRepository = feeRepository;
        _tradingAccountRepository = tradingAccountRepository;
    }

    public async Task<RecordFeeResult> HandleAsync(
        RecordFeeCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate TradingAccount exists
        var account = await _tradingAccountRepository.GetByIdAsync(
            command.TradingAccountId,
            command.TenantId,
            cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException(
                $"TradingAccount {command.TradingAccountId} not found");
        }

        if (account.PersonId != command.PersonId)
        {
            throw new InvalidOperationException(
                $"TradingAccount {command.TradingAccountId} does not belong to person {command.PersonId}");
        }

        // Validate FeeType
        if (command.FeeType != FeeType.Commission &&
            command.FeeType != FeeType.Spread &&
            command.FeeType != FeeType.Gas &&
            command.FeeType != FeeType.Withdrawal &&
            command.FeeType != FeeType.Deposit &&
            command.FeeType != FeeType.Funding &&
            command.FeeType != FeeType.Other)
        {
            throw new ArgumentException(
                $"Invalid fee type: {command.FeeType}",
                nameof(command.FeeType));
        }

        // Validate SourceType
        if (command.SourceType != SourceType.Import &&
            command.SourceType != SourceType.Manual &&
            command.SourceType != SourceType.Derived &&
            command.SourceType != SourceType.Corrected &&
            command.SourceType != SourceType.Reconciled)
        {
            throw new ArgumentException(
                $"Invalid source type: {command.SourceType}",
                nameof(command.SourceType));
        }

        // Create fee (repository handles idempotency on external_fee_id when present)
        var feeId = await _feeRepository.CreateAsync(
            personId: command.PersonId,
            tenantId: command.TenantId,
            tradingAccountId: command.TradingAccountId,
            tradeId: command.TradeId,
            feeType: command.FeeType,
            feeAmount: command.FeeAmount,
            feeCurrency: command.FeeCurrency,
            externalFeeId: command.ExternalFeeId,
            incurredAt: command.IncurredAt,
            source: command.RecordedAt.ToString("O"),
            sourceType: command.SourceType,
            createdBy: command.CreatedBy,
            cancellationToken: cancellationToken);

        return new RecordFeeResult(feeId);
    }
}

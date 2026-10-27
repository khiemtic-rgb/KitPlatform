using KitPlatform.Packs.PersonalIntelligence.Research;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public sealed class ResearchPublicationCapabilityHandler : IBusinessCapabilityHandler
{
    readonly IResearchPublicationReader _reader;

    public ResearchPublicationCapabilityHandler(IResearchPublicationReader reader) => _reader = reader;

    public string CapabilityId => PlatformCapabilityCatalog.ResearchPublicationRead;

    public async Task<CapabilityExecution> ExecuteAsync(
        BusinessCaller caller,
        IReadOnlyDictionary<string, string> slots,
        CancellationToken cancellationToken)
    {
        var read = await _reader.ReadAsync(cancellationToken);
        var completed = read.Availability is ResearchAvailability.Fresh or ResearchAvailability.Stale
            && read.ReadError is null
            && read.Objects.Any(item => !string.IsNullOrWhiteSpace(item.ContentSha256));
        var json = BusinessIntentJson.Serialize(new
        {
            status = "publication_read",
            availability = read.Availability,
            publicationId = read.PublicationId,
            tenantScoped = false,
            readError = read.ReadError?.Code,
            objects = read.Objects.Select(item => new { id = item.Id, sha256 = item.ContentSha256 }),
            wroteData = false,
            externalCall = false,
        });
        return new CapabilityExecution("publication_read", completed, json, false, false);
    }
}

public sealed class TradingDeskSummaryCapabilityHandler : IBusinessCapabilityHandler
{
    readonly ITradingDesk _desk;

    public TradingDeskSummaryCapabilityHandler(ITradingDesk desk) => _desk = desk;

    public string CapabilityId => PlatformCapabilityCatalog.TradingDeskSummaryRead;

    public async Task<CapabilityExecution> ExecuteAsync(
        BusinessCaller caller,
        IReadOnlyDictionary<string, string> slots,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _desk.GetSummaryAsync(cancellationToken);
            var json = BusinessIntentJson.Serialize(new
            {
                status = "desk_summary",
                tenantId = caller.TenantId,
                tenantScoped = true,
                summary.TradeCount,
                summary.FixtureTradeCount,
                summary.OtherSourceTradeCount,
                summary.DecisionCount,
                summary.PaperExecutionCount,
                summary.RiskVetoCount,
                summary.LatestSource,
                summary.LatestRecordedAt,
                summary.OnlyFixtureData,
                summary.MissingData,
                summary.ImportTradeCount,
                summary.ExchangeTradeCount,
                wroteData = false,
                externalCall = false,
            });
            var verifiedLedger = summary.MissingData.Count == 0 && !summary.OnlyFixtureData;
            return new CapabilityExecution("desk_summary", verifiedLedger, json, false, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var json = BusinessIntentJson.Serialize(new
            {
                status = "store_unavailable",
                tenantId = caller.TenantId,
                tenantScoped = true,
                exception = ex.GetType().Name,
                wroteData = false,
                externalCall = false,
            });
            return new CapabilityExecution("store_unavailable", false, json, false, false);
        }
    }
}

using System.Text;

namespace KitPlatform.Packs.PersonalIntelligence.Intent;

/// <summary>
/// Capabilities that already have handlers. Descriptions state behavior.
/// They are not sample utterances and must not grow into a phrase list.
/// </summary>
public static class PlatformCapabilityCatalog
{
    public const string ResearchPublicationRead = "personal_intelligence.research.publication.read";
    public const string TradingDeskSummaryRead = "personal_intelligence.trading.desk.summary.read";
    public const string TradingPaperEvaluate = "personal_intelligence.trading.paper.evaluate";

    public const string TradingRunPermission = "personal_intelligence.trading.run";
    public const string ResearchReadPermission = "personal_intelligence.research.read";

    static readonly CapabilityDescriptor[] Items =
    [
        new(
            ResearchPublicationRead,
            "research",
            "Reads the host-configured research publication and returns availability plus each object content hash. It does not create an experiment, edit files, or send an exchange order. The configured root is process configuration, not a tenant-scoped store. A missing file stays an explicit missing result.",
            SideEffectKind.None,
            TenantScoped: false,
            CanSatisfyCallerGoal: true,
            RefusesLive: false,
            Permissions: [ResearchReadPermission, TradingRunPermission],
            Slots: []),
        new(
            TradingDeskSummaryRead,
            "trading",
            "Reads aggregate counts already stored for the caller tenant: trades, decisions, and paper executions. It does not insert rows, place an order, or replace an empty store with estimated numbers.",
            SideEffectKind.None,
            TenantScoped: true,
            CanSatisfyCallerGoal: true,
            RefusesLive: false,
            Permissions: [TradingRunPermission],
            Slots: []),
        new(
            TradingPaperEvaluate,
            "trading",
            "Applies the existing risk engine and the existing paper gate to one proposed order and returns their verdict. It does not write a ledger row, does not approve, and does not send an exchange order. Live mode is refused by those gates. Numeric risk limits are taken only from values present in the request.",
            SideEffectKind.None,
            TenantScoped: true,
            CanSatisfyCallerGoal: false,
            RefusesLive: true,
            Permissions: [TradingRunPermission],
            Slots:
            [
                new("symbol", true, "Instrument code the caller already stated."),
                new("side", true, "BUY or SELL."),
                new("quantity", true, "Positive decimal using a dot."),
                new("price", true, "Positive decimal using a dot. Do not invent one."),
                new("mode", true, "paper or live, and only when the caller stated which."),
                new("maxNotional", false, "Only when the caller stated a maximum notional."),
                new("maxLeverage", false, "Only when the caller stated a maximum leverage."),
                new("minConfidence", false, "Only when the caller stated a minimum confidence."),
                new("leverage", false, "Only when the caller stated a leverage."),
                new("confidence", false, "Only when the caller stated a confidence."),
            ]),
    ];

    public static IReadOnlyList<CapabilityDescriptor> All => Items;

    public static string Fingerprint(IReadOnlyList<CapabilityDescriptor> catalog)
    {
        var sb = new StringBuilder();
        foreach (var item in catalog.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            sb.Append(item.Id).Append('\n');
            sb.Append(item.Domain).Append('\n');
            sb.Append(item.Summary).Append('\n');
            sb.Append(item.SideEffect).Append('\n');
            sb.Append(item.TenantScoped).Append('\n');
            sb.Append(item.CanSatisfyCallerGoal).Append('\n');
            sb.Append(item.RefusesLive).Append('\n');
            foreach (var permission in item.Permissions.OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(permission).Append(',');
            sb.Append('\n');
            foreach (var slot in item.Slots.OrderBy(x => x.Name, StringComparer.Ordinal))
                sb.Append(slot.Name).Append('|').Append(slot.Required).Append('|').Append(slot.Description).Append('\n');
            sb.Append("---\n");
        }

        return BusinessIntentHash.Sha256(sb.ToString());
    }
}

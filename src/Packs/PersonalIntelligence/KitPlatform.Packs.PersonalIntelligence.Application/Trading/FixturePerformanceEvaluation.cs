namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public enum EvidenceBasis
{
    Fixture,
    LiveObserved,
}

public sealed record EvaluationReport(
    EvidenceBasis Basis,
    bool Consistent,
    IReadOnlyList<string> DuplicateExternalIds,
    decimal ReconstructedNetQuantity,
    string Limitation);

public static class FixturePerformanceEvaluation
{
    public static EvaluationReport Evaluate(IReadOnlyList<ObservedTrade> trades)
    {
        var duplicates = trades
            .GroupBy(row => row.ExternalTradeId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        decimal net = 0;
        foreach (var trade in trades)
        {
            var signed = trade.Side == "SELL" ? -trade.Quantity : trade.Quantity;
            net += signed;
        }

        return new EvaluationReport(
            EvidenceBasis.Fixture,
            duplicates.Length == 0,
            duplicates,
            net,
            "Fixture reconstruction is not evidence of live trading performance.");
    }
}

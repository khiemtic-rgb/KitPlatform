namespace KitPlatform.Application.Success;

/// <summary>One closed shift: who closed it and its signed cash variance.</summary>
public sealed record CashVarianceShiftSample(
    Guid? CloserUserId,
    string CloserName,
    decimal? CashVariance);

public sealed record LossCashVarianceByCloserRowDto(
    Guid? CloserUserId,
    string CloserName,
    int ShiftCount,
    decimal AbsVarianceTotal,
    decimal NetVarianceTotal);

public static class CashVarianceAccountability
{
    public const string UnknownCloserName = "(Không rõ người đóng ca)";

    /// <summary>
    /// Sum of |variance| and shift count per closer. Shifts without a variance are skipped.
    /// Largest absolute total first.
    /// </summary>
    public static IReadOnlyList<LossCashVarianceByCloserRowDto> GroupByCloser(
        IEnumerable<CashVarianceShiftSample> samples)
    {
        return samples
            .Where(s => s.CashVariance is not null)
            .GroupBy(s => s.CloserUserId)
            .Select(g =>
            {
                var name = g.Select(s => s.CloserName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                           ?? UnknownCloserName;
                return new LossCashVarianceByCloserRowDto(
                    g.Key,
                    name,
                    g.Count(),
                    g.Sum(s => Math.Abs(s.CashVariance!.Value)),
                    g.Sum(s => s.CashVariance!.Value));
            })
            .OrderByDescending(r => r.AbsVarianceTotal)
            .ThenByDescending(r => r.ShiftCount)
            .ThenBy(r => r.CloserName, StringComparer.CurrentCulture)
            .ToList();
    }
}

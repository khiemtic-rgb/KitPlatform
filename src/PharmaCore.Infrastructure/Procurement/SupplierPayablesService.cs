using PharmaCore.Application.Procurement;

namespace PharmaCore.Infrastructure.Procurement;

internal sealed class SupplierPayablesService : ISupplierPayablesService
{
    private readonly ProcurementRepository _repository;

    public SupplierPayablesService(ProcurementRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<SupplierPayablesRowDto>> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetGrnPayableSourceRowsAsync(cancellationToken);
        var credits = await _repository.GetUnlinkedSupplierPaymentTotalsAsync(cancellationToken);
        return BuildSupplierGroups(rows, credits)
            .Select(BuildSummaryRow)
            .Where(x => x.TotalPayable > 0.009m || x.UnappliedCredit > 0.009m)
            .OrderByDescending(x => x.TotalPayable)
            .ThenBy(x => x.SupplierName)
            .ToList();
    }

    public async Task<SupplierPayablesDetailDto?> GetDetailAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetGrnPayableSourceRowsAsync(cancellationToken);
        var credits = await _repository.GetUnlinkedSupplierPaymentTotalsAsync(cancellationToken);
        var group = BuildSupplierGroups(rows, credits)
            .FirstOrDefault(x => x.SupplierId == supplierId);
        if (group is null)
            return null;

        var summary = BuildSummaryRow(group);
        var lines = group.Lines
            .Select(line => new SupplierPayablesDetailLineDto(
                line.GrnId,
                line.GrnNumber,
                line.ReceiptDate,
                line.GrnTotal,
                line.GrnLinkedPaid,
                line.Outstanding,
                line.DaysOutstanding))
            .ToList();

        return new SupplierPayablesDetailDto(
            group.SupplierId,
            group.SupplierCode,
            group.SupplierName,
            group.PaymentTerms,
            summary.TotalPayable,
            summary.UnappliedCredit,
            summary.Aging,
            lines);
    }

    private static SupplierPayablesRowDto BuildSummaryRow(SupplierPayablesGroup group)
    {
        var aging = BucketAging(group.Lines);
        return new SupplierPayablesRowDto(
            group.SupplierId,
            group.SupplierCode,
            group.SupplierName,
            group.PaymentTerms,
            group.TotalPayable,
            group.UnappliedCredit,
            aging,
            group.Lines.Count(x => x.Outstanding > 0.009m));
    }

    private static IReadOnlyList<SupplierPayablesGroup> BuildSupplierGroups(
        IReadOnlyList<GrnPayableSourceRow> rows,
        IReadOnlyDictionary<Guid, decimal> credits)
    {
        return rows
            .GroupBy(x => x.SupplierId)
            .Select(group =>
            {
                var first = group.First();
                var credit = credits.TryGetValue(group.Key, out var amount) ? amount : 0m;
                var lines = group
                    .Select(row =>
                    {
                        var outstanding = Math.Max(0, row.GrnTotal - row.GrnLinkedPaid);
                        var days = Math.Max(0, (DateTime.UtcNow.Date - row.ReceiptDate.Date).Days);
                        return new SupplierPayablesLine(
                            row.GrnId,
                            row.GrnNumber,
                            row.ReceiptDate,
                            row.GrnTotal,
                            row.GrnLinkedPaid,
                            outstanding,
                            days);
                    })
                    .OrderBy(x => x.ReceiptDate)
                    .ToList();

                var remainingCredit = ApplyUnlinkedCredit(lines, credit);

                return new SupplierPayablesGroup(
                    group.Key,
                    first.SupplierCode,
                    first.SupplierName,
                    first.PaymentTerms,
                    remainingCredit,
                    lines);
            })
            .ToList();
    }

    private static decimal ApplyUnlinkedCredit(IList<SupplierPayablesLine> lines, decimal credit)
    {
        var remaining = credit;
        foreach (var line in lines)
        {
            if (remaining <= 0.009m || line.Outstanding <= 0.009m)
                continue;

            var applied = Math.Min(line.Outstanding, remaining);
            line.Outstanding -= applied;
            remaining -= applied;
        }

        return remaining;
    }

    private static SupplierPayablesAgingBucketsDto BucketAging(IEnumerable<SupplierPayablesLine> lines)
    {
        decimal current = 0;
        decimal days31To60 = 0;
        decimal days61To90 = 0;
        decimal over90 = 0;

        foreach (var line in lines)
        {
            if (line.Outstanding <= 0.009m)
                continue;

            if (line.DaysOutstanding <= 30)
                current += line.Outstanding;
            else if (line.DaysOutstanding <= 60)
                days31To60 += line.Outstanding;
            else if (line.DaysOutstanding <= 90)
                days61To90 += line.Outstanding;
            else
                over90 += line.Outstanding;
        }

        return new SupplierPayablesAgingBucketsDto(current, days31To60, days61To90, over90);
    }

    private sealed record SupplierPayablesGroup(
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        int PaymentTerms,
        decimal UnappliedCredit,
        IReadOnlyList<SupplierPayablesLine> Lines)
    {
        public decimal TotalPayable => Lines.Sum(x => x.Outstanding);
    }

    private sealed class SupplierPayablesLine
    {
        public SupplierPayablesLine(
            Guid grnId,
            string grnNumber,
            DateTime receiptDate,
            decimal grnTotal,
            decimal grnLinkedPaid,
            decimal outstanding,
            int daysOutstanding)
        {
            GrnId = grnId;
            GrnNumber = grnNumber;
            ReceiptDate = receiptDate;
            GrnTotal = grnTotal;
            GrnLinkedPaid = grnLinkedPaid;
            Outstanding = outstanding;
            DaysOutstanding = daysOutstanding;
        }

        public Guid GrnId { get; }
        public string GrnNumber { get; }
        public DateTime ReceiptDate { get; }
        public decimal GrnTotal { get; }
        public decimal GrnLinkedPaid { get; }
        public decimal Outstanding { get; set; }
        public int DaysOutstanding { get; }
    }
}

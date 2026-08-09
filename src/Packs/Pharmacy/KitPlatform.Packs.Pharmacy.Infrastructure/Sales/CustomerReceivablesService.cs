using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class CustomerReceivablesService : ICustomerReceivablesService
{
    private readonly SalesRepository _repository;
    private readonly IBranchAccessService _branchAccess;

    public CustomerReceivablesService(SalesRepository repository, IBranchAccessService branchAccess)
    {
        _repository = repository;
        _branchAccess = branchAccess;
    }

    public async Task<IReadOnlyList<CustomerReceivablesRowDto>> GetSummaryAsync(
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var warehouseScope = await ResolveWarehouseScopeAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesOrderReceivableSourceRowsAsync(warehouseScope, cancellationToken);
        var credits = await _repository.GetUnlinkedCustomerPaymentTotalsAsync(warehouseScope, cancellationToken);
        return BuildCustomerGroups(rows, credits)
            .Select(BuildSummaryRow)
            .Where(x => x.TotalReceivable > 0.009m || x.UnappliedCredit > 0.009m)
            .OrderByDescending(x => x.TotalReceivable)
            .ThenBy(x => x.CustomerName)
            .ToList();
    }

    public async Task<CustomerReceivablesDetailDto?> GetDetailAsync(
        Guid customerId,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var warehouseScope = await ResolveWarehouseScopeAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesOrderReceivableSourceRowsAsync(warehouseScope, cancellationToken);
        var credits = await _repository.GetUnlinkedCustomerPaymentTotalsAsync(warehouseScope, cancellationToken);
        var group = BuildCustomerGroups(rows, credits)
            .FirstOrDefault(x => x.CustomerId == customerId);
        if (group is null)
            return null;

        var summary = BuildSummaryRow(group);
        var lines = group.Lines
            .Select(line => new CustomerReceivablesDetailLineDto(
                line.SalesOrderId,
                line.OrderNumber,
                line.OrderDate,
                line.OrderTotal,
                line.PaidAmount,
                line.Outstanding,
                line.DaysOutstanding))
            .ToList();

        return new CustomerReceivablesDetailDto(
            group.CustomerId,
            group.CustomerCode,
            group.CustomerName,
            group.CustomerPhone,
            summary.TotalReceivable,
            summary.UnappliedCredit,
            summary.Aging,
            lines);
    }

    private async Task<Guid[]?> ResolveWarehouseScopeAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        var (scopedId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        if (scopedId is Guid id)
            return [id];
        return allowed;
    }

    private static CustomerReceivablesRowDto BuildSummaryRow(CustomerReceivablesGroup group)
    {
        var aging = BucketAging(group.Lines);
        return new CustomerReceivablesRowDto(
            group.CustomerId,
            group.CustomerCode,
            group.CustomerName,
            group.CustomerPhone,
            group.TotalReceivable,
            group.UnappliedCredit,
            aging,
            group.Lines.Count(x => x.Outstanding > 0.009m));
    }

    private static IReadOnlyList<CustomerReceivablesGroup> BuildCustomerGroups(
        IReadOnlyList<SalesOrderReceivableSourceRow> rows,
        IReadOnlyDictionary<Guid, decimal> credits)
    {
        return rows
            .GroupBy(x => x.CustomerId)
            .Select(group =>
            {
                var first = group.First();
                var credit = credits.TryGetValue(group.Key, out var amount) ? amount : 0m;
                var lines = group
                    .Select(row =>
                    {
                        var days = Math.Max(0, (DateTime.UtcNow.Date - row.OrderDate.Date).Days);
                        return new CustomerReceivablesLine(
                            row.SalesOrderId,
                            row.OrderNumber,
                            row.OrderDate,
                            row.OrderTotal,
                            row.AmountPaid,
                            row.Outstanding,
                            days);
                    })
                    .OrderBy(x => x.OrderDate)
                    .ToList();

                var remainingCredit = ApplyUnlinkedCredit(lines, credit);

                return new CustomerReceivablesGroup(
                    group.Key,
                    first.CustomerCode,
                    first.CustomerName,
                    first.CustomerPhone,
                    remainingCredit,
                    lines);
            })
            .ToList();
    }

    private static decimal ApplyUnlinkedCredit(IList<CustomerReceivablesLine> lines, decimal credit)
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

    private static CustomerReceivablesAgingBucketsDto BucketAging(IEnumerable<CustomerReceivablesLine> lines)
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

        return new CustomerReceivablesAgingBucketsDto(current, days31To60, days61To90, over90);
    }

    private sealed record CustomerReceivablesGroup(
        Guid CustomerId,
        string CustomerCode,
        string CustomerName,
        string? CustomerPhone,
        decimal UnappliedCredit,
        IReadOnlyList<CustomerReceivablesLine> Lines)
    {
        public decimal TotalReceivable => Lines.Sum(x => x.Outstanding);
    }

    private sealed class CustomerReceivablesLine
    {
        public CustomerReceivablesLine(
            Guid salesOrderId,
            string orderNumber,
            DateTime orderDate,
            decimal orderTotal,
            decimal paidAmount,
            decimal outstanding,
            int daysOutstanding)
        {
            SalesOrderId = salesOrderId;
            OrderNumber = orderNumber;
            OrderDate = orderDate;
            OrderTotal = orderTotal;
            PaidAmount = paidAmount;
            Outstanding = outstanding;
            DaysOutstanding = daysOutstanding;
        }

        public Guid SalesOrderId { get; }
        public string OrderNumber { get; }
        public DateTime OrderDate { get; }
        public decimal OrderTotal { get; }
        public decimal PaidAmount { get; }
        public decimal Outstanding { get; set; }
        public int DaysOutstanding { get; }
    }
}

using System.Text;
using KitPlatform.Packs.Pharmacy.Integration.Qd540;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Integration.Qd540;

internal sealed class Qd540Table1Service : IQd540Table1Service
{
    private readonly Qd540Table1Repository _repository;

    public Qd540Table1Service(Qd540Table1Repository repository) => _repository = repository;

    public async Task<Qd540Table1ExportResult> ExportTable1Async(
        Qd540Table1Query query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        if (query.BranchId is Guid branchId &&
            await _repository.BranchMissingRetailCodeAsync(branchId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Chi nhánh chưa có Ma_co_so_ban_le (retail_facility_code). Cập nhật tại Hệ thống → Chi nhánh.");
        }

        var fromUtc = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusiveUtc = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var grnLines = await _repository.LoadGrnLinesAsync(fromUtc, toExclusiveUtc, query.BranchId, cancellationToken);
        var saleLines = await _repository.LoadSaleLinesAsync(fromUtc, toExclusiveUtc, query.BranchId, cancellationToken);
        var sourceRows = grnLines.Concat(saleLines).OrderBy(r => r.EventAt).ThenBy(r => r.SourceId).ToList();

        var warnings = new List<string>();
        var mapped = new List<Qd540Table1RowDto>();
        var skipped = 0;

        foreach (var source in sourceRows)
        {
            var row = Qd540Transform.MapSourceRow(source, warnings);
            if (row is null)
            {
                skipped++;
                continue;
            }

            mapped.Add(row);
        }

        var distinctWarnings = warnings.Distinct(StringComparer.Ordinal).ToList();
        var hash = Qd540ExportHash.Compute(mapped);
        await _repository.LogExportAsync(query, mapped.Count, hash, "success", null, cancellationToken);

        return new Qd540Table1ExportResult(mapped, distinctWarnings, skipped);
    }

    public async Task<byte[]> ExportTable1CsvAsync(
        Qd540Table1Query query,
        CancellationToken cancellationToken = default)
    {
        var result = await ExportTable1Async(query, cancellationToken);
        var csv = Qd540Transform.ToCsv(result.Rows);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }

    private static void ValidateQuery(Qd540Table1Query query)
    {
        if (query.To < query.From)
            throw new InvalidOperationException("Ngày kết thúc phải >= ngày bắt đầu.");
    }
}

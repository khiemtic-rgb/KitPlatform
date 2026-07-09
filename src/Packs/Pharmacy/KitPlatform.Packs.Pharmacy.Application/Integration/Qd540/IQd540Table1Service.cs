namespace KitPlatform.Packs.Pharmacy.Integration.Qd540;

public interface IQd540Table1Service
{
    Task<Qd540Table1ExportResult> ExportTable1Async(Qd540Table1Query query, CancellationToken cancellationToken = default);

    Task<byte[]> ExportTable1CsvAsync(Qd540Table1Query query, CancellationToken cancellationToken = default);
}

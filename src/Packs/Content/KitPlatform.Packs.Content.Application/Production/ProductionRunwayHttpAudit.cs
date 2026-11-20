namespace KitPlatform.Packs.Content.Production;

public sealed class ProductionRunwayHttpAudit
{
    public int StartCount { get; set; }
    public int PollCount { get; set; }
    public int DownloadCount { get; set; }
    public string? LastProviderJobId { get; set; }
}

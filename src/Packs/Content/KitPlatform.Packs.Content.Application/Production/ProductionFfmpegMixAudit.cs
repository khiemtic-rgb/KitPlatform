namespace KitPlatform.Packs.Content.Production;

public sealed class ProductionFfmpegMixAudit
{
    public int ExecutionCount { get; set; }
    public int ComposeExecutionCount { get; set; }
    public int ConcatExecutionCount { get; set; }
    public string? LastCommand { get; set; }
    public string? LastComposeCommand { get; set; }
    public string? LastConcatCommand { get; set; }
}

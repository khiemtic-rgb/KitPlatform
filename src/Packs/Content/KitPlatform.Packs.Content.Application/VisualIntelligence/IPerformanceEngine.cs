namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// One frame of a performance. A later engine can implement this without changing clips or timelines.
/// </summary>
public interface IPerformanceEngine
{
    string EngineId { get; }

    byte[] Render(PerformanceRequest request);
}

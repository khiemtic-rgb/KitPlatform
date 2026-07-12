namespace KitPlatform.Packs.Connect;

/// <summary>C0 overview — scaffold only; no clinical or e-Rx data.</summary>
public sealed record ConnectOverviewDto(
    string PackCode,
    string DisplayName,
    string Phase,
    string LegalBoundary,
    IReadOnlyList<string> EnabledCapabilities,
    IReadOnlyList<string> ExplicitNonGoals);

public interface IConnectOverviewService
{
    Task<ConnectOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

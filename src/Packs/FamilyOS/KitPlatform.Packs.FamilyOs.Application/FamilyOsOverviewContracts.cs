namespace KitPlatform.Packs.FamilyOs;

/// <summary>F0 overview — product boundary for FamilyOS Starter.</summary>
public sealed record FamilyOsOverviewDto(
    string PackCode,
    string DisplayName,
    string Phase,
    string Tagline,
    string LegalBoundary,
    IReadOnlyList<string> EnabledCapabilities,
    IReadOnlyList<string> ExplicitNonGoals);

public interface IFamilyOsOverviewService
{
    Task<FamilyOsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

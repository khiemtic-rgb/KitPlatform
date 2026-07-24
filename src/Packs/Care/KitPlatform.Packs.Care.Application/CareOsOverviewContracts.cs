namespace KitPlatform.Packs.Care;

public sealed record CareOsOverviewDto(
    string PackCode,
    string DisplayName,
    string Phase,
    string Tagline,
    string Purpose,
    string LegalBoundary,
    IReadOnlyList<string> EnabledCapabilities,
    IReadOnlyList<string> ExplicitNonGoals,
    IReadOnlyList<CareCapabilityReadinessDto> Readiness);

public sealed record CareCapabilityReadinessDto(
    string Code,
    string Title,
    string Status,
    string NeedsData,
    string RunnableWhen,
    string? NotImplementedReason);

public interface ICareOsOverviewService
{
    Task<CareOsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

namespace KitPlatform.Packs.FamilyOs;

/// <summary>Family Growth Blueprint™ — sparse context engines read; DNA card is the UI snapshot.</summary>
public static class FamilyBlueprintSchema
{
    public const int CurrentVersion = 1;
}

public sealed record FamilyBlueprintDto(
    Guid FamilyId,
    string LayersJson,
    string DnaJson,
    int SchemaVersion,
    DateTimeOffset? HydratedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Parent-facing DNA card (4 lines + care value + calibration). Free may teaser-truncate.</summary>
public sealed record FamilyDnaCardDto(
    Guid FamilyId,
    bool HasBlueprint,
    bool IsTeaser,
    string TierCode,
    string? StageLabelVi,
    IReadOnlyList<string> ValuesLabelsVi,
    IReadOnlyList<string> FocusLabelsVi,
    string? NextStepVi,
    string? UpgradeHintVi,
    string? CalibrationPhaseCode = null,
    string? CalibrationLabelVi = null,
    string? CoachTipVi = null,
    bool NeedsCalibrationCapture = false,
    string? CareValueVi = null,
    string? GrowthBalanceLabelVi = null,
    string? PrimaryWorryCode = null);

public sealed record FamilyBlueprintUpsertRequest(
    string? LayersJson,
    string? DnaJson,
    bool Replace = false);

public sealed record FamilyBlueprintHydrateRequest(
    /// <summary>Optional onboarding payload JSON; when null, load from family_onboarding.</summary>
    string? OnboardingPayloadJson = null);

/// <summary>Light parent capture — calibration + Growth Balance (CAL-01 + GB-01).</summary>
public sealed record FamilyCalibrationCaptureRequest(
    string? SchoolContextCode,
    string? SelfViewCode,
    string? PeerShockCode,
    string? ChildShortName = null,
    string? NoteVi = null,
    string? ResourceBandCode = null,
    string? PrimaryWorryCode = null);

public interface IFamilyBlueprintService
{
    Task<FamilyBlueprintDto?> GetAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyDnaCardDto> GetDnaCardAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyBlueprintDto> UpsertAsync(
        Guid familyId,
        FamilyBlueprintUpsertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Hydrate L1/L6/L8 (+ DNA) from onboarding answers — Wave A.</summary>
    Task<FamilyBlueprintDto> HydrateFromOnboardingAsync(
        Guid familyId,
        FamilyBlueprintHydrateRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>Capture school bubble / self-view / peer shock / growth balance — refresh DNA.</summary>
    Task<FamilyDnaCardDto> CaptureCalibrationAsync(
        Guid familyId,
        FamilyCalibrationCaptureRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Best-effort: bump illusionHits7d when Behavior marks illusion_risk.</summary>
    Task NoteIllusionRiskAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}

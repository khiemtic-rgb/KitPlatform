namespace KitPlatform.Packs.Pharmacy.Knowledge;

public static class PharmacyLocalRegimenRules
{
    public const string Draft = "DRAFT";
    public const string InReview = "IN_REVIEW";
    public const string Approved = "APPROVED";
    public const string Active = "ACTIVE";
    public const string Stopped = "STOPPED";

    public static readonly string[] MatchStatuses =
    [
        "MATCHED",
        "UNMATCHED",
        "SUBSTITUTE_PROPOSED",
        "SUBSTITUTE_CONFIRMED",
    ];

    public static readonly string[] GrantActions = ["DRAFT", "SUBMIT", "APPROVE", "ACTIVATE"];

    public static string RequireText(string? value, string label, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"Thiếu {label}.");
        if (text.Length > max)
            throw new InvalidOperationException($"{label} vượt quá {max} ký tự.");
        return text;
    }

    public static string NormalizeMatch(string? status)
    {
        var value = status?.Trim().ToUpperInvariant();
        if (value is null || !MatchStatuses.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException("Trạng thái khớp danh mục không hợp lệ.");
        return value;
    }

    public static string NormalizeGrant(string? action)
    {
        var value = action?.Trim().ToUpperInvariant();
        if (value is null || !GrantActions.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException("Quyền mẫu đơn không hợp lệ.");
        return value;
    }

    public static bool IsValidChoice(string matchStatus, Guid? productId) =>
        productId is not null
        && matchStatus is "MATCHED" or "SUBSTITUTE_CONFIRMED";
}

public sealed record LocalRegimenItemWrite(
    string Ingredient,
    string? Strength,
    string? DosageForm,
    Guid? ProductId,
    string? ProductLabel,
    string MatchStatus);

public sealed record LocalRegimenWriteRequest(
    string Title,
    string ConditionGroup,
    string Audience,
    string Symptoms,
    string Applicability,
    string Limits,
    IReadOnlyList<LocalRegimenItemWrite> Items);

public sealed record LocalRegimenTransitionRequest(
    Guid VersionId,
    DateTime ExpectedUpdatedAt,
    string? Note,
    DateTime? ReviewDueOn = null);

public sealed record LocalRegimenGrantRequest(Guid UserId, string Action);

public sealed record LocalRegimenItemDto(
    Guid Id,
    string Ingredient,
    string? Strength,
    string? DosageForm,
    Guid? ProductId,
    string? CatalogProductName,
    string? CatalogProductCode,
    string? ProductLabel,
    string MatchStatus,
    bool ValidChoice);

public sealed record LocalRegimenEventDto(
    string Action,
    int Version,
    string ActorUserId,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record LocalRegimenSummaryDto(
    Guid Id,
    Guid VersionId,
    int Version,
    string Title,
    string ConditionGroup,
    string LifecycleStatus,
    string? ActivationStatus,
    DateTime UpdatedAt,
    DateTime? ReviewDueOn = null,
    bool ReviewOverdue = false);

public sealed record LocalRegimenDetailDto(
    Guid Id,
    Guid VersionId,
    int Version,
    string Title,
    string ConditionGroup,
    string Audience,
    string Symptoms,
    string Applicability,
    string Limits,
    string LifecycleStatus,
    string? ActivationStatus,
    string CreatedBy,
    string? ApprovedBy,
    DateTime UpdatedAt,
    IReadOnlyList<LocalRegimenItemDto> Items,
    IReadOnlyList<LocalRegimenEventDto> Events,
    DateTime? ReviewDueOn = null,
    bool ReviewOverdue = false);

public sealed record LocalRegimenInUseResult(
    IReadOnlyList<LocalRegimenInUseDto> Items,
    bool StoreReady);

public sealed record LocalRegimenInUseDto(
    Guid Id,
    Guid VersionId,
    int Version,
    string Title,
    string ConditionGroup,
    string Audience,
    string Symptoms,
    string Applicability,
    string Limits,
    IReadOnlyList<LocalRegimenItemDto> Items,
    DateTime? ReviewDueOn = null,
    bool ReviewOverdue = false);

public interface IPharmacyLocalRegimenService
{
    Task<IReadOnlyList<LocalRegimenSummaryDto>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<LocalRegimenDetailDto?> GetAsync(
        Guid tenantId,
        Guid regimenId,
        CancellationToken cancellationToken = default);

    Task<LocalRegimenInUseResult> ListInUseAsync(
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken = default);

    Task<LocalRegimenDetailDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        LocalRegimenWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<LocalRegimenDetailDto> UpdateAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid regimenId,
        LocalRegimenWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<LocalRegimenDetailDto> TransitionAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid regimenId,
        string action,
        LocalRegimenTransitionRequest request,
        CancellationToken cancellationToken = default);

    Task GrantAsync(
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        LocalRegimenGrantRequest request,
        CancellationToken cancellationToken = default);
}

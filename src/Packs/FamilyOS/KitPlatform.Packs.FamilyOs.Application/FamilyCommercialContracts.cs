namespace KitPlatform.Packs.FamilyOs;

public sealed record FamilyAuthSessionDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid UserId,
    Guid TenantId,
    string TenantCode,
    string Username,
    string Email);

public sealed record FamilyRegisterRequest(
    string FamilyName,
    string ParentDisplayName,
    string Username,
    string Email,
    string Password,
    string ParentPin,
    string? Timezone = null,
    string? Child1Name = null,
    string? Child2Name = null);

public sealed record FamilyRegisterResponse(
    string TenantCode,
    Guid TenantId,
    Guid FamilyId,
    string FamilyName,
    FamilyAuthSessionDto Session,
    FamilySubscriptionDto Subscription);

public sealed record FamilyInviteCreateRequest(
    string? RoleCode = null,
    int? MaxUses = null,
    int? ValidDays = null);

public sealed record FamilyInviteDto(
    Guid Id,
    string Code,
    string RoleCode,
    DateTimeOffset ExpiresAt,
    int MaxUses,
    int UsedCount);

public sealed record FamilyInviteAcceptRequest(
    string Code,
    string ParentDisplayName,
    string Username,
    string Email,
    string Password,
    string? ParentPin = null);

public sealed record FamilyInviteAcceptResponse(
    string TenantCode,
    Guid FamilyId,
    string FamilyName,
    FamilyAuthSessionDto Session);

public sealed record FamilySubscriptionDto(
    Guid FamilyId,
    string PlanCode,
    string Status,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEnd,
    bool IsEntitled,
    /// <summary>Days left in trial (0 when expired / not trial).</summary>
    int? TrialDaysRemaining = null,
    /// <summary>Configured trial length used for progress bar (remaining / total).</summary>
    int? TrialDaysTotal = null);

/// <summary>Ops-only: extend a family's trial by N days (Admin → Billing).</summary>
public sealed record ExtendFamilyTrialRequest(int ExtraDays);

public sealed record SetParentPinRequest(string Pin);

public sealed record VerifyParentPinRequest(string Pin);

public static class FamilySubscriptionStatuses
{
    public const string Trial = "trial";
    public const string Active = "active";
    public const string PastDue = "past_due";
    public const string Expired = "expired";
    public const string Canceled = "canceled";
}

public interface IFamilyCommercialService
{
    Task<FamilyRegisterResponse> RegisterAsync(
        FamilyRegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyInviteDto> CreateInviteAsync(
        Guid familyId,
        FamilyInviteCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyInviteAcceptResponse> AcceptInviteAsync(
        FamilyInviteAcceptRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilySubscriptionDto> GetSubscriptionAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task EnsureEntitledAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    /// <summary>Ops-only: extend trial window; re-opens trial when expired.</summary>
    Task<FamilySubscriptionDto> ExtendTrialAsync(
        Guid familyId,
        ExtendFamilyTrialRequest request,
        CancellationToken cancellationToken = default);

    Task SetParentPinAsync(
        Guid familyId,
        SetParentPinRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyParentPinAsync(
        Guid familyId,
        VerifyParentPinRequest request,
        CancellationToken cancellationToken = default);
}

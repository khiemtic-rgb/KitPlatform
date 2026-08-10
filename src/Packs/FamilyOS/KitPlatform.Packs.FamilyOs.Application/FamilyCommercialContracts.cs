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
    int? TrialDaysTotal = null,
    /// <summary>Days left in post-trial soft grace (null when not in grace).</summary>
    int? TrialGraceDaysRemaining = null,
    string? TierCode = null,
    string? DisplayNameVi = null,
    string? OutcomeNameVi = null,
    int? MaxChildren = null,
    IReadOnlyList<string>? Capabilities = null,
    string? RecommendedUpgradePlanCode = null,
    string? UpgradeHintVi = null);

/// <summary>Ops-only: extend a family's trial by N days (Admin → Billing).</summary>
public sealed record ExtendFamilyTrialRequest(int ExtraDays);

/// <summary>Cross-tenant Family OS trial / interest signup (ops ledger).</summary>
public sealed record FamilyOsTrialSignupDto(
    Guid Id,
    Guid TenantId,
    string TenantCode,
    Guid FamilyId,
    string FamilyName,
    string ParentDisplayName,
    string Email,
    string Username,
    int MemberCount,
    string PlanCode,
    string Status,
    DateTimeOffset? TrialEndsAt,
    string Source,
    DateTimeOffset RegisteredAt,
    int? TrialDaysRemaining);

public sealed record FamilyOsTrialSignupListDto(
    int Total,
    int TrialActive,
    int TrialExpired,
    int PaidActive,
    int Other,
    IReadOnlyList<FamilyOsTrialSignupDto> Items);

/// <summary>GTM demo-house visit totals for admin ops.</summary>
public sealed record FamilyOsDemoHouseViewsDto(
    string DemoTenantCode,
    int ViewsToday,
    int Views7d,
    int UniqueToday,
    int Unique7d,
    DateTimeOffset? LastViewAt,
    int AvgSecondsToday,
    int AvgSeconds7d,
    int TotalSecondsToday,
    int TotalSeconds7d);

public sealed record RecordDemoHouseViewRequest(string? ClientKey, Guid? SessionId);

public sealed record DemoHousePingResponse(Guid SessionId);

public sealed record DemoHouseHeartbeatRequest(Guid SessionId);

public sealed record SetParentPinRequest(string Pin);

public sealed record VerifyParentPinRequest(string Pin);

public static class FamilySubscriptionStatuses
{
    public const string Trial = "trial";
    /// <summary>Trial calendar ended but soft grace still entitled (Pro).</summary>
    public const string TrialGrace = "trial_grace";
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

    Task<FamilyCapabilityPackDto> GetCapabilityPackAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    /// <summary>Paid/trial still required for some write paths; prefer EnsureCapabilityAsync.</summary>
    Task EnsureEntitledAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task EnsureCapabilityAsync(
        Guid familyId,
        string capabilityCode,
        CancellationToken cancellationToken = default);

    Task EnsureCanAddChildAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    /// <summary>Ops-only: extend trial window; re-opens trial when expired.</summary>
    Task<FamilySubscriptionDto> ExtendTrialAsync(
        Guid familyId,
        ExtendFamilyTrialRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Ops-only: all Family OS trial/interest signups across tenants.</summary>
    Task<FamilyOsTrialSignupListDto> ListTrialSignupsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Record a /demo enter (current tenant must be demo house or DEMO_FAMILY).</summary>
    Task<DemoHousePingResponse> RecordDemoHouseViewAsync(
        RecordDemoHouseViewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Extend dwell for an open demo session (heartbeat / pagehide).</summary>
    Task HeartbeatDemoHouseViewAsync(
        DemoHouseHeartbeatRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Ops: demo-house view totals (today / 7d / unique / dwell).</summary>
    Task<FamilyOsDemoHouseViewsDto> GetDemoHouseViewsAsync(
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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public enum SideEffectKind
{
    None,
    TenantWrite,
    ExternalCall,
    Unknown,
}

public sealed record CapabilitySlot(string Name, bool Required, string Description);

public sealed record CapabilityDescriptor(
    string Id,
    string Domain,
    string Summary,
    SideEffectKind SideEffect,
    bool TenantScoped,
    bool CanSatisfyCallerGoal,
    bool RefusesLive,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<CapabilitySlot> Slots);

public sealed record BusinessCaller(
    Guid UserId,
    Guid TenantId,
    bool IsAdmin,
    IReadOnlySet<string> Permissions);

public sealed record InterpretationContext(
    Guid TenantId,
    bool IsAdmin,
    IReadOnlyList<string> Permissions);

public sealed record MissingInformation(string Slot, string Why);

public sealed record HumanDecision(string CapabilityId, string Why);

public sealed record IntentBinding(
    string CapabilityId,
    IReadOnlyDictionary<string, string> Slots,
    string Purpose);

public sealed record IntentInterpretation(
    string Goal,
    IReadOnlyList<string> CompletionConditions,
    IReadOnlyList<string> ContextUsed,
    IReadOnlyList<MissingInformation> MissingInformation,
    IReadOnlyList<HumanDecision> HumanDecisions,
    IReadOnlyList<IntentBinding> Bindings,
    bool Ambiguous,
    string? AmbiguityNote,
    string? Limit,
    string? NextStep,
    string? RequestedTenant);

public abstract record InterpretationOutcome
{
    public sealed record Ready(IntentInterpretation Interpretation) : InterpretationOutcome;

    public sealed record Unavailable(string Reason) : InterpretationOutcome;

    public sealed record Unreadable(string Reason) : InterpretationOutcome;
}

public sealed record CapabilityExecution(
    string Status,
    bool CapabilityCompletionMet,
    string EvidenceJson,
    bool WroteData,
    bool ExternalCall);

public interface IBusinessIntentInterpreter
{
    Task<InterpretationOutcome> InterpretAsync(
        string utterance,
        IReadOnlyList<CapabilityDescriptor> catalog,
        InterpretationContext context,
        CancellationToken cancellationToken);
}

public interface IBusinessCapabilityHandler
{
    string CapabilityId { get; }

    Task<CapabilityExecution> ExecuteAsync(
        BusinessCaller caller,
        IReadOnlyDictionary<string, string> slots,
        CancellationToken cancellationToken);
}

public static class IntentDisposition
{
    public const string Run = "run";
    public const string Evaluate = "evaluate";
    public const string MissingInformation = "missing_information";
    public const string PermissionDenied = "permission_denied";
    public const string NotInCatalog = "not_in_catalog";
    public const string UnknownSideEffect = "unknown_side_effect";
    public const string TenantConflict = "tenant_conflict";
    public const string Ambiguous = "ambiguous";
    public const string NoCapability = "no_capability";
    public const string EmptyRequest = "empty_request";
    public const string TooLong = "too_long";
}

public sealed record PlanStep(
    string? CapabilityId,
    string Disposition,
    IReadOnlyDictionary<string, string> Slots,
    IReadOnlyList<string> MissingSlots,
    string Reason,
    bool RunHandler);

public sealed record RejectedBinding(string CapabilityId, string Reason);

public sealed record BusinessPlan(
    string Understanding,
    IntentInterpretation? Interpretation,
    IReadOnlyList<PlanStep> Steps,
    IReadOnlyList<RejectedBinding> Rejected,
    IReadOnlyList<HumanDecision> HumanDecisions);

public sealed record StepEvidence(
    string CapabilityId,
    string Disposition,
    string Status,
    bool CapabilityCompletionMet,
    bool CallerOutcomeMet,
    bool WroteData,
    bool ExternalCall,
    string EvidenceSha256,
    string EvidenceJson);

public sealed record BusinessIntentEvidence(
    Guid CorrelationId,
    string Outcome,
    string UtteranceSha256,
    string CatalogSha256,
    IReadOnlyList<string> CatalogIds,
    string Understanding,
    string? Goal,
    IReadOnlyList<string> CompletionConditions,
    IReadOnlyList<string> ContextUsed,
    IReadOnlyList<MissingInformation> MissingInformation,
    IReadOnlyList<HumanDecision> HumanDecisions,
    bool Ambiguous,
    string? ModelLimit,
    string? ModelNextStep,
    string? RequestedTenant,
    IReadOnlyList<RejectedBinding> Rejected,
    IReadOnlyList<PlanStep> Plan,
    IReadOnlyList<StepEvidence> Execution,
    bool CallerOutcomeMet,
    string NextStep);

public interface IBusinessIntentSession
{
    Task<BusinessIntentEvidence> HandleAsync(
        string? utterance,
        BusinessCaller caller,
        CancellationToken cancellationToken);
}

public sealed class BusinessIntentModelOptions
{
    public const string SectionName = "PersonalIntelligence:BusinessIntent";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "";

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Model);
}

public static class BusinessIntentJson
{
    public static readonly JsonSerializerOptions Serializer = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Serializer);
}

public static class BusinessIntentHash
{
    public static string Sha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

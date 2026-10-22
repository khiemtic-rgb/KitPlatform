namespace KitPlatform.Packs.PersonalIntelligence;

public static class TenantScope
{
    public static void RequireCurrent(Guid currentTenantId)
    {
        if (currentTenantId == Guid.Empty)
            throw new InvalidOperationException("The current tenant is required.");
    }

    public static void RequireMatch(Guid currentTenantId, Guid requestedTenantId)
    {
        RequireCurrent(currentTenantId);
        if (requestedTenantId != currentTenantId)
            throw new InvalidOperationException("The requested tenant does not match the current tenant.");
    }
}

public static class EpistemicKindSql
{
    public static string ToDb(EpistemicKind kind) => kind switch
    {
        EpistemicKind.Fact => "FACT",
        EpistemicKind.Observation => "OBSERVATION",
        EpistemicKind.Inference => "INFERENCE",
        EpistemicKind.Hypothesis => "HYPOTHESIS",
        EpistemicKind.Policy => "POLICY",
        EpistemicKind.Preference => "PREFERENCE",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static EpistemicKind FromDb(string value) => value switch
    {
        "FACT" => EpistemicKind.Fact,
        "OBSERVATION" => EpistemicKind.Observation,
        "INFERENCE" => EpistemicKind.Inference,
        "HYPOTHESIS" => EpistemicKind.Hypothesis,
        "POLICY" => EpistemicKind.Policy,
        "PREFERENCE" => EpistemicKind.Preference,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown epistemic kind."),
    };
}

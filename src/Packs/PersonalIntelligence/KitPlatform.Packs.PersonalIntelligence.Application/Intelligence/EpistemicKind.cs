namespace KitPlatform.Packs.PersonalIntelligence;

/// <summary>
/// How sure a statement is. An agent claim cannot be stored as Fact.
/// </summary>
public enum EpistemicKind
{
    Fact,
    Observation,
    Inference,
    Hypothesis,
    Policy,
    Preference,
}

public static class EpistemicGuard
{
    public static EpistemicKind AsAgentClaim(EpistemicKind requested) =>
        requested == EpistemicKind.Fact ? EpistemicKind.Inference : requested;

    public static bool IsVerifiedFact(EpistemicKind kind) => kind == EpistemicKind.Fact;
}

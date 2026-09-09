using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1 — Age Appearance Profile
/// must enter the existing Character Factory generation request and prompt.
/// Does not create a pipeline, call Gemini, auto-approve, auto-lock, or generate video.
/// </summary>
public static class CharacterAgeGenerationIntegrationV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1_REGRESSION";

    public static readonly string[] RequiredViews = CharacterStudioV1Rules.RequiredViews.ToArray();

    public sealed record AgeAwareGenerationRequest(
        string CharacterId,
        string? ProjectVisualStyleSha,
        string? ProjectVisualStylePrompt,
        string IdentityBrief,
        string? IdentitySha,
        string MasterSha256,
        string DnaSha256,
        string PrpSha256,
        int ChronologicalAge,
        int TargetAppearanceAgeMin,
        int TargetAppearanceAgeMax,
        string AgeAppearanceProfile,
        IReadOnlyList<string> ReferenceSetContract,
        string EraId = "ERA-01",
        CharacterAppearanceProfile? AppearanceProfile = null,
        string? AppearanceProfileBlock = null,
        string? AppearanceProfileSha = null);

    public static AgeAwareGenerationRequest BuildRequest(
        string characterId,
        string eraId,
        string? projectVisualStyleSha,
        string? projectVisualStylePrompt,
        string identityBrief,
        string? identitySha,
        string masterSha256,
        string dnaSha256,
        string prpSha256,
        AgeExpressionTarget age,
        string? gender,
        CharacterAppearanceProfile? appearance = null)
    {
        var profile = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age, gender);
        var appearanceBlock = appearance is null ? null : CharacterAppearanceProfileV1Rules.PromptBlock(appearance);
        return new AgeAwareGenerationRequest(
            CharacterStudioV1Rules.NormalizeCharacterId(characterId),
            projectVisualStyleSha,
            projectVisualStylePrompt,
            identityBrief ?? "",
            identitySha,
            masterSha256 ?? "",
            dnaSha256 ?? "",
            prpSha256 ?? "",
            age.ChronologicalAge,
            age.TargetAppearanceAgeMin,
            age.TargetAppearanceAgeMax,
            profile,
            RequiredViews,
            string.IsNullOrWhiteSpace(eraId) ? CharacterStudioV1Rules.DefaultEra : eraId.Trim().ToUpperInvariant(),
            appearance,
            appearanceBlock,
            appearance?.ProfileSha);
    }

    public static string? ValidateRequest(AgeAwareGenerationRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.AgeAppearanceProfile))
            return CharacterAgeConsistencyV1Rules.GateNotReady;
        return CharacterAgeConsistencyV1Rules.ValidateProfile(
            request.ChronologicalAge,
            request.TargetAppearanceAgeMin,
            request.TargetAppearanceAgeMax);
    }

    public static bool RequestHasAgeProfile(AgeAwareGenerationRequest? request) =>
        request is not null
        && request.ChronologicalAge > 0
        && request.TargetAppearanceAgeMin <= request.TargetAppearanceAgeMax
        && !string.IsNullOrWhiteSpace(request.AgeAppearanceProfile);

    public static bool RequestHasRequiredFields(AgeAwareGenerationRequest? request) =>
        request is not null
        && !string.IsNullOrWhiteSpace(request.CharacterId)
        && (!string.IsNullOrWhiteSpace(request.ProjectVisualStyleSha)
            || !string.IsNullOrWhiteSpace(request.ProjectVisualStylePrompt))
        && !string.IsNullOrWhiteSpace(request.IdentityBrief)
        && CharacterReferencePackRules.ShaExists(request.MasterSha256)
        && CharacterReferencePackRules.ShaExists(request.DnaSha256)
        && CharacterReferencePackRules.ShaExists(request.PrpSha256)
        && RequestHasAgeProfile(request)
        && request.ReferenceSetContract.Count == RequiredViews.Length
        && RequiredViews.All(v => request.ReferenceSetContract.Contains(v, StringComparer.OrdinalIgnoreCase));

    public static string AgePromptBlock(AgeAwareGenerationRequest request) =>
        string.Join(" ",
            $"ChronologicalAge: {request.ChronologicalAge}.",
            $"TargetAppearanceAgeMin: {request.TargetAppearanceAgeMin}.",
            $"TargetAppearanceAgeMax: {request.TargetAppearanceAgeMax}.",
            $"AgeAppearanceProfile: {request.AgeAppearanceProfile}.");

    public static string ViewCamera(string view) => CharacterStudioIdentityLockV1Rules.ViewCamera(view);

    public static string ComposeViewPrompt(
        AgeAwareGenerationRequest request,
        string view,
        IReadOnlyList<string>? refLabels = null)
    {
        var type = (view ?? "").Trim().ToUpperInvariant();
        var refs = refLabels is { Count: > 0 } ? refLabels : (IReadOnlyList<string>)["MASTER"];
        var ageBlock = AgePromptBlock(request);
        var appearanceBlock = request.AppearanceProfileBlock
            ?? (request.AppearanceProfile is null
                ? ""
                : CharacterAppearanceProfileV1Rules.PromptBlock(request.AppearanceProfile));
        return string.Join(" ", new[]
        {
            FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(request.ProjectVisualStylePrompt?.Trim() ?? ""),
            ageBlock,
            request.IdentityBrief.Trim(),
            appearanceBlock,
            CharacterStudioIdentityLockV1Rules.StudioLock,
            $"View: {type}. {ViewCamera(type)}.",
            $"Using the exact same character from {string.Join(" and ", refs)}.",
            "Preserve exact face, hairstyle, age, skin tone, body proportions, and visual style from the attached MASTER and FRONT references.",
            "Do not redesign the character.",
        }.Where(x => x.Length > 0));
    }

    public static bool PromptContainsAge(string? prompt, AgeAwareGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(prompt) || !RequestHasAgeProfile(request))
            return false;
        return prompt.Contains($"ChronologicalAge: {request.ChronologicalAge}", StringComparison.Ordinal)
            && prompt.Contains($"TargetAppearanceAgeMin: {request.TargetAppearanceAgeMin}", StringComparison.Ordinal)
            && prompt.Contains($"TargetAppearanceAgeMax: {request.TargetAppearanceAgeMax}", StringComparison.Ordinal)
            && prompt.Contains("AgeAppearanceProfile:", StringComparison.Ordinal)
            && prompt.Contains(request.AgeAppearanceProfile, StringComparison.Ordinal);
    }

    public static bool ViewsShareAgeProfile(
        IReadOnlyDictionary<string, string> viewPrompts,
        AgeAwareGenerationRequest request)
    {
        if (viewPrompts.Count == 0 || !RequestHasAgeProfile(request))
            return false;
        return viewPrompts.Values.All(p => PromptContainsAge(p, request));
    }

    public static bool ViewsDifferOnlyByCamera(IReadOnlyDictionary<string, string> viewPrompts)
    {
        if (viewPrompts.Count < 2) return false;
        var blocks = viewPrompts.ToDictionary(
            kv => kv.Key,
            kv => StripViewSpecific(kv.Value),
            StringComparer.OrdinalIgnoreCase);
        var first = blocks.Values.First();
        return blocks.Values.All(b => string.Equals(b, first, StringComparison.Ordinal));
    }

    public static string StripViewSpecific(string prompt)
    {
        var t = prompt;
        foreach (var view in RequiredViews)
            t = t.Replace($"View: {view}. {ViewCamera(view)}.", "", StringComparison.OrdinalIgnoreCase);
        return string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static CharacterReferenceSetRequest ToProviderRequest(
        AgeAwareGenerationRequest request,
        IReadOnlyList<CharacterReferenceViewRequest> views) =>
        new(
            request.CharacterId,
            request.EraId,
            request.MasterSha256,
            request.DnaSha256,
            request.PrpSha256,
            views,
            request.ChronologicalAge,
            request.TargetAppearanceAgeMin,
            request.TargetAppearanceAgeMax,
            request.AgeAppearanceProfile,
            request.ProjectVisualStyleSha,
            request.IdentitySha,
            request.AppearanceProfileBlock,
            request.AppearanceProfileSha);

    public static bool ProviderRequestHasAge(CharacterReferenceSetRequest? request) =>
        request is not null
        && CharacterAgeConsistencyV1Rules.ValidateProfile(
            request.ChronologicalAge,
            request.TargetAppearanceAgeMin,
            request.TargetAppearanceAgeMax) is null
        && !string.IsNullOrWhiteSpace(request.AgeAppearanceProfile);

    public static string EnsureAgeInPrompt(string? prompt, CharacterReferenceSetRequest request)
    {
        var text = prompt ?? "";
        if (!ProviderRequestHasAge(request))
            return text;
        if (text.Contains("ChronologicalAge:", StringComparison.OrdinalIgnoreCase)
            && text.Contains("AgeAppearanceProfile:", StringComparison.OrdinalIgnoreCase)
            && text.Contains(request.AgeAppearanceProfile!, StringComparison.Ordinal))
            return text;
        var block = string.Join(" ",
            $"ChronologicalAge: {request.ChronologicalAge}.",
            $"TargetAppearanceAgeMin: {request.TargetAppearanceAgeMin}.",
            $"TargetAppearanceAgeMax: {request.TargetAppearanceAgeMax}.",
            $"AgeAppearanceProfile: {request.AgeAppearanceProfile}.");
        return string.IsNullOrWhiteSpace(text) ? block : block + " " + text;
    }

    public static CharacterReferenceSetRequest EnsureAgeOnProviderRequest(CharacterReferenceSetRequest request)
    {
        if (!ProviderRequestHasAge(request))
            return request;
        var views = request.Views
            .Select(v => v with { CanonicalText = EnsureAppearanceInPrompt(v.CanonicalText, request) })
            .ToList();
        return request with { Views = views };
    }

    public static bool ProviderRequestHasAppearance(CharacterReferenceSetRequest? request) =>
        request is not null
        && !string.IsNullOrWhiteSpace(request.CharacterAppearanceProfile)
        && !string.IsNullOrWhiteSpace(request.AppearanceProfileSha);

    public static string? ValidateAppearance(CharacterAppearanceProfile? profile) =>
        CharacterAppearanceProfileV1Rules.Validate(profile);

    public static string EnsureAppearanceInPrompt(string? prompt, CharacterReferenceSetRequest request)
    {
        var text = EnsureAgeInPrompt(prompt, request);
        if (!ProviderRequestHasAppearance(request))
            return text;
        if (text.Contains("AppearanceProfileSha:", StringComparison.Ordinal)
            && text.Contains(request.AppearanceProfileSha!, StringComparison.OrdinalIgnoreCase))
            return text;
        var block = request.CharacterAppearanceProfile!;
        var withAppearance = string.IsNullOrWhiteSpace(text) ? block : text + " " + block;
        return CharacterDesignLanguageV2Rules.EnsureInPrompt(withAppearance);
    }

    public static string ProviderPrompt(
        CharacterReferenceSetRequest request,
        CharacterReferenceViewRequest view) =>
        EnsureAppearanceInPrompt(view.CanonicalText, request);

    public static bool ReadyAllowed(bool facePass, bool agePass, bool stylePass) =>
        facePass && agePass && stylePass;

    public static bool UsesCharacterSpecificBranch() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool GeneratesVideo() => false;
    public static bool GeneratesProduction() => false;
    public static bool CallsGemini() => false;
    public static bool RegeneratesExistingCharacters() => false;
    public static bool ChangesProjectVisualStyle() => false;
}

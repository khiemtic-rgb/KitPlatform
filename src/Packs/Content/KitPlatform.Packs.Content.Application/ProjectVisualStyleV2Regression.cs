using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ProjectVisualStyleV2Regression
{
    public const string SuiteId = ProjectVisualStyleV2Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var v1 = ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!;
        var v1Sha = ProjectVisualStyleV1Rules.Sha(v1);
        var v1Prompt = ProjectVisualStyleV1Rules.BuildPrompt(v1);
        var v2Sha = ProjectVisualStyleV2Rules.Sha();
        var v2Prompt = ProjectVisualStyleV2Rules.BuildPrompt();
        var canonical = ProjectVisualStyleV2Rules.Canonical();
        var src = FileExists(RulesPath()) ? System.IO.File.ReadAllText(RulesPath()) : "";
        var providerSrc = FileExists(ProviderPath()) ? System.IO.File.ReadAllText(ProviderPath()) : "";

        Ok(v2Sha == ProjectVisualStyleV2Rules.Sha()
            && v2Sha == KitVideoIntegrityRules.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(canonical))
            && v2Sha != v1Sha
            && ProjectVisualStyleV1Rules.LookLikeSha(v2Sha),
            "01 same project + same style input → deterministic SHA");

        var inherited = new[]
        {
            ("CHAR-001", "Minh", 11, "male", "Con"),
            ("CHAR-002", "Nam", 38, "male", "Bố"),
            ("CHAR-003", "Linh", 27, "female", "Mẹ"),
            ("CHAR-004", "An", 11, "female", "Con"),
            ("CHAR-006", "Thảo", 27, "female", "Cô giáo"),
        }.Select(c => StyleForCharacter(c.Item1, c.Item2, c.Item3, c.Item4, c.Item5)).ToList();
        Ok(inherited.All(p => p == v2Prompt)
            && inherited.TrueForAll(p => !ProjectVisualStyleV2Rules.ContainsCharacterName(p)),
            "02–06 every character inherits the same project PVS V2");

        Ok(StyleForCharacter("CHAR-099", "Lan", 38, "female", "Mẹ") == v2Prompt
            && StyleForCharacter("CHAR-098", "Hùng", 38, "male", "Bố") == v2Prompt
            && !ProjectVisualStyleV2Rules.DependsOnCharacterName()
            && !ProjectVisualStyleV2Rules.ContainsCharacterName(v2Prompt)
            && !ProjectVisualStyleV2Rules.ContainsCharacterName(canonical),
            "07 different character names → same style block");

        Ok(StyleForCharacter("CHAR-097", "Lan", 38, "female", "Bố") == v2Prompt
            && StyleForCharacter("CHAR-097", "Lan", 38, "female", "Cô giáo") == v2Prompt
            && !ProjectVisualStyleV2Rules.DependsOnRole()
            && !ProjectVisualStyleV2Rules.PromptOwnsRole(v2Prompt),
            "08 different roles → same style block");

        Ok(StyleForCharacter("CHAR-096", "Lan", 11, "female", null) == v2Prompt
            && StyleForCharacter("CHAR-096", "Lan", 38, "female", null) == v2Prompt
            && StyleForCharacter("CHAR-096", "Lan", 65, "female", null) == v2Prompt
            && !ProjectVisualStyleV2Rules.DecidesChronologicalAge()
            && !ProjectVisualStyleV2Rules.DecidesAppearanceAge()
            && !ProjectVisualStyleV2Rules.PromptOwnsAge(v2Prompt),
            "09 different ages → same style block");

        var age11 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(11);
        var age38 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var age65 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(65);
        var age27 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(27);
        Ok(age11.ChronologicalAge == 11 && age11.TargetAppearanceAgeMin == 10 && age11.TargetAppearanceAgeMax == 12
            && ProjectVisualStyleV2Rules.SemanticKeepsAgeTruth(v2Prompt, age11)
            && !ProjectVisualStyleV2Rules.DuplicatesAppearanceProfile(),
            "10 age 11 → PVS does not alter Age Policy");

        Ok(age38.ChronologicalAge == 38 && age38.TargetAppearanceAgeMin == 35 && age38.TargetAppearanceAgeMax == 41
            && ProjectVisualStyleV2Rules.SemanticKeepsAgeTruth(v2Prompt, age38),
            "11 age 38 → PVS does not alter Age Policy");

        Ok(age65.ChronologicalAge == 65 && age65.TargetAppearanceAgeMin == 61 && age65.TargetAppearanceAgeMax == 69
            && ProjectVisualStyleV2Rules.SemanticKeepsAgeTruth(v2Prompt, age65),
            "12 age 65 → PVS does not alter Age Policy");

        Ok(ProjectVisualStyleV2Rules.GenerationBlocked(null, null)
            && ProjectVisualStyleV2Rules.ValidateGeneration(null, v2Prompt) == ProjectVisualStyleV2Rules.GateNotReady,
            "13 PVS missing → generation blocked");

        Ok(ProjectVisualStyleV2Rules.ValidateGeneration(v2Sha, "not a style") == ProjectVisualStyleV2Rules.GateInvalid
            && ProjectVisualStyleV2Rules.ValidateGeneration(new string('f', 64), v2Prompt)
                == ProjectVisualStyleV2Rules.GateInvalid
            && ProjectVisualStyleV2Rules.ValidateGeneration(v2Sha, v2Prompt) == ProjectVisualStyleV1Rules.GateValid
            && ProjectVisualStyleV2Rules.ValidateGeneration(v1Sha, v1Prompt) == ProjectVisualStyleV1Rules.GateValid,
            "14 PVS invalid → generation blocked; valid V1/V2 allowed");

        Ok(ProjectVisualStyleV2Rules.V1ShaUnchanged(v1Sha)
            && v1Sha == ProjectVisualStyleV2Rules.ProtectedV1Sha
            && v1Prompt != v2Prompt
            && !ProjectVisualStyleV2Rules.OverwritesV1()
            && !v1Prompt.Contains(ProjectVisualStyleV2Rules.StylePromptBlock, StringComparison.Ordinal),
            "15 PVS V1 locked → unchanged");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && ProjectVisualStyleV2Rules.MutationForbidden(true)
            && !ProjectVisualStyleV2Rules.AutoRegenerate(),
            "16 Minh locked → no mutation");

        var appearance38 = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                38, "male", "Bố", "warm", "contemporary adult man", null, v2Sha));
        var who38 = Who("CHAR-002", "Nam", 38, "male", "Bố");
        var composed38 = ProjectVisualStyleV2Rules.ComposeGenerationPrompt(
            CharacterAppearanceProfileV1Rules.PromptBlock(appearance38),
            who38,
            "Master/DNA/PRP constraints remain authoritative for identity continuity.",
            "View: FRONT. " + CharacterAgeGenerationIntegrationV1Rules.ViewCamera("FRONT"));
        var contract38 = ProjectVisualStyleV2Rules.GenerationContract(
            "FAMIXA", "CHAR-002", CharacterAppearanceProfileV1Rules.PromptBlock(appearance38), who38, "FRONT");
        Ok(ProjectVisualStyleV2Rules.ContractHasCompiledPvs(contract38)
            && composed38.Contains(v2Prompt, StringComparison.Ordinal)
            && composed38.Contains(CharacterAppearanceProfileV1Rules.PromptBlock(appearance38), StringComparison.Ordinal),
            "17 provider adapter receives compiled PVS block");

        Ok(!ProjectVisualStyleV2Rules.ProviderOwnsStyle()
            && providerSrc.Contains("ProviderPrompt", StringComparison.Ordinal)
            && !providerSrc.Contains("if (characterName", StringComparison.Ordinal)
            && !providerSrc.Contains("StylePromptBlock", StringComparison.Ordinal)
            && !providerSrc.Contains("if (role", StringComparison.Ordinal),
            "18 provider adapter does not invent style");

        Ok(ProjectVisualStyleV2Rules.PromptBlocksPhotoreal(v2Prompt)
            && v2Prompt.Contains("NOT_PHOTOREALISTIC", StringComparison.Ordinal)
            && v2Prompt.Contains("NOT_PHOTOGRAPHIC_PORTRAIT", StringComparison.Ordinal)
            && v2Prompt.Contains("photorealistic human", StringComparison.OrdinalIgnoreCase),
            "19 prompt contains photorealism-negative rules");

        Ok(ProjectVisualStyleV2Rules.PromptHasV2Style(v2Prompt)
            && v2Prompt.Contains("designed stylized 3D", StringComparison.OrdinalIgnoreCase)
            && v2Prompt.Contains(ProjectVisualStyleV2Rules.CanonicalStyleDescription, StringComparison.Ordinal)
            && ProjectVisualStyleV2Rules.MoreStylizedThanV1(),
            "20 prompt contains stylized 3D character language");

        Ok(!src.Contains("if (characterId", StringComparison.Ordinal)
            && !src.Contains("if (characterName", StringComparison.Ordinal)
            && !ProjectVisualStyleV1Rules.ContainsCharacterHardcode(src)
            && !ProjectVisualStyleV2Rules.ContainsCharacterName(v2Prompt),
            "21 prompt does not contain character name as style branch");

        Ok(!src.Contains("if (role", StringComparison.Ordinal)
            && !ProjectVisualStyleV2Rules.PromptOwnsRole(v2Prompt)
            && !v2Prompt.Contains("FatherVisualStyle", StringComparison.Ordinal),
            "22 prompt does not contain role-specific style branch");

        var views = CharacterStudioIdentityLockV1Rules.GenerationOrder.ToDictionary(
            v => v,
            v => ProjectVisualStyleV2Rules.ComposeGenerationPrompt(
                CharacterAppearanceProfileV1Rules.PromptBlock(appearance38),
                who38,
                "Master/DNA/PRP constraints remain authoritative for identity continuity.",
                $"View: {v}. {CharacterAgeGenerationIntegrationV1Rules.ViewCamera(v)}."),
            StringComparer.OrdinalIgnoreCase);
        Ok(ProjectVisualStyleV2Rules.ViewsShareStyle(views)
            && views.Values.All(p => p.Contains(v2Prompt, StringComparison.Ordinal)),
            "23 four CRP views share the same PVS block");

        Ok(CharacterAgeGenerationIntegrationV1Rules.ViewsDifferOnlyByCamera(views),
            "24 four CRP views differ only by view/camera composition");

        var appearance11 = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                11, "male", null, null, null, null, v2Sha));
        var appearance27 = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                27, "female", null, null, null, null, v2Sha));
        var appearance65 = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                65, "male", null, null, null, null, v2Sha));
        var composed11 = ProjectVisualStyleV2Rules.ComposeGenerationPrompt(
            CharacterAppearanceProfileV1Rules.PromptBlock(appearance11), Who("CHAR-099", "Lan", 11, "male", null), "",
            "View: FRONT. " + CharacterAgeGenerationIntegrationV1Rules.ViewCamera("FRONT"));
        var composed27 = ProjectVisualStyleV2Rules.ComposeGenerationPrompt(
            CharacterAppearanceProfileV1Rules.PromptBlock(appearance27), Who("CHAR-099", "Lan", 27, "female", null), "",
            "View: FRONT. " + CharacterAgeGenerationIntegrationV1Rules.ViewCamera("FRONT"));
        var composed65 = ProjectVisualStyleV2Rules.ComposeGenerationPrompt(
            CharacterAppearanceProfileV1Rules.PromptBlock(appearance65), Who("CHAR-099", "Lan", 65, "male", null), "",
            "View: FRONT. " + CharacterAgeGenerationIntegrationV1Rules.ViewCamera("FRONT"));

        Ok(ProjectVisualStyleV2Rules.SemanticAdultLate30s(composed38, appearance38)
            && ProjectVisualStyleV2Rules.PromptIsContemporary(v2Prompt)
            && !v2Prompt.Contains("rural father", StringComparison.OrdinalIgnoreCase)
            && !v2Prompt.Contains("elderly Vietnamese man", StringComparison.OrdinalIgnoreCase),
            "30 Nam 38 + Appearance + PVS = contemporary stylized 3D adult man");

        Ok(ProjectVisualStyleV2Rules.SemanticChild(composed11, appearance11)
            && ProjectVisualStyleV2Rules.SemanticKeepsAgeTruth(v2Prompt, age11),
            "31 age 11 = stylized 3D child; Age Policy unchanged");

        Ok(ProjectVisualStyleV2Rules.SemanticYoungAdultWoman(composed27, appearance27)
            && ProjectVisualStyleV2Rules.SemanticKeepsAgeTruth(v2Prompt, age27),
            "32 age 27 = stylized 3D young adult woman");

        Ok(ProjectVisualStyleV2Rules.SemanticOlderAdult(composed65, appearance65)
            && ProjectVisualStyleV2Rules.SemanticKeepsAgeTruth(v2Prompt, age65),
            "33 age 65 = stylized 3D older adult");

        Ok(ProjectVisualStyleV2Rules.CurrentRemainsAuthority(ProjectVisualStyleV2Rules.StatusDraft)
            && ProjectVisualStyleV2Rules.CurrentRemainsAuthority(ProjectVisualStyleV2Rules.StatusPendingReview)
            && ProjectVisualStyleV2Rules.CurrentRemainsAuthority(ProjectVisualStyleV2Rules.StatusApproved)
            && !ProjectVisualStyleV2Rules.CandidateIsAuthority(ProjectVisualStyleV2Rules.StatusApproved)
            && ProjectVisualStyleV2Rules.EvaluateCreate(new ProjectVisualStyleV2Rules.RevisionSource(
                true, "V1", v1Sha, null, false, "director")) == ProjectVisualStyleV2Rules.GateConfirmation
            && ProjectVisualStyleV2Rules.EvaluateAdvance(ProjectVisualStyleV2Rules.StatusPendingReview, "LOCK")
                == ProjectVisualStyleV2Rules.GateNotApproved
            && ProjectVisualStyleV2Rules.CandidateIsAuthority(ProjectVisualStyleV2Rules.StatusLocked),
            "21b candidate does not silently become authority");

        Ok(!ProjectVisualStyleV2Rules.CallsGemini()
            && !ProjectVisualStyleV2Rules.CreatesPixels()
            && !ProjectVisualStyleV2Rules.AutoApprove()
            && !ProjectVisualStyleV2Rules.AutoLock()
            && !src.Contains("ContentGeminiClient", StringComparison.Ordinal)
            && !src.Contains("Google.GenAI", StringComparison.Ordinal),
            "24b no Gemini / pixels / auto-approve / auto-lock");

        return fail;
    }

    private static string StyleForCharacter(
        string characterId, string name, int age, string gender, string? role)
    {
        _ = characterId;
        _ = name;
        _ = age;
        _ = gender;
        _ = role;
        return ProjectVisualStyleV2Rules.BuildPrompt();
    }

    private static string Who(string characterId, string name, int age, string gender, string? role) =>
        string.Join(" ", new[]
        {
            "Character: " + name + ".",
            "CharacterId: " + characterId + ".",
            "ChronologicalAge: " + age + ".",
            "Gender: " + gender + ".",
            string.IsNullOrWhiteSpace(role) ? "" : "NarrativeRole: " + role + ".",
        }.Where(x => x.Length > 0));

    private static bool FileExists(string path) => System.IO.File.Exists(path);

    private static string RulesPath() => FindRepoFile(
        "src/Packs/Content/KitPlatform.Packs.Content.Application/ProjectVisualStyleV2Rules.cs");

    private static string ProviderPath() => FindRepoFile(
        "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterReferenceGenerationProvider.cs");

    private static string FindRepoFile(string relative)
    {
        var dir = System.IO.Path.GetDirectoryName(typeof(ProjectVisualStyleV2Rules).Assembly.Location) ?? "";
        for (var i = 0; i < 8; i++)
        {
            var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, relative));
            if (System.IO.File.Exists(candidate))
                return candidate;
            var parent = System.IO.Path.GetDirectoryName(dir);
            if (string.IsNullOrWhiteSpace(parent) || parent == dir)
                break;
            dir = parent;
        }
        return "";
    }
}

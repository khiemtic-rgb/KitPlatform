using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_PROJECT_VISUAL_STYLE_V1 — Visual Style Authority belongs to the Project.
/// Characters inherit. Presets are templates only. No character-specific hard-code.
/// Does not generate images, call Gemini, approve, or lock characters.
/// </summary>
public static class ProjectVisualStyleV1Rules
{
    public const string DocumentId = "FAMIXA_PROJECT_VISUAL_STYLE_V1";
    public const string SuiteId = "FAMIXA_PROJECT_VISUAL_STYLE_V1_REGRESSION";
    public const string SystemCode = "PROJECT_VISUAL_STYLE";
    public const string Version = "PROJECT_VISUAL_STYLE_V1";
    public const string DefaultProject = "FAMIXA";

    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Locked = "LOCKED";
    public const string Archived = "ARCHIVED";
    public const string NotConfigured = "VISUAL_STYLE_NOT_CONFIGURED";

    public const string InheritInherited = "INHERITED";
    public const string InheritLockedByProject = "LOCKED_BY_PROJECT";
    public const string InheritMismatch = "VISUAL_STYLE_MISMATCH";

    public const string GateNotReady = "PROJECT_VISUAL_STYLE_NOT_READY";
    public const string GateConflict = "VISUAL_STYLE_CONFLICT";
    public const string GateCrpMismatch = "CRP_VISUAL_STYLE_MISMATCH";
    public const string GateDuplicate = "BLOCK_DUPLICATE";
    public const string GateLocked = "STYLE_LOCKED";
    public const string GateValid = "VALID";

    public const string StaffNotConfigured = "Project chưa thiết lập phong cách hình ảnh.";
    public const string StaffSetup = "Thiết lập phong cách";
    public const string StaffInherited = "Nhân vật này sẽ sử dụng phong cách hình ảnh của Project.";
    public const string StaffLockedByProject = "LOCKED BY PROJECT";
    public const string StaffAuthority = "PROJECT";

    public static bool CharacterMaySelectIndependentStyle() => false;
    public static bool StyleExceptionEnabled() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool RegeneratesExistingCharacters() => false;

    public sealed record StyleDefinition(
        string StyleKey,
        string StyleName,
        string Description,
        string RenderingStyle,
        string CharacterStyle,
        string EnvironmentStyle,
        string LightingStyle,
        string ColorStyle,
        string CameraStyle,
        string TextureStyle,
        string RealismLevel,
        string AgeRepresentationRule,
        string AnatomyRule,
        string ConsistencyRules,
        string NegativeRules,
        IReadOnlyList<string> PreviewBullets);

    public static readonly IReadOnlyList<StyleDefinition> Presets =
    [
        new("3D_STYLIZED_REALISM", "3D Stylized Realism",
            "Stylized 3D humans in one cinematic family world. Not photoreal. Not cartoon squash.",
            "Stylized3D", "StylizedRealistic", "CinematicStylized", "SoftCinematic",
            "NaturalMuted", "CinematicNatural", "SoftDetailed", "Medium",
            "AgeConsistent", "AgeConsistent", "SingleVisualUniverse",
            "photoreal photograph, celebrity likeness, anime cel, cartoon squash, text overlay",
            [
                "Nhân vật 3D",
                "Gương mặt stylized",
                "Tỷ lệ phù hợp độ tuổi",
                "Da và tóc có texture mềm",
                "Không photorealistic",
                "Không chuyển thành người thật",
                "Ánh sáng cinematic nhẹ",
                "Giữ nhận diện nhân vật cao giữa các shot",
            ]),
        new("3D_CARTOON", "3D Cartoon",
            "Readable 3D cartoon volumes. Simple shapes. Same world for every character.",
            "Stylized3DCartoon", "CartoonReadable", "StylizedInterior", "FlatEven",
            "SoftGraphic", "NeutralSheet", "GraphicSmooth", "Low",
            "AgeConsistent", "StylizedCartoonAnatomy", "SingleVisualUniverse",
            "photoreal, gritty realism, anime cel, text overlay",
            ["Khối 3D dễ đọc", "Tỷ lệ stylized", "Không photoreal", "Một vũ trụ cho mọi nhân vật"]),
        new("ANIMATION", "Animation",
            "Animated feature look. Designed characters, not photographed people.",
            "FeatureAnimation", "DesignedCharacter", "CinematicStylized", "SoftCinematic",
            "NaturalMuted", "CinematicNatural", "PaintedSmooth", "MediumLow",
            "AgeConsistent", "AgeConsistent", "SingleVisualUniverse",
            "photoreal, live-action likeness, text overlay",
            ["Nhân vật thiết kế", "Chuyển động animation", "Không giả người thật"]),
        new("PHOTOREALISTIC", "Photorealistic",
            "Photoreal identity world. Natural skin and light. Still one project universe.",
            "Photoreal", "NaturalHuman", "NaturalEnvironment", "NeutralDaylight",
            "Natural", "IdentityNeutral", "RealSkinFabric", "High",
            "AgeAccurate", "AnatomicallyAccurate", "SingleVisualUniverse",
            "cartoon, anime, collage, text overlay",
            ["Người thật stylized tối thiểu", "Da và vải tự nhiên", "Một universe cho project"]),
        new("CINEMATIC_REALISM", "Cinematic Realism",
            "Filmic realistic humans. Motivated light. Not a snapshot collage.",
            "CinematicReal", "FilmicHuman", "CinematicLocation", "MotivatedCinematic",
            "FilmicMuted", "CinematicNatural", "NaturalMaterials", "HighMedium",
            "AgeAccurate", "AgeConsistent", "SingleVisualUniverse",
            "cartoon, anime, text overlay, multi-panel",
            ["Ánh sáng điện ảnh", "Người filmic", "Không collage"]),
        new("ILLUSTRATION", "Illustration",
            "Painted illustration world. Readable faces. Stable line.",
            "PaintedIllustration", "IllustratedHuman", "PaintedEnvironment", "EvenStorybook",
            "PaintedMuted", "NeutralIllustration", "PaintedPaper", "MediumLow",
            "AgeConsistent", "IllustratedAnatomy", "SingleVisualUniverse",
            "photoreal, 3D render, anime cel, text",
            ["Minh họa vẽ", "Mặt đọc được", "Không photoreal"]),
        new("ANIME", "Anime",
            "Clean anime character world. Consistent face and hair across the project.",
            "AnimeCel", "AnimeCharacter", "AnimeEnvironment", "EvenAnime",
            "AnimeClear", "CharacterSheet", "CelShade", "Low",
            "AgeConsistent", "AnimeProportions", "SingleVisualUniverse",
            "photoreal, western cartoon, text",
            ["Anime sạch", "Mặt và tóc ổn định", "Không photoreal"]),
        new("CUSTOM", "Custom",
            "Director-defined project visual constitution. Still one ACTIVE authority.",
            "CustomDirected", "ProjectDirected", "ProjectDirected", "ProjectDirected",
            "ProjectDirected", "ProjectDirected", "ProjectDirected", "Directed",
            "AgeConsistent", "AgeConsistent", "SingleVisualUniverse",
            "mixed visual universes, silent style drift",
            ["Director định nghĩa", "Vẫn một Visual Style Authority"]),
    ];

    public static StyleDefinition? PresetOf(string? key)
    {
        var want = NormalizeKey(key);
        return Presets.FirstOrDefault(p => p.StyleKey == want || StudioKeyOf(p.StyleKey) == want);
    }

    public static string NormalizeKey(string? raw)
    {
        var t = (raw ?? "").Trim().ToUpperInvariant();
        if (t.StartsWith("STYLE_", StringComparison.Ordinal))
            t = t[6..];
        return t switch
        {
            "2D_STORYBOOK" => "ILLUSTRATION",
            "CARTOON" => "3D_CARTOON",
            _ => t,
        };
    }

    public static string StudioKeyOf(string styleKey) => "STYLE_" + NormalizeKey(styleKey);

    public static bool PresetValid(string? key) => PresetOf(key) is not null;

    public static string Canonical(StyleDefinition style) => string.Join('\n', new[]
    {
        "PROJECT_STYLE",
        "StyleKey=" + style.StyleKey,
        "RenderingStyle=" + style.RenderingStyle,
        "RealismLevel=" + style.RealismLevel,
        "CharacterStyle=" + style.CharacterStyle,
        "EnvironmentStyle=" + style.EnvironmentStyle,
        "LightingStyle=" + style.LightingStyle,
        "ColorStyle=" + style.ColorStyle,
        "TextureStyle=" + style.TextureStyle,
        "CameraStyle=" + style.CameraStyle,
        "AnatomyRule=" + style.AnatomyRule,
        "AgeRepresentationRule=" + style.AgeRepresentationRule,
        "ConsistencyRule=" + style.ConsistencyRules,
        "NegativeRules=" + style.NegativeRules,
    });

    public static string Sha(StyleDefinition style) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(Canonical(style)));

    public static bool SameSha(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
        && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool StatusAllowsGeneration(string? status)
    {
        var s = (status ?? "").Trim().ToUpperInvariant();
        return s is Active or Locked;
    }

    public static bool StatusAllowsMutate(string? status) =>
        string.Equals((status ?? "").Trim(), Draft, StringComparison.OrdinalIgnoreCase);

    public static bool IsConfigured(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && !string.Equals(status, NotConfigured, StringComparison.OrdinalIgnoreCase);

    public static string ValidateReady(string? status, string? sha)
    {
        if (!IsConfigured(status) || !StatusAllowsGeneration(status) || !LookLikeSha(sha))
            return GateNotReady;
        return GateValid;
    }

    public static bool LookLikeSha(string? sha)
    {
        var t = (sha ?? "").Trim();
        return t.Length == 64 && t.All(Uri.IsHexDigit);
    }

    public static string DbStatusOf(string logical) => (logical ?? "").Trim().ToUpperInvariant() switch
    {
        Active => "proposed",
        Locked => "locked",
        Archived => "superseded",
        _ => "draft",
    };

    public static string LogicalStatusOf(string? db, string? rulesStatus = null)
    {
        var fromRules = (rulesStatus ?? "").Trim().ToUpperInvariant();
        if (fromRules is Draft or Active or Locked or Archived) return fromRules;
        return (db ?? "").Trim().ToLowerInvariant() switch
        {
            "proposed" => Active,
            "locked" => Locked,
            "superseded" => Archived,
            _ => Draft,
        };
    }

    public static string NextVersion(string? current)
    {
        var t = (current ?? "V1").Trim().ToUpperInvariant();
        if (t.Length < 2 || t[0] != 'V' || !int.TryParse(t[1..], out var n))
            return "V2";
        return $"V{n + 1}";
    }

    public static bool CharacterOverrideAccepted(string? requestedKey, string projectKey) =>
        CharacterMaySelectIndependentStyle()
        && string.Equals(NormalizeKey(requestedKey), NormalizeKey(projectKey), StringComparison.Ordinal);

    public static string InheritStatus(string? characterKey, string projectKey, bool characterLockedByProject = true)
    {
        if (string.IsNullOrWhiteSpace(characterKey))
            return characterLockedByProject ? InheritLockedByProject : InheritInherited;
        return NormalizeKey(characterKey) == NormalizeKey(projectKey)
            ? (characterLockedByProject ? InheritLockedByProject : InheritInherited)
            : InheritMismatch;
    }

    public static bool PrpConflicts(string? prpStyleKey, string? prpRendering, string? prpRealism, StyleDefinition project)
    {
        if (!string.IsNullOrWhiteSpace(prpStyleKey) && NormalizeKey(prpStyleKey) != project.StyleKey)
            return true;
        if (!string.IsNullOrWhiteSpace(prpRendering)
            && !string.Equals(prpRendering.Trim(), project.RenderingStyle, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(prpRealism)
            && !string.Equals(prpRealism.Trim(), project.RealismLevel, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public static bool CrpStylesConsistent(IReadOnlyList<string?> slotStyleKeys)
    {
        var keys = slotStyleKeys
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return keys.Count <= 1;
    }

    public static string? CrpMismatchCode(IReadOnlyList<string?> slotStyleKeys) =>
        CrpStylesConsistent(slotStyleKeys) ? null : GateCrpMismatch;

    public static bool ProviderMayMutateStyle() => false;

    public static bool ProviderMutated(string? requestedSha, string authoritativeSha) =>
        !SameSha(requestedSha, authoritativeSha);

    public static bool HistoricalKeepsOriginal(string? artifactSha, string? originalStyleSha, string? currentStyleSha) =>
        !string.IsNullOrWhiteSpace(artifactSha)
        && SameSha(originalStyleSha, originalStyleSha)
        && (string.IsNullOrWhiteSpace(currentStyleSha) || !string.IsNullOrWhiteSpace(originalStyleSha));

    public static bool RelinkForbidden(string? historicalStyleSha, string? newActiveSha) =>
        LookLikeSha(historicalStyleSha) && LookLikeSha(newActiveSha)
        && !SameSha(historicalStyleSha, newActiveSha);

    public static string BuildPrompt(StyleDefinition style) => string.Join(" ", new[]
    {
        $"Render all characters consistently in the project's {style.StyleName} visual universe.",
        "Do not convert the character into a photorealistic human unless the project style is photorealistic.",
        $"Maintain {style.CharacterStyle} facial proportions.",
        $"Maintain {style.AgeRepresentationRule} anatomy.",
        "Maintain consistent skin, hair, eye and facial rendering.",
        "All reference views must represent the exact same character and the same visual style.",
        $"Lighting: {style.LightingStyle}. Color: {style.ColorStyle}. Texture: {style.TextureStyle}.",
        $"Forbidden: {style.NegativeRules}.",
        "Do not invent a private visual style for this character.",
    });

    public static object GenerationContract(
        string projectId,
        string characterId,
        string characterAuthority,
        StyleDefinition style,
        string visualStyleId,
        string visualStyleSha,
        string masterSha,
        string dnaSha,
        string prpSha,
        IReadOnlyList<string> referenceTypes,
        string intent) => new
    {
        projectId = (projectId ?? DefaultProject).Trim().ToUpperInvariant(),
        characterId = CharacterStudioV1Rules.NormalizeCharacterId(characterId),
        characterAuthority = (characterAuthority ?? "").Trim().ToLowerInvariant(),
        projectVisualStyle = style.StyleKey,
        projectVisualStyleSha = (visualStyleSha ?? "").Trim().ToLowerInvariant(),
        visualStyleId = (visualStyleId ?? "").Trim(),
        visualStyleSha = (visualStyleSha ?? "").Trim().ToLowerInvariant(),
        visualStyleCanonical = Canonical(style),
        masterSha = (masterSha ?? "").Trim().ToLowerInvariant(),
        dnaSha = (dnaSha ?? "").Trim().ToLowerInvariant(),
        prpSha = (prpSha ?? "").Trim().ToLowerInvariant(),
        referenceTypes,
        intent = (intent ?? "CHARACTER_REFERENCE").Trim().ToUpperInvariant(),
        canonical = Canonical(style),
    };

    public static bool ContractContainsStyleSha(object contract, string expectedSha)
    {
        var json = JsonSerializer.Serialize(contract, CanonicalOptions);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("projectVisualStyleSha", out var sha)
            && !doc.RootElement.TryGetProperty("visualStyleSha", out sha))
            return false;
        return SameSha(sha.GetString(), expectedSha);
    }

    public static object FingerprintSurface(
        string projectId,
        string visualStyleSha,
        string characterId,
        string masterSha,
        string dnaSha,
        string prpSha,
        string referenceSetDefinition) => new
    {
        project_id = (projectId ?? DefaultProject).Trim().ToUpperInvariant(),
        project_visual_style_sha = (visualStyleSha ?? "").Trim().ToLowerInvariant(),
        character_id = CharacterStudioV1Rules.NormalizeCharacterId(characterId),
        master_sha256 = (masterSha ?? "").Trim().ToLowerInvariant(),
        dna_sha256 = (dnaSha ?? "").Trim().ToLowerInvariant(),
        prp_sha256 = (prpSha ?? "").Trim().ToLowerInvariant(),
        reference_set_definition = (referenceSetDefinition ?? "").Trim().ToUpperInvariant(),
        document = DocumentId,
    };

    public static string ExecutionFingerprint(
        string projectId,
        string visualStyleSha,
        string characterId,
        string masterSha,
        string dnaSha,
        string prpSha,
        string referenceSetDefinition) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(
                FingerprintSurface(projectId, visualStyleSha, characterId, masterSha, dnaSha, prpSha, referenceSetDefinition),
                CanonicalOptions)));

    public static object DnaStyleReference(string visualStyleId, string visualStyleSha, StyleDefinition style) => new
    {
        project_visual_style_id = visualStyleId,
        project_visual_style_sha = visualStyleSha.Trim().ToLowerInvariant(),
        style_family = style.StyleKey,
        rendering_style = style.RenderingStyle,
        realism_level = style.RealismLevel,
        anatomy_rules = style.AnatomyRule,
        face_rendering_rules = style.CharacterStyle,
        hair_rendering_rules = style.TextureStyle,
        skin_rendering_rules = style.TextureStyle,
        lighting_rules = style.LightingStyle,
        color_rules = style.ColorStyle,
        authority = StaffAuthority,
    };

    public static object PersistDocument(
        Guid id,
        string projectId,
        StyleDefinition style,
        string version,
        string status,
        string sha,
        string? lockedBy = null) => new
    {
        documentId = DocumentId,
        id = id.ToString(),
        projectId,
        styleKey = style.StyleKey,
        styleName = style.StyleName,
        description = style.Description,
        renderingStyle = style.RenderingStyle,
        characterStyle = style.CharacterStyle,
        environmentStyle = style.EnvironmentStyle,
        lightingStyle = style.LightingStyle,
        colorStyle = style.ColorStyle,
        cameraStyle = style.CameraStyle,
        textureStyle = style.TextureStyle,
        realismLevel = style.RealismLevel,
        ageRepresentationRule = style.AgeRepresentationRule,
        anatomyRule = style.AnatomyRule,
        consistencyRules = style.ConsistencyRules,
        negativeRules = style.NegativeRules,
        version,
        status,
        sha,
        authority = StaffAuthority,
        lockedBy,
        preview = style.PreviewBullets,
        prompt = BuildPrompt(style),
        canonical = Canonical(style),
    };

    public static StyleDefinition FromDocument(JsonElement rules)
    {
        string Read(string name, string fallback) =>
            rules.ValueKind == JsonValueKind.Object && rules.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? fallback
                : fallback;
        var key = NormalizeKey(Read("styleKey", "3D_STYLIZED_REALISM"));
        return PresetOf(key) ?? Presets[0] with
        {
            StyleKey = key,
            StyleName = Read("styleName", key),
            Description = Read("description", ""),
            RenderingStyle = Read("renderingStyle", "Stylized3D"),
            CharacterStyle = Read("characterStyle", "StylizedRealistic"),
            EnvironmentStyle = Read("environmentStyle", "CinematicStylized"),
            LightingStyle = Read("lightingStyle", "SoftCinematic"),
            ColorStyle = Read("colorStyle", "NaturalMuted"),
            CameraStyle = Read("cameraStyle", "CinematicNatural"),
            TextureStyle = Read("textureStyle", "SoftDetailed"),
            RealismLevel = Read("realismLevel", "Medium"),
            AgeRepresentationRule = Read("ageRepresentationRule", "AgeConsistent"),
            AnatomyRule = Read("anatomyRule", "AgeConsistent"),
            ConsistencyRules = Read("consistencyRules", "SingleVisualUniverse"),
            NegativeRules = Read("negativeRules", "photoreal photograph"),
        };
    }

    public static bool ContainsCharacterHardcode(string source)
    {
        var prefix = "if (characterId == \"";
        return source.Contains(prefix + "CHAR-001\"", StringComparison.Ordinal)
            || source.Contains(prefix + "CHAR-002\"", StringComparison.Ordinal)
            || source.Contains("Minh" + "VisualStyle", StringComparison.Ordinal)
            || source.Contains("Nam" + "VisualStyle", StringComparison.Ordinal);
    }

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

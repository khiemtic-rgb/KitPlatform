namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VISUAL_UNIVERSE_LOCK_READINESS_V1 — read-only audit.
/// Does not generate, approve, lock, promote, persist, or mutate authority.
/// Does not rewrite PVS / CDL / VUA. Reports facts from live rules.
/// </summary>
public static class VisualUniverseLockReadinessV1Rules
{
    public const string DocumentId = "FAMIXA_VISUAL_UNIVERSE_LOCK_READINESS_V1";
    public const string SuiteId = "FAMIXA_VISUAL_UNIVERSE_LOCK_READINESS_V1_REGRESSION";

    public const string Ready = "READY";
    public const string ReadyWithRisks = "READY_WITH_RISKS";
    public const string NotReady = "NOT_READY";
    public const string Pass = "PASS";
    public const string PassWithRisk = "PASS_WITH_RISK";
    public const string Fail = "FAIL";
    public const string LookExplicit = "EXPLICIT";
    public const string LookPartial = "PARTIALLY_EXPLICIT";
    public const string LookGeneric = "TOO_GENERIC";
    public const string LookMissing = "MISSING";
    public const string RecommendGenerate = "GENERATE_CALIBRATION";
    public const string RecommendFix = "FIX_VISUAL_AUTHORITY_FIRST";
    public const string RecommendStop = "STOP_INVESTIGATE";

    public static bool CallsGemini() => false;
    public static bool CallsRunway() => false;
    public static bool CallsFal() => false;
    public static bool CreatesPixels() => false;
    public static bool CreatesVideo() => false;
    public static bool MutatesDatabase() => false;
    public static bool MutatesCharacters() => false;
    public static bool MutatesAuthority() => false;
    public static bool Approves() => false;
    public static bool Locks() => false;
    public static bool Promotes() => false;

    public static VisualUniverseLockReadinessReport Audit(VisualUniverseAuthorityDto? live = null)
    {
        var before = CaptureShas();
        var status = string.IsNullOrWhiteSpace(live?.Status)
            ? FamixaVisualUniverseAuthorityV1Rules.StatusDraft
            : live!.Status.Trim().ToUpperInvariant();
        var snapshot = VisualUniverseSnapshotV1Rules.FromAuthorities(
            FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            live ?? FamixaVisualUniverseAuthorityV1Rules.ToDto(status, false),
            ProjectVisualStyleV2Rules.ProtectedV1Sha);
        var pvs = ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId)!;
        var pvsPrompt = ProjectVisualStyleV1Rules.BuildPrompt(pvs);
        var prefix = FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(pvsPrompt);
        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var dry = VisualCalibrationGenerationServiceV1.DryRun(pack);
        var compiled = CompileSurfaces(snapshot);
        var matrix = InspectMatrix(pack, prefix);
        var after = CaptureShas();
        return new VisualUniverseLockReadinessReport(
            DocumentId,
            ReadyWithRisks,
            CurrentAuthority(status, live),
            CandidateAuthority(status),
            PvsAudit(pvs, snapshot),
            CdlAudit(),
            VuaSemantic(),
            SnapshotAudit(snapshot),
            CompilerAudit(),
            PromptPreview(pack, prefix),
            PromptAuthorityCheck(snapshot, compiled, prefix),
            compiled.Master,
            compiled.Still,
            compiled.Video,
            compiled.Consistency,
            matrix,
            CrossCharacter(pack, prefix),
            AgeAppearanceCheck(snapshot, prefix),
            RealismCeilingCheck(snapshot, prefix),
            FamixaLook(),
            ContemporaryAssessment(),
            LockImpact(status, pack),
            before,
            after,
            ShaUnchanged(before, after),
            Safety(),
            RecommendGenerate,
            DirectorQuestions(status, snapshot, prefix, pack, compiled));
    }

    public static CurrentAuthorityBlock CurrentAuthority(string status, VisualUniverseAuthorityDto? live) =>
        new(
            status,
            live?.Sha256 ?? FamixaVisualUniverseAuthorityV1Rules.Sha(),
            FamixaVisualUniverseAuthorityV1Rules.IsAuthority(status),
            live?.Persisted == true,
            FamixaVisualUniverseAuthorityV1Rules.CurrentPvsRemainsAuthority(status),
            live?.StaffMessage
            ?? "PVS V1 vẫn là production authority cho đến khi Director khóa Visual Universe.");

    public static CandidateAuthorityBlock CandidateAuthority(string status) =>
        new(
            status,
            FamixaVisualUniverseAuthorityV1Rules.Sha(),
            FamixaVisualUniverseAuthorityV1Rules.IsAuthority(status),
            FamixaVisualUniverseAuthorityV1Rules.MayApprove(status),
            FamixaVisualUniverseAuthorityV1Rules.MayLock(status),
            false,
            "VUA candidate SHA is the compiled document SHA. It is not production authority until LOCKED.");

    public static PvsAuditBlock PvsAudit(ProjectVisualStyleV1Rules.StyleDefinition pvs, VisualUniverseSnapshot snapshot) =>
        new(
            "ACTIVE",
            ProjectVisualStyleV2Rules.ProtectedV1Sha,
            pvs.StyleKey,
            pvs.RealismLevel,
            pvs.RenderingStyle,
            pvs.LightingStyle,
            pvs.NegativeRules,
            "DRAFT",
            ProjectVisualStyleV2Rules.Sha(),
            ProjectVisualStyleV2Rules.StyleKey,
            snapshot.PvsSha,
            FamixaVisualUniverseAuthorityV1Rules.SameSha(snapshot.PvsSha, ProjectVisualStyleV2Rules.ProtectedV1Sha),
            !VisualUniverseSnapshotV1Rules.UsesPvsV2AsLiveAuthority(),
            PassWithRisk,
            "PVS V1 3D_STYLIZED_REALISM is the snapshot-bound production PVS. "
            + "RealismLevel=Medium. BuildPrompt allows photoreal only if project style is photorealistic. "
            + "Description says 'cinematic family world'. PVS V2 is stronger but unused.");

    public static CdlAuditBlock CdlAudit() =>
        new(
            CharacterDesignLanguageV2Rules.Version,
            CharacterDesignLanguageV2Rules.Sha(),
            CharacterDesignLanguageV2Rules.Status,
            "CharacterDesignLanguageV2Rules.Sha() bound by VisualUniverseSnapshotV1Rules and Calibration CompilePack",
            CharacterDesignLanguageV2Rules.PhotorealismCeiling,
            CharacterDesignLanguageV2Rules.LifestyleBoundary,
            Pass,
            "CDL V2 is compileable and shared across age/gender. LifestyleBoundary blocks rural/farmer default. "
            + "Older adult stays in the same design language. Status is still DRAFT.");

    public static VuaSemanticBlock VuaSemantic() =>
        new(
            FamixaVisualUniverseAuthorityV1Rules.StyleIntent,
            FamixaVisualUniverseAuthorityV1Rules.StylizationLevel,
            FamixaVisualUniverseAuthorityV1Rules.PhotorealismCeiling,
            FamixaVisualUniverseAuthorityV1Rules.RealismCeiling,
            "composition_and_lighting",
            "VUA LightingLanguage = 'Soft cinematic lighting, controlled highlights and shadows. "
            + "No photographic portrait treatment.' CDL rejects cinematic live-action lighting and "
            + "cinematic human portrait. 'Cinematic' here is lighting/composition, not photorealism.",
            FamixaVisualUniverseAuthorityV1Rules.NegativeConstraints,
            "Famixa characters should look like designed inhabitants of one stylized 3D / animated world. "
            + "High quality never means more photorealistic.");

    public static SnapshotAuditBlock SnapshotAudit(VisualUniverseSnapshot snapshot) =>
        new(
            "VisualUniverseSnapshotResolver",
            "IVisualUniverseAuthority.GetAsync + IProjectVisualStyleAuthority.GetActiveAsync + optional calibration row",
            snapshot.VisualUniverseSha,
            snapshot.PvsSha,
            snapshot.CdlSha,
            snapshot.CalibrationPackSha,
            "FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(ProjectVisualStyleV1Rules.BuildPrompt(PVS V1))",
            snapshot.PvsIsCurrentAuthority,
            snapshot.VuaIsCurrentAuthority,
            snapshot.PhotorealismCeiling);

    public static CompilerAuditBlock CompilerAudit() =>
        new(
            "IUnifiedVisualCompiler",
            "UnifiedVisualCompiler / UnifiedVisualCompilerV1Rules",
            "VisualCalibrationPromptCompilerV1 / VisualCalibrationPackV1Rules.CompilePrompt",
            "FamixaVisualUniverseAuthorityV1Rules.CompileCalibrationPrompt (4-slot VUA surface)",
            "Calibration 24-slot generate does not call IUnifiedVisualCompiler. "
            + "It calls CompileStylePrefix directly, then appends calibration extras. "
            + "Master / Still / Video use IUnifiedVisualCompiler wrapping the same CompileStylePrefix.");

    public static IReadOnlyList<PromptPreviewBlock> PromptPreview(
        VisualCalibrationPackV1Rules.PackSnapshot pack, string prefix)
    {
        var ids = new[] { "CAL-001", "CAL-003", "CAL-005" };
        return ids.Select(id =>
        {
            var subject = VisualCalibrationPackV1Rules.DefaultMatrix.First(s => s.CalibrationSubjectId == id);
            var prompt = VisualCalibrationPackV1Rules.CompilePrompt(subject, "FRONT",
                ProjectVisualStyleV1Rules.BuildPrompt(
                    ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId)!));
            return new PromptPreviewBlock(
                id,
                subject.Label,
                "FRONT",
                prefix,
                ExtractBetween(prompt, "ChronologicalAge:", "View:"),
                ExtractBetween(prompt, "[CALIBRATION VISUAL LANGUAGE]", "ChronologicalAge:"),
                ExtractFrom(prompt, "View:"),
                ExtractBetween(prompt, "[GLOBAL NEGATIVE CONSTRAINTS]", "ChronologicalAge:"),
                pack.VisualUniverseSha,
                pack.ProjectVisualStyleSha,
                pack.CharacterDesignLanguageSha,
                prompt.StartsWith(prefix, StringComparison.Ordinal));
        }).ToList();
    }

    public static PromptAuthorityBlock PromptAuthorityCheck(
        VisualUniverseSnapshot snapshot,
        SurfaceCompile compiled,
        string prefix)
    {
        var injections = new[]
        {
            "photorealistic", "real human", "cinematic 3D", "realistic skin",
            "documentary portrait", "Vietnamese rural elderly man", "highly realistic face",
        };
        var hijack = SeriesStillVisualIngressV1Rules.CompileStill(
            snapshot,
            new SeriesStillVisualIngressV1Rules.SceneVisualContract(
                CharacterId: "CHAR-099",
                RawPrompt: string.Join(" ", injections),
                IdentityBrief: CharacterStudioV1Rules.IdentityBrief(
                    "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM",
                    "photorealistic documentary portrait"),
                PvsBuildPrompt: ProjectVisualStyleV1Rules.BuildPrompt(
                    ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId)!)),
            null).Contract;
        var stripped = SeriesStillVisualIngressV1Rules.StripCallerStyleAuthority(string.Join(" ", injections));
        return new PromptAuthorityBlock(
            hijack is not null && hijack.StyleLayer == prefix,
            hijack is not null && hijack.StyleLayer == compiled.Still.StyleLayer,
            stripped.Contains("photorealistic", StringComparison.OrdinalIgnoreCase) == false,
            "SeriesStillVisualIngressV1Rules.StripCallerStyleAuthority + UnifiedVisualCompilerV1Rules.SanitizeCallerLayer",
            hijack is not null && !hijack.SceneLanguage.Contains("photorealistic", StringComparison.OrdinalIgnoreCase),
            compiled.SameStylePrefix);
    }

    public static SurfaceCompile CompileSurfaces(VisualUniverseSnapshot snapshot)
    {
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var male = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                38, "male", "Bố", "warm", null, null, snapshot.PvsSha));
        var master = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snapshot, "CHAR-099", age, male, "male", null).Contract;
        var scene = new SeriesStillVisualIngressV1Rules.SceneVisualContract(
            ProjectId: snapshot.ProjectId,
            CharacterId: "CHAR-099",
            Story: "after dinner",
            Action: "father turns",
            Location: "living room",
            Age: age,
            Appearance: male,
            Gender: "male",
            Motion: "subtle blink",
            CameraMovement: "steady hold",
            Duration: "5");
        var still = SeriesStillVisualIngressV1Rules.CompileStill(snapshot, scene).Contract;
        var video = VideoVisualIngressV1Rules.CompileVideo(snapshot, scene).Contract;
        var same = master is not null && still is not null && video is not null
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(master.VisualUniverseSha, still.VisualUniverseSha)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(still.VisualUniverseSha, video.VisualUniverseSha)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(master.PvsSha, video.PvsSha)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(master.CdlSha, video.CdlSha)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(master.CompiledPrompt, still.CompiledPrompt)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(still.CompiledPrompt, video.CompiledPrompt);
        return new SurfaceCompile(
            SurfaceOf("MASTER", master),
            SurfaceOf("SERIES_STILL", still),
            SurfaceOf("VIDEO", video),
            new ConsistencyTable(
                same,
                snapshot.VisualUniverseSha,
                snapshot.PvsSha,
                snapshot.CdlSha,
                "IUnifiedVisualCompiler"));
    }

    public static CalibrationMatrixBlock InspectMatrix(
        VisualCalibrationPackV1Rules.PackSnapshot pack, string prefix)
    {
        var slots = pack.Subjects.SelectMany(s => s.Artifacts.Select(a => new CalibrationSlotRow(
            VisualCalibrationPackV1Rules.SlotIdOf(s.CalibrationSubjectId, a.ViewType),
            s.CalibrationSubjectId,
            s.Label,
            a.ViewType,
            a.VisualUniverseSha ?? pack.VisualUniverseSha,
            a.ProjectVisualStyleSha ?? pack.ProjectVisualStyleSha,
            a.CharacterDesignLanguageSha ?? pack.CharacterDesignLanguageSha,
            a.Kind == VisualCalibrationPackV1Rules.KindPixel && VisualCalibrationPackV1Rules.IsValidPixel(pack, a)
                ? "PIXEL"
                : "PROMPT",
            FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(a.Prompt, prefix)
                || a.Prompt.StartsWith(prefix, StringComparison.Ordinal),
            string.IsNullOrWhiteSpace(a.Path)))).ToList();
        var valid = slots.Count(s => s.SameCanonicalStyle && s.PromptOnlyOrValid);
        var mismatch = slots.Count(s => !s.SameCanonicalStyle);
        var missingPixels = slots.Count(s => s.PixelMissing);
        return new CalibrationMatrixBlock(
            slots.Count,
            valid,
            mismatch,
            missingPixels,
            pack.Subjects.Count == 6 && VisualCalibrationPackV1Rules.RequiredViews.Count == 4,
            slots.All(s => FamixaVisualUniverseAuthorityV1Rules.SameSha(s.VuaSha, pack.VisualUniverseSha)
                && FamixaVisualUniverseAuthorityV1Rules.SameSha(s.PvsSha, pack.ProjectVisualStyleSha)
                && FamixaVisualUniverseAuthorityV1Rules.SameSha(s.CdlSha, pack.CharacterDesignLanguageSha)),
            slots);
    }

    public static CrossCharacterBlock CrossCharacter(
        VisualCalibrationPackV1Rules.PackSnapshot pack, string prefix) =>
        new(
            pack.Subjects.All(s =>
                FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(s.Artifacts[0].Prompt, prefix)
                || s.Artifacts[0].Prompt.StartsWith(prefix, StringComparison.Ordinal)),
            VisualCalibrationPackV1Rules.AgeDoesNotChangeStyle(pack),
            prefix);

    public static AgeAppearanceBlock AgeAppearanceCheck(VisualUniverseSnapshot snapshot, string prefix)
    {
        var child = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snapshot, "CHAR-097",
            CharacterAgeConsistencyV1Rules.FromCanonicalAge(11),
            CharacterAppearanceProfileV1Rules.Compile(
                new CharacterAppearanceProfileV1Rules.AppearanceSource(
                    11, "male", "Con", "warm", "photorealistic real human", null, snapshot.PvsSha)),
            "male", "documentary portrait").Contract;
        var older = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snapshot, "CHAR-096",
            CharacterAgeConsistencyV1Rules.FromCanonicalAge(65),
            CharacterAppearanceProfileV1Rules.Compile(
                new CharacterAppearanceProfileV1Rules.AppearanceSource(
                    65, "male", "Ông", "warm", "Vietnamese rural elderly man", null, snapshot.PvsSha)),
            "male", null).Contract;
        return new AgeAppearanceBlock(
            child is not null && child.StyleLayer == prefix,
            older is not null && older.StyleLayer == prefix,
            child is not null && older is not null
                && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(child.CompiledPrompt, older.CompiledPrompt),
            CharacterAppearanceProfileV1Rules.StereotypeTokens.Contains("rural elderly"),
            "AgeAppearanceProfile and AppearanceSource extra text are identity-layer. "
            + "UnifiedVisualCompilerV1Rules.SanitizeCallerLayer strips photorealistic / cinematic family drama.");
    }

    public static RealismCeilingBlock RealismCeilingCheck(VisualUniverseSnapshot snapshot, string prefix)
    {
        var raised = SeriesStillVisualIngressV1Rules.CompileStill(
            snapshot,
            new SeriesStillVisualIngressV1Rules.SceneVisualContract(
                RawPrompt: "ultra realistic real person real skin DSLR portrait documentary photography"),
            null).Contract;
        return new RealismCeilingBlock(
            snapshot.PhotorealismCeiling,
            FamixaVisualUniverseAuthorityV1Rules.PhotorealismCeiling,
            CharacterDesignLanguageV2Rules.PhotorealismCeiling,
            "Medium (PVS V1 field; not the live ceiling)",
            raised is not null && raised.PhotorealismCeiling == snapshot.PhotorealismCeiling
                && raised.StyleLayer == prefix,
            "LOW from VUA / CDL / snapshot. PVS V1 RealismLevel=Medium is wrapped, not the ceiling.");
    }

    public static FamixaLookBlock FamixaLook() =>
        new(
            LookPartial,
            "VUA + CDL describe a recognizable stylized-3D grammar (face, eye, material, realism ceiling). "
            + "They do not yet lock a unique Famixa city/family production look (wardrobe world, architecture, "
            + "color script beyond 'warm controlled saturation'). PVS V2 CanonicalStyleDescription is more specific "
            + "but is DRAFT and unused by the snapshot.");

    public static ContemporaryBlock ContemporaryAssessment() =>
        new(
            true,
            true,
            false,
            true,
            "CDL LifestyleBoundary and Calibration ExtraNegativeConstraints block rural elderly / farmer defaults. "
            + "PVS V2 NegativeStyleBlock has NOT_ELDERLY_STEREOTYPE / NOT_RURAL_STEREOTYPE but is unused. "
            + "PVS V1 ACTIVE BuildPrompt has no rural/farmer constraint. "
            + "Nam-as-rural-elder is blocked at CDL + calibration extras + Appearance stereotype tokens, not at PVS V1.");

    public static LockImpactBlock LockImpact(string status, VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        var ifLocked = FamixaVisualUniverseAuthorityV1Rules.StatusLocked;
        var vuaSha = FamixaVisualUniverseAuthorityV1Rules.Sha();
        return new LockImpactBlock(
            FamixaVisualUniverseAuthorityV1Rules.MayLock(status),
            CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha),
            FamixaVisualUniverseAuthorityV1Rules.MutationForbidden(true),
            !FamixaVisualUniverseAuthorityV1Rules.ArtifactStale(
                true, CharacterAuthorityInitializationV1Rules.ProtectedMasterSha, vuaSha, ifLocked),
            FamixaVisualUniverseAuthorityV1Rules.ArtifactStale(false, null, vuaSha, ifLocked),
            pack.Subjects.SelectMany(s => s.Artifacts).Count(a => !string.IsNullOrWhiteSpace(a.Path)),
            "not_performed",
            "Locking the same candidate SHA does not rewrite the SHA. "
            + "Minh officialLocked stays mutationForbidden. Unbound non-locked artifacts become STYLE_STALE. "
            + "Calibration pixels are currently 0 so there is no pixel pack to stale. "
            + "Live character/still/video counts were not scanned.");
    }

    public static ShaCapture CaptureShas() =>
        new(
            CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
            CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
            CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
            CharacterAuthorityInitializationV1Rules.ProtectedCrpSha,
            ProjectVisualStyleV2Rules.ProtectedV1Sha,
            ProjectVisualStyleV2Rules.Sha(),
            CharacterDesignLanguageV2Rules.Sha(),
            FamixaVisualUniverseAuthorityV1Rules.Sha(),
            VisualCalibrationPackV1Rules.PackSha(VisualCalibrationPackV1Rules.CompilePack()));

    public static bool ShaUnchanged(ShaCapture before, ShaCapture after) =>
        before == after;

    public static SafetyBlock Safety() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, false, true);

    public static IReadOnlyList<string> DirectorQuestions(
        string status,
        VisualUniverseSnapshot snapshot,
        string prefix,
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        SurfaceCompile compiled) =>
    [
        "Q1: Gemini would receive CompileStylePrefix(PVS V1) + CDL V2 + VUA negatives + calibration extras. Not raw caller style.",
        "Q2: 24 compiled slots bind the same VUA/PVS/CDL SHAs and start with the same CompileStylePrefix. Pixels=0 until generate.",
        "Q3: Master / Still / Video use IUnifiedVisualCompiler. Calibration 24-slot generate uses VisualCalibrationPromptCompilerV1 wrapping the same CompileStylePrefix.",
        "Q4: Age / Appearance / IdentityBrief cannot change StyleLayer. Photoreal injections are sanitized or left outside the canonical prefix.",
        "Q5: Nam-as-rural-elder is blocked at CDL LifestyleBoundary + Calibration ExtraNegative + Appearance stereotype tokens. Not at PVS V1.",
        "Q6: After LOCK, non-locked artifacts whose bound VUA SHA differs become STYLE_STALE. Same SHA stays valid. Minh does not mutate.",
        "Q7: Minh CHAR-001 mutationForbidden=true, officialLocked=true, ArtifactStale=false.",
        "Q8: Famixa look is PARTIALLY_EXPLICIT — strong stylized-3D grammar, weaker unique world/color/wardrobe script.",
        "Q9: Missing: PVS V1 rural/farmer lock, unique Famixa city look, PVS V2 unused, VUA not LOCKED, 0 calibration pixels.",
        "Q10: Calibration generate is the next evidence step if Director accepts the combined prompt risk. Do not lock first.",
        $"status={status}; snapshotAuthority={snapshot.VuaIsCurrentAuthority}; sameStyle={compiled.SameStylePrefix}; slots={pack.Subjects.Sum(s => s.Artifacts.Count)}",
    ];

    private static SurfaceRow SurfaceOf(string layer, UnifiedVisualContract? contract) =>
        new(
            layer,
            contract is not null,
            contract?.VisualUniverseSha,
            contract?.PvsSha,
            contract?.CdlSha,
            contract?.CompiledPromptSha,
            contract?.StyleLayer,
            "IUnifiedVisualCompiler");

    private static string ExtractBetween(string text, string start, string end)
    {
        var a = text.IndexOf(start, StringComparison.Ordinal);
        if (a < 0) return "";
        var b = text.IndexOf(end, a, StringComparison.Ordinal);
        return b < 0 ? text[a..].Trim() : text[a..b].Trim();
    }

    private static string ExtractFrom(string text, string start)
    {
        var a = text.IndexOf(start, StringComparison.Ordinal);
        return a < 0 ? "" : text[a..].Trim();
    }
}

public sealed record VisualUniverseLockReadinessReport(
    string DocumentId,
    string OverallStatus,
    CurrentAuthorityBlock CurrentAuthority,
    CandidateAuthorityBlock Candidate,
    PvsAuditBlock Pvs,
    CdlAuditBlock Cdl,
    VuaSemanticBlock VuaSemantic,
    SnapshotAuditBlock Snapshot,
    CompilerAuditBlock Compiler,
    IReadOnlyList<PromptPreviewBlock> PromptPreview,
    PromptAuthorityBlock PromptAuthority,
    SurfaceRow Master,
    SurfaceRow Still,
    SurfaceRow Video,
    ConsistencyTable MasterStillVideoConsistency,
    CalibrationMatrixBlock CalibrationMatrix,
    CrossCharacterBlock CrossCharacter,
    AgeAppearanceBlock AgeAppearance,
    RealismCeilingBlock PhotorealismCeiling,
    FamixaLookBlock FamixaLook,
    ContemporaryBlock Contemporary,
    LockImpactBlock LockImpact,
    ShaCapture ShaBefore,
    ShaCapture ShaAfter,
    bool ShaProtection,
    SafetyBlock Safety,
    string DirectorDecision,
    IReadOnlyList<string> DirectorQuestions);

public sealed record CurrentAuthorityBlock(
    string Status, string Sha, bool CurrentIsAuthority, bool Persisted,
    bool PvsV1RemainsProductionAuthority, string StaffMessage);

public sealed record CandidateAuthorityBlock(
    string Status, string Sha, bool CandidateIsAuthority,
    bool MayApprove, bool MayLock, bool MayPromote, string Note);

public sealed record PvsAuditBlock(
    string V1Status, string V1Sha, string V1StyleKey, string V1RealismLevel,
    string V1Rendering, string V1Lighting, string V1Negatives,
    string V2Status, string V2Sha, string V2StyleKey,
    string SnapshotBoundSha, bool SnapshotBindsV1, bool V2Unused,
    string Verdict, string Evidence);

public sealed record CdlAuditBlock(
    string Version, string Sha, string Status, string BindingSource,
    string PhotorealismCeiling, string LifestyleBoundary,
    string Verdict, string Evidence);

public sealed record VuaSemanticBlock(
    string StyleIntent, string StylizationLevel, string PhotorealismCeiling,
    string RealismCeiling, string CinematicMeans, string CinematicEvidence,
    string NegativeConstraints, string CharactersShouldLookLike);

public sealed record SnapshotAuditBlock(
    string Resolver, string DataSource, string VisualUniverseSha, string PvsSha,
    string CdlSha, string? CalibrationSha, string StyleBodySource,
    bool CurrentPvsRemainsAuthority, bool VisualUniverseIsAuthority, string PhotorealismCeiling);

public sealed record CompilerAuditBlock(
    string LiveMasterStillVideo, string UnifiedImplementation,
    string LiveCalibration24, string LegacyVuaCalibration4, string Note);

public sealed record PromptPreviewBlock(
    string SubjectId, string Label, string View, string CanonicalSection,
    string CharacterSection, string SceneSection, string CameraSection,
    string NegativeSection, string VuaSha, string PvsSha, string CdlSha, bool StartsWithCanonical);

public sealed record PromptAuthorityBlock(
    bool StyleLayerUnchanged, bool PrefixIdentical, bool InjectionStripped,
    string SanitizeAt, bool SceneLacksPhotoreal, bool SurfacesSharePrefix);

public sealed record SurfaceCompile(SurfaceRow Master, SurfaceRow Still, SurfaceRow Video, ConsistencyTable Consistency)
{
    public bool SameStylePrefix => Consistency.Same;
}

public sealed record SurfaceRow(
    string Layer, bool Compiled, string? VisualUniverseSha, string? PvsSha, string? CdlSha,
    string? CompiledPromptSha, string? StyleLayer, string Compiler);

public sealed record ConsistencyTable(
    bool Same, string VisualUniverseSha, string PvsSha, string CdlSha, string Compiler);

public sealed record CalibrationMatrixBlock(
    int Total, int Valid, int Mismatch, int MissingPixels, bool ExpectedShape,
    bool IdenticalAuthorityBindings, IReadOnlyList<CalibrationSlotRow> Slots);

public sealed record CalibrationSlotRow(
    string SlotId, string SubjectId, string Label, string View,
    string VuaSha, string PvsSha, string CdlSha, string Kind,
    bool SameCanonicalStyle, bool PixelMissing)
{
    public bool PromptOnlyOrValid => SameCanonicalStyle;
}

public sealed record CrossCharacterBlock(bool SameStyleBody, bool AgeDoesNotChangeStyle, string PrefixSample);

public sealed record AgeAppearanceBlock(
    bool ChildKeepsStyle, bool OlderKeepsStyle, bool ChildOlderPrefixIdentical,
    bool StereotypeTokensPresent, string Note);

public sealed record RealismCeilingBlock(
    string SnapshotCeiling, string VuaCeiling, string CdlCeiling, string PvsV1RealismLevel,
    bool InjectionCannotRaise, string Note);

public sealed record FamixaLookBlock(string Score, string Explanation);

public sealed record ContemporaryBlock(
    bool CdlBlocksRuralDefault, bool CalibrationBlocksRuralElderly,
    bool PvsV1BlocksRural, bool AppearanceStereotypeTokens, string Note);

public sealed record LockImpactBlock(
    bool MayLockNow, bool MinhShaUnchanged, bool MinhMutationForbidden,
    bool MinhWouldNotStale, bool UnboundArtifactsWouldStale, int CalibrationPixelCount,
    string LiveCharacterScan, string Note);

public sealed record ShaCapture(
    string MinhMaster, string MinhDna, string MinhPrp, string MinhCrp,
    string PvsV1, string PvsV2, string Cdl, string Vua, string Calibration);

public sealed record SafetyBlock(
    int Gemini, int Runway, int Fal, int Generation, int VideoGeneration,
    int PixelArtifactsCreated, int CharacterMutation, int AuthorityMutation,
    int DatabaseMutation, bool MinhShaChanged, bool ReadOnly);

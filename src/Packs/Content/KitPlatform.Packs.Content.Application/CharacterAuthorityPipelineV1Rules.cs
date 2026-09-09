using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_AUTHORITY_PIPELINE_V1 — one Global Character System.
/// Profile → Master review/lock → auto DNA → auto PRP → CRP set review/lock → CHARACTER_READY.
/// Does not auto-approve, auto-lock, generate production stills, or open video.
/// </summary>
public static class CharacterAuthorityPipelineV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_AUTHORITY_PIPELINE_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_AUTHORITY_PIPELINE_V1_REGRESSION";
    public const string Version = "CHARACTER_AUTHORITY_PIPELINE_V1";
    public const string VisualStyleDocument = KitVideoVisualSystemRules.FamixaDocumentId;
    public const string VisualStyleVersion = "V1";
    public const string VisualLanguage = "3D stylized realism, family-friendly cinematic illustration";
    public const string SeedNamMasterPng = "CHAR-002-nam-master.png";

    public const string ProfileCreated = "PROFILE_CREATED";
    public const string MasterGenerating = "MASTER_GENERATING";
    public const string MasterPendingReview = "MASTER_PENDING_REVIEW";
    public const string MasterApproved = "MASTER_APPROVED";
    public const string MasterLocked = "MASTER_LOCKED";
    public const string DnaReady = "DNA_READY";
    public const string PrpReady = "PRP_READY";
    public const string CrpGenerating = "CRP_GENERATING";
    public const string CrpPendingReview = "CRP_PENDING_REVIEW";
    public const string CrpApproved = "CRP_APPROVED";
    public const string CrpLocked = "CRP_LOCKED";
    public const string CharacterReady = "CHARACTER_READY";

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoRetry() => false;
    public static bool AutoRunFullPipeline() => false;
    public static bool OpensFirstRealProduction() => false;
    public static bool GeneratesShot() => false;
    public static bool GeneratesVideo() => false;
    public static bool MutatesMinh() => false;
    public static bool OverwritesLocked() => false;

    public static string StaffCreate => "Tạo bộ nhân vật";
    public static string StaffApproveMaster => "Duyệt Master";
    public static string StaffApproveCrp => "Duyệt bộ ảnh";
    public static string StaffReject => "Không đạt";
    public static string StaffDetails => "Xem thông tin hệ thống";
    public static string StaffReady => "Nhân vật đã sẵn sàng";
    public static string StaffMasterReview => "Chờ duyệt nhân vật";
    public static string StaffBuilding => "Đang tạo bộ nhân vật";
    public static string StaffCrpBuilding => "Bộ ảnh chuẩn đang được tạo";
    public static string StaffCrpReview => "Cần duyệt bộ ảnh chuẩn";
    public static string StaffConfirm => "Cần xác nhận trước khi tạo.";
    public static string StaffProvider => "Vui lòng chọn nhà cung cấp.";
    public static string StaffCapability => "Nhà cung cấp này chưa hỗ trợ.";
    public static string StaffDuplicate => "Bộ nhận diện này đã được tạo trước đó.";
    public static string StaffHistorical => "Không dùng ảnh production cũ làm bộ nhận diện.";
    public static string StaffPhotoreal => "Ảnh người thật không trở thành Master tự động.";
    public static string StaffWrongCharacter => "Sai nhân vật. Không dùng được.";
    public static string StaffProfile => "Hồ sơ nhân vật chưa sẵn sàng.";
    public static string StaffMaster => "Master chưa khóa.";
    public static string StaffDna => "DNA chưa sẵn sàng.";
    public static string StaffPrp => "PRP chưa sẵn sàng.";
    public static string StaffCrpLock => "Cần duyệt bộ ảnh trước khi khóa.";
    public static string StaffProduction => "Nhân vật chưa sẵn sàng cho production.";

    public sealed record Snapshot(
        bool CharacterExists,
        bool ProfileReady,
        bool WorkspaceCreated,
        bool AuthorityLocked,
        string MasterStatus,
        string DnaStatus,
        string PrpStatus,
        string? CrpStatus,
        int Coverage,
        bool ConsistencyPass,
        bool CrpApproved,
        bool CrpLocked,
        bool CanUse,
        bool PhotorealisticAsset = false,
        bool HistoricalStill = false,
        bool WrongCharacter = false,
        string? DirectorProvider = null,
        bool Confirm = false,
        bool Duplicate = false);

    public sealed record Gate(
        string PipelineState,
        string? Code,
        string StaffMessage,
        string NextAction,
        bool WorkspaceCreated,
        bool MasterReviewPending,
        bool MayGenerateMaster,
        bool MayLockMaster,
        bool DnaAutoBuild,
        bool PrpAutoBuild,
        bool MayGenerateCrpSet,
        bool MayApproveCrp,
        bool MayLockCrp,
        bool CharacterReady,
        bool ProductionReady);

    public static bool ProfileReady(string? name, string? role, string? visual, string? lifecycle) =>
        CharacterAuthorityInitializationV1Rules.IdentityReady(name, role, visual, lifecycle);

    public static bool IsAuthorityLocked(string? master, string? dna, string? prp) =>
        CharacterAuthorityInitializationV1Rules.IsAuthorityLocked(master, dna, prp);

    public static bool RejectHistoricalStill(string? assetId, string? path = null) =>
        CharacterAuthorityInitializationV1Rules.RejectHistoricalStill(assetId, path);

    public static bool RejectPhotorealisticExternal(string? path, string? source = null)
    {
        var hay = $"{path} {source}";
        return hay.Contains(SeedNamMasterPng, StringComparison.OrdinalIgnoreCase)
               || hay.Contains("photoreal", StringComparison.OrdinalIgnoreCase)
               || hay.Contains("real_person", StringComparison.OrdinalIgnoreCase)
               || hay.Contains("uploaded_external", StringComparison.OrdinalIgnoreCase)
               || RejectHistoricalStill(null, path);
    }

    public static bool RejectWrongCharacter(string? requested, string? owned) =>
        !string.IsNullOrWhiteSpace(requested)
        && !string.IsNullOrWhiteSpace(owned)
        && !string.Equals(requested.Trim(), owned.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string SetCapability(string? provider) =>
        CharacterAuthorityInitializationV1Rules.SetCapability(provider);

    public static bool DerivedReady(string? status) =>
        CharacterAuthorityInitializationV1Rules.IsLocked(status)
        || string.Equals(status, "VALID", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "READY", StringComparison.OrdinalIgnoreCase);

    public static bool CharacterAuthorityReady(Snapshot snap) =>
        snap.CharacterExists
        && snap.ProfileReady
        && CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus)
        && DerivedReady(snap.DnaStatus)
        && DerivedReady(snap.PrpStatus)
        && snap.CrpLocked
        && snap.CanUse
        && snap.Coverage >= 4
        && snap.ConsistencyPass;

    public static bool ProductionAllowed(string pipelineState) =>
        pipelineState == CharacterReady && !OpensFirstRealProduction() && !GeneratesShot();

    public static bool SameAuthorityForAllShots() => true;
    public static bool CreatesAuthorityPerShot() => false;

    public static string ResolveState(Snapshot snap)
    {
        if (CharacterAuthorityReady(snap))
            return CharacterReady;
        if (snap.CrpLocked) return CrpLocked;
        if (snap.CrpApproved) return CrpApproved;
        if (CharacterReferenceRegenerationV1Rules.IsRejected(snap.CrpStatus))
            return "CRP_REJECTED";
        if (CharacterReferenceRegenerationV1Rules.IsGenerating(snap.CrpStatus))
            return CrpGenerating;
        if (snap.Coverage >= 4 && snap.ConsistencyPass) return CrpPendingReview;
        if (snap.Coverage is > 0 and < 4) return CrpGenerating;
        if (DerivedReady(snap.PrpStatus) && CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus))
            return PrpReady;
        if (DerivedReady(snap.DnaStatus) && CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus))
            return DnaReady;
        if (CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus)) return MasterLocked;
        if (CharacterAuthorityInitializationV1Rules.IsApproved(snap.MasterStatus)) return MasterApproved;
        if (CharacterAuthorityInitializationV1Rules.IsReadyForDirector(snap.MasterStatus)) return MasterPendingReview;
        return snap.ProfileReady ? ProfileCreated : ProfileCreated;
    }

    public static string StaffOf(string state) => state switch
    {
        CharacterReady => "✓ " + StaffReady,
        "CRP_REJECTED" => CharacterReferenceRegenerationV1Rules.StaffRejected,
        CrpPendingReview or CrpApproved => StaffCrpReview,
        CrpGenerating => StaffCrpBuilding,
        MasterPendingReview or MasterApproved => StaffMasterReview,
        PrpReady or DnaReady or MasterLocked => "Sẵn sàng tạo bộ ảnh tham chiếu",
        _ => StaffBuilding,
    };

    public static Gate Evaluate(Snapshot snap, string action = "GET")
    {
        var state = ResolveState(snap);
        if (snap.HistoricalStill)
            return Block(state, "HISTORICAL_STILL", StaffHistorical, snap);
        if (snap.WrongCharacter)
            return Block(state, "WRONG_CHARACTER", StaffWrongCharacter, snap);
        if (snap.PhotorealisticAsset && action is "EXECUTE_MASTER" or "LOCK_MASTER")
            return Block(state, "INVALID_REFERENCE", StaffPhotoreal, snap);
        if (!snap.CharacterExists)
            return Block(state, "INVALID_CHARACTER", CharacterAuthorityInitializationV1Rules.StaffCharacter, snap);
        if (!snap.ProfileReady)
            return Block(state, "PROFILE_NOT_READY", StaffProfile, snap, workspace: false);

        if (action == "EXECUTE_MASTER")
        {
            if (snap.AuthorityLocked)
                return Block(state, "AUTHORITY_CONFLICT", CharacterAuthorityInitializationV1Rules.StaffLocked, snap);
            if (string.IsNullOrWhiteSpace(snap.DirectorProvider))
                return new Gate(state, "PROVIDER_MISSING", StaffProvider, StaffCreate, snap.WorkspaceCreated,
                    false, false, false, false, false, false, false, false, false, false);
            if (CharacterAuthorityInitializationV1Rules.SetCapability(snap.DirectorProvider) != "SUPPORTED")
                return Block(state, "PROVIDER_CAPABILITY_UNSUPPORTED", StaffCapability, snap, workspace: snap.WorkspaceCreated);
            if (snap.Duplicate)
                return Block(state, "BLOCK_DUPLICATE", StaffDuplicate, snap, workspace: true);
            if (!snap.Confirm)
                return Block(state, "CONFIRMATION_REQUIRED", StaffConfirm, snap, workspace: true);
            return new Gate(MasterGenerating, null, StaffBuilding, StaffCreate, true,
                false, true, false, false, false, false, false, false, false, false);
        }

        if (action == "LOCK_MASTER")
        {
            if (!CharacterAuthorityInitializationV1Rules.IsApproved(snap.MasterStatus))
                return Block(state, "MASTER_NOT_APPROVED", StaffMaster, snap, workspace: true);
            return new Gate(MasterLocked, null, StaffOf(MasterLocked), StaffApproveMaster, true,
                false, false, true, true, true, false, false, false, false, false);
        }

        if (action == "GENERATE_CRP")
        {
            if (!CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus))
                return Block(state, "MASTER_NOT_LOCKED", StaffMaster, snap, workspace: true);
            if (!DerivedReady(snap.DnaStatus))
                return Block(state, "DNA_NOT_READY", StaffDna, snap, workspace: true);
            if (!DerivedReady(snap.PrpStatus))
                return Block(state, "PRP_NOT_READY", StaffPrp, snap, workspace: true);
            if (snap.CrpLocked)
                return Block(state, "CRP_ALREADY_LOCKED", CharacterReferenceAutoGenerationV2Rules.StaffComplete, snap, workspace: true);
            if (CharacterReferenceRegenerationV1Rules.IsRejected(snap.CrpStatus))
                return Block(state, "CRP_NOT_REJECTED", CharacterReferenceRegenerationV1Rules.StaffNotRejected, snap, workspace: true);
            return new Gate(PrpReady, null, StaffOf(PrpReady), CharacterReferenceAutoGenerationV2Rules.StaffCreate, true,
                false, false, false, false, false, true, false, false, false, false);
        }

        if (action == "LOCK_CRP")
        {
            if (!snap.CrpApproved)
                return Block(state, "CRP_NOT_APPROVED", StaffCrpLock, snap, workspace: true);
            return new Gate(CrpLocked, null, StaffOf(CrpLocked), StaffApproveCrp, true,
                false, false, false, false, false, false, false, true, false, false);
        }

        var ready = CharacterAuthorityReady(snap);
        return new Gate(
            state,
            null,
            StaffOf(state),
            ready ? StaffReady : PrimaryCta(state),
            snap.WorkspaceCreated || snap.ProfileReady,
            state == MasterPendingReview,
            state == ProfileCreated && !snap.AuthorityLocked,
            state == MasterApproved,
            CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus),
            DerivedReady(snap.DnaStatus) && CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus),
            state == PrpReady,
            state == CrpPendingReview,
            state == CrpApproved,
            ready,
            ProductionAllowed(state));
    }

    public static bool MayCallProvider(Gate gate, string action) =>
        action == "EXECUTE_MASTER" && gate.MayGenerateMaster && !AutoSelectProvider() && !AutoApprove();

    public static string GlobalStyleBrief() =>
        $"{VisualLanguage}. Consistent FAMIXA facial language, proportions, materials, and lighting. Not photorealistic. Not a real photograph.";

    private static string PrimaryCta(string state) => state switch
    {
        MasterPendingReview or MasterApproved => StaffApproveMaster,
        "CRP_REJECTED" => CharacterReferenceRegenerationV1Rules.StaffRegenerate,
        CrpPendingReview or CrpApproved => StaffApproveCrp,
        PrpReady => CharacterReferenceAutoGenerationV2Rules.StaffCreate,
        _ => StaffCreate,
    };

    private static Gate Block(string state, string code, string staff, Snapshot snap, bool workspace = false) =>
        new(state, code, staff, staff, workspace || snap.WorkspaceCreated,
            false, false, false, false, false, false, false, false, false, false);
}

public static class CharacterAuthorityPipelineV1Application
{
    public sealed record Outcome(
        CharacterAuthorityPipelineV1Rules.Gate Gate,
        string PipelineState,
        bool ProviderCalled,
        int ProviderCalls,
        int ReferenceCount,
        bool DnaBuilt,
        bool PrpBuilt,
        bool Approved,
        bool Locked,
        bool CanUse,
        bool CharacterReady);

    public static Outcome AfterMasterLock(CharacterAuthorityPipelineV1Rules.Snapshot snap)
    {
        var gate = CharacterAuthorityPipelineV1Rules.Evaluate(snap, "LOCK_MASTER");
        if (gate.Code is not null)
            return new Outcome(gate, gate.PipelineState, false, 0, 0, false, false, false, false, false, false);
        return new Outcome(gate, CharacterAuthorityPipelineV1Rules.PrpReady, false, 0, 0, true, true, false, true, false, false);
    }

    public static async Task<Outcome> GenerateCrpSetAsync(
        CharacterAuthorityPipelineV1Rules.Snapshot snap,
        ICharacterReferenceGenerationProvider provider,
        CharacterReferenceSetRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = CharacterAuthorityPipelineV1Rules.Evaluate(snap, "GENERATE_CRP");
        if (!gate.MayGenerateCrpSet)
            return new Outcome(gate, gate.PipelineState, false, 0, 0, true, true, false, false, false, false);

        var v2 = new CharacterReferenceAutoGenerationV2Rules.GateInput(
            true, true, true, true, true, true, true, true, true, true, true, true, true,
            snap.CrpLocked && snap.CanUse, "GEMINI", true, null, true, snap.Duplicate, snap.HistoricalStill);
        var outcome = await CharacterReferenceAutoGenerationV2Application.ExecuteOnceAsync(v2, provider, request, cancellationToken);
        return new Outcome(
            gate, outcome.Coverage == 4 ? CharacterAuthorityPipelineV1Rules.CrpPendingReview : CharacterAuthorityPipelineV1Rules.CrpGenerating,
            outcome.ProviderCalled, outcome.ProviderCalls, outcome.ReferenceCount,
            true, true, false, false, false, false);
    }
}

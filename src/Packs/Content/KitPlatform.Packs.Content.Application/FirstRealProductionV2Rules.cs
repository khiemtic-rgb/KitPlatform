using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_FIRST_REAL_PRODUCTION_V2 — one SHOT-001 still, Director-selected Gemini, CRP LOCKED 4/4.
/// Wraps V1 gates. Does not auto-select, auto-retry, auto-approve, or open video.
/// </summary>
public static class FirstRealProductionV2Rules
{
    public const string DocumentId = "FAMIXA_FIRST_REAL_PRODUCTION_V2";
    public const string SuiteId = "FAMIXA_FIRST_REAL_PRODUCTION_V2_REGRESSION";
    public const int AllowedCount = 1;
    public const string PendingReview = "READY_FOR_DIRECTOR";

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoRetry() => false;
    public static bool AutoFix() => false;
    public static bool AllowsRunway() => false;
    public static bool AllowsVeo() => false;
    public static bool AllowsBatch() => false;
    public static bool AllowsVideo() => false;
    public static bool OpensShot002() => false;

    public static bool IsHistoricalStill(string? executionId) =>
        CharacterReferenceCompletionRules.IsHistoricalStill(executionId);

    public static bool IsShot001(string? shotCode)
    {
        if (string.IsNullOrWhiteSpace(shotCode)) return false;
        var n = shotCode.Trim().ToUpperInvariant();
        var idx = n.LastIndexOf("SHOT-", StringComparison.Ordinal);
        if (idx < 0) return false;
        var digits = new string(n[(idx + 5)..].TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var num) && num == 1;
    }

    public static bool CrpReadyForProduction(string? status, bool canUse, int coverage)
        => string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)
           && canUse
           && coverage >= 4;

    public static bool ShaMatch(string? expected, string? live)
        => CharacterIdentityGovernanceRules.ShaExists(expected)
           && CharacterIdentityGovernanceRules.SameSha(expected, live);

    public sealed record GateInput(
        bool ShotExists,
        bool ShotReady,
        string? ShotCode,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool MasterShaMatch,
        bool DnaShaMatch,
        bool PrpShaMatch,
        bool CrpUsable,
        string? CrpStatus,
        int CrpCoverage,
        bool ShotContractValid,
        bool IntentValid,
        string? DirectorProvider,
        bool CapabilityReady,
        string? CapabilityCode,
        bool Confirm,
        bool DuplicateFingerprint,
        string? ShotCharacterId,
        string? RequestedCharacterId,
        int RequestedCount = 1);

    public static FirstRealProductionRules.Gate Evaluate(GateInput input)
    {
        if (!input.ShotExists)
            return Block("SHOT_NOT_FOUND", "Không tìm thấy Shot 01.", input);
        if (!IsShot001(input.ShotCode) || !input.ShotReady)
            return Block("SHOT_NOT_READY", "Shot 01 chưa sẵn sàng để tạo hình.", input);
        if (!input.MasterShaMatch)
            return Block("MASTER_MISMATCH", "Hồ sơ nhân vật không khớp. Không thể tạo hình production.", input);
        if (!input.DnaShaMatch)
            return Block("DNA_MISMATCH", "Quy tắc nhận diện không khớp. Không thể tạo hình production.", input);
        if (!input.PrpShaMatch)
            return Block("PRP_MISMATCH", "Ảnh tham chiếu sản xuất không khớp. Không thể tạo hình production.", input);
        if (!CrpReadyForProduction(input.CrpStatus, input.CrpUsable, input.CrpCoverage))
            return Block("CRP_NOT_READY", FirstRealProductionRules.StaffCrpBlock, input);
        if (input.RequestedCount != AllowedCount)
            return Block("GENERATION_COUNT_INVALID", "Chỉ được tạo đúng 01 hình.", input);

        return FirstRealProductionRules.Evaluate(new FirstRealProductionRules.GateInput(
            input.MasterLocked,
            input.DnaLocked,
            input.PrpLocked,
            CrpReadyForProduction(input.CrpStatus, input.CrpUsable, input.CrpCoverage),
            input.CrpStatus,
            input.ShotContractValid,
            input.IntentValid,
            input.DirectorProvider,
            input.CapabilityReady,
            input.CapabilityCode,
            input.Confirm,
            input.DuplicateFingerprint,
            input.ShotCharacterId,
            input.RequestedCharacterId));
    }

    public static bool MayCallProvider(FirstRealProductionRules.Gate gate) =>
        FirstRealProductionRules.MayCallProvider(gate) && !AutoRetry() && !AutoSelectProvider();

    public static bool HasRequiredCrpRefs(IEnumerable<ImageGenerationReferenceBytes> refs)
    {
        var types = refs
            .Select(r => CharacterReferencePackRules.NormalizeRefType(r.Role))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CharacterReferencePackRules.RequiredTypes.All(t => types.Contains(t));
    }

    public static bool AllowedCrpPath(string characterId, string? path, string? assetId, string? metadataCharacterId, string? type = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (CharacterReferencePackRules.TouchesGolden(path)) return false;
        if (CharacterReferenceCompletionRules.IsHistoricalStill(assetId)
            || path.Contains(CharacterReferenceCompletionRules.HistoricalStillId, StringComparison.OrdinalIgnoreCase))
            return false;
        var view = CharacterReferencePackRules.NormalizeRefType(type);
        if (view == "FULL_BODY"
            && path.Contains("MASTER", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("FULL_BODY", StringComparison.OrdinalIgnoreCase))
            return false;
        var id = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId);
        if (!string.IsNullOrWhiteSpace(metadataCharacterId)
            && !FirstRealProductionRules.SameCharacter(id, metadataCharacterId))
            return false;
        if (string.IsNullOrWhiteSpace(metadataCharacterId)
            && !path.Contains(id, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static FirstRealProductionRules.Gate Block(string code, string staff, GateInput input) =>
        new("BLOCKED", code, staff,
            input.MasterLocked && input.DnaLocked && input.PrpLocked && input.MasterShaMatch && input.DnaShaMatch && input.PrpShaMatch,
            CrpReadyForProduction(input.CrpStatus, input.CrpUsable, input.CrpCoverage),
            input.IntentValid,
            FirstRealProductionRules.ProviderSelected(input.DirectorProvider),
            input.CapabilityReady,
            false,
            !input.DuplicateFingerprint,
            false);
}

/// <summary>
/// Application → IImageGenerationProvider → mock. No HTTP. No Gemini SDK.
/// </summary>
public static class FirstRealProductionV2Application
{
    public sealed record Outcome(
        FirstRealProductionRules.Gate Gate,
        bool GeminiCalled,
        bool Generation,
        bool ArtifactCreated,
        bool ArtifactApproved,
        int ProviderCalls,
        string? ArtifactId);

    public static async Task<Outcome> ExecuteOnceAsync(
        FirstRealProductionV2Rules.GateInput input,
        IImageGenerationProvider provider,
        ImageGenerationExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = FirstRealProductionV2Rules.Evaluate(input);
        if (!FirstRealProductionV2Rules.MayCallProvider(gate))
            return new Outcome(gate, false, false, false, false, 0, null);

        var result = await provider.GenerateAsync(request, cancellationToken);
        var calls = provider is MockCharacterReferenceProvider mock ? mock.CallCount : 1;
        if (!result.Accepted || !result.Succeeded || result.Bytes is null || result.Bytes.Length == 0)
        {
            return new Outcome(
                gate with
                {
                    Status = "FAILED",
                    Code = "GENERATION_FAILED",
                    StaffMessage = "Tạo hình không thành công. Không tự gọi lại.",
                    GenerationAllowed = false,
                    MayCallProvider = false,
                },
                true, true, false, false, calls, null);
        }

        return new Outcome(gate, true, true, true, false, calls, "artifact-1");
    }
}

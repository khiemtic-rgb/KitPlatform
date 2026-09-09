using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_REFERENCE_APPROVAL_LOCK_V1 — Director APPROVE then LOCK.
/// Reuses Character Reference Pack validation. Engine does not approve, lock, or generate.
/// </summary>
public static class CharacterReferenceApprovalLockRules
{
    public const string DocumentId = "FAMIXA_CHARACTER_REFERENCE_APPROVAL_LOCK_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_REFERENCE_APPROVAL_LOCK_V1_REGRESSION";
    public const string ApprovedStatus = "DIRECTOR_APPROVED";
    public const string LockedStatus = "LOCKED";

    public const string PackNotReady = "REFERENCE_PACK_NOT_READY";
    public const string PackIncomplete = "REFERENCE_PACK_INCOMPLETE";
    public const string MasterMismatch = "REFERENCE_MASTER_MISMATCH";
    public const string DnaMismatch = "REFERENCE_DNA_MISMATCH";
    public const string PrpMismatch = "REFERENCE_PRP_MISMATCH";
    public const string IdentityMismatch = "REFERENCE_IDENTITY_MISMATCH";
    public const string AssetInvalid = "REFERENCE_ASSET_INVALID";
    public const string AlreadyLocked = "REFERENCE_ALREADY_LOCKED";
    public const string InvalidState = "INVALID_REFERENCE_PACK_STATE";
    public const string DirectorRequired = "DIRECTOR_APPROVAL_REQUIRED";

    public static readonly string[] MachineActors = ["", "anonymous", "ai", "engine", "auto", "system"];

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoReplace() => false;
    public static bool AutoRegenerate() => false;
    public static bool AutoSelect() => false;
    public static bool MutatesMaster() => false;
    public static bool MutatesDna() => false;
    public static bool MutatesPrp() => false;
    public static bool OpensFirstRealProduction() => false;
    public static bool CreatesPixels(string? action) => CharacterReferencePackRules.CreatesPixels(action);
    public static bool AllowsProvider(string? provider) => false;

    public static bool IsDraft(string? status) =>
        (status ?? "DRAFT").Equals("DRAFT", StringComparison.OrdinalIgnoreCase)
        || (status ?? "").Equals("REJECTED", StringComparison.OrdinalIgnoreCase);

    public static bool IsDirectorApproved(string? status) =>
        CharacterReferencePackRules.IsDirectorApproved(status);

    public static bool IsLocked(string? status) =>
        string.Equals(status, LockedStatus, StringComparison.OrdinalIgnoreCase);

    public static bool MayApproveFrom(string? status) =>
        !IsLocked(status) && !IsDirectorApproved(status)
        && (status is null or "" or "DRAFT" or "REVIEW" or "VALIDATED" or "REJECTED" or "READY_FOR_DIRECTOR");

    public static bool ShouldWriteApprove(string? status) =>
        MayApproveFrom(status);

    public static bool ShouldWriteLock(string? status) =>
        !IsLocked(status);

    public static bool MayLockFrom(string? status) =>
        IsDirectorApproved(status) && !IsLocked(status);

    public static void EnsureDirector(string? actor)
    {
        var a = (actor ?? "").Trim();
        if (a.Length == 0 || MachineActors.Contains(a, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{DirectorRequired}: chỉ Director được duyệt và khóa bộ ảnh chuẩn.");
    }

    public static void EnsureMutable(string action, string? status)
    {
        if (IsLocked(status))
            throw new InvalidOperationException($"{AlreadyLocked}: V1 không {action}. Tạo REFERENCE PACK V2.");
        if (IsDirectorApproved(status) && action is "EDIT" or "REGENERATE" or "DELETE" or "UPLOAD")
            throw new InvalidOperationException($"{InvalidState}: bộ ảnh đã duyệt không quay về DRAFT. Khóa hoặc tạo V2.");
    }

    public static void EnsureCanApprove(string? status)
    {
        if (IsLocked(status))
            throw new InvalidOperationException($"{AlreadyLocked}: bộ ảnh chuẩn đã khóa.");
        if (IsDirectorApproved(status)) return;
        if (!MayApproveFrom(status))
            throw new InvalidOperationException($"{InvalidState}: không duyệt từ {status}.");
    }

    public static void EnsureCanLock(string? status)
    {
        if (IsLocked(status)) return;
        if (!IsDirectorApproved(status))
            throw new InvalidOperationException($"{DirectorRequired}: Director chưa duyệt. Không khóa từ DRAFT.");
    }

    public static string? ApproveBlock(
        string? status,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> authority,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> coverage,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> artifacts,
        IReadOnlyList<CharacterReferencePackRules.IdentityFinding> identity,
        string? packCharacterId,
        IReadOnlyList<CharacterReferencePackRules.RefEntry>? entries = null)
    {
        if (IsLocked(status)) return AlreadyLocked;
        if (IsDirectorApproved(status)) return null;
        if (!MayApproveFrom(status)) return InvalidState;
        var mapped = MapGate(authority, coverage, artifacts, identity, packCharacterId, entries);
        if (mapped is not null) return mapped;
        if (!CharacterReferencePackRules.ReadyForDirector(authority, coverage, artifacts, identity))
            return PackNotReady;
        return null;
    }

    public static string? LockBlock(
        string? status,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> authority,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> coverage,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> artifacts,
        IReadOnlyList<CharacterReferencePackRules.IdentityFinding> identity,
        string? packCharacterId,
        IReadOnlyList<CharacterReferencePackRules.RefEntry>? entries = null)
    {
        if (IsLocked(status)) return null;
        if (!IsDirectorApproved(status)) return DirectorRequired;
        return MapGate(authority, coverage, artifacts, identity, packCharacterId, entries)
            ?? (CharacterReferencePackRules.ReadyForDirector(authority, coverage, artifacts, identity)
                ? null
                : PackNotReady);
    }

    public static void EnsureApproveReady(
        string? status,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> authority,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> coverage,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> artifacts,
        IReadOnlyList<CharacterReferencePackRules.IdentityFinding> identity,
        string? packCharacterId,
        IReadOnlyList<CharacterReferencePackRules.RefEntry>? entries = null)
    {
        EnsureCanApprove(status);
        var code = ApproveBlock(status, authority, coverage, artifacts, identity, packCharacterId, entries);
        if (code is not null)
            throw new InvalidOperationException($"{code}: bộ ảnh chuẩn chưa đủ điều kiện duyệt.");
    }

    public static void EnsureLockReady(
        string? status,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> authority,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> coverage,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> artifacts,
        IReadOnlyList<CharacterReferencePackRules.IdentityFinding> identity,
        string? packCharacterId,
        IReadOnlyList<CharacterReferencePackRules.RefEntry>? entries = null)
    {
        EnsureCanLock(status);
        var code = LockBlock(status, authority, coverage, artifacts, identity, packCharacterId, entries);
        if (code is not null)
            throw new InvalidOperationException($"{code}: bộ ảnh chuẩn chưa đủ điều kiện khóa.");
    }

    public static bool AssetBelongsToCharacter(JsonElement metadata, string? characterId, string? path = null)
    {
        var owned = ReadMeta(metadata, "characterId");
        if (owned.Length > 0)
            return CharacterReferencePackRules.SameTenant(owned, characterId);
        var id = (characterId ?? "").Trim();
        return id.Length > 0
            && !string.IsNullOrWhiteSpace(path)
            && path.Contains(CharacterReferencePackRules.NormalizeCharacterId(id), StringComparison.OrdinalIgnoreCase);
    }

    public static bool AssetUsable(string? path, JsonElement metadata)
    {
        if (CharacterReferencePackRules.TouchesGolden(path)) return false;
        var image = ReadMeta(metadata, "imageType");
        if (image.Equals("PRODUCTION_STILL", StringComparison.OrdinalIgnoreCase)) return false;
        if (image.Equals("HISTORICAL_STILL", StringComparison.OrdinalIgnoreCase)) return false;
        var assetId = ReadMeta(metadata, "assetId");
        if (CharacterReferenceCompletionRules.IsHistoricalStill(assetId)) return false;
        return true;
    }

    public static bool AuthorityUnchanged(string? before, string? after) =>
        CharacterReferencePackRules.SameSha(before, after);

    public static string NextVersion(string? current) => CharacterReferencePackRules.NextVersion(current);

    public static string StaffDraft => "Bộ ảnh đang hoàn thiện";
    public static string StaffApproved => "Đã duyệt — chờ khóa";
    public static string StaffLocked => "✓ Sẵn sàng production";
    public static string StaffComplete => "Bộ ảnh chuẩn đã hoàn tất.";
    public static string StaffProductionReady => "Sẵn sàng sử dụng trong production.";
    public static string StaffNotReady => "Bộ ảnh chưa sẵn sàng để duyệt";
    public static string StaffAuthority => "Character Reference Authority";
    public static string StaffApprove => "Duyệt bộ ảnh";
    public static string StaffLock => "Khóa bộ ảnh chuẩn";
    public static string NeedsDirectorReview => "NEEDS_DIRECTOR_REVIEW";

    public static string ConsistencyVerdict(string? verdict) =>
        string.Equals(verdict, "NEEDS_REVIEW", StringComparison.OrdinalIgnoreCase)
        || string.Equals(verdict, "UNKNOWN", StringComparison.OrdinalIgnoreCase)
            ? NeedsDirectorReview
            : (verdict ?? "").ToUpperInvariant();

    private static string? MapGate(
        IReadOnlyList<CharacterReferencePackRules.CheckItem> authority,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> coverage,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> artifacts,
        IReadOnlyList<CharacterReferencePackRules.IdentityFinding> identity,
        string? packCharacterId,
        IReadOnlyList<CharacterReferencePackRules.RefEntry>? entries)
    {
        if (Fail(authority, "master_sha") || Fail(authority, "master_exists") || Fail(authority, "master_locked"))
            return MasterMismatch;
        if (Fail(authority, "dna_sha") || Fail(authority, "dna_exists") || Fail(authority, "dna_locked"))
            return DnaMismatch;
        if (Fail(authority, "prp_sha") || Fail(authority, "prp_exists") || Fail(authority, "prp_locked"))
            return PrpMismatch;
        if (Fail(authority, "ownership")) return IdentityMismatch;
        if (!CharacterReferencePackRules.GatePass(coverage)) return PackIncomplete;
        if (!CharacterReferencePackRules.GatePass(artifacts)) return AssetInvalid;
        if (CharacterReferencePackRules.IdentityBlocks(identity)) return IdentityMismatch;
        if (entries is not null)
        {
            foreach (var entry in entries.Where(e => !string.IsNullOrWhiteSpace(e.ArtifactPath)))
            {
                if (!AssetBelongsToCharacter(entry.Metadata, packCharacterId, entry.ArtifactPath)) return IdentityMismatch;
                if (!AssetUsable(entry.ArtifactPath, entry.Metadata)) return AssetInvalid;
            }
        }
        return null;
    }

    private static bool Fail(IReadOnlyList<CharacterReferencePackRules.CheckItem> items, string code) =>
        items.Any(x => x.Code == code && !x.Pass);

    private static string ReadMeta(JsonElement el, string key)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(key, out var n)) return "";
        return n.ValueKind == JsonValueKind.String ? n.GetString()?.Trim() ?? "" : "";
    }
}

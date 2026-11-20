namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Shared visual demand, brand style, and library records.
/// Brands are data. The engine does not treat Famixa, Novixa, or Trade as a fixed set.
/// </summary>
public sealed record VisualBrandStyle(
    string Code,
    string DisplayName,
    string Medium,
    string Lighting,
    string Material,
    int LineWeight,
    string Motif,
    uint Background,
    uint Ink,
    uint Accent,
    uint Secondary,
    uint Skin,
    uint Prop,
    string World,
    string[] AllowedSettings,
    string[] ForbiddenMotifs);

public sealed record CharacterIdentity(
    string Id,
    string BrandCode,
    string Name,
    string FaceShape,
    string Hair,
    uint HairColor,
    uint OutfitColor,
    string Mark,
    int HeadScale);

public sealed record VisualAsset(
    string Id,
    string Kind,
    string BrandScope,
    string StyleId,
    string SubjectKey,
    string Action,
    string Setting,
    string Pose,
    string Expression,
    string Camera,
    string Medium,
    string? CharacterId,
    string Prop,
    string[] Tokens,
    string Approval,
    string Sha256);

public sealed record ShotVisualDemand(
    string ShotId,
    string FilmId,
    string BrandCode,
    string SubjectKey,
    string Action = "",
    string Setting = "",
    string Narration = "",
    string Pose = "stand",
    string Expression = "calm",
    string Camera = "medium",
    string Medium = "auto",
    string? CharacterId = null,
    string Prop = "",
    string[]? Tokens = null,
    string[]? RequiredLabels = null,
    bool ContinuityLock = false);

public sealed record ShotBinding(
    string FilmId,
    string ShotId,
    string AssetId,
    string Decision,
    bool Approved,
    string BrandCodeAtBind,
    string AssetSha256);

public sealed record UsageMark(string FilmId, string ShotId, string AssetId, string Signature);

public sealed record CandidateScore(string AssetId, int Score, string ReasonCode, bool Eligible, int Uses);

public sealed record TextPlan(string Spoken, string[] Overlay, string[] Rejected);

public sealed record GenerationContract(
    string Medium,
    string ImagePrompt,
    string Spoken,
    string[] ControlledOverlay,
    string[] RejectedLabels,
    string? CharacterId,
    string StyleId,
    string RendererId,
    string ProviderId,
    bool ProviderCallAuthorized,
    string BlockReason);

public sealed record VisualResolveOptions(
    bool AllowGenerate,
    bool RenderLocal,
    bool AllowPaidProvider,
    bool Regenerate,
    int ReuseThreshold)
{
    public static VisualResolveOptions Studio { get; } = new(true, true, false, false, 72);

    /// <summary>Look up only. Does not render and does not authorize a provider.</summary>
    public static VisualResolveOptions Search { get; } = new(false, false, false, false, 72);

    public VisualResolveOptions AsRegenerate() => this with { Regenerate = true };
}

public sealed record VisualShotLink(
    bool Found,
    string FilmId,
    string ShotId,
    string? AssetId,
    string? Decision,
    bool Approved,
    string? BrandCode,
    string? Sha256,
    bool HasImage)
{
    public static VisualShotLink Missing(string filmId, string shotId) =>
        new(false, filmId, shotId, null, null, false, null, null, false);
}

public static class VisualDecisions
{
    public const string Bound = "BOUND";
    public const string Reuse = "REUSE";
    public const string GenerateLocal = "GENERATE_LOCAL";
    public const string GenerationRequired = "GENERATION_REQUIRED";
    public const string NoFit = "NO_FIT";
    public const string VariantStored = "VARIANT_STORED";

    public const string IllustratedRenderer = "kit-illustrated-v1";
    public const string CodeRenderer = "kit-code-graphic-v1";
    public const string PhotoProvider = "gemini-3.1-flash-image";
}

public sealed class RenderedFrame
{
    public required byte[] Png { get; init; }
    public required string Sha256 { get; init; }
    public uint SampledHair { get; init; }
    public uint SampledOutfit { get; init; }
    public string FaceShape { get; init; } = "";
    public string Mark { get; init; } = "";
    public int LineWeight { get; init; }
    public string Motif { get; init; } = "";
    public string Medium { get; init; } = "";
    public string StructureCode { get; init; } = "";
    public string OverlayText { get; init; } = "";
    public bool BoxHasInk { get; init; }
    public int BoxBottom { get; init; } = -1;
    public int CaptionTop { get; init; } = -1;
    public byte[] LumaGrid { get; init; } = [];
    public string RendererId { get; init; } = "";
    public uint GroundSample { get; init; }
}

public sealed class VisualResolution
{
    public required string Decision { get; init; }
    public string? AssetId { get; init; }
    public string? VariantAssetId { get; init; }
    public int Score { get; init; }
    public string[] ReasonCodes { get; init; } = [];
    public string Reason { get; init; } = "";
    public GenerationContract? Contract { get; init; }
    public RenderedFrame? Frame { get; init; }
    public int PaidProviderCalls { get; init; }
    public int LocalRenders { get; init; }
    public bool ReplacedApproved { get; init; }
    public CandidateScore[] Candidates { get; init; } = [];
}

public sealed class VisualSession
{
    public List<VisualBrandStyle> Brands { get; } = [];
    public List<CharacterIdentity> Characters { get; } = [];
    public List<VisualAsset> Assets { get; } = [];
    public List<ShotBinding> Bindings { get; } = [];
    public List<UsageMark> Usage { get; } = [];
    public Dictionary<string, byte[]> Pixels { get; } = new(StringComparer.Ordinal);
    public int LocalRenders { get; set; }
    public int PaidProviderCalls { get; set; }

    public VisualBrandStyle? FindBrand(string? code) =>
        Brands.FirstOrDefault(b => string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase));

    public CharacterIdentity? FindCharacter(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Characters.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    public VisualAsset? FindAsset(string? id) =>
        Assets.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));

    public ShotBinding? FindBinding(string filmId, string shotId) =>
        Bindings.FirstOrDefault(b => b.FilmId == filmId && b.ShotId == shotId);

    public void RegisterBrand(VisualBrandStyle brand)
    {
        var code = brand.Code.Trim().ToLowerInvariant();
        if (Brands.Any(b => string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Brand {code} đã có hồ sơ hình.");
        Brands.Add(brand with { Code = code });
    }

    public void AddCharacter(CharacterIdentity identity) => Characters.Add(identity);

    public void AddAsset(VisualAsset asset) => Assets.Add(asset);

    public void Remember(string assetId, byte[] png)
    {
        var index = Assets.FindIndex(a => a.Id == assetId);
        if (index < 0) throw new InvalidOperationException($"Không có asset {assetId}.");
        var asset = Assets[index];
        var frozen = asset.Approval is "approved" or "locked";
        if (frozen && Pixels.TryGetValue(assetId, out var current) && !current.AsSpan().SequenceEqual(png))
            throw new InvalidOperationException("Không ghi đè asset đã duyệt.");
        var sha = VisualHashes.Sha256(png);
        if (frozen && asset.Sha256.Length > 0 && !string.Equals(asset.Sha256, sha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Không ghi đè asset đã duyệt.");
        Pixels[assetId] = png;
        Assets[index] = asset with { Sha256 = sha };
    }

    public void Approve(string filmId, string shotId)
    {
        var index = Bindings.FindIndex(b => b.FilmId == filmId && b.ShotId == shotId);
        if (index < 0) throw new InvalidOperationException("Shot chưa có ảnh để duyệt.");
        var binding = Bindings[index];
        var asset = FindAsset(binding.AssetId) ?? throw new InvalidOperationException("Asset của shot không còn trong thư viện.");
        Bindings[index] = binding with { Approved = true, AssetSha256 = asset.Sha256 };
        var assetIndex = Assets.FindIndex(a => a.Id == asset.Id);
        Assets[assetIndex] = asset with { Approval = "approved" };
    }

    public void NoteUse(ShotVisualDemand demand, VisualAsset asset) =>
        Usage.Add(new UsageMark(demand.FilmId, demand.ShotId, asset.Id, VisualDemandResolver.Signature(demand)));

    public void Bind(ShotVisualDemand demand, VisualAsset asset, string decision, bool approved)
    {
        var next = new ShotBinding(
            demand.FilmId, demand.ShotId, asset.Id, decision, approved, demand.BrandCode.Trim().ToLowerInvariant(), asset.Sha256);
        var index = Bindings.FindIndex(b => b.FilmId == demand.FilmId && b.ShotId == demand.ShotId);
        if (index < 0) Bindings.Add(next);
        else Bindings[index] = next;
    }
}

public static class VisualHashes
{
    public static string Sha256(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    public static bool SafeId(string id) =>
        id.Length is > 0 and <= 80 && id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
}

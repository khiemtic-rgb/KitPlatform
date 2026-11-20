using System.Text.Json;
using System.Text.Json.Serialization;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.VisualIntelligence;

public interface ISharedVisualIntelligence
{
    VisualSession CreateEmptySession();
    VisualSession CreateSampleSession();
    VisualResolution Resolve(VisualSession session, ShotVisualDemand demand, VisualResolveOptions options);
    void Save(string directory, VisualSession session);
    VisualSession Load(string directory);
    VisualSession? TryLoad(string directory);
    VisualSession LoadOrSample(string directory);
}

public interface IVisualIntelligenceGateway
{
    VisualResolution Resolve(ShotVisualDemand demand, VisualResolveOptions? options = null);
    byte[]? ReadImage(string assetId);
    VisualShotLink ReadShot(string filmId, string shotId);
    VisualShotLink ApproveShot(string filmId, string shotId);
}

public static class VisualStillBridge
{
    public static ContentSeriesStillDto ToDto(VisualResolution resolution)
    {
        if (resolution.PaidProviderCalls != 0)
            throw new InvalidOperationException("Visual intelligence không được gọi provider trả phí.");
        if (resolution.Frame is null)
            throw new InvalidOperationException(resolution.Reason);
        var model = resolution.Frame.RendererId.Length > 0
            ? resolution.Frame.RendererId
            : resolution.Contract?.RendererId ?? VisualDecisions.IllustratedRenderer;
        if (model.Contains("gemini", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ảnh cục bộ không được gắn model Gemini.");
        return new ContentSeriesStillDto(
            "data:image/png;base64," + Convert.ToBase64String(resolution.Frame.Png),
            model,
            "16:9",
            resolution.Decision,
            "visual-intelligence");
    }
}

public sealed class SharedVisualIntelligence : ISharedVisualIntelligence
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public VisualSession CreateEmptySession()
    {
        var session = new VisualSession();
        foreach (var brand in BrandSeed.All) session.RegisterBrand(brand);
        session.AddCharacter(BrandSeed.Lan);
        session.AddCharacter(BrandSeed.Koi);
        return session;
    }

    public VisualSession CreateSampleSession()
    {
        var session = CreateEmptySession();
        session.AddAsset(BrandSeed.SharedArrow);
        foreach (var asset in BrandSeed.Stills) session.AddAsset(asset);
        foreach (var asset in session.Assets.ToList())
        {
            if (asset.Kind == "icon") continue;
            Materialize(session, asset);
        }
        session.LocalRenders = 0;
        session.PaidProviderCalls = 0;
        return session;
    }

    public VisualResolution Resolve(VisualSession session, ShotVisualDemand demand, VisualResolveOptions options) =>
        VisualDemandResolver.Resolve(session, demand, options);

    public void Save(string directory, VisualSession session)
    {
        Directory.CreateDirectory(directory);
        foreach (var asset in session.Assets)
        {
            if (!VisualHashes.SafeId(asset.Id) || !session.Pixels.TryGetValue(asset.Id, out var png)) continue;
            var path = Path.Combine(directory, asset.Id + ".png");
            if ((asset.Approval is "approved" or "locked") && File.Exists(path))
            {
                var onDisk = File.ReadAllBytes(path);
                if (string.Equals(VisualHashes.Sha256(onDisk), asset.Sha256, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            File.WriteAllBytes(path, png);
        }
        var file = new VisualLibraryFile
        {
            Version = 1,
            Brands = session.Brands,
            Characters = session.Characters,
            Assets = session.Assets,
            Bindings = session.Bindings,
            Usage = session.Usage,
        };
        File.WriteAllText(Path.Combine(directory, "library.json"), JsonSerializer.Serialize(file, Json));
    }

    public VisualSession Load(string directory)
    {
        var path = Path.Combine(directory, "library.json");
        var file = JsonSerializer.Deserialize<VisualLibraryFile>(File.ReadAllText(path), Json)
            ?? throw new InvalidOperationException("Thư viện hình không đọc được.");
        if (file.Version != 1) throw new InvalidOperationException("Phiên bản thư viện hình không khớp.");
        var session = new VisualSession();
        foreach (var brand in file.Brands) session.Brands.Add(brand);
        foreach (var character in file.Characters) session.Characters.Add(character);
        foreach (var asset in file.Assets) session.Assets.Add(asset);
        foreach (var binding in file.Bindings) session.Bindings.Add(binding);
        foreach (var use in file.Usage) session.Usage.Add(use);
        foreach (var asset in session.Assets)
        {
            var image = Path.Combine(directory, asset.Id + ".png");
            if (!File.Exists(image)) continue;
            var png = File.ReadAllBytes(image);
            var sha = VisualHashes.Sha256(png);
            if (asset.Sha256.Length > 0 && !string.Equals(sha, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Pixel của {asset.Id} không khớp bản đã lưu.");
            session.Pixels[asset.Id] = png;
        }
        return session;
    }

    public VisualSession? TryLoad(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return null;
        var path = Path.Combine(directory, "library.json");
        return File.Exists(path) ? Load(directory) : null;
    }

    public VisualSession LoadOrSample(string directory)
    {
        var path = Path.Combine(directory, "library.json");
        return File.Exists(path) ? Load(directory) : CreateSampleSession();
    }

    private static void Materialize(VisualSession session, VisualAsset asset)
    {
        var brand = session.FindBrand(asset.BrandScope) ?? session.FindBrand(asset.StyleId);
        if (brand is null) return;
        var demand = new ShotVisualDemand(
            asset.Id, "seed", brand.Code, asset.SubjectKey, asset.Action, asset.Setting, "",
            asset.Pose, asset.Expression, asset.Camera, asset.Medium, asset.CharacterId, asset.Prop, asset.Tokens);
        var contract = VisualDemandResolver.BuildContract(session, demand, brand, VisualTextPolicy.Separate("", null), asset.Medium);
        var frame = IllustratedSceneRenderer.Render(session, demand, brand, contract);
        session.Remember(asset.Id, frame.Png);
    }

    private sealed class VisualLibraryFile
    {
        public int Version { get; set; }
        public List<VisualBrandStyle> Brands { get; set; } = [];
        public List<CharacterIdentity> Characters { get; set; } = [];
        public List<VisualAsset> Assets { get; set; } = [];
        public List<ShotBinding> Bindings { get; set; } = [];
        public List<UsageMark> Usage { get; set; } = [];
    }
}

public static class BrandSeed
{
    public static CharacterIdentity Lan { get; } = new(
        "CHAR-VIS-LAN", "famixa", "Lan", "round", "bob", 0x6B3A2A, 0x335C8A, "freckle", 34);

    public static CharacterIdentity Koi { get; } = new(
        "CHAR-VIS-KOI", "famixa", "Koi", "oval", "crop", 0x111111, 0xE1B12C, "glasses", 28);

    public static IReadOnlyList<VisualBrandStyle> All { get; } =
    [
        new("famixa", "Famixa", "illustrated-story", "warm-window", "paper-gouache", 3, "home-threshold",
            0xF3E6D0, 0x2C2420, 0xC4554A, 0x6E8B74, 0xF0C7A8, 0xD7B48A, "family-interior",
            ["kitchen", "doorway", "living-room", "park", "classroom", "night-window", "yard", "bedroom", "balcony", "market-walk", "school-gate", "rain-window", "work-counter"],
            ["candle-axis", "pharmacy-shelf", "price-tag"]),
        new("novixa", "Novixa", "operational-illustration", "cool-clinical", "flat-board", 2, "counter-grid",
            0xF4F7F8, 0x16324F, 0x1F7A72, 0xE2B15A, 0xE6D2C4, 0xF7F4EF, "pharmacy-ops",
            ["pharmacy-counter", "shelf-aisle", "stock-room", "delivery-door", "consult-corner", "cash-desk", "night-close", "cleaning", "waiting-bench", "handoff-counter", "expiry-shelf", "opening-gate", "work-counter"],
            ["bedtime", "candle-axis", "family-bed"]),
        new("trade", "Hành trình Trade", "market-schematic", "high-contrast-desk", "ink-chart", 1, "candle-axis",
            0x1C2128, 0xE8E4D8, 0x3DDC97, 0xE15A4A, 0xC8B8A8, 0x2E3640, "trading-desk",
            ["trading-desk", "risk-board", "review-table", "session-open", "session-close", "dual-screen", "candle-cluster", "range-bars", "work-counter"],
            ["pharmacy-pack", "bedtime", "family-threshold"]),
    ];

    public static VisualAsset SharedArrow { get; } = new(
        "shared-arrow", "icon", "shared", "neutral", "icon-arrow", "point", "none",
        "stand", "calm", "close", "code", null, "", ["arrow", "icon"], "approved", "");

    public static IReadOnlyList<VisualAsset> Stills { get; } =
    [
        new("nx-counter-consult", "still", "novixa", "novixa", "pharmacy-consult", "explain", "pharmacy-counter",
            "stand", "calm", "medium", "illustrated", null, "", ["pharmacy", "counter", "consult", "pharmacist"], "approved", ""),
        new("fx-breakfast", "still", "famixa", "famixa", "family-breakfast", "share-meal", "kitchen",
            "sit", "smile", "medium", "illustrated", "CHAR-VIS-LAN", "", ["family", "breakfast", "table", "parent"], "approved", ""),
        new("tr-desk-review", "still", "trade", "trade", "trade-review", "review", "trading-desk",
            "stand", "calm", "wide", "code", null, "", ["chart", "desk", "review", "candle"], "approved", ""),
    ];
}

using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Content.VisualIntelligence;

public static class VisualTextPolicy
{
    public static TextPlan Separate(string? narration, string[]? labels)
    {
        var spoken = (narration ?? "").Trim();
        var overlay = new List<string>();
        var rejected = new List<string>();
        foreach (var raw in labels ?? [])
        {
            var label = (raw ?? "").Trim();
            if (label.Length == 0) continue;
            if (IsLeak(spoken, label)) rejected.Add(label);
            else overlay.Add(label);
        }
        return new TextPlan(spoken, overlay.ToArray(), rejected.ToArray());
    }

    public static bool IsLeak(string spoken, string label)
    {
        if (label.Length > 40) return true;
        if (spoken.Length == 0) return false;
        if (string.Equals(Fold(spoken), Fold(label), StringComparison.Ordinal)) return true;
        if (Fold(label).Contains(Fold(spoken), StringComparison.Ordinal) && spoken.Length >= 12) return true;
        if (spoken.Contains(label, StringComparison.OrdinalIgnoreCase) && label.Length >= 24) return true;
        var words = Fold(spoken).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 4) return false;
        var foldedLabel = Fold(label);
        for (var i = 0; i <= words.Length - 4; i++)
        {
            var slice = string.Join(' ', words.Skip(i).Take(4));
            if (slice.Length >= 12 && foldedLabel.Contains(slice, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public static string Fold(string value)
    {
        var form = value.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                buffer.Append(char.ToLowerInvariant(ch));
        }
        return buffer.ToString().Normalize(NormalizationForm.FormC);
    }
}

public static class VisualDemandResolver
{
    public static VisualResolution Resolve(VisualSession session, ShotVisualDemand demand, VisualResolveOptions options)
    {
        var brandCode = (demand.BrandCode ?? "").Trim().ToLowerInvariant();
        var brand = session.FindBrand(brandCode);
        if (brand is null)
        {
            return Finish(session, VisualDecisions.NoFit, null, null, 0,
                ["BRAND_UNKNOWN"],
                "Chưa có hồ sơ hình cho brand này. Không dùng tạm hình của brand khác.",
                null, null, false, []);
        }

        var text = VisualTextPolicy.Separate(demand.Narration, demand.RequiredLabels);
        var existing = session.FindBinding(demand.FilmId, demand.ShotId);
        if (existing is not null && !options.Regenerate)
        {
            var bound = session.FindAsset(existing.AssetId);
            session.Pixels.TryGetValue(existing.AssetId, out var png);
            var codes = existing.Approved ? new[] { "APPROVED_LOCK" } : new[] { "BOUND" };
            var reason = existing.Approved
                ? "Giữ ảnh đã duyệt của shot. Đổi brand hoặc mở lại phim không tạo ảnh mới và không thay pixel."
                : "Dùng ảnh đã lưu cho shot này. Không tạo thêm.";
            return Finish(session, VisualDecisions.Bound, existing.AssetId, null, 100, codes, reason, null,
                png is null || bound is null ? null : Stored(png, bound), false, []);
        }

        var scored = Score(session, demand, brand);
        var best = scored.Where(s => s.Eligible).OrderByDescending(s => s.Score).ThenBy(s => s.Uses).FirstOrDefault();
        if (best is not null && !options.Regenerate)
        {
            var asset = session.FindAsset(best.AssetId)!;
            session.NoteUse(demand, asset);
            session.Bind(demand, asset, VisualDecisions.Reuse, asset.Approval is "approved" or "locked");
            session.Pixels.TryGetValue(asset.Id, out var png);
            return Finish(session, VisualDecisions.Reuse, asset.Id, null, best.Score,
                ["REUSE", best.ReasonCode],
                "Thư viện có tài nguyên đúng nội dung, brand và bố cục. Dùng lại, không sinh thêm.",
                null, png is null ? null : Stored(png, asset), false, scored.ToArray());
        }

        if (!options.AllowGenerate)
        {
            return Finish(session, VisualDecisions.NoFit, null, null, best?.Score ?? 0,
                ["NO_FIT"],
                DescribeGap(scored),
                null, null, false, scored.ToArray());
        }

        var medium = ResolveMedium(demand, brand);
        var contract = BuildContract(session, demand, brand, text, medium, options.AllowPaidProvider);
        if (medium == "photo" || !options.RenderLocal)
        {
            return Finish(session, VisualDecisions.GenerationRequired, null, null, 0,
                ["GENERATION_REQUIRED", medium == "photo" ? "PAID_BLOCKED" : "RENDER_OFF"],
                contract.BlockReason, contract, null, false, scored.ToArray());
        }

        var frame = IllustratedSceneRenderer.Render(session, demand, brand, contract);
        var created = AddProposed(session, demand, brand, frame, medium);
        session.LocalRenders++;
        session.NoteUse(demand, created);

        if (existing is { Approved: true })
        {
            return Finish(session, VisualDecisions.VariantStored, existing.AssetId, created.Id, 0,
                ["VARIANT_STORED", "APPROVED_LOCK"],
                "Đã lưu biến thể mới. Ảnh đã duyệt của shot vẫn giữ nguyên.",
                contract, frame, false, scored.ToArray());
        }

        session.Bind(demand, created, VisualDecisions.GenerateLocal, false);
        return Finish(session, VisualDecisions.GenerateLocal, created.Id, null, 0,
            ["GENERATE_LOCAL"],
            "Không có tài nguyên đủ đúng. Đã tạo hình cục bộ và đưa vào thư viện để dùng lại.",
            contract, frame, false, scored.ToArray());
    }

    public static GenerationContract BuildContract(
        VisualSession session,
        ShotVisualDemand demand,
        VisualBrandStyle brand,
        TextPlan text,
        string? medium = null,
        bool allowPaid = false)
    {
        medium ??= ResolveMedium(demand, brand);
        var character = session.FindCharacter(demand.CharacterId);
        var prompt = new StringBuilder();
        prompt.Append("style ").Append(brand.Medium).Append(", material ").Append(brand.Material)
            .Append(", light ").Append(brand.Lighting).Append(", motif ").Append(brand.Motif).Append(". ");
        prompt.Append("action ").Append(demand.Action).Append(". setting ").Append(demand.Setting)
            .Append(". pose ").Append(demand.Pose).Append(". expression ").Append(demand.Expression)
            .Append(". camera ").Append(demand.Camera).Append(". ");
        if (character is not null)
        {
            prompt.Append("identity ").Append(character.Id)
                .Append(" face ").Append(character.FaceShape)
                .Append(" hair ").Append(character.Hair)
                .Append(" mark ").Append(character.Mark).Append(". ");
        }
        if (demand.Prop.Length > 0)
            prompt.Append("prop ").Append(demand.Prop).Append(" without printed words. ");
        if (demand.Tokens is { Length: > 0 })
            prompt.Append("presentation ").Append(string.Join(' ', demand.Tokens)).Append(". ");
        prompt.Append("No readable text, no captions, no packaging labels, no watermarks, no letters on objects.");

        var spoken = text.Spoken;
        var imagePrompt = prompt.ToString();
        if (spoken.Length > 0 && imagePrompt.Contains(spoken, StringComparison.Ordinal))
            imagePrompt = imagePrompt.Replace(spoken, "", StringComparison.Ordinal);

        var photo = medium == "photo";
        var renderer = medium == "code" ? VisualDecisions.CodeRenderer : VisualDecisions.IllustratedRenderer;
        return new GenerationContract(
            medium,
            imagePrompt,
            spoken,
            text.Overlay,
            text.Rejected,
            character?.Id ?? demand.CharacterId,
            brand.Code,
            photo ? "" : renderer,
            photo ? VisualDecisions.PhotoProvider : "local",
            photo && allowPaid,
            photo
                ? "Cần ảnh chân thực. Engine không gọi Gemini khi chưa có phê duyệt chi phí cho lần sinh này."
                : "");
    }

    public static string Signature(ShotVisualDemand demand) =>
        string.Join('|', demand.BrandCode, demand.SubjectKey, demand.Action, demand.Setting, demand.Pose, demand.Camera, demand.Expression, demand.Prop, demand.CharacterId, PresentationTag(demand.Tokens, "view:"), PresentationTag(demand.Tokens, "garment:"));

    public static string ResolveMedium(ShotVisualDemand demand, VisualBrandStyle brand)
    {
        var asked = (demand.Medium ?? "auto").Trim().ToLowerInvariant();
        if (asked is "photo" or "illustrated" or "code") return asked;
        if (brand.Motif == "candle-axis" || brand.Medium.Contains("schematic", StringComparison.OrdinalIgnoreCase))
            return "code";
        return "illustrated";
    }

    private static VisualAsset AddProposed(
        VisualSession session, ShotVisualDemand demand, VisualBrandStyle brand, RenderedFrame frame, string medium)
    {
        var id = Slug($"gen-{demand.FilmId}-{demand.ShotId}");
        if (session.FindAsset(id) is not null) id = Slug($"{id}-{session.Assets.Count}");
        var asset = new VisualAsset(
            id, "still", brand.Code, brand.Code, demand.SubjectKey, demand.Action, demand.Setting,
            demand.Pose, demand.Expression, demand.Camera, medium, demand.CharacterId, demand.Prop,
            demand.Tokens ?? [], "proposed", "");
        session.AddAsset(asset);
        session.Remember(id, frame.Png);
        return session.FindAsset(id)!;
    }

    private static List<CandidateScore> Score(VisualSession session, ShotVisualDemand demand, VisualBrandStyle brand)
    {
        var list = new List<CandidateScore>();
        foreach (var asset in session.Assets)
        {
            if (asset.Approval == "archived") continue;
            string? hard = null;
            if (!BrandOk(asset, brand)) hard = "BRAND";
            else if (!MediumOk(demand, asset)) hard = "MEDIUM";
            else if (!CharacterOk(demand, asset)) hard = "IDENTITY";
            else if (!Eq(asset.SubjectKey, demand.SubjectKey)) hard = "UNRELATED";

            var exact = hard is null
                && Eq(asset.Action, demand.Action)
                && Eq(asset.Setting, demand.Setting)
                && Eq(asset.Pose, demand.Pose)
                && Eq(asset.Camera, demand.Camera)
                && Eq(asset.Expression, demand.Expression)
                && Eq(asset.Prop, demand.Prop)
                && PresentationMatches(asset, demand);

            var score = 0;
            if (hard is null)
            {
                score += 40;
                score += Eq(asset.Action, demand.Action) ? 14 : -50;
                score += Eq(asset.Setting, demand.Setting) ? 14 : -50;
                score += Eq(asset.Pose, demand.Pose) ? 12 : -50;
                score += Eq(asset.Camera, demand.Camera) ? 10 : -50;
                score += Eq(asset.Expression, demand.Expression) ? 8 : -50;
                score += Eq(asset.Prop, demand.Prop) ? 6 : -50;
                if (!PresentationMatches(asset, demand)) score -= 50;
                if (Eq(asset.StyleId, brand.Code)) score += 10;
                if (demand.ContinuityLock && Eq(asset.CharacterId, demand.CharacterId)) score += 8;
                if (exact) score = Math.Max(score, 90);
                var uses = Uses(session, demand.FilmId, asset.Id);
                if (exact) score -= Math.Min(uses, 4) * 3;
                list.Add(new CandidateScore(asset.Id, score, score >= 72 ? "FIT" : "BELOW_THRESHOLD", score >= 72, uses));
            }
            else
            {
                var uses = Uses(session, demand.FilmId, asset.Id);
                list.Add(new CandidateScore(asset.Id, 0, hard, false, uses));
            }
        }
        return list;
    }

    private static int Uses(VisualSession session, string filmId, string assetId) =>
        session.Usage.Count(u => u.FilmId == filmId && u.AssetId == assetId);

    private static bool BrandOk(VisualAsset asset, VisualBrandStyle brand)
    {
        if (Eq(asset.BrandScope, brand.Code)) return true;
        return Eq(asset.BrandScope, "shared") && asset.Kind == "icon";
    }

    private static bool MediumOk(ShotVisualDemand demand, VisualAsset asset)
    {
        var want = (demand.Medium ?? "auto").Trim().ToLowerInvariant();
        if (want.Length == 0 || want == "auto") return asset.Medium is "illustrated" or "code";
        return Eq(asset.Medium, want);
    }

    private static bool CharacterOk(ShotVisualDemand demand, VisualAsset asset)
    {
        if (string.IsNullOrWhiteSpace(demand.CharacterId)) return true;
        return Eq(asset.CharacterId, demand.CharacterId);
    }

    private static bool PresentationMatches(VisualAsset asset, ShotVisualDemand demand) =>
        PresentationTag(asset.Tokens, "view:") == PresentationTag(demand.Tokens, "view:")
        && PresentationTag(asset.Tokens, "garment:") == PresentationTag(demand.Tokens, "garment:");

    private static string PresentationTag(string[]? tokens, string prefix)
    {
        if (tokens is null) return "";
        foreach (var token in tokens)
        {
            if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return token.ToLowerInvariant();
        }
        return "";
    }

    private static bool Eq(string? left, string? right) =>
        string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static string DescribeGap(IReadOnlyList<CandidateScore> scored)
    {
        if (scored.Count == 0) return "Thư viện trống. Không lấy ảnh thế chỗ.";
        var parts = scored
            .OrderByDescending(s => s.Score)
            .Take(6)
            .Select(s => $"{s.AssetId}:{s.ReasonCode}");
        return "Không có ứng viên đủ đúng nội dung, brand, phong cách và bố cục. " + string.Join(", ", parts);
    }

    private static RenderedFrame Stored(byte[] png, VisualAsset asset) => new()
    {
        Png = png,
        Sha256 = VisualHashes.Sha256(png),
        Motif = asset.StyleId,
        Medium = asset.Medium,
        StructureCode = asset.Setting,
        RendererId = asset.Medium == "code" ? VisualDecisions.CodeRenderer : VisualDecisions.IllustratedRenderer,
    };

    private static VisualResolution Finish(
        VisualSession session,
        string decision,
        string? assetId,
        string? variantId,
        int score,
        string[] codes,
        string reason,
        GenerationContract? contract,
        RenderedFrame? frame,
        bool replaced,
        CandidateScore[] candidates) => new()
    {
        Decision = decision,
        AssetId = assetId,
        VariantAssetId = variantId,
        Score = score,
        ReasonCodes = codes,
        Reason = reason,
        Contract = contract,
        Frame = frame,
        PaidProviderCalls = session.PaidProviderCalls,
        LocalRenders = session.LocalRenders,
        ReplacedApproved = replaced,
        Candidates = candidates,
    };

    private static string Slug(string value)
    {
        var buffer = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch)) buffer.Append(ch);
            else if (buffer.Length > 0 && buffer[^1] != '-') buffer.Append('-');
        }
        var slug = buffer.ToString().Trim('-');
        return slug.Length == 0 ? "vis" : slug[..Math.Min(slug.Length, 80)];
    }
}

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Plans which variant kinds + how many images to generate from brand site/channel targets.
/// Nơi đăng is the source of truth — org variant-kind catalogs are not applied here.
/// </summary>
internal static class ContentDestinationPlan
{
    public sealed record Slot(
        string Key,
        string Label,
        string DestType,
        IReadOnlyList<string> VariantKinds,
        bool NeedsImages);

    public sealed record Plan(
        IReadOnlyList<string> VariantKinds,
        IReadOnlyList<Slot> Slots,
        int SuggestedImageCandidates,
        bool NeedsImages,
        int SiteCount,
        int ChannelCount,
        string Summary);

    public static Plan FromTargets(
        IReadOnlyList<ContentRepository.SiteRow> sites,
        IReadOnlyList<ContentRepository.ChannelRow> channels,
        int maxImageCandidates)
    {
        var activeSites = sites.Where(s => s.IsActive).ToList();
        var activeChannels = channels.Where(c => c.IsActive).ToList();
        var slots = new List<Slot>();

        foreach (var site in activeSites)
        {
            var type = (site.ConnectorType ?? "").Trim().ToLowerInvariant();
            slots.Add(new Slot(
                "site:" + site.Id.ToString("N"),
                string.IsNullOrWhiteSpace(site.Name) ? site.Code : site.Name,
                string.IsNullOrWhiteSpace(type) ? "site" : type,
                ["web_long", "seo_meta"],
                NeedsImages: type is not "manual"));
        }

        foreach (var ch in activeChannels)
        {
            var type = (ch.ChannelType ?? "").Trim().ToLowerInvariant();
            var label = string.IsNullOrWhiteSpace(ch.Name) ? ch.Code : ch.Name;
            var key = "channel:" + ch.Id.ToString("N");
            switch (type)
            {
                case "facebook_page":
                    slots.Add(new Slot(key, label, type, ["fb_page", "fb_short", "social_caption"], true));
                    break;
                case "facebook_group":
                    slots.Add(new Slot(key, label, type, ["group_suggested"], false));
                    break;
                case "instagram":
                    slots.Add(new Slot(key, label, type, ["instagram"], true));
                    break;
                case "linkedin":
                    slots.Add(new Slot(key, label, type, ["linkedin"], true));
                    break;
                case "threads":
                    slots.Add(new Slot(key, label, type, ["fb_short", "social_caption"], false));
                    break;
                case "tiktok":
                case "youtube":
                    slots.Add(new Slot(key, label, type, ["tiktok_script", "social_caption"], false));
                    break;
                case "zalo_oa":
                case "other":
                    slots.Add(new Slot(key, label, type, ["fb_short", "social_caption"], false));
                    break;
                default:
                    slots.Add(new Slot(key, label, type.Length == 0 ? "other" : type, ["social_caption"], false));
                    break;
            }
        }

        var kinds = new List<string>();
        foreach (var slot in slots)
        {
            foreach (var kind in slot.VariantKinds)
            {
                if (!kinds.Any(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase)))
                    kinds.Add(kind);
            }
        }

        if (slots.Count == 0)
        {
            return new Plan(
                [],
                [],
                SuggestedImageCandidates: 0,
                NeedsImages: false,
                activeSites.Count,
                activeChannels.Count,
                "Chưa có nơi đăng — vào Thương hiệu để thêm Website / Fanpage / nhóm.");
        }

        var visualSlots = slots.Count(s => s.NeedsImages);
        var cap = Math.Clamp(maxImageCandidates, 0, 10);
        var suggested = cap <= 0 || visualSlots == 0 ? 0 : 1;

        var summary =
            $"{activeSites.Count} web · {activeChannels.Count} MXH → {kinds.Count} bản viết" +
            (suggested > 0 ? ", 1 ảnh" : ", không ảnh");

        return new Plan(
            kinds,
            slots,
            suggested,
            NeedsImages: suggested > 0,
            activeSites.Count,
            activeChannels.Count,
            summary);
    }

    public static Plan Restrict(Plan plan, IReadOnlyList<string>? requestedKinds)
    {
        if (requestedKinds is not { Count: > 0 })
            return plan;

        var want = requestedKinds
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var slots = plan.Slots
            .Where(s => s.VariantKinds.Any(k => want.Contains(k)))
            .ToList();
        var kinds = plan.VariantKinds.Where(k => want.Contains(k)).ToList();
        if (kinds.Count == 0)
            throw new InvalidOperationException(
                "Kênh đã chọn không khớp nơi đăng của brand. Bỏ tick bớt chỗ, hoặc thêm nơi đăng ở Thương hiệu.");

        var needsImages = plan.NeedsImages && slots.Any(s => s.NeedsImages);
        var suggested = needsImages ? plan.SuggestedImageCandidates : 0;
        return new Plan(
            kinds,
            slots,
            suggested,
            needsImages,
            plan.SiteCount,
            plan.ChannelCount,
            plan.Summary + " · lọc " + string.Join("/", kinds));
    }
}

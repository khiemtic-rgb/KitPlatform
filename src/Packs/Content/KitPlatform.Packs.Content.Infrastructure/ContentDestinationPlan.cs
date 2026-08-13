namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Plans which variant kinds + how many images to generate from brand site/channel targets.
/// </summary>
internal static class ContentDestinationPlan
{
    public sealed record Plan(
        IReadOnlyList<string> VariantKinds,
        int SuggestedImageCandidates,
        bool NeedsImages,
        int SiteCount,
        int ChannelCount,
        string Summary);

    public static Plan FromTargets(
        IReadOnlyList<ContentRepository.SiteRow> sites,
        IReadOnlyList<ContentRepository.ChannelRow> channels,
        IReadOnlyList<string> orgVariantKinds,
        int maxImageCandidates)
    {
        var activeSites = sites.Where(s => s.IsActive).ToList();
        var activeChannels = channels.Where(c => c.IsActive).ToList();
        var allowed = orgVariantKinds.Count > 0
            ? orgVariantKinds.Select(k => k.Trim()).Where(k => k.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var kinds = new List<string>();
        void Add(string kind)
        {
            if (allowed is not null && !allowed.Contains(kind)) return;
            if (kinds.Any(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase))) return;
            kinds.Add(kind);
        }

        var visualSlots = 0; // one slot per destination that typically needs a visual

        foreach (var _ in activeSites)
        {
            Add("web_long");
            Add("seo_meta");
            visualSlots++;
        }

        foreach (var ch in activeChannels)
        {
            switch ((ch.ChannelType ?? "").Trim().ToLowerInvariant())
            {
                case "facebook_page":
                    Add("fb_page");
                    Add("fb_short");
                    Add("social_caption");
                    visualSlots++;
                    break;
                case "facebook_group":
                    Add("group_suggested");
                    visualSlots++;
                    break;
                case "instagram":
                    Add("instagram");
                    visualSlots++;
                    break;
                case "linkedin":
                    Add("linkedin");
                    visualSlots++;
                    break;
                case "threads":
                    Add("fb_short");
                    visualSlots++;
                    break;
                case "tiktok":
                case "youtube":
                    Add("tiktok_script");
                    Add("social_caption");
                    visualSlots++;
                    break;
                case "zalo_oa":
                case "other":
                    // Short caption-style pack — reuse fb_short when available.
                    Add("fb_short");
                    Add("social_caption");
                    visualSlots++;
                    break;
            }
        }

        if (kinds.Count == 0)
        {
            // No destinations configured — cheapest useful pack, no images.
            Add("web_long");
            return new Plan(
                kinds,
                SuggestedImageCandidates: 0,
                NeedsImages: false,
                activeSites.Count,
                activeChannels.Count,
                "Chưa có nơi đăng — chỉ gen web_long, không ảnh. Khai báo Website/MXH ở Thương hiệu để gen đúng kênh.");
        }

        var cap = Math.Clamp(maxImageCandidates, 0, 10);
        // 1 ảnh / chỗ đăng cần ảnh, tối đa = cap org (tiết kiệm hơn gen đủ max mỗi lần).
        var suggested = cap <= 0 ? 0 : Math.Min(cap, Math.Max(1, visualSlots));

        var summary =
            $"{activeSites.Count} web · {activeChannels.Count} MXH → {kinds.Count} bản viết" +
            (suggested > 0 ? $", {suggested} ảnh" : ", không ảnh");

        return new Plan(
            kinds,
            suggested,
            NeedsImages: suggested > 0,
            activeSites.Count,
            activeChannels.Count,
            summary);
    }
}

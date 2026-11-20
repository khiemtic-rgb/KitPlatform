using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Đọc thương hiệu, nơi đăng, bài đã sẵn sàng và job hiện có; xếp lịch đề xuất bằng <see cref="ContentScheduleRules"/>.
/// Chỉ đọc: không ghi DB, không tạo publish_job, không gọi mạng ngoài.
/// </summary>
internal sealed class ContentScheduleAdvisor : IContentScheduleAdvisor
{
    private const int MaxTopicsEvaluated = 40;
    private static readonly string[] ReadyStatuses = ["Review", "Approved", "Scheduled"];
    private const int VietnamOffsetHours = 7;

    private readonly ContentRepository _repo;
    private readonly IContentPublishPlanStore _plans;
    private readonly ContentPlatformPostStore _platform;

    public ContentScheduleAdvisor(ContentRepository repo, IContentPublishPlanStore plans, ContentPlatformPostStore platform)
    {
        _repo = repo;
        _plans = plans;
        _platform = platform;
    }

    public Task<ContentSchedulePlanDto> PlanAsync(
        string brand,
        int days,
        int maxPerDay,
        CancellationToken cancellationToken = default)
        => PlanCoreAsync(brand, days, maxPerDay, null, cancellationToken);

    public Task<ContentSchedulePlanDto> PlanForTopicsAsync(
        string brand,
        IReadOnlyCollection<Guid> topicIds,
        CancellationToken cancellationToken = default)
        => PlanCoreAsync(brand, null, null, topicIds.ToHashSet(), cancellationToken);

    /// <param name="days">null = theo cài đặt lịch đăng đã lưu.</param>
    /// <param name="onlyTopics">null = mọi bài sẵn sàng; có giá trị = chỉ xếp các bài này.</param>
    private async Task<ContentSchedulePlanDto> PlanCoreAsync(
        string brand,
        int? days,
        int? maxPerDay,
        IReadOnlySet<Guid>? onlyTopics,
        CancellationToken cancellationToken)
    {
        var brands = await _repo.ListBrandsAsync(true, cancellationToken);
        var key = (brand ?? "").Trim();
        var b = Guid.TryParse(key, out var gid)
            ? brands.FirstOrDefault(x => x.Id == gid)
            : brands.FirstOrDefault(x =>
                string.Equals(x.Code, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase));
        if (b is null)
            throw new InvalidOperationException($"Không tìm thấy thương hiệu «{key}» đang hoạt động.");

        var warnings = new List<string>
        {
            "Đây chỉ là đề xuất. Chưa có job đăng nào được tạo và chưa có gì được đăng.",
        };

        var sites = await _repo.ListSitesAsync(b.Id, cancellationToken);
        var channels = await _repo.ListChannelsAsync(b.Id, cancellationToken);
        var targets = new List<ContentScheduleTarget>();

        foreach (var s in sites.Where(x => x.IsActive))
        {
            var type = (s.ConnectorType ?? "").Trim().ToLowerInvariant();
            var label = string.IsNullOrWhiteSpace(s.Name) ? s.Code : s.Name;
            if (ContentScheduleRules.SiteAutoConnectors.Contains(type))
                targets.Add(new ContentScheduleTarget(
                    "site:" + s.Id.ToString("N"), label, type, s.Id, null, ["web_long"]));
            else
                warnings.Add($"Website «{label}» ({(type.Length == 0 ? "chưa chọn connector" : type)}) không đăng tự động; dùng Đăng tay.");
        }

        foreach (var c in channels.Where(x => x.IsActive))
        {
            var type = (c.ChannelType ?? "").Trim().ToLowerInvariant();
            var label = string.IsNullOrWhiteSpace(c.Name) ? c.Code : c.Name;
            if (type == ContentScheduleRules.FacebookPage)
            {
                if (!ContentScheduleRules.IsFacebookAutoReady(c.ConfigJson, c.SecretRef))
                    warnings.Add($"Fanpage «{label}» chưa đăng tự động được (chưa có API Meta): lượt đăng thành đăng tay theo lịch ở Lịch đăng & Agent → Đăng tay.");

                var accepts = ContentChannelAccepts.Parse(c.ConfigJson);
                if (!ContentChannelAccepts.Allows(accepts, ContentChannelAccepts.Video))
                    warnings.Add(ContentChannelAccepts.SkipWarning(label, ContentChannelAccepts.Video));
                if (!ContentChannelAccepts.Allows(accepts, ContentChannelAccepts.Post))
                    warnings.Add(ContentChannelAccepts.SkipWarning(label, ContentChannelAccepts.Post));

                targets.Add(new ContentScheduleTarget(
                    "channel:" + c.Id.ToString("N"), label, type, null, c.Id,
                    ["fb_page", "fb_short", "social_caption"], accepts));
            }
            else if (type == "youtube_channel")
            {
                if ((c.ConfigJson ?? "").Contains("NEED_RECONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Kênh YouTube «{label}» cần kết nối lại; bỏ qua cho đến khi nối lại.");
                    continue;
                }

                targets.Add(new ContentScheduleTarget(
                    "channel:" + c.Id.ToString("N"), label, type, null, c.Id, ["video_asset"]));
            }
            else if (type == "tiktok_channel")
            {
                if ((c.ConfigJson ?? "").Contains("NEED_RECONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Kênh TikTok «{label}» cần kết nối lại; bỏ qua cho đến khi nối lại.");
                    continue;
                }

                targets.Add(new ContentScheduleTarget(
                    "channel:" + c.Id.ToString("N"), label, type, null, c.Id, ["video_asset"]));
            }
            else
            {
                warnings.Add($"Kênh «{label}» ({type}) không đăng tự động; dùng Đăng tay.");
            }
        }

        ContentBrandScheduleSettingsDto schedule;
        var plansReady = true;
        try
        {
            schedule = await _plans.GetSettingsAsync(b.Id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Bảng lịch đăng chưa có: vẫn xếp được theo mặc định, chỉ chưa có hạn mức theo kế hoạch bài.
            schedule = ContentPublishPlanRules.Defaults();
            plansReady = false;
            warnings.Add(ex.Message);
        }
        var effectiveDays = days ?? schedule.Days;
        var effectiveMaxPerDay = maxPerDay ?? schedule.MaxPerDayPerChannel;

        var topics = (await _repo.ListTopicsAsync(b.Id, null, cancellationToken))
            .Where(t => ReadyStatuses.Contains(t.Status, StringComparer.OrdinalIgnoreCase))
            .Where(t => onlyTopics is null || onlyTopics.Contains(t.Id))
            .OrderBy(t => t.UpdatedAt)
            .Take(onlyTopics is null ? MaxTopicsEvaluated : 200)
            .ToList();
        var jobRows = (await _repo.ListPublishJobsAsync(null, cancellationToken))
            .Where(j => j.BrandId == b.Id)
            .ToList();
        // Kế hoạch đăng (dài/ngắn, loại kênh) của bài sắp xếp và của bài đã có lịch, để tính hạn mức đúng.
        // Lượt đăng tay (Fanpage chưa có API) không có publish_job nhưng vẫn giữ chỗ trên lịch.
        var manualSlots = plansReady
            ? await _plans.ListManualSlotsAsync(b.Id, cancellationToken)
            : Array.Empty<ContentManualSlot>();
        var plans = plansReady
            ? await _plans.GetPlansAsync(
                topics.Select(t => t.Id).Concat(jobRows.Select(j => j.TopicId)).Concat(manualSlots.Select(m => m.TopicId)).Distinct().ToList(),
                cancellationToken)
            : new Dictionary<Guid, ContentTopicPublishPlanDto>();

        var connectorTypes = targets.Select(t => t.ConnectorType).Distinct().ToList();
        var candidates = new List<ContentScheduleCandidate>();
        foreach (var t in topics)
        {
            var variants = await _repo.ListVariantsAsync(t.Id, cancellationToken);
            var hasVideo = (await _repo.ListAssetsAsync(t.Id, cancellationToken)).Any(a => a.Kind == "video");
            var gate = await EvaluateGateAsync(b, t, variants, cancellationToken);
            var fact = await ContentFactCheck.EvaluateAsync(_repo, t.Id, cancellationToken);
            var blocks = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in connectorTypes)
            {
                try
                {
                    if (fact.Applies && !fact.Passed)
                        throw new InvalidOperationException(ContentFactGate.Refuse(fact.Issues));
                    ContentQualityRunner.ThrowIfCannotPublish(gate, type, fact.Applies && fact.Passed);
                    blocks[type] = null;
                }
                catch (InvalidOperationException ex)
                {
                    blocks[type] = ex.Message;
                }
            }

            plans.TryGetValue(t.Id, out var plan);
            candidates.Add(new ContentScheduleCandidate(
                t.Id,
                t.Title,
                t.Priority,
                t.UpdatedAt,
                variants.Select(v => v.Kind).Concat(hasVideo ? ["video_asset"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                blocks,
                plan?.Kind,
                plan?.Connectors,
                plan?.Seq ?? 0));
        }

        IReadOnlyList<ContentPlatformPostSnapshot> platformPosts = [];
        try
        {
            platformPosts = await _platform.ListSnapshotsAsync(b.Id, cancellationToken);
        }
        catch (Exception ex) when (ContentPlatformPostStore.IsMissingTable(ex))
        {
            warnings.Add("Chưa có bảng lịch nền tảng (migration 442). Agent chưa thấy bài đã hẹn trên Fanpage hoặc YouTube.");
        }

        var existing = jobRows
            .Select(j => new ContentScheduleExistingJob(
                j.TopicId, j.SiteTargetId, j.ChannelTargetId, j.Status, j.PublishAt,
                plans.TryGetValue(j.TopicId, out var jp) ? jp.Kind : null))
            .Concat(manualSlots.Select(m => new ContentScheduleExistingJob(
                m.TopicId, m.SiteId, m.ChannelId, string.Equals(m.Status, "Posted", StringComparison.OrdinalIgnoreCase) ? "Succeeded" : "Queued",
                m.PublishAt, plans.TryGetValue(m.TopicId, out var mp) ? mp.Kind : null)))
            .Concat(platformPosts.Select(ContentPlatformRules.ToBlockingJob))
            .ToList();

        var settings = new ContentScheduleSettings(
            effectiveDays,
            effectiveMaxPerDay,
            schedule.MinGapHours,
            schedule.LocalSlots,
            VietnamOffsetHours,
            schedule.ShortPerDayPerChannel,
            schedule.LongPerWeekPerChannel);
        var now = DateTimeOffset.UtcNow;
        var (proposals, skipped) = ContentScheduleRules.Plan(now, targets, candidates, existing, settings);

        if (topics.Count == 0)
            warnings.Add("Chưa có bài nào ở trạng thái Review, Approved hoặc Scheduled.");

        return new ContentSchedulePlanDto(
            b.Code,
            b.Name,
            now,
            Math.Clamp(effectiveDays, 1, 14),
            Math.Clamp(effectiveMaxPerDay, 1, 4),
            proposals,
            skipped,
            warnings,
            ReadOnly: true);
    }

    private async Task<ContentQualityGateDto> EvaluateGateAsync(
        ContentRepository.BrandRow brand,
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        CancellationToken ct)
    {
        var packageId = await _repo.GetPackageIdByTopicAsync(topic.Id, ct);
        if (packageId is Guid pid)
        {
            var package = await _repo.GetPackageAsync(pid, ct);
            if (package is not null)
                return await ContentQualityRunner.EvaluateAsync(_repo, package, ct);
        }

        var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
        return ContentQualityGate.Evaluate(
            knowledge,
            null,
            topic.Title,
            variants.Select(v => (v.Kind, v.BodyMarkdown)).ToList(),
            brand.Name);
    }
}

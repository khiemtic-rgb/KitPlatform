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

    public ContentScheduleAdvisor(ContentRepository repo)
    {
        _repo = repo;
    }

    public async Task<ContentSchedulePlanDto> PlanAsync(
        string brand,
        int days,
        int maxPerDay,
        CancellationToken cancellationToken = default)
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
                if ((c.ConfigJson ?? "").Contains("NEED_RECONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Fanpage «{label}» cần kết nối lại; bỏ qua cho đến khi nối lại.");
                    continue;
                }

                targets.Add(new ContentScheduleTarget(
                    "channel:" + c.Id.ToString("N"), label, type, null, c.Id,
                    ["fb_page", "fb_short", "social_caption"]));
            }
            else
            {
                warnings.Add($"Kênh «{label}» ({type}) không đăng tự động; dùng Đăng tay.");
            }
        }

        var topics = (await _repo.ListTopicsAsync(b.Id, null, cancellationToken))
            .Where(t => ReadyStatuses.Contains(t.Status, StringComparer.OrdinalIgnoreCase))
            .OrderBy(t => t.UpdatedAt)
            .Take(MaxTopicsEvaluated)
            .ToList();

        var connectorTypes = targets.Select(t => t.ConnectorType).Distinct().ToList();
        var candidates = new List<ContentScheduleCandidate>();
        foreach (var t in topics)
        {
            var variants = await _repo.ListVariantsAsync(t.Id, cancellationToken);
            var gate = await EvaluateGateAsync(b, t, variants, cancellationToken);
            var blocks = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in connectorTypes)
            {
                try
                {
                    ContentQualityRunner.ThrowIfCannotPublish(gate, type);
                    blocks[type] = null;
                }
                catch (InvalidOperationException ex)
                {
                    blocks[type] = ex.Message;
                }
            }

            candidates.Add(new ContentScheduleCandidate(
                t.Id,
                t.Title,
                t.Priority,
                t.UpdatedAt,
                variants.Select(v => v.Kind).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                blocks));
        }

        var existing = (await _repo.ListPublishJobsAsync(null, cancellationToken))
            .Where(j => j.BrandId == b.Id)
            .Select(j => new ContentScheduleExistingJob(j.TopicId, j.SiteTargetId, j.ChannelTargetId, j.Status, j.PublishAt))
            .ToList();

        var settings = new ContentScheduleSettings(
            days,
            maxPerDay,
            MinGapHours: 6,
            ContentScheduleRules.DefaultLocalSlots,
            VietnamOffsetHours);
        var now = DateTimeOffset.UtcNow;
        var (proposals, skipped) = ContentScheduleRules.Plan(now, targets, candidates, existing, settings);

        if (topics.Count == 0)
            warnings.Add("Chưa có bài nào ở trạng thái Review, Approved hoặc Scheduled.");

        return new ContentSchedulePlanDto(
            b.Code,
            b.Name,
            now,
            Math.Clamp(days, 1, 14),
            Math.Clamp(maxPerDay, 1, 4),
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

namespace KitPlatform.Packs.Content;

/// <summary>
/// Bước 1 của Agent quản trị đăng bài (Park Marketing): chỉ ĐỀ XUẤT lịch, không tạo job, không đăng.
/// </summary>
public sealed record ContentScheduleTarget(
    string Key,
    string Label,
    string ConnectorType,
    Guid? SiteId,
    Guid? ChannelId,
    IReadOnlyList<string> VariantKinds);

public sealed record ContentScheduleCandidate(
    Guid TopicId,
    string Title,
    string Priority,
    DateTimeOffset ReadySince,
    IReadOnlyList<string> VariantKinds,
    IReadOnlyDictionary<string, string?> QualityBlockByConnector);

public sealed record ContentScheduleExistingJob(
    Guid TopicId,
    Guid? SiteId,
    Guid? ChannelId,
    string Status,
    DateTimeOffset? PublishAt);

public sealed record ContentScheduleSettings(
    int Days,
    int MaxPerDayPerTarget,
    int MinGapHours,
    IReadOnlyList<string> LocalSlots,
    int UtcOffsetHours);

public sealed record ContentScheduleProposalDto(
    Guid TopicId,
    string Title,
    string TargetKey,
    string TargetLabel,
    string ConnectorType,
    Guid? SiteId,
    Guid? ChannelId,
    DateTimeOffset ProposedAtUtc,
    string ProposedAtLocal,
    string Reason);

public sealed record ContentScheduleSkippedDto(Guid TopicId, string Title, string Reason);

public sealed record ContentSchedulePlanDto(
    string BrandCode,
    string BrandName,
    DateTimeOffset GeneratedAtUtc,
    int Days,
    int MaxPerDayPerTarget,
    IReadOnlyList<ContentScheduleProposalDto> Proposals,
    IReadOnlyList<ContentScheduleSkippedDto> Skipped,
    IReadOnlyList<string> Warnings,
    bool ReadOnly);

public interface IContentScheduleAdvisor
{
    /// <param name="brand">Mã thương hiệu, tên, hoặc Guid.</param>
    Task<ContentSchedulePlanDto> PlanAsync(
        string brand,
        int days,
        int maxPerDay,
        CancellationToken cancellationToken = default);
}

/// <summary>Luật xếp lịch thuần (không đụng DB) để kiểm thử được.</summary>
public static class ContentScheduleRules
{
    public static readonly string[] SiteAutoConnectors = ["astro_git", "wordpress_rest", "local_os"];
    public const string FacebookPage = "facebook_page";
    private static readonly string[] BlockingJobStatuses = ["Queued", "Running", "Succeeded"];

    public static readonly string[] DefaultLocalSlots = ["09:00", "19:30"];

    public static (IReadOnlyList<ContentScheduleProposalDto> Proposals, IReadOnlyList<ContentScheduleSkippedDto> Skipped) Plan(
        DateTimeOffset nowUtc,
        IReadOnlyList<ContentScheduleTarget> targets,
        IReadOnlyList<ContentScheduleCandidate> candidates,
        IReadOnlyList<ContentScheduleExistingJob> existing,
        ContentScheduleSettings settings)
    {
        var offset = TimeSpan.FromHours(Math.Clamp(settings.UtcOffsetHours, -12, 14));
        var slots = ParseSlots(settings.LocalSlots);
        var days = Math.Clamp(settings.Days, 1, 14);
        var perDay = Math.Clamp(settings.MaxPerDayPerTarget, 1, 4);
        var gap = TimeSpan.FromHours(Math.Clamp(settings.MinGapHours, 0, 48));

        var ordered = candidates
            .OrderBy(c => c.Priority, StringComparer.Ordinal)
            .ThenBy(c => c.ReadySince)
            .ThenBy(c => c.TopicId)
            .ToList();

        var proposals = new List<ContentScheduleProposalDto>();
        var proposedTopics = new HashSet<Guid>();
        var firstLocalDay = DateOnly.FromDateTime((nowUtc.ToUniversalTime() + offset).DateTime);

        foreach (var target in targets.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            var jobs = existing.Where(e => MatchesTarget(e, target)
                                           && BlockingJobStatuses.Contains(e.Status, StringComparer.OrdinalIgnoreCase)).ToList();
            var topicsWithJob = jobs.Select(j => j.TopicId).ToHashSet();
            var accepted = jobs.Select(j => j.PublishAt ?? nowUtc).ToList();
            var usedForTarget = new HashSet<Guid>();
            var exhausted = false;

            for (var d = 0; d < days && !exhausted; d++)
            {
                var day = firstLocalDay.AddDays(d);
                foreach (var slot in slots)
                {
                    var local = new DateTimeOffset(day.ToDateTime(slot), offset);
                    var utc = local.ToUniversalTime();
                    if (utc <= nowUtc.AddMinutes(15)) continue;

                    var sameDay = accepted.Count(a => DateOnly.FromDateTime((a.ToUniversalTime() + offset).DateTime) == day);
                    if (sameDay >= perDay) continue;
                    if (accepted.Any(a => Math.Abs((a - utc).TotalHours) < gap.TotalHours)) continue;

                    var pick = ordered.FirstOrDefault(c =>
                        !topicsWithJob.Contains(c.TopicId)
                        && !usedForTarget.Contains(c.TopicId)
                        && IsEligible(c, target));
                    if (pick is null)
                    {
                        exhausted = true;
                        break;
                    }

                    usedForTarget.Add(pick.TopicId);
                    proposedTopics.Add(pick.TopicId);
                    accepted.Add(utc);
                    proposals.Add(new ContentScheduleProposalDto(
                        pick.TopicId,
                        pick.Title,
                        target.Key,
                        target.Label,
                        target.ConnectorType,
                        target.SiteId,
                        target.ChannelId,
                        utc,
                        FormatLocal(local, settings.UtcOffsetHours),
                        $"Ưu tiên {pick.Priority}, chờ từ {pick.ReadySince:yyyy-MM-dd}; qua cổng chất lượng cho {target.ConnectorType}."));
                }
            }
        }

        var skipped = new List<ContentScheduleSkippedDto>();
        foreach (var c in ordered.Where(c => !proposedTopics.Contains(c.TopicId)))
            skipped.Add(new ContentScheduleSkippedDto(c.TopicId, c.Title, ExplainSkip(c, targets, existing)));

        return (
            proposals.OrderBy(p => p.ProposedAtUtc).ThenBy(p => p.TargetKey, StringComparer.Ordinal).ToList(),
            skipped);
    }

    public static bool IsEligible(ContentScheduleCandidate candidate, ContentScheduleTarget target)
    {
        var hasVariant = target.VariantKinds.Any(k =>
            candidate.VariantKinds.Contains(k, StringComparer.OrdinalIgnoreCase));
        if (!hasVariant) return false;
        return !candidate.QualityBlockByConnector.TryGetValue(target.ConnectorType, out var block)
               || string.IsNullOrWhiteSpace(block);
    }

    private static bool MatchesTarget(ContentScheduleExistingJob job, ContentScheduleTarget target) =>
        (target.SiteId is Guid s && job.SiteId == s)
        || (target.ChannelId is Guid c && job.ChannelId == c);

    private static string ExplainSkip(
        ContentScheduleCandidate c,
        IReadOnlyList<ContentScheduleTarget> targets,
        IReadOnlyList<ContentScheduleExistingJob> existing)
    {
        if (targets.Count == 0)
            return "Chưa có nơi đăng tự động nào đang bật cho thương hiệu này.";

        var withVariant = targets.Where(t => t.VariantKinds.Any(k =>
            c.VariantKinds.Contains(k, StringComparer.OrdinalIgnoreCase))).ToList();
        if (withVariant.Count == 0)
            return "Chưa có bản viết hợp với các nơi đăng tự động (cần web_long hoặc bản Fanpage).";

        var passing = withVariant.Where(t => IsEligible(c, t)).ToList();
        if (passing.Count == 0)
        {
            var first = withVariant
                .Select(t => c.QualityBlockByConnector.TryGetValue(t.ConnectorType, out var b) ? b : null)
                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            return "Chưa qua cổng chất lượng: " + (first ?? "không rõ lý do") + ".";
        }

        var allHaveJob = passing.All(t => existing.Any(e =>
            e.TopicId == c.TopicId && MatchesTarget(e, t)
            && BlockingJobStatuses.Contains(e.Status, StringComparer.OrdinalIgnoreCase)));
        if (allHaveJob)
            return "Đã có job đăng cho mọi nơi đăng phù hợp.";

        return "Hết khung giờ trong khoảng ngày đã chọn; sẽ được xếp khi có khung mới.";
    }

    private static IReadOnlyList<TimeOnly> ParseSlots(IReadOnlyList<string>? raw)
    {
        var list = new List<TimeOnly>();
        foreach (var s in (raw is { Count: > 0 } ? raw : DefaultLocalSlots))
            if (TimeOnly.TryParse(s, out var t)) list.Add(t);
        if (list.Count == 0)
            list.AddRange(DefaultLocalSlots.Select(TimeOnly.Parse));
        return list.Distinct().OrderBy(t => t).ToList();
    }

    private static string FormatLocal(DateTimeOffset local, int offsetHours) =>
        $"{local:yyyy-MM-dd HH:mm} (UTC{(offsetHours >= 0 ? "+" : "")}{offsetHours})";
}

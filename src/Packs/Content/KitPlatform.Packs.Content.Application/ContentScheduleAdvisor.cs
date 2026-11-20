namespace KitPlatform.Packs.Content;

/// <summary>
/// Bước 1 của Agent quản trị đăng bài (Park Marketing): chỉ ĐỀ XUẤT lịch, không tạo job, không đăng.
/// </summary>
/// <param name="Accepts">video/post của Fanpage. null = nhận cả hai.</param>
public sealed record ContentScheduleTarget(
    string Key,
    string Label,
    string ConnectorType,
    Guid? SiteId,
    Guid? ChannelId,
    IReadOnlyList<string> VariantKinds,
    IReadOnlyList<string>? Accepts = null);

/// <param name="Format">"long" / "short" theo kế hoạch đăng của bài; null = bài không có kế hoạch (không tính hạn mức dài/ngắn).</param>
/// <param name="Connectors">Loại kênh bài được đăng; null = mọi nơi đăng phù hợp (như trước).</param>
/// <param name="Seq">Thứ tự trong lô (bản dài trước, video ngắn sau).</param>
public sealed record ContentScheduleCandidate(
    Guid TopicId,
    string Title,
    string Priority,
    DateTimeOffset ReadySince,
    IReadOnlyList<string> VariantKinds,
    IReadOnlyDictionary<string, string?> QualityBlockByConnector,
    string? Format = null,
    IReadOnlyList<string>? Connectors = null,
    int Seq = 0);

/// <param name="PlatformLabel">Fanpage hoặc YouTube khi hàng đến từ platform_post. null = job hoặc đăng tay.</param>
/// <param name="CountSlot">false = chặn topic nhưng không chiếm khung giờ (bài nền tảng không có giờ).</param>
public sealed record ContentScheduleExistingJob(
    Guid TopicId,
    Guid? SiteId,
    Guid? ChannelId,
    string Status,
    DateTimeOffset? PublishAt,
    string? Format = null,
    string? PlatformLabel = null,
    bool CountSlot = true);

/// <param name="ShortPerDay">Hạn mức video ngắn mỗi ngày mỗi kênh; null = không giới hạn riêng.</param>
/// <param name="LongPerWeek">Hạn mức video dài mỗi tuần (thứ Hai–Chủ nhật) mỗi kênh; null = không giới hạn riêng.</param>
public sealed record ContentScheduleSettings(
    int Days,
    int MaxPerDayPerTarget,
    int MinGapHours,
    IReadOnlyList<string> LocalSlots,
    int UtcOffsetHours,
    int? ShortPerDay = null,
    int? LongPerWeek = null);

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

    /// <summary>
    /// Xếp lịch chỉ cho các bài đã chỉ định (ví dụ thành phẩm của một phim), theo cài đặt lịch đã lưu của thương hiệu.
    /// Lịch đã có (job đã duyệt hoặc đã đăng) vẫn được tính để không trùng khung giờ và hạn mức.
    /// </summary>
    Task<ContentSchedulePlanDto> PlanForTopicsAsync(
        string brand,
        IReadOnlyCollection<Guid> topicIds,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>Luật xếp lịch thuần (không đụng DB) để kiểm thử được.</summary>
public static class ContentScheduleRules
{
    public static readonly string[] SiteAutoConnectors = ["astro_git", "wordpress_rest", "local_os"];
    public const string FacebookPage = "facebook_page";
    private static readonly string[] BlockingJobStatuses = ["Queued", "Running", "Succeeded"];

    public static readonly string[] DefaultLocalSlots = ["09:00", "19:30"];

    /// <summary>
    /// Fanpage tự đăng được khi đã kết nối Meta (facebook.status = CONNECTED) hoặc có token qua biến môi trường.
    /// Chưa được thì lượt đăng thành đăng tay: vẫn xếp giờ, người dùng copy và bấm đăng theo lịch.
    /// </summary>
    public static bool IsFacebookAutoReady(string? configJson, string? secretRef)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            // Người dùng chọn đăng tay cho Fanpage này (ví dụ token cũ không còn tin được): luôn đăng tay.
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty("manualPosting", out var manual)
                && manual.ValueKind == System.Text.Json.JsonValueKind.True)
                return false;
            if (!string.IsNullOrWhiteSpace(secretRef)) return true;
            if (HasStoredPageToken(doc.RootElement)) return true;
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("facebook", out var fb)
                || fb.ValueKind != System.Text.Json.JsonValueKind.Object)
                return false;
            foreach (var name in new[] { "status", "Status" })
            {
                if (fb.TryGetProperty(name, out var st)
                    && st.ValueKind == System.Text.Json.JsonValueKind.String
                    && string.Equals(st.GetString(), "CONNECTED", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>Page token lưu trong config_json.storedSecret. Không đọc secret_ref.</summary>
    private static bool HasStoredPageToken(System.Text.Json.JsonElement root)
    {
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object
            || !root.TryGetProperty("storedSecret", out var secret)
            || secret.ValueKind != System.Text.Json.JsonValueKind.String)
            return false;
        return !string.IsNullOrWhiteSpace(secret.GetString());
    }

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
        var shortPerDay = settings.ShortPerDay is int sp ? Math.Clamp(sp, 0, 4) : (int?)null;
        var longPerWeek = settings.LongPerWeek is int lw ? Math.Clamp(lw, 0, 14) : (int?)null;
        DateOnly LocalDay(DateTimeOffset at) => DateOnly.FromDateTime((at.ToUniversalTime() + offset).DateTime);
        static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

        var ordered = candidates
            .OrderBy(c => c.Priority, StringComparer.Ordinal)
            .ThenBy(c => c.Seq)
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
            var topicsWithJob = jobs.Where(j => j.TopicId != Guid.Empty).Select(j => j.TopicId).ToHashSet();
            var accepted = jobs.Where(j => j.CountSlot).Select(j => (At: j.PublishAt ?? nowUtc, j.Format)).ToList();
            var remaining = ordered.Where(c => !topicsWithJob.Contains(c.TopicId) && IsEligible(c, target)).ToList();

            // Hạn mức theo loại video: ngắn tính theo ngày, dài tính theo tuần (và tối đa một bản dài mỗi ngày).
            bool FitsQuota(string? format, DateOnly day)
            {
                if (format == "short" && shortPerDay is int maxShort)
                    return accepted.Count(a => a.Format == "short" && LocalDay(a.At) == day) < maxShort;
                if (format == "long" && longPerWeek is int maxLong)
                {
                    var week = WeekStart(day);
                    return accepted.Count(a => a.Format == "long" && WeekStart(LocalDay(a.At)) == week) < maxLong
                           && accepted.Count(a => a.Format == "long" && LocalDay(a.At) == day) < 1;
                }
                return true;
            }

            for (var d = 0; d < days && remaining.Count > 0; d++)
            {
                var day = firstLocalDay.AddDays(d);
                foreach (var slot in slots)
                {
                    if (remaining.Count == 0) break;
                    var local = new DateTimeOffset(day.ToDateTime(slot), offset);
                    var utc = local.ToUniversalTime();
                    if (utc <= nowUtc.AddMinutes(15)) continue;

                    var sameDay = accepted.Count(a => LocalDay(a.At) == day);
                    if (sameDay >= perDay) continue;
                    if (accepted.Any(a => Math.Abs((a.At - utc).TotalHours) < gap.TotalHours)) continue;

                    var pick = remaining.FirstOrDefault(c => FitsQuota(c.Format, day));
                    if (pick is null) continue;

                    remaining.Remove(pick);
                    proposedTopics.Add(pick.TopicId);
                    accepted.Add((utc, pick.Format));
                    var kind = pick.Format == "long" ? "Video dài. " : pick.Format == "short" ? "Video ngắn. " : "";
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
                        $"{kind}Ưu tiên {pick.Priority}, chờ từ {pick.ReadySince:yyyy-MM-dd}; qua cổng chất lượng cho {target.ConnectorType}."));
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
        if (candidate.Connectors is { Count: > 0 } only
            && !only.Contains(target.ConnectorType, StringComparer.OrdinalIgnoreCase))
            return false;
        var hasVariant = target.VariantKinds.Any(k =>
            candidate.VariantKinds.Contains(k, StringComparer.OrdinalIgnoreCase));
        if (!hasVariant) return false;
        if (!ContentChannelAccepts.Allows(target.Accepts, ContentChannelAccepts.KindOf(candidate)))
            return false;
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
            var kind = ContentChannelAccepts.KindOf(c);
            var skipped = withVariant
                .Where(t => !ContentChannelAccepts.Allows(t.Accepts, kind))
                .Select(t => ContentChannelAccepts.SkipWarning(t.Label, kind))
                .FirstOrDefault();
            var quality = withVariant
                .Select(t => c.QualityBlockByConnector.TryGetValue(t.ConnectorType, out var b) ? b : null)
                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            if (skipped is not null && string.IsNullOrWhiteSpace(quality))
                return skipped;
            return "Chưa qua cổng chất lượng: " + (quality ?? skipped ?? "không rõ lý do") + ".";
        }

        var allHaveJob = passing.All(t => existing.Any(e =>
            e.TopicId == c.TopicId && MatchesTarget(e, t)
            && BlockingJobStatuses.Contains(e.Status, StringComparer.OrdinalIgnoreCase)));
        if (allHaveJob)
        {
            var platformHits = passing
                .Select(t => existing.FirstOrDefault(e =>
                    e.TopicId == c.TopicId && MatchesTarget(e, t)
                    && e.PlatformLabel is not null
                    && BlockingJobStatuses.Contains(e.Status, StringComparer.OrdinalIgnoreCase)))
                .Where(e => e is not null)
                .Cast<ContentScheduleExistingJob>()
                .ToList();
            if (platformHits.Count > 0)
            {
                var scheduled = platformHits.FirstOrDefault(e =>
                    !string.Equals(e.Status, "Succeeded", StringComparison.OrdinalIgnoreCase));
                if (scheduled?.PublishAt is { } at)
                {
                    var local = at.ToOffset(TimeSpan.FromHours(7));
                    return $"Đã có trên Fanpage/YouTube (hẹn giờ {local.ToString("dd'/'MM HH:mm")})";
                }
                return "Đã đăng trên nền tảng";
            }
            return "Đã có job đăng cho mọi nơi đăng phù hợp.";
        }

        if (c.Connectors is { Count: > 0 } only && !targets.Any(t => only.Contains(t.ConnectorType, StringComparer.OrdinalIgnoreCase)))
            return "Chưa có kênh đang bật cho loại kênh của bài (" + string.Join(", ", only) + ").";

        return "Hết khung giờ hoặc hết hạn mức (video ngắn/ngày, video dài/tuần) trong khoảng ngày đã xếp; sẽ được xếp khi có khung mới.";
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

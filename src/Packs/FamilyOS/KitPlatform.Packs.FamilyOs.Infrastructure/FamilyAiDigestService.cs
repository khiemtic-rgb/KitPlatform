using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAiDigestService : IFamilyAiDigestService
{
    private static readonly HashSet<string> WinKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        FamilyMemoryKinds.BeautifulDay,
        FamilyMemoryKinds.StreakMilestone,
        FamilyMemoryKinds.TeamUnlock,
        FamilyMemoryKinds.Reward,
        FamilyMemoryKinds.FirstTime,
        FamilyMemoryKinds.Gratitude,
        FamilyMemoryKinds.Photo,
        FamilyMemoryKinds.Manual,
    };

    private readonly IFamilyMemoryService _memories;
    private readonly IFamilyParentSuccessService _parentSuccess;
    private readonly FamilyGraphRepository _families;

    public FamilyAiDigestService(
        IFamilyMemoryService memories,
        IFamilyParentSuccessService parentSuccess,
        FamilyGraphRepository families)
    {
        _memories = memories;
        _parentSuccess = parentSuccess;
        _families = families;
    }

    public async Task<FamilyAiWinsDigestDto> GetWinsDigestAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var end = to ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(null).DateTime);
        var start = from ?? end.AddDays(-13);
        if (start > end)
            (start, end) = (end, start);

        var cap = Math.Clamp(limit, 1, 30);
        var rows = await _memories.ListAsync(
            familyId, start, end, favoritesOnly: false, limit: 80, cancellationToken);

        var wins = rows
            .Where(m => WinKinds.Contains(m.Kind))
            .Where(m =>
                m.Kind is not (FamilyMemoryKinds.Manual or FamilyMemoryKinds.Photo)
                || m.IsFavorite
                || !string.IsNullOrWhiteSpace(m.NoteVi))
            .OrderByDescending(m => m.IsFavorite)
            .ThenByDescending(m => m.HappenedAt)
            .Take(cap)
            .Select(m => new FamilyAiWinDto(
                m.Id.ToString("D"),
                m.Kind,
                m.TitleVi,
                m.NoteVi,
                m.FlowDate,
                m.Icon ?? IconFor(m.Kind),
                m.IsFavorite,
                m.HappenedAt))
            .ToList();

        var beautiful = rows.Count(m => m.Kind == FamilyMemoryKinds.BeautifulDay);
        var streaks = rows.Count(m => m.Kind == FamilyMemoryKinds.StreakMilestone);
        var gratitude = rows.Count(m => m.Kind == FamilyMemoryKinds.Gratitude);

        var headline = wins.Count == 0
            ? "Chưa có AI Wins trong khoảng này — nhà mình cứ sống, hệ thống sẽ ghi lại."
            : $"AI Wins · {wins.Count} khoảnh khắc đáng nhớ";

        var sub = wins.Count == 0
            ? $"{start:dd/MM}–{end:dd/MM}"
            : string.Join(" · ", new[]
            {
                beautiful > 0 ? $"{beautiful} ngày đẹp" : null,
                streaks > 0 ? $"{streaks} mốc streak" : null,
                gratitude > 0 ? $"{gratitude} lời cảm ơn" : null,
            }.Where(x => x is not null));

        return new FamilyAiWinsDigestDto(start, end, wins.Count, headline, sub ?? "", wins);
    }

    public async Task<FamilyAiLetterDto> GetMonthlyLetterAsync(
        Guid familyId,
        DateOnly? month = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var anchor = month ?? new DateOnly(today.Year, today.Month, 1);
        var periodStart = new DateOnly(anchor.Year, anchor.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        if (periodEnd > today)
            periodEnd = today;

        var recap = await _memories.GetRecapAsync(familyId, periodStart, periodEnd, cancellationToken);
        var rop = await _parentSuccess.GetRopAsync(
            familyId,
            days: 30,
            asOf: periodEnd,
            cancellationToken);

        var familyName = string.IsNullOrWhiteSpace(family.DisplayName)
            ? "nhà mình"
            : family.DisplayName.Trim();
        var monthLabel = $"tháng {periodStart.Month}/{periodStart.Year}";
        var thin = recap.TotalCount < 2 && rop.DataDays < 5;

        var greeting = $"Gửi bố mẹ của {familyName},";

        string body;
        if (thin)
        {
            body =
                $"Trong {monthLabel}, Famixa vẫn đang lắng nghe nhịp nhà. " +
                "Chưa đủ khoảnh khắc để viết một bức thư dày — nhưng mỗi ngày nhẹ hơn một chút đều được ghi nhận. " +
                "Cứ tiếp tục; thư tháng sau sẽ đầy hơn.";
        }
        else
        {
            var nudgeLine = rop.ParentNudgesLate <= rop.ParentNudgesEarly
                ? $"Số lần bố mẹ phải nhắc đang dịu lại ({rop.ParentNudgesEarly} → {rop.ParentNudgesLate})."
                : "Tháng này nhà còn phải nhắc nhiều — không sao, đây là tín hiệu để Coach giúp nhẹ tay hơn.";

            var autonomyLine = rop.SelfStartsLate > rop.SelfStartsEarly
                ? $"Điều có thể chưa nhận ra: con đã tự bắt đầu {rop.SelfStartsLate} lần — nhiều hơn nửa đầu kỳ."
                : "Con vẫn đang học tự bắt đầu; mỗi lần Observe-only là một bước trưởng thành.";

            body =
                $"Trong {monthLabel}, {recap.HeadlineVi} " +
                $"{nudgeLine} {autonomyLine} " +
                "Famixa không chỉ đếm việc xong — mà giữ lại những lần nhà mình nhẹ hơn và gần nhau hơn.";
        }

        var highlights = new List<string>();
        foreach (var h in recap.Highlights.Take(4))
            highlights.Add($"{h.FlowDate:dd/MM}: {h.TitleVi}");
        foreach (var b in rop.GrowthBulletsVi.Take(3))
        {
            if (!highlights.Contains(b))
                highlights.Add(b);
        }
        if (highlights.Count == 0)
            highlights.Add("Tháng này là nền — khoảnh khắc sẽ tới.");

        var closing = thin
            ? "Thân ái,\nFamixa — người bạn đồng hành của bố mẹ."
            : "Có lẽ con đang lớn lên — và bố mẹ cũng đang nhẹ tay hơn.\n\nThân ái,\nFamixa";

        return new FamilyAiLetterDto(
            familyId,
            familyName,
            periodStart,
            periodEnd,
            DateTimeOffset.UtcNow,
            monthLabel,
            greeting,
            body,
            highlights,
            closing,
            thin);
    }

    public async Task<FamilyReplayDto> GetMonthlyReplayAsync(
        Guid familyId,
        DateOnly? month = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var anchor = month ?? new DateOnly(today.Year, today.Month, 1);
        var periodStart = new DateOnly(anchor.Year, anchor.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        if (periodEnd > today)
            periodEnd = today;

        var familyName = string.IsNullOrWhiteSpace(family.DisplayName)
            ? "nhà mình"
            : family.DisplayName.Trim();
        var monthLabel = $"tháng {periodStart.Month}/{periodStart.Year}";

        var letter = await GetMonthlyLetterAsync(familyId, periodStart, cancellationToken);
        var memories = await _memories.ListAsync(
            familyId, periodStart, periodEnd, favoritesOnly: false, limit: 60, cancellationToken);
        var rop = await _parentSuccess.GetRopAsync(familyId, days: 30, asOf: periodEnd, cancellationToken);

        var scenes = new List<FamilyReplaySceneDto>
        {
            new(
                periodStart,
                "🌱",
                $"Mở {monthLabel}",
                $"Famixa bắt đầu ghi lại nhịp của {familyName}.",
                "open"),
        };

        foreach (var m in memories
                     .OrderBy(x => x.FlowDate)
                     .ThenBy(x => x.HappenedAt)
                     .Take(10))
        {
            scenes.Add(new FamilyReplaySceneDto(
                m.FlowDate,
                m.Icon ?? IconFor(m.Kind),
                m.TitleVi,
                string.IsNullOrWhiteSpace(m.NoteVi)
                    ? (m.MemberName is null ? null : m.MemberName)
                    : m.NoteVi,
                m.Kind));
        }

        if (rop.ParentNudgesLate <= rop.ParentNudgesEarly && rop.DataDays >= 5)
        {
            scenes.Add(new FamilyReplaySceneDto(
                periodEnd,
                "🍃",
                "Nhắc dịu lại",
                $"{rop.ParentNudgesEarly} → {rop.ParentNudgesLate} lần nhắc trong kỳ.",
                "growth"));
        }

        if (rop.SelfStartsLate > rop.SelfStartsEarly)
        {
            scenes.Add(new FamilyReplaySceneDto(
                periodEnd,
                "🌱",
                "Con tự bắt đầu nhiều hơn",
                $"{rop.SelfStartsEarly} → {rop.SelfStartsLate} lần tự bắt đầu.",
                "growth"));
        }

        if (rop.QualityMoments > 0)
        {
            scenes.Add(new FamilyReplaySceneDto(
                periodEnd,
                "💌",
                "Khoảnh khắc chất lượng",
                $"{rop.QualityMoments} kỷ niệm / lời cảm ơn được giữ lại.",
                "moment"));
        }

        scenes.Add(new FamilyReplaySceneDto(
            periodEnd,
            "✨",
            "Khép lại tháng",
            letter.IsThinData
                ? "Tháng còn mỏng — nhưng Famixa đã lắng nghe."
                : "Đây không còn là app — đây là kỷ niệm nhà mình.",
            "close"));

        // Cap scenes for shareable length
        if (scenes.Count > 14)
        {
            scenes = scenes
                .Take(1)
                .Concat(scenes.Skip(1).Take(scenes.Count - 2).Take(11))
                .Concat(scenes.TakeLast(1))
                .ToList();
        }

        var thin = letter.IsThinData && memories.Count < 2;
        var title = thin
            ? $"Replay · {monthLabel} (đang mở)"
            : $"Family Replay · {monthLabel}";
        var opening = thin
            ? $"Trong {monthLabel}, {familyName} mới bắt đầu để Famixa ghi lại. Replay sẽ dày hơn khi nhà có thêm kỷ niệm."
            : $"Trong {monthLabel}, hãy xem lại những gì {familyName} đã sống — không phải checklist, mà là những lần nhẹ hơn và gần nhau hơn.";

        var closing = thin
            ? "Tháng sau, Famixa sẽ có nhiều cảnh hơn để kể.\n— Famixa"
            : "Có lẽ con đang lớn lên — và bố mẹ cũng đang nhẹ tay hơn.\n— Famixa";

        var shareLines = new List<string>
        {
            title,
            "",
            opening,
            "",
        };
        foreach (var s in scenes)
        {
            var datePart = s.Date is DateOnly d ? $"{d:dd/MM} · " : "";
            shareLines.Add($"{s.Icon} {datePart}{s.TitleVi}");
            if (!string.IsNullOrWhiteSpace(s.DetailVi))
                shareLines.Add($"   {s.DetailVi}");
        }
        shareLines.Add("");
        shareLines.Add(closing);

        return new FamilyReplayDto(
            familyId,
            familyName,
            periodStart,
            periodEnd,
            DateTimeOffset.UtcNow,
            monthLabel,
            title,
            opening,
            scenes,
            closing,
            string.Join("\n", shareLines),
            thin);
    }

    private static string IconFor(string kind) =>
        kind.ToLowerInvariant() switch
        {
            FamilyMemoryKinds.BeautifulDay => "☀️",
            FamilyMemoryKinds.StreakMilestone => "🔥",
            FamilyMemoryKinds.Gratitude => "💌",
            FamilyMemoryKinds.Photo => "📷",
            FamilyMemoryKinds.TeamUnlock => "🎬",
            FamilyMemoryKinds.Reward => "🎁",
            FamilyMemoryKinds.FirstTime => "🌱",
            _ => "✨",
        };
}

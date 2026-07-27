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

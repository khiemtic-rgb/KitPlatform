using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyParentSuccessService : IFamilyParentSuccessService
{
    private const int MinutesPerNudge = 3;
    private const int MinDataDaysForScore = 5;
    private const int MaxNoteLength = 400;

    private readonly FamilyParentSuccessRepository _repo;
    private readonly FamilyGraphRepository _families;

    public FamilyParentSuccessService(
        FamilyParentSuccessRepository repo,
        FamilyGraphRepository families)
    {
        _repo = repo;
        _families = families;
    }

    public async Task<ParentSuccessRopDto> GetRopAsync(
        Guid familyId,
        int days = 30,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var window = days is 90 or 60 ? days : 30;
        var timezone = family.Timezone;
        var localNow = FamilyTimeZones.NowIn(timezone);
        var end = asOf ?? DateOnly.FromDateTime(localNow.DateTime);
        var start = end.AddDays(-(window - 1));
        var mid = start.AddDays(window / 2);

        var startUtc = ToUtcStart(start, timezone);
        var midUtc = ToUtcStart(mid, timezone);
        var endExclusiveUtc = ToUtcStart(end.AddDays(1), timezone);

        var early = await _repo.CountEventsAsync(familyId, startUtc, midUtc, cancellationToken);
        var late = await _repo.CountEventsAsync(familyId, midUtc, endExclusiveUtc, cancellationToken);
        var qualityEarly = await _repo.CountQualityMomentsAsync(familyId, startUtc, midUtc, cancellationToken);
        var qualityLate = await _repo.CountQualityMomentsAsync(familyId, midUtc, endExclusiveUtc, cancellationToken);
        var dataDays = await _repo.CountDistinctEventDaysAsync(
            familyId, startUtc, endExclusiveUtc, timezone, cancellationToken);

        var isPartial = dataDays < MinDataDaysForScore;
        string? partialNote = isPartial
            ? $"Mới có {dataDays}/{window} ngày tín hiệu Behavior OS — số liệu mang tính tham khảo."
            : null;

        var nudgeEarly = early.ParentNudges;
        var nudgeLate = late.ParentNudges;
        var nudgeDelta = nudgeEarly - nudgeLate;
        var minutesSaved = Math.Max(0, nudgeDelta * MinutesPerNudge);

        var selfEarly = early.SelfStarts;
        var selfLate = late.SelfStarts;
        var selfPct = PctChange(selfEarly, selfLate);

        var remEarly = early.ReminderFired;
        var remLate = late.ReminderFired;

        var doneEarly = early.CommitmentDone;
        var doneLate = late.CommitmentDone;
        var skipEarly = early.CommitmentSkipped;
        var skipLate = late.CommitmentSkipped;

        var conflictProxyEarly = skipEarly + nudgeEarly;
        var conflictProxyLate = skipLate + nudgeLate;
        var conflictPct = PctDrop(conflictProxyEarly, conflictProxyLate);

        var graduations = early.HabitGraduations + late.HabitGraduations;
        var qualityTotal = qualityEarly + qualityLate;

        var metrics = new List<ParentSuccessMetricDto>
        {
            new(
                "nudges",
                "Lần nhắc của bố mẹ",
                $"{nudgeEarly}",
                $"{nudgeLate}",
                FormatDeltaCount(nudgeEarly, nudgeLate, preferDown: true),
                nudgeLate <= nudgeEarly,
                "lần"),
            new(
                "self_start",
                "Con tự bắt đầu",
                $"{selfEarly}",
                $"{selfLate}",
                selfPct is null ? "chưa đủ mẫu" : $"{(selfPct >= 0 ? "+" : "")}{selfPct}%",
                selfLate >= selfEarly,
                "lần"),
            new(
                "reminders",
                "AI/app phải nhắc",
                $"{remEarly}",
                $"{remLate}",
                FormatDeltaCount(remEarly, remLate, preferDown: true),
                remLate <= remEarly,
                "lần"),
            new(
                "conflict_proxy",
                "Căng thẳng (proxy: skip + nhắc)",
                $"{conflictProxyEarly}",
                $"{conflictProxyLate}",
                conflictPct is null ? "chưa đủ mẫu" : $"{(conflictPct >= 0 ? "-" : "+")}{Math.Abs(conflictPct.Value)}%",
                conflictProxyLate <= conflictProxyEarly),
            new(
                "quality",
                "Khoảnh khắc chất lượng",
                $"{qualityEarly}",
                $"{qualityLate}",
                FormatDeltaCount(qualityEarly, qualityLate, preferDown: false),
                qualityLate >= qualityEarly,
                "lần"),
        };

        var growthBullets = new List<string>();
        if (nudgeDelta > 0)
            growthBullets.Add($"Nhắc của bố mẹ giảm {nudgeDelta} lần so với nửa đầu kỳ (~{minutesSaved} phút dịu hơn).");
        if (selfLate > selfEarly)
            growthBullets.Add($"Con tự bắt đầu tăng từ {selfEarly} → {selfLate} lần.");
        if (graduations > 0)
            growthBullets.Add($"{graduations} lần thói quen tiến tới tự chủ / duy trì.");
        if (qualityLate > qualityEarly)
            growthBullets.Add($"Khoảnh khắc ấm (cảm ơn / kỷ niệm) tăng {qualityLate - qualityEarly}.");
        if (conflictPct is > 0)
            growthBullets.Add($"Proxy căng thẳng giảm khoảng {conflictPct}%.");
        if (growthBullets.Count == 0)
            growthBullets.Add("Đang thu thập tín hiệu — hãy dùng thêm vài ngày để ROP rõ hơn.");

        var outcomes = new List<string>();
        if (nudgeLate < nudgeEarly)
            outcomes.Add("Bố mẹ phải nhắc ít hơn");
        if (selfLate > selfEarly)
            outcomes.Add("Con chủ động hơn");
        if (conflictProxyLate < conflictProxyEarly)
            outcomes.Add("Nhà dịu hơn (ít skip + nhắc)");
        if (qualityTotal > 0)
            outcomes.Add($"Có {qualityTotal} khoảnh khắc đáng nhớ");
        if (outcomes.Count == 0)
            outcomes.Add("Famixa đang lắng nghe nhịp nhà — chưa đủ để chốt kết quả");

        var growthScore = isPartial
            ? (int?)null
            : ComputeGrowthScore(nudgeEarly, nudgeLate, selfEarly, selfLate, conflictProxyEarly, conflictProxyLate, qualityEarly, qualityLate);

        var headline = growthScore is >= 70
            ? $"Gia đình đang tốt lên · Growth {growthScore}/100"
            : growthScore is >= 45
                ? $"Đang chuyển động tích cực · Growth {growthScore}/100"
                : isPartial
                    ? "Return on Parenting — đang học nhịp nhà"
                    : growthScore is int gs
                        ? $"Còn dư địa nhẹ nhàng hơn · Growth {gs}/100"
                        : "Return on Parenting";

        var summary =
            $"Trong {window} ngày ({start:dd/MM}–{end:dd/MM}): nhắc bố mẹ {nudgeEarly}→{nudgeLate}, " +
            $"tự bắt đầu {selfEarly}→{selfLate}, proxy căng thẳng {conflictProxyEarly}→{conflictProxyLate}. " +
            (minutesSaved > 0
                ? $"Ước tính tiết kiệm ~{FormatHours(minutesSaved)} nhắc nhở."
                : "Chưa thấy giảm nhắc rõ — Coach sẽ ưu tiên giảm can thiệp.");

        var renewLine = minutesSaved > 0 || (selfLate > selfEarly)
            ? "Đây là ROP của Famixa: bạn đang nuôi dạy nhẹ hơn và hiệu quả hơn — đáng để giữ nhịp."
            : "Tiếp tục vài tuần nữa — khi nhắc ↓ và tự bắt đầu ↑, đây sẽ là bằng chứng gia hạn.";

        return new ParentSuccessRopDto(
            familyId,
            window,
            start,
            end,
            dataDays,
            isPartial,
            partialNote,
            DateTimeOffset.UtcNow,
            growthScore,
            headline,
            summary,
            renewLine,
            metrics,
            growthBullets,
            outcomes,
            minutesSaved,
            nudgeEarly,
            nudgeLate,
            selfEarly,
            selfLate,
            remEarly,
            remLate,
            doneEarly,
            doneLate,
            graduations,
            qualityTotal);
    }

    public async Task<ParentSuccessCheckinDto?> GetEveningCheckinAsync(
        Guid familyId,
        Guid memberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        await EnsureGuardianAsync(familyId, memberId, cancellationToken);

        var date = flowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var row = await _repo.GetCheckinAsync(familyId, memberId, date, cancellationToken);
        return row is null ? null : MapCheckin(row);
    }

    public async Task<ParentSuccessCheckinDto> UpsertEveningCheckinAsync(
        Guid familyId,
        UpsertParentSuccessCheckinRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        await EnsureGuardianAsync(familyId, request.MemberId, cancellationToken);

        var date = request.FlowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var note = NormalizeNote(request.Note);

        var row = await _repo.UpsertCheckinAsync(
            familyId,
            request.MemberId,
            date,
            request.QLessNudge,
            request.QLessTension,
            request.QQualityTime,
            note,
            cancellationToken);

        return MapCheckin(row);
    }

    public async Task<ParentAchievementsDto> ListAchievementsAsync(
        Guid familyId,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var end = asOf ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var start = end.AddDays(-13);
        var mid = start.AddDays(7);
        var timezone = family.Timezone;

        var startUtc = ToUtcStart(start, timezone);
        var midUtc = ToUtcStart(mid, timezone);
        var endExclusiveUtc = ToUtcStart(end.AddDays(1), timezone);

        var early = await _repo.CountEventsAsync(familyId, startUtc, midUtc, cancellationToken);
        var late = await _repo.CountEventsAsync(familyId, midUtc, endExclusiveUtc, cancellationToken);
        var quality = await _repo.CountQualityMomentsAsync(
            familyId, startUtc, endExclusiveUtc, cancellationToken);
        var yesNudgeDays = await _repo.CountPositiveCheckinsAsync(
            familyId, null, end.AddDays(-6), end, "q_less_nudge", cancellationToken);

        var lighterUnlocked = late.ParentNudges < early.ParentNudges || yesNudgeDays >= 3;
        var selfUnlocked = late.SelfStarts > early.SelfStarts;
        var qualityUnlocked = quality >= 3;

        var items = new List<ParentAchievementDto>
        {
            new(
                "parent_lighter_hand",
                "Nhẹ tay hơn",
                lighterUnlocked
                    ? "Nhà đang nhắc ít hơn — hoặc bạn đã cảm nhận được điều đó."
                    : "Khi số lần nhắc dịu lại, Famixa ghi nhận nhẹ tay của bố mẹ.",
                "🍃",
                lighterUnlocked,
                lighterUnlocked
                    ? "Đã mở"
                    : yesNudgeDays > 0
                        ? $"Check-in “ít nhắc hơn”: {yesNudgeDays}/3 ngày gần đây"
                        : $"Nhắc nửa sau kỳ: {late.ParentNudges} · nửa đầu: {early.ParentNudges}"),
            new(
                "parent_self_start_seen",
                "Con tự bắt đầu",
                selfUnlocked
                    ? "Con đã tự bắt đầu nhiều hơn — tín hiệu trưởng thành thật."
                    : "Khi con tự bắt đầu nhiều hơn, đây là thành tựu của cả nhà.",
                "🌱",
                selfUnlocked,
                selfUnlocked
                    ? "Đã mở"
                    : $"Tự bắt đầu: {early.SelfStarts} → {late.SelfStarts} (14 ngày)"),
            new(
                "parent_quality_moment",
                "Có kỷ niệm",
                qualityUnlocked
                    ? "Đã có khoảnh khắc chất lượng — Memory đang giữ lại."
                    : "Cảm ơn, ảnh, ngày đẹp… vài khoảnh khắc là đủ để mở.",
                "💌",
                qualityUnlocked,
                qualityUnlocked
                    ? "Đã mở"
                    : $"Khoảnh khắc chất lượng 14 ngày: {quality}/3"),
        };

        var unlocked = items.Count(i => i.Unlocked);
        var headline = unlocked == 0
            ? "Ghi nhận nhẹ cho bố mẹ — không phải bảng xếp hạng."
            : unlocked == 3
                ? "Ba ghi nhận đang mở — nhà mình đang đi đúng hướng Trust."
                : $"{unlocked}/3 ghi nhận đang mở — cứ nhẹ thôi.";

        return new ParentAchievementsDto(familyId, end, headline, items);
    }

    public async Task<ParentCoachActedDto> RecordCoachActedAsync(
        Guid familyId,
        ParentCoachActedRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        await EnsureGuardianAsync(familyId, request.MemberId, cancellationToken);

        var tipId = (request.TipId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tipId))
            throw new InvalidOperationException("tipId là bắt buộc.");
        if (tipId.Length > 80)
            tipId = tipId[..80];

        var date = request.FlowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);

        var acted = await _repo.ListCoachActedTipIdsAsync(
            familyId, request.MemberId, date, family.Timezone, cancellationToken);

        if (acted.Any(x => string.Equals(x, tipId, StringComparison.OrdinalIgnoreCase)))
        {
            return new ParentCoachActedDto(
                familyId,
                request.MemberId,
                date,
                tipId,
                AlreadyActed: true,
                "Famixa đã ghi nhận bạn thử gợi ý này hôm nay.",
                acted);
        }

        var title = string.IsNullOrWhiteSpace(request.TitleVi)
            ? null
            : request.TitleVi.Trim().Length <= 120
                ? request.TitleVi.Trim()
                : request.TitleVi.Trim()[..120];

        await _repo.InsertCoachActedAsync(
            familyId,
            request.MemberId,
            date,
            tipId,
            TrimOrNull(request.TipSource, 40),
            TrimOrNull(request.Slot, 24),
            title,
            cancellationToken);

        var next = acted.Append(tipId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new ParentCoachActedDto(
            familyId,
            request.MemberId,
            date,
            tipId,
            AlreadyActed: false,
            "Cảm ơn — Famixa ghi lại để học cách đồng hành tốt hơn.",
            next);
    }

    public async Task<ParentCoachActedDto> ListCoachActedTodayAsync(
        Guid familyId,
        Guid memberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        await EnsureGuardianAsync(familyId, memberId, cancellationToken);

        var date = flowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var acted = await _repo.ListCoachActedTipIdsAsync(
            familyId, memberId, date, family.Timezone, cancellationToken);

        return new ParentCoachActedDto(
            familyId,
            memberId,
            date,
            TipId: "",
            AlreadyActed: acted.Count > 0,
            acted.Count == 0
                ? "Chưa thử gợi ý nào hôm nay."
                : $"Đã thử {acted.Count} gợi ý hôm nay.",
            acted);
    }

    private static string? TrimOrNull(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length <= max ? t : t[..max];
    }

    private async Task EnsureGuardianAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.");
        var role = (member.RoleCode ?? "").ToLowerInvariant();
        if (role is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver))
            throw new InvalidOperationException("Chỉ bố/mẹ hoặc người chăm sóc mới dùng Famixa Parent Success.");
    }

    private static ParentSuccessCheckinDto MapCheckin(FamilyParentSuccessRepository.CheckinRow row)
    {
        var yes = (row.QLessNudge ? 1 : 0) + (row.QLessTension ? 1 : 0) + (row.QQualityTime ? 1 : 0);
        var reflection = yes switch
        {
            3 => "Ba tín hiệu đều tốt — Famixa giữ lại ngày này như một trang nhẹ.",
            2 => "Hai trên ba — nhà đang nghiêng về phía nhẹ hơn.",
            1 => "Một tín hiệu tốt đã đủ để ghi nhận. Ngày mai thử thêm một chút.",
            _ => "Hôm nay chưa nhẹ — không sao. Famixa vẫn ở đây, không chấm điểm bố mẹ.",
        };

        return new ParentSuccessCheckinDto(
            row.Id,
            row.FamilyId,
            row.MemberId,
            row.FlowDate,
            row.QLessNudge,
            row.QLessTension,
            row.QQualityTime,
            row.Note,
            row.UpdatedAt,
            reflection);
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength];
    }

    private static DateTimeOffset ToUtcStart(DateOnly localDate, string? timezoneId)
    {
        var tz = FamilyTimeZones.Resolve(timezoneId);
        var local = new DateTime(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static int? PctChange(int before, int after)
    {
        if (before <= 0 && after <= 0) return null;
        if (before <= 0) return 100;
        return (int)Math.Round((after - before) * 100.0 / before);
    }

    private static int? PctDrop(int before, int after)
    {
        if (before <= 0 && after <= 0) return null;
        if (before <= 0) return after == 0 ? 0 : -100;
        return (int)Math.Round((before - after) * 100.0 / before);
    }

    private static string FormatDeltaCount(int before, int after, bool preferDown)
    {
        var d = after - before;
        if (d == 0) return "không đổi";
        if (preferDown)
            return d < 0 ? $"↓ {Math.Abs(d)}" : $"↑ {d}";
        return d > 0 ? $"↑ {d}" : $"↓ {Math.Abs(d)}";
    }

    private static string FormatHours(int minutes)
    {
        if (minutes < 60) return $"{minutes} phút";
        var h = minutes / 60.0;
        return h >= 10 ? $"{Math.Round(h)} giờ" : $"{h:0.#} giờ";
    }

    private static int ComputeGrowthScore(
        int nudgeEarly, int nudgeLate,
        int selfEarly, int selfLate,
        int conflictEarly, int conflictLate,
        int qualityEarly, int qualityLate)
    {
        double score = 50;
        if (nudgeEarly > 0)
            score += Math.Clamp((nudgeEarly - nudgeLate) * 100.0 / nudgeEarly, -20, 25);
        else if (nudgeLate == 0)
            score += 8;

        if (selfEarly > 0)
            score += Math.Clamp((selfLate - selfEarly) * 100.0 / selfEarly, -15, 20);
        else if (selfLate > 0)
            score += 12;

        if (conflictEarly > 0)
            score += Math.Clamp((conflictEarly - conflictLate) * 100.0 / conflictEarly, -15, 20);

        if (qualityLate > qualityEarly)
            score += Math.Min(10, (qualityLate - qualityEarly) * 3);

        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }
}

using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyRelationshipService : IFamilyRelationshipService
{
    private readonly FamilyParentVoiceRepository _voice;
    private readonly FamilyRelationshipTriggerStateRepository _triggerStates;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyTeamUnlockService _team;
    private readonly IFamilyTeamNudgeService _nudges;
    private readonly IFamilyGratitudeService _gratitude;
    private readonly IFamilyMemoryService _memories;
    private readonly FamilyCalendarPeriodRepository _periods;
    private readonly FamilyBlueprintRepository _blueprint;
    private readonly ITenantContext _tenant;

    public FamilyRelationshipService(
        FamilyParentVoiceRepository voice,
        FamilyRelationshipTriggerStateRepository triggerStates,
        FamilyGraphRepository families,
        IFamilyTeamUnlockService team,
        IFamilyTeamNudgeService nudges,
        IFamilyGratitudeService gratitude,
        IFamilyMemoryService memories,
        FamilyCalendarPeriodRepository periods,
        FamilyBlueprintRepository blueprint,
        ITenantContext tenant)
    {
        _voice = voice;
        _triggerStates = triggerStates;
        _families = families;
        _team = team;
        _nudges = nudges;
        _gratitude = gratitude;
        _memories = memories;
        _periods = periods;
        _blueprint = blueprint;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FamilyRelationshipTriggerDto>> ListTriggersAsync(
        Guid familyId,
        Guid forMemberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var viewer = members.FirstOrDefault(m => m.Id == forMemberId)
            ?? throw new InvalidOperationException("forMemberId không thuộc gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;
        var team = await _team.GetTeamDayAsync(familyId, date, cancellationToken);
        var ranked = new List<(int Rank, FamilyRelationshipTriggerDto T)>();

        var isParent =
            viewer.RoleCode.Equals(FamilyMembershipRoles.Guardian, StringComparison.OrdinalIgnoreCase)
            || viewer.RoleCode.Equals(FamilyMembershipRoles.Caregiver, StringComparison.OrdinalIgnoreCase);
        var isChild = viewer.RoleCode.Equals(FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase);

        if (isParent)
        {
            var season = await ResolveSeasonAsync(familyId, date, cancellationToken);
            await AddParentTriggersAsync(
                ranked, familyId, viewer, members, team, date, season, cancellationToken);
        }
        else if (isChild)
        {
            await AddChildTriggersAsync(
                ranked, familyId, viewer, team, date, cancellationToken);
        }

        return ranked
            .OrderBy(x => x.Rank)
            .Select(x => x.T)
            .Take(2)
            .ToList();
    }

    public async Task<IReadOnlyList<FamilyRelationshipTriggerStateDto>> ListTriggerStatesAsync(
        Guid familyId,
        Guid viewerMemberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        if (members.All(m => m.Id != viewerMemberId))
            throw new InvalidOperationException("viewerMemberId không thuộc gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;
        var rows = await _triggerStates.ListAsync(familyId, viewerMemberId, date, cancellationToken);
        return rows.Select(MapTriggerState).ToList();
    }

    public async Task<FamilyRelationshipTriggerStateDto> UpsertTriggerStateAsync(
        Guid familyId,
        FamilyRelationshipTriggerStateUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var state = (request.State ?? "").Trim().ToLowerInvariant();
        if (!FamilyRelationshipTriggerUiStates.All.Contains(state))
            throw new InvalidOperationException("state phải là opened | dismissed | sent.");

        var code = (request.TriggerCode ?? "").Trim().ToLowerInvariant();
        if (code.Length == 0 || code.Length > 48)
            throw new InvalidOperationException("triggerCode không hợp lệ.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var viewer = members.FirstOrDefault(m => m.Id == request.ViewerMemberId)
            ?? throw new InvalidOperationException("viewerMemberId không thuộc gia đình.");

        var viewerIsAdult =
            viewer.RoleCode.Equals(FamilyMembershipRoles.Guardian, StringComparison.OrdinalIgnoreCase)
            || viewer.RoleCode.Equals(FamilyMembershipRoles.Caregiver, StringComparison.OrdinalIgnoreCase);
        if (!viewerIsAdult)
            throw new InvalidOperationException("Chỉ bố/mẹ / caregiver lưu trạng thái trigger parent voice.");

        if (request.ToMemberId is Guid toId)
        {
            if (members.All(m => m.Id != toId))
                throw new InvalidOperationException("toMemberId không thuộc gia đình.");
            if (toId == viewer.Id)
                throw new InvalidOperationException("toMemberId không được trùng viewer.");
        }

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = request.FlowDate ?? today;

        string? draft = request.DraftBodyVi?.Trim();
        if (draft is { Length: > 380 }) draft = draft[..380];
        string? title = request.TitleVi?.Trim();
        if (title is { Length: > 160 }) title = title[..160];
        string? body = request.BodyVi?.Trim();
        if (body is { Length: > 380 }) body = body[..380];
        string? template = request.TemplateCode?.Trim().ToLowerInvariant();
        if (template is { Length: > 32 }) template = template[..32];

        var row = await _triggerStates.UpsertAsync(
            familyId,
            viewer.Id,
            date,
            code,
            request.ToMemberId,
            state,
            draft,
            template,
            title,
            body,
            cancellationToken);

        return MapTriggerState(row);
    }

    public async Task<IReadOnlyList<FamilyParentVoiceDto>> ListParentVoiceAsync(
        Guid familyId,
        Guid? forMemberId = null,
        Guid? fromMemberId = null,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var rows = await _voice.ListAsync(
            familyId, forMemberId, fromMemberId, flowDate, cancellationToken);
        return rows.Select(MapVoice).ToList();
    }

    public async Task<FamilyParentVoiceDto> SendParentVoiceAsync(
        Guid familyId,
        FamilyParentVoiceSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var template = (request.TemplateCode ?? "").Trim().ToLowerInvariant();
        if (!FamilyParentVoiceTemplates.All.Contains(template))
            throw new InvalidOperationException("templateCode không hợp lệ.");

        var body = (request.BodyVi ?? "").Trim();
        if (body.Length == 0)
            throw new InvalidOperationException("Nội dung lời gửi trống.");
        if (body.Length > 380)
            body = body[..380];

        if (request.FromMemberId == request.ToMemberId)
            throw new InvalidOperationException("Không thể gửi lời cho chính mình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var from = members.FirstOrDefault(m => m.Id == request.FromMemberId)
            ?? throw new InvalidOperationException("fromMemberId không thuộc gia đình.");
        var to = members.FirstOrDefault(m => m.Id == request.ToMemberId)
            ?? throw new InvalidOperationException("toMemberId không thuộc gia đình.");

        var fromIsParent =
            from.RoleCode.Equals(FamilyMembershipRoles.Guardian, StringComparison.OrdinalIgnoreCase)
            || from.RoleCode.Equals(FamilyMembershipRoles.Caregiver, StringComparison.OrdinalIgnoreCase);
        if (!fromIsParent)
            throw new InvalidOperationException("Chỉ bố/mẹ (guardian/caregiver) mới gửi lời parent voice.");

        var toIsChild = to.RoleCode.Equals(FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase);
        var toIsAdult =
            to.RoleCode.Equals(FamilyMembershipRoles.Guardian, StringComparison.OrdinalIgnoreCase)
            || to.RoleCode.Equals(FamilyMembershipRoles.Caregiver, StringComparison.OrdinalIgnoreCase);

        if (toIsChild)
        {
            if (!FamilyParentVoiceTemplates.ChildFacing.Contains(template))
                throw new InvalidOperationException("templateCode không hợp lệ cho lời gửi tới con.");
        }
        else if (toIsAdult)
        {
            if (!FamilyParentVoiceTemplates.AdultFacing.Contains(template))
                throw new InvalidOperationException(
                    "Lời tới bố/mẹ / caregiver dùng thanks_partner | help_offer | warm_adult | custom.");
        }
        else
            throw new InvalidOperationException("Chỉ gửi lời tới con hoặc bố/mẹ / caregiver.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = request.FlowDate ?? today;

        var id = await _voice.InsertAsync(
            familyId, date, from.Id, to.Id, template, body, cancellationToken);

        var row = await _voice.GetAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được lời gửi.");

        try
        {
            var fromLabel = ParentLabel(from.DisplayName);
            var title = toIsAdult
                ? $"{fromLabel} gửi lời tới {ShortName(to.DisplayName)}"
                : $"{fromLabel} gửi lời tới {ShortName(to.DisplayName)}";
            var icon = template switch
            {
                FamilyParentVoiceTemplates.Encourage => "🌿",
                FamilyParentVoiceTemplates.ThanksPartner => "🤝",
                FamilyParentVoiceTemplates.HelpOffer => "🤲",
                FamilyParentVoiceTemplates.Birthday => "🎂",
                _ => "❤️",
            };
            await _memories.TryCaptureAsync(
                _tenant.TenantId,
                familyId,
                date,
                FamilyMemoryKinds.ParentVoice,
                title,
                noteVi: body,
                icon: icon,
                sourceRef: id.ToString("D"),
                memberId: to.Id,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Memory is best-effort.
        }

        return MapVoice(row);
    }

    public async Task AckParentVoiceAsync(
        Guid familyId,
        Guid messageId,
        FamilyParentVoiceAckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (!FamilyParentVoiceStatuses.Ack.Contains(status))
            throw new InvalidOperationException("status ack phải là read hoặc thanks.");

        if (!await _voice.AckAsync(familyId, messageId, status, cancellationToken))
            throw new InvalidOperationException("Không tìm thấy lời gửi hoặc đã xác nhận.");
    }

    private static readonly string[] EveningPrompts =
    [
        "Điều gì hôm nay khiến bạn vui nhất?",
        "Hôm nay bạn muốn cảm ơn ai trong nhà?",
        "Khoảnh khắc nào hôm nay đáng nhớ nhất?",
        "Ai đã giúp bạn hôm nay?",
        "Một điều nhỏ bạn muốn nói với cả nhà?",
        "Hôm nay bạn tự hào về điều gì?",
        "Điều gì làm bạn cười hôm nay?",
    ];

    public async Task<FamilyEveningCircleDto> GetEveningCircleAsync(
        Guid familyId,
        Guid? forMemberId = null,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;
        var prompt = EveningPrompts[Math.Abs(date.DayNumber) % EveningPrompts.Length];

        var rows = await _memories.ListAsync(
            familyId, date, date, favoritesOnly: false, limit: 80, cancellationToken: cancellationToken);
        var answers = rows
            .Where(m => string.Equals(m.Kind, FamilyMemoryKinds.EveningCircle, StringComparison.OrdinalIgnoreCase))
            .Where(m => m.MemberId.HasValue)
            .Select(m => new FamilyEveningCircleAnswerDto(
                m.Id,
                m.MemberId!.Value,
                m.MemberName ?? "Thành viên",
                m.NoteVi ?? m.TitleVi,
                m.HappenedAt))
            .ToList();

        var already = forMemberId is Guid mid
            && answers.Any(a => a.MemberId == mid);

        return new FamilyEveningCircleDto(date, prompt, already, answers);
    }

    public async Task<FamilyEveningCircleDto> AnswerEveningCircleAsync(
        Guid familyId,
        FamilyEveningCircleAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == request.MemberId)
            ?? throw new InvalidOperationException("memberId không thuộc gia đình.");

        var answer = (request.AnswerVi ?? "").Trim();
        if (answer.Length == 0)
            throw new InvalidOperationException("Câu trả lời trống.");
        if (answer.Length > 280)
            answer = answer[..280];

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = request.FlowDate ?? today;
        var prompt = EveningPrompts[Math.Abs(date.DayNumber) % EveningPrompts.Length];
        var sourceRef = $"evening_circle:{date:yyyy-MM-dd}:{member.Id:D}";

        var existing = await GetEveningCircleAsync(familyId, member.Id, date, cancellationToken);
        if (existing.AlreadyAnswered)
            return existing;

        await _memories.TryCaptureAsync(
            _tenant.TenantId,
            familyId,
            date,
            FamilyMemoryKinds.EveningCircle,
            prompt,
            noteVi: $"{ShortName(member.DisplayName)}: {answer}",
            icon: "⭐",
            sourceRef: sourceRef,
            memberId: member.Id,
            cancellationToken: cancellationToken);

        return await GetEveningCircleAsync(familyId, member.Id, date, cancellationToken);
    }

    public async Task<FamilyWeeklyStoryDto> GetWeeklyStoryAsync(
        Guid familyId,
        DateOnly? asOf = null,
        Guid? forMemberId = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var end = asOf ?? today;
        var start = StartOfWeekMonday(end);

        if (forMemberId is Guid memberId)
        {
            return await BuildChildWeeklyStoryAsync(
                familyId, memberId, start, end, cancellationToken);
        }

        var rows = await _memories.ListAsync(
            familyId, start, end, favoritesOnly: false, limit: 120, cancellationToken: cancellationToken);

        int CountKind(string kind) =>
            rows.Count(m => string.Equals(m.Kind, kind, StringComparison.OrdinalIgnoreCase));

        var voice = CountKind(FamilyMemoryKinds.ParentVoice);
        var help = CountKind(FamilyMemoryKinds.Help);
        var gratitude = CountKind(FamilyMemoryKinds.Gratitude);
        var ritual = CountKind(FamilyMemoryKinds.ParentHabit);
        var streak = CountKind(FamilyMemoryKinds.StreakMilestone);
        var circle = CountKind(FamilyMemoryKinds.EveningCircle);

        var lines = new List<FamilyWeeklyStoryLineDto>();
        if (voice > 0)
            lines.Add(new("❤️", $"{voice} lời từ bố/mẹ gửi tới con", null));
        if (help > 0)
            lines.Add(new("🤝", $"{help} lần anh chị cổ vũ nhau", null));
        if (gratitude > 0)
            lines.Add(new("💌", $"{gratitude} lời cảm ơn", null));
        if (ritual > 0)
            lines.Add(new("🌿", $"{ritual} ritual bố mẹ", null));
        if (streak > 0)
            lines.Add(new("🔥", $"{streak} mốc streak được ghi lại", null));
        if (circle > 0)
            lines.Add(new("⭐", $"{circle} câu trả lời Evening Circle", null));

        if (lines.Count == 0)
        {
            lines.Add(new(
                "🌱",
                "Tuần này nhà mình chưa ghi nhiều khoảnh khắc — một lời ấm cũng đủ bắt đầu.",
                null));
        }

        var totalWarm = voice + help + gratitude + circle;
        var headline = totalWarm > 0
            ? $"Tuần này nhà mình đã nói chuyện với nhau {totalWarm} lần."
            : "Tuần này Famixa đang chờ những khoảnh khắc nhà mình.";

        return new FamilyWeeklyStoryDto(
            start,
            end,
            headline,
            voice,
            help,
            gratitude,
            ritual,
            streak,
            circle,
            lines);
    }

    /// <summary>
    /// Kid home: week story scoped to one child so Huy/Nhi don't see the same household totals.
    /// </summary>
    private async Task<FamilyWeeklyStoryDto> BuildChildWeeklyStoryAsync(
        Guid familyId,
        Guid memberId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var child = members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException("forMemberId không thuộc gia đình.");
        var shortName = ShortName(child.DisplayName);

        var voice = 0;
        var cheerIn = 0;
        var cheerOut = 0;
        var thanksIn = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var dayVoices = await _voice.ListAsync(
                familyId, memberId, fromMemberId: null, flowDate: d, cancellationToken);
            voice += dayVoices.Count(v =>
                v.ToMemberId == memberId
                && !string.Equals(v.Status, "draft", StringComparison.OrdinalIgnoreCase));

            var dayNudges = await _nudges.ListAsync(familyId, d, memberId, cancellationToken);
            foreach (var n in dayNudges)
            {
                if (string.Equals(n.Status, FamilyTeamNudgeStatuses.Draft, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Status, FamilyTeamNudgeStatuses.Deferred, StringComparison.OrdinalIgnoreCase))
                    continue;

                var isThanksBack = string.Equals(
                    n.TemplateCode, FamilyTeamNudgeTemplates.ThanksBack, StringComparison.OrdinalIgnoreCase);
                if (n.ToMemberId == memberId)
                {
                    if (isThanksBack) thanksIn++;
                    else cheerIn++;
                }
                else if (n.FromMemberId == memberId && !isThanksBack)
                {
                    cheerOut++;
                }
            }
        }

        var mine = await _memories.ListAsync(
            familyId,
            start,
            end,
            favoritesOnly: false,
            limit: 80,
            memberId: memberId,
            cancellationToken: cancellationToken);

        int CountMine(string kind) =>
            mine.Count(m => string.Equals(m.Kind, kind, StringComparison.OrdinalIgnoreCase));

        var gratitude = CountMine(FamilyMemoryKinds.Gratitude);
        var circle = CountMine(FamilyMemoryKinds.EveningCircle);
        var streak = CountMine(FamilyMemoryKinds.StreakMilestone);
        var ritual = 0; // household ritual — không gán cho từng con
        var help = cheerIn + cheerOut; // sibling bond involving this child

        var lines = new List<FamilyWeeklyStoryLineDto>();
        if (voice > 0)
            lines.Add(new("❤️", $"{voice} lời bố/mẹ gửi riêng tới {shortName}", null));
        if (cheerIn > 0)
            lines.Add(new("💛", $"{cheerIn} lần anh/chị cổ vũ {shortName}", null));
        if (cheerOut > 0)
            lines.Add(new("🤝", $"{cheerOut} lần {shortName} cổ vũ anh/chị", null));
        if (thanksIn > 0)
            lines.Add(new("💌", $"{thanksIn} lời cảm ơn gửi tới {shortName}", null));
        if (gratitude > 0)
            lines.Add(new("💖", $"{gratitude} lần {shortName} cảm ơn bố/mẹ", null));
        if (circle > 0)
            lines.Add(new("⭐", $"{circle} câu Evening Circle của {shortName}", null));
        if (streak > 0)
            lines.Add(new("🔥", $"{streak} mốc streak của {shortName}", null));

        if (lines.Count == 0)
        {
            lines.Add(new(
                "🌱",
                $"Tuần này {shortName} chưa có nhiều khoảnh khắc riêng — một lời ấm cũng đủ bắt đầu.",
                null));
        }

        var totalWarm = voice + cheerIn + cheerOut + thanksIn + gratitude + circle;
        var headline = totalWarm > 0
            ? $"Tuần này {shortName} đã có {totalWarm} khoảnh khắc gắn kết."
            : $"Tuần này Famixa đang chờ khoảnh khắc riêng của {shortName}.";

        return new FamilyWeeklyStoryDto(
            start,
            end,
            headline,
            voice,
            help,
            gratitude,
            ritual,
            streak,
            circle,
            lines);
    }

    private static DateOnly StartOfWeekMonday(DateOnly day)
    {
        var diff = ((int)day.DayOfWeek + 6) % 7; // Monday=0
        return day.AddDays(-diff);
    }

    private async Task AddParentTriggersAsync(
        List<(int Rank, FamilyRelationshipTriggerDto T)> ranked,
        Guid familyId,
        FamilyGraphRepository.MembershipRow viewer,
        IReadOnlyList<FamilyGraphRepository.MembershipRow> members,
        FamilyTeamDayDto team,
        DateOnly date,
        SeasonBias season,
        CancellationToken cancellationToken)
    {
        var children = members
            .Where(m => m.RoleCode.Equals(FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.SortOrder)
            .ToList();
        if (children.Count == 0) return;

        var parentLabel = ParentLabel(viewer.DisplayName);
        var adults = members
            .Where(m =>
                m.Id != viewer.Id
                && (m.RoleCode.Equals(FamilyMembershipRoles.Guardian, StringComparison.OrdinalIgnoreCase)
                    || m.RoleCode.Equals(FamilyMembershipRoles.Caregiver, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(m => m.SortOrder)
            .ToList();

        // P1.9 — unread partner voice inbox.
        var unreadPartner = (await _voice.ListAsync(
                familyId, forMemberId: viewer.Id, fromMemberId: null, flowDate: date, cancellationToken))
            .Where(v => string.Equals(v.Status, FamilyParentVoiceStatuses.Sent, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (unreadPartner.Count > 0)
        {
            var first = unreadPartner[0];
            ranked.Add((-1, new FamilyRelationshipTriggerDto(
                "partner_voice_inbox",
                $"Lời từ {ParentLabel(first.FromMemberName)}",
                first.BodyVi,
                "Đọc lời",
                first.FromMemberId,
                first.FromMemberName,
                null,
                null,
                "inbox:partner_unread")));
        }

        // P1.9 — thank / care co-parent (opt-in soft card).
        if (adults.Count > 0)
        {
            var partner = adults[0];
            var already = await _voice.HasSentTodayAsync(
                familyId, viewer.Id, partner.Id, date, null, cancellationToken);
            if (!already)
            {
                var shortP = ShortName(partner.DisplayName);
                ranked.Add((4, new FamilyRelationshipTriggerDto(
                    FamilyRelationshipTriggerCodes.ThankPartner,
                    $"Gửi một lời ấm tới {shortP}",
                    "Cảm ơn / phụ việc nhẹ — không nói % việc do ai.",
                    "Gửi lời",
                    partner.Id,
                    partner.DisplayName,
                    $"{shortP} ơi, cảm ơn hôm nay mình cùng giữ nhà nhé.",
                    FamilyParentVoiceTemplates.ThanksPartner,
                    season.Tag("partner:care"))));
            }
        }

        // P1.10 — birthday window ±1 day (family TZ).
        foreach (var child in children)
        {
            if (child.DateOfBirth is not DateOnly dob) continue;
            if (!IsBirthdayWindow(dob, date)) continue;
            if (await _voice.HasSentTodayAsync(
                    familyId, viewer.Id, child.Id, date,
                    FamilyParentVoiceTemplates.Birthday, cancellationToken))
                continue;

            var shortChild = ShortName(child.DisplayName);
            var age = date.Year - dob.Year;
            if (dob.AddYears(age) > date) age--;
            ranked.Add((0, new FamilyRelationshipTriggerDto(
                FamilyRelationshipTriggerCodes.BirthdayWish,
                age > 0
                    ? $"Hôm nay gần sinh nhật {shortChild} ({age} tuổi)"
                    : $"Hôm nay gần sinh nhật {shortChild}",
                "Chọn một ý chúc / thưởng — bạn gửi, Famixa không tự gửi.",
                "Chọn lời chúc",
                child.Id,
                child.DisplayName,
                $"{shortChild} ơi, sinh nhật vui vẻ! {parentLabel} thương con nhiều.",
                FamilyParentVoiceTemplates.Birthday,
                season.Tag($"birthday:{dob:MM-dd}"),
                IsGolden: true)));
            break;
        }

        var candidates = new List<(int ColdDays, int Streak, int Open, int Total, bool DoneToday, FamilyGraphRepository.MembershipRow Child)>();

        foreach (var child in children)
        {
            var slice = team.Children.FirstOrDefault(s => s.MemberId == child.Id);
            var open = slice?.Open ?? 0;
            var total = slice?.Total ?? 0;
            var doneToday = total > 0 && open == 0;
            var streak = await _voice.CountChildDoneStreakAsync(
                familyId, child.Id, date, cancellationToken);
            var cold = await _voice.DaysSinceLastFromAsync(
                familyId, viewer.Id, child.Id, date, cancellationToken);
            candidates.Add((cold, streak, open, total, doneToday, child));
        }

        // P1.8 — season ranks: exam → encourage first; summer/holiday → warm/play + golden.
        var rankGolden = 0;
        var rankPraise = season.IsExam ? 2 : 1;
        var rankEncourage = season.IsExam ? 0 : 2;
        var rankWarm = season.IsPlaySeason ? 1 : 5;

        // P1.1 — golden: team finished today.
        if (team.TeamComplete && team.TeamTotal > 0)
        {
            var target = candidates
                .OrderByDescending(x => x.ColdDays)
                .ThenByDescending(x => x.Streak)
                .FirstOrDefault();
            if (target.Child is not null
                && !await _voice.HasSentTodayAsync(
                    familyId, viewer.Id, target.Child.Id, date,
                    FamilyParentVoiceTemplates.Praise, cancellationToken))
            {
                var shortChild = ShortName(target.Child.DisplayName);
                ranked.Add((rankGolden, new FamilyRelationshipTriggerDto(
                    FamilyRelationshipTriggerCodes.TeamEarlyFinish,
                    "Cả nhà vừa xong ngày hôm nay",
                    $"Đừng bỏ lỡ — một lời của {parentLabel.ToLowerInvariant()} sẽ làm khoảnh khắc này đáng nhớ.",
                    "Gửi lời chúc mừng",
                    target.Child.Id,
                    target.Child.DisplayName,
                    $"{shortChild} ơi, cả nhà mình xong rồi! {parentLabel} tự hào lắm.",
                    FamilyParentVoiceTemplates.Praise,
                    season.Tag("golden:team_early_finish"),
                    IsGolden: true)));
            }
        }

        // Prefer cold edge + high streak for praise (boost golden milestones).
        foreach (var c in candidates.OrderByDescending(x => x.Streak).ThenByDescending(x => x.ColdDays))
        {
            if (c.Streak < 1) continue;
            if (await _voice.HasSentTodayAsync(
                    familyId, viewer.Id, c.Child.Id, date,
                    FamilyParentVoiceTemplates.Praise, cancellationToken))
                continue;

            var shortChild = ShortName(c.Child.DisplayName);
            var isMilestone = c.DoneToday && (c.Streak is 7 or 14 or 21 or 30);
            var isFirstDay = c.DoneToday && c.Streak == 1;

            if (isMilestone)
            {
                ranked.Add((rankGolden, new FamilyRelationshipTriggerDto(
                    FamilyRelationshipTriggerCodes.PraiseStreak,
                    $"Đây là ngày thứ {c.Streak} {shortChild} giữ nhịp",
                    $"Đừng bỏ lỡ cơ hội khen con — lời của {parentLabel.ToLowerInvariant()} quan trọng hơn phần thưởng.",
                    "Gửi lời khen",
                    c.Child.Id,
                    c.Child.DisplayName,
                    $"{shortChild} ơi, con tiến bộ nhiều lắm. {parentLabel} rất tự hào.",
                    FamilyParentVoiceTemplates.Praise,
                    season.Tag($"golden:streak:{c.Streak}"),
                    IsGolden: true)));
                break;
            }

            if (isFirstDay)
            {
                ranked.Add((rankGolden, new FamilyRelationshipTriggerDto(
                    FamilyRelationshipTriggerCodes.FirstDayComplete,
                    $"{shortChild} vừa hoàn thành cả ngày lần đầu gần đây",
                    $"Một lời khen nhỏ của {parentLabel.ToLowerInvariant()} sẽ gắn khoảnh khắc này.",
                    "Gửi lời khen",
                    c.Child.Id,
                    c.Child.DisplayName,
                    $"{shortChild} ơi, hôm nay con làm rất tốt. {parentLabel} thấy rồi nhé.",
                    FamilyParentVoiceTemplates.Praise,
                    season.Tag("golden:first_day_complete"),
                    IsGolden: true)));
                break;
            }

            if (c.Streak < 7) continue;

            ranked.Add((rankPraise, new FamilyRelationshipTriggerDto(
                FamilyRelationshipTriggerCodes.PraiseStreak,
                $"{shortChild} đã hoàn thành {c.Streak} ngày liên tiếp",
                $"Một lời khen của {parentLabel.ToLowerInvariant()} hôm nay sẽ có ý nghĩa hơn mọi phần thưởng.",
                "Gửi lời khen",
                c.Child.Id,
                c.Child.DisplayName,
                $"{shortChild} ơi, con tiến bộ nhiều lắm. {parentLabel} rất tự hào.",
                FamilyParentVoiceTemplates.Praise,
                season.Tag($"streak:{c.Streak}"))));
            break;
        }

        // Exam season: lower open threshold so encourage_dip surfaces sooner.
        var openNeed = season.IsExam ? 1 : 2;
        foreach (var c in candidates.OrderByDescending(x => x.Open).ThenByDescending(x => x.ColdDays))
        {
            if (c.Open < openNeed && !(c.Open >= 1 && c.Streak == 0)) continue;
            if (await _voice.HasSentTodayAsync(
                    familyId, viewer.Id, c.Child.Id, date,
                    FamilyParentVoiceTemplates.Encourage, cancellationToken))
                continue;

            var shortChild = ShortName(c.Child.DisplayName);
            var draft = season.IsExam
                ? $"{shortChild} ơi, {parentLabel.ToLowerInvariant()} ôm con cái. Không sao nếu hôm nay chậm một chút — bố/mẹ ở đây."
                : $"{shortChild} cố lên nhé. {parentLabel} tin con.";
            ranked.Add((rankEncourage, new FamilyRelationshipTriggerDto(
                FamilyRelationshipTriggerCodes.EncourageDip,
                season.IsExam
                    ? $"{shortChild} đang mùa thi — cần lời ấm hơn nhắc việc"
                    : $"{shortChild} có vẻ đang mất nhịp",
                season.IsExam
                    ? $"Ưu tiên ôm / động viên của {parentLabel.ToLowerInvariant()} — không thêm áp lực."
                    : $"Một lời động viên của {parentLabel.ToLowerInvariant()} có thể giúp con.",
                "Gửi lời động viên",
                c.Child.Id,
                c.Child.DisplayName,
                draft,
                FamilyParentVoiceTemplates.Encourage,
                season.Tag($"open:{c.Open}"))));
            break;
        }

        // Always-on soft invite — or play-season ritual invite alongside other triggers.
        var hasParentVoiceTrigger = ranked.Any(x =>
            x.T.Code is FamilyRelationshipTriggerCodes.PraiseStreak
                or FamilyRelationshipTriggerCodes.EncourageDip
                or FamilyRelationshipTriggerCodes.TeamEarlyFinish
                or FamilyRelationshipTriggerCodes.FirstDayComplete
                or FamilyRelationshipTriggerCodes.WarmCheckin
                or FamilyRelationshipTriggerCodes.ThankPartner
                or FamilyRelationshipTriggerCodes.BirthdayWish);
        var wantWarm = !hasParentVoiceTrigger
            || (season.IsPlaySeason
                && !ranked.Any(x => x.T.Code == FamilyRelationshipTriggerCodes.WarmCheckin));
        if (wantWarm)
        {
            var warm = candidates.OrderByDescending(x => x.ColdDays).FirstOrDefault();
            if (warm.Child is not null
                && !await _voice.HasSentTodayAsync(
                    familyId, viewer.Id, warm.Child.Id, date, null, cancellationToken))
            {
                var shortChild = ShortName(warm.Child.DisplayName);
                var draft = season.IsPlaySeason
                    ? $"{shortChild} ơi, chiều nay mình chơi gì với {parentLabel.ToLowerInvariant()} nhé?"
                    : $"{shortChild} ơi, hôm nay {parentLabel.ToLowerInvariant()} nghĩ đến con.";
                ranked.Add((hasParentVoiceTrigger ? Math.Max(rankWarm, 3) : rankWarm,
                    new FamilyRelationshipTriggerDto(
                        FamilyRelationshipTriggerCodes.WarmCheckin,
                        season.IsPlaySeason
                            ? $"Rủ {shortChild} chơi chung một chút"
                            : $"Gửi một lời ấm tới {shortChild}",
                        season.IsPlaySeason
                            ? "Nghỉ hè / nghỉ lễ — một ritual chơi chung ấm hơn checklist."
                            : $"Không cần lý do lớn — một câu của {parentLabel.ToLowerInvariant()} hôm nay cũng đủ gần hơn.",
                        season.IsPlaySeason ? "Gửi lời rủ chơi" : "Gửi lời",
                        warm.Child.Id,
                        warm.Child.DisplayName,
                        draft,
                        FamilyParentVoiceTemplates.Custom,
                        season.Tag($"warm:cold:{warm.ColdDays}"))));
            }
        }
    }

    private readonly record struct SeasonBias(string Code)
    {
        public bool IsExam => Code is "exam";
        public bool IsPlaySeason => Code is "summer" or "holiday" or "travel";

        public string Tag(string why) => $"{why}|mode:{Code}";
    }

    private async Task<SeasonBias> ResolveSeasonAsync(
        Guid familyId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        try
        {
            var period = await _periods.GetActiveCoveringAsync(familyId, date, cancellationToken);
            var kind = (period?.Kind ?? "").Trim().ToLowerInvariant();
            if (kind is "exam") return new SeasonBias("exam");
            if (kind is "summer") return new SeasonBias("summer");
            if (kind is "holiday") return new SeasonBias("holiday");
            if (kind is "travel") return new SeasonBias("travel");
        }
        catch
        {
            // Period table missing — fall through to blueprint chips.
        }

        try
        {
            var bp = await _blueprint.GetAsync(familyId, cancellationToken);
            if (bp is not null && HasExamChip(bp.LayersJson))
                return new SeasonBias("exam");
        }
        catch
        {
            // Blueprint optional.
        }

        return new SeasonBias("normal");
    }

    private static bool HasExamChip(string? layersJson)
    {
        if (string.IsNullOrWhiteSpace(layersJson) || layersJson is "{}") return false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(layersJson);
            if (!doc.RootElement.TryGetProperty("context", out var ctx)) return false;
            if (!ctx.TryGetProperty("chips", out var chips) || chips.ValueKind != System.Text.Json.JsonValueKind.Array)
                return false;
            foreach (var c in chips.EnumerateArray())
            {
                if (c.ValueKind == System.Text.Json.JsonValueKind.String
                    && string.Equals(c.GetString(), "exam_season", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private async Task AddChildTriggersAsync(
        List<(int Rank, FamilyRelationshipTriggerDto T)> ranked,
        Guid familyId,
        FamilyGraphRepository.MembershipRow viewer,
        FamilyTeamDayDto team,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var unreadVoice = (await _voice.ListAsync(
                familyId, forMemberId: viewer.Id, fromMemberId: null, flowDate: date, cancellationToken))
            .Where(v => string.Equals(v.Status, FamilyParentVoiceStatuses.Sent, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (unreadVoice.Count > 0)
        {
            var first = unreadVoice[0];
            ranked.Add((0, new FamilyRelationshipTriggerDto(
                "parent_voice_inbox",
                $"Lời từ {ParentLabel(first.FromMemberName)}",
                first.BodyVi,
                "Đọc lời",
                first.FromMemberId,
                first.FromMemberName,
                null,
                null,
                "inbox:unread")));
        }

        var candidates = await _nudges.ListFromCandidatesAsync(familyId, date, cancellationToken);
        var me = candidates.FirstOrDefault(c => c.MemberId == viewer.Id);
        if (me is { CanInvite: true } && team.RemainingMissions >= 1)
        {
            var target = candidates.FirstOrDefault(c =>
                c.MemberId != viewer.Id && !c.MissionsComplete);
            if (target is not null)
            {
                ranked.Add((3, new FamilyRelationshipTriggerDto(
                    FamilyRelationshipTriggerCodes.CheerSibling,
                    $"Cổ vũ {ShortName(target.DisplayName)}",
                    $"Cả nhà còn {team.RemainingMissions} việc — gửi một lời cố lên nhé.",
                    "Gửi lời cổ vũ",
                    target.MemberId,
                    target.DisplayName,
                    null,
                    "cheer_up",
                    "team:remaining")));
            }
        }

        var slice = team.Children.FirstOrDefault(s => s.MemberId == viewer.Id);
        var doneToday = slice is { Done: > 0 };
        if (doneToday)
        {
            var thanks = await _gratitude.ListAsync(familyId, date, viewer.Id, cancellationToken);
            if (thanks.Count == 0)
            {
                ranked.Add((4, new FamilyRelationshipTriggerDto(
                    FamilyRelationshipTriggerCodes.ThankParent,
                    "Gửi lời cảm ơn bố/mẹ",
                    "Một lời cảm ơn ngắn làm ngày hôm nay ấm hơn.",
                    "Cảm ơn bố/mẹ",
                    null,
                    null,
                    null,
                    null,
                    "gratitude:missing")));
            }
        }
    }

    private static FamilyParentVoiceDto MapVoice(FamilyParentVoiceRepository.VoiceRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.FromMemberId,
            row.FromMemberName,
            row.ToMemberId,
            row.ToMemberName,
            row.FlowDate,
            row.TemplateCode,
            row.BodyVi,
            row.Status,
            row.SentAt,
            row.AckAt);

    private static FamilyRelationshipTriggerStateDto MapTriggerState(
        FamilyRelationshipTriggerStateRepository.StateRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.ViewerMemberId,
            row.FlowDate,
            row.TriggerCode,
            row.ToMemberId,
            row.State,
            row.DraftBodyVi,
            row.TemplateCode,
            row.TitleVi,
            row.BodyVi,
            row.UpdatedAt);

    private static string ShortName(string displayName)
    {
        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : displayName.Trim();
    }

    private static string ParentLabel(string displayName)
    {
        var t = displayName.Trim();
        if (t.StartsWith("Mẹ", StringComparison.OrdinalIgnoreCase) || t.Contains("mẹ", StringComparison.OrdinalIgnoreCase))
            return "Mẹ";
        if (t.StartsWith("Bố", StringComparison.OrdinalIgnoreCase) || t.Contains("bố", StringComparison.OrdinalIgnoreCase))
            return "Bố";
        return ShortName(t);
    }

    /// <summary>Birthday window: same month/day or ±1 calendar day (handles year wrap via constructed dates).</summary>
    private static bool IsBirthdayWindow(DateOnly dob, DateOnly today)
    {
        static DateOnly Anniversary(DateOnly birth, int year)
        {
            var day = birth.Day;
            if (birth.Month == 2 && birth.Day == 29 && !DateTime.IsLeapYear(year))
                day = 28;
            return new DateOnly(year, birth.Month, day);
        }

        var thisYear = Anniversary(dob, today.Year);
        var prevYear = Anniversary(dob, today.Year - 1);
        var nextYear = Anniversary(dob, today.Year + 1);
        return Math.Abs(today.DayNumber - thisYear.DayNumber) <= 1
            || Math.Abs(today.DayNumber - prevYear.DayNumber) <= 1
            || Math.Abs(today.DayNumber - nextYear.DayNumber) <= 1;
    }
}

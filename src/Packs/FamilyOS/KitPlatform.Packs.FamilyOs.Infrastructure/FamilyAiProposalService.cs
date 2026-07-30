using System.Text.Json;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAiProposalService : IFamilyAiProposalService
{
    private readonly FamilyAiProposalRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyScreenWalletRepository _wallets;
    private readonly IFamilyScreenWalletService _walletService;
    private readonly IFamilyModeService _modes;
    private readonly IFamilyScoreService _score;
    private readonly IFamilyCalendarPeriodService _periods;
    private readonly IFamilyRoutineService _routines;
    private readonly IFamilyOsParentPushService _parentPush;
    private readonly KitPlatform.Application.Abstractions.ITenantContext _tenant;
    private readonly IFamilyCommercialService _commercial;

    public FamilyAiProposalService(
        FamilyAiProposalRepository repo,
        FamilyGraphRepository families,
        FamilyScreenWalletRepository wallets,
        IFamilyScreenWalletService walletService,
        IFamilyModeService modes,
        IFamilyScoreService score,
        IFamilyCalendarPeriodService periods,
        IFamilyRoutineService routines,
        IFamilyOsParentPushService parentPush,
        KitPlatform.Application.Abstractions.ITenantContext tenant,
        IFamilyCommercialService commercial)
    {
        _repo = repo;
        _families = families;
        _wallets = wallets;
        _walletService = walletService;
        _modes = modes;
        _score = score;
        _periods = periods;
        _routines = routines;
        _parentPush = parentPush;
        _tenant = tenant;
        _commercial = commercial;
    }

    public async Task<IReadOnlyList<FamilyAiProposalDto>> ListPendingAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");
        var rows = await _repo.ListPendingAsync(familyId, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyAiProposalDto?> TryCreateAsync(
        Guid familyId,
        string kind,
        string titleVi,
        string bodyVi,
        string? payloadJson,
        string sourceRef,
        Guid? memberId = null,
        CancellationToken cancellationToken = default)
    {
        var id = await _repo.TryInsertAsync(
            familyId, memberId, kind, titleVi, bodyVi, payloadJson, sourceRef, cancellationToken);
        if (id is null) return null;
        var row = await _repo.GetAsync(familyId, id.Value, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<FamilyAiProposalDto> DecideAsync(
        Guid familyId,
        Guid proposalId,
        FamilyAiProposalDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var decider = members.FirstOrDefault(m => m.Id == request.DecidedByMemberId)
            ?? throw new InvalidOperationException("Người quyết định không thuộc gia đình.");
        if (decider.RoleCode is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver))
            throw new InvalidOperationException("Chỉ bố mẹ / người chăm sóc mới duyệt.");

        var row = await _repo.GetAsync(familyId, proposalId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy đề xuất AI.");
        if (!string.Equals(row.Status, FamilyAiProposalStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return Map(row);

        var decision = (request.Decision ?? "").Trim().ToLowerInvariant();
        var approved = decision is "approve" or "approved";
        var rejected = decision is "reject" or "rejected";
        if (!approved && !rejected)
            throw new InvalidOperationException("Decision phải là approve | reject.");

        var status = approved ? FamilyAiProposalStatuses.Approved : FamilyAiProposalStatuses.Rejected;
        await _repo.DecideAsync(familyId, proposalId, status, request.DecidedByMemberId, cancellationToken);

        if (approved)
            await ApplyApprovedAsync(familyId, row, request.DecidedByMemberId, cancellationToken);

        return Map((await _repo.GetAsync(familyId, proposalId, cancellationToken))!);
    }

    public async Task<int> ScanAdaptiveAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(
            familyId, FamilyCapabilityCodes.AiSuggest, cancellationToken);
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var created = 0;
        var score = await _score.GetWeekScoreAsync(familyId, cancellationToken);
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var children = members
            .Where(m => string.Equals(m.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            .ToList();

        string? modeKind = null;
        try
        {
            var resolved = await _periods.ResolveAsync(familyId, null, cancellationToken);
            modeKind = resolved.PeriodKind;
        }
        catch
        {
            // optional
        }

        foreach (var child in children)
        {
            var weeks = await _wallets.ListRecentActiveWeeksAsync(
                familyId, child.Id, 3, cancellationToken);
            if (weeks.Count >= 3
                && weeks.All(w => w.BudgetMinutes > 0
                                  && (w.SpentMinutes + w.GrantedMinutes) < w.BudgetMinutes * 0.55))
            {
                var newBudget = Math.Max(60, (int)(weeks[0].BudgetMinutes * 0.75 / 15) * 15);
                if (newBudget < weeks[0].BudgetMinutes)
                {
                    var dto = await TryCreateAsync(
                        familyId,
                        FamilyAiProposalKinds.ScreenAdjust,
                        $"Giảm ngân sách màn hình của {ShortName(child.DisplayName)}?",
                        $"3 tuần gần đây dùng dưới nửa ngân sách. AI đề xuất giảm còn {newBudget} phút/tuần.",
                        JsonSerializer.Serialize(new
                        {
                            memberId = child.Id,
                            budgetMinutes = newBudget,
                        }),
                        $"screen_adjust_down:{child.Id:D}:{weeks[0].BudgetMinutes}",
                        child.Id,
                        cancellationToken);
                    if (dto is not null) created++;
                }
            }

            if (score.AllowBonusMinutes
                && string.Equals(modeKind, FamilyCalendarPeriodKinds.Summer, StringComparison.OrdinalIgnoreCase))
            {
                var bump = _walletService.SuggestBudgetMinutes(null, "summer");
                var dto = await TryCreateAsync(
                    familyId,
                    FamilyAiProposalKinds.ScreenAdjust,
                    $"Nghỉ hè — tăng ngân sách cho {ShortName(child.DisplayName)}?",
                    $"Đang ở chế độ Nghỉ hè. AI đề xuất ngân sách tuần ~{bump} phút (thỏa thuận, không đo máy).",
                    JsonSerializer.Serialize(new { memberId = child.Id, budgetMinutes = bump }),
                    $"screen_adjust_summer:{child.Id:D}:{DateTime.UtcNow:yyyy-MM}",
                    child.Id,
                    cancellationToken);
                if (dto is not null) created++;
            }

            if (score.AllowBonusMinutes && score.Score >= 80)
            {
                var dto = await TryCreateAsync(
                    familyId,
                    FamilyAiProposalKinds.RewardMinutes,
                    $"Thưởng thêm 20 phút cho {ShortName(child.DisplayName)}?",
                    $"{score.HeadlineVi} AI gợi ý thưởng +20 phút màn hình tuần này.",
                    JsonSerializer.Serialize(new { memberId = child.Id, grantMinutes = 20 }),
                    $"reward_minutes:{child.Id:D}:{DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime):yyyy-MM-dd}",
                    child.Id,
                    cancellationToken);
                if (dto is not null) created++;
            }
        }

        // P2: dense routine → suggest hiding 1–2 optional templates (apply from tomorrow).
        try
        {
            var resolved = await _periods.ResolveAsync(familyId, null, cancellationToken);
            var routine = await _routines.GetRoutineAsync(familyId, resolved.RoutineId, cancellationToken);
            if (routine is not null)
            {
                var active = routine.Templates
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.WindowStart ?? TimeOnly.MaxValue)
                    .ToList();
                var trimThreshold = modeKind is FamilyCalendarPeriodKinds.Summer
                    or FamilyCalendarPeriodKinds.Holiday
                    or FamilyCalendarPeriodKinds.Travel
                    ? 8
                    : 12;
                if (active.Count >= trimThreshold)
                {
                    var candidates = active
                        .Where(t => !IsCoreHabitTitle(t.Title))
                        .OrderByDescending(t => t.WindowStart ?? TimeOnly.MinValue)
                        .ThenByDescending(t => t.SortOrder)
                        .Take(2)
                        .ToList();
                    if (candidates.Count > 0)
                    {
                        var titles = string.Join(", ", candidates.Select(c => $"「{c.Title}」"));

                        // Bố mẹ phải biết bớt việc của ai — gắn chủ sở hữu nếu các việc
                        // này cùng thuộc một con; nhiều con thì để phạm vi cả nhà.
                        var ownerIds = candidates
                            .Select(c => c.MemberId)
                            .Where(id => id.HasValue)
                            .Select(id => id!.Value)
                            .Distinct()
                            .ToList();
                        Guid? ownerId = ownerIds.Count == 1 ? ownerIds[0] : null;
                        var ownerName = ownerId is null
                            ? null
                            : members.FirstOrDefault(m => m.Id == ownerId.Value)?.DisplayName;
                        var whoPrefix = string.IsNullOrWhiteSpace(ownerName)
                            ? string.Empty
                            : $"{ShortName(ownerName)} · ";

                        var dto = await TryCreateAsync(
                            familyId,
                            FamilyAiProposalKinds.RoutineTrim,
                            $"{whoPrefix}Lịch 「{routine.DisplayName}」 đang hơi nhiều việc — bớt {candidates.Count} việc?",
                            $"Famixa đề xuất tạm ẩn {titles}. Áp dụng từ ngày mai, hôm nay giữ nguyên. Bạn chỉ cần bấm Áp dụng.",
                            JsonSerializer.Serialize(new
                            {
                                routineId = routine.Id,
                                deactivateTemplateIds = candidates.Select(c => c.Id).ToArray(),
                            }),
                            $"routine_trim:{routine.Id:D}:{resolved.FlowDate:yyyy-MM-dd}",
                            ownerId,
                            cancellationToken);
                        if (dto is not null) created++;
                    }
                }
            }
        }
        catch
        {
            // optional
        }

        // AI+ deep: sibling balance + evening risk (templated, no LLM).
        try
        {
            var pack = await _commercial.GetCapabilityPackAsync(familyId, cancellationToken);
            var hasDeep = pack.Capabilities.Contains(
                FamilyCapabilityCodes.AiPlusDeep, StringComparer.OrdinalIgnoreCase);
            if (hasDeep && children.Count >= 2)
            {
                var a = children[0];
                var b = children[1];
                var dto = await TryCreateAsync(
                    familyId,
                    FamilyAiProposalKinds.Other,
                    $"Cân bằng anh chị — {ShortName(a.DisplayName)} & {ShortName(b.DisplayName)}?",
                    "AI+ gợi ý tuần này: mỗi con một việc tự chọn trước 19:00, bố mẹ chỉ quan sát — giảm so sánh.",
                    JsonSerializer.Serialize(new
                    {
                        kind = "sibling_balance",
                        memberIds = new[] { a.Id, b.Id },
                    }),
                    $"sibling_balance:{a.Id:D}:{b.Id:D}:{DateTime.UtcNow:yyyy-MM-dd}",
                    null,
                    cancellationToken);
                if (dto is not null) created++;
            }

            if (hasDeep)
            {
                var localNow = FamilyTimeZones.NowIn(family.Timezone).DateTime;
                if (localNow.Hour >= 18)
                {
                    var focus = children.FirstOrDefault();
                    var dto = await TryCreateAsync(
                        familyId,
                        FamilyAiProposalKinds.Other,
                        "Tối nay: giảm rủi ro căng thẳng?",
                        "AI+ gợi ý: cắt 1 nhắc không cần thiết sau 19:30, đổi thành câu hỏi ngắn (“Cần giúp gì?”).",
                        JsonSerializer.Serialize(new
                        {
                            kind = "evening_risk",
                            memberId = focus?.Id,
                        }),
                        $"evening_risk:{DateOnly.FromDateTime(localNow):yyyy-MM-dd}",
                        focus?.Id,
                        cancellationToken);
                    if (dto is not null) created++;
                }
            }
        }
        catch
        {
            // optional deep scan
        }

        if (created > 0)
        {
            try
            {
                var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
                await _parentPush.TryNotifyFamilyAsync(
                    _tenant.TenantId,
                    familyId,
                    today,
                    kind: "ai_proposal",
                    title: "AI cần bạn · vài giây",
                    body: $"Có {created} đề xuất mới cần duyệt.",
                    url: "/today",
                    dataType: "familyos_ai_proposal",
                    payloadSummary: $"scan:{today:yyyy-MM-dd}:{created}",
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // optional
            }
        }

        return created;
    }

    private async Task ApplyApprovedAsync(
        Guid familyId,
        FamilyAiProposalRepository.AiProposalRow row,
        Guid decidedBy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.PayloadJson)) return;

        using var doc = JsonDocument.Parse(row.PayloadJson);
        var root = doc.RootElement;

        if (row.Kind is FamilyAiProposalKinds.ScreenBudget or FamilyAiProposalKinds.ScreenAdjust)
        {
            if (root.TryGetProperty("walletId", out var wid) && wid.TryGetGuid(out var walletId))
            {
                await _walletService.ActivateAsync(familyId, walletId, decidedBy, cancellationToken);
                return;
            }

            if (root.TryGetProperty("memberId", out var mid) && mid.TryGetGuid(out var memberId)
                && root.TryGetProperty("budgetMinutes", out var bm) && bm.TryGetInt32(out var budget))
            {
                var wallet = await _walletService.ProposeBudgetAsync(
                    familyId,
                    new FamilyScreenWalletProposeRequest(memberId, budget, decidedBy),
                    cancellationToken);
                await _walletService.ActivateAsync(familyId, wallet.Id, decidedBy, cancellationToken);
            }
        }
        else if (row.Kind == FamilyAiProposalKinds.RewardMinutes)
        {
            if (root.TryGetProperty("memberId", out var mid) && mid.TryGetGuid(out var memberId)
                && root.TryGetProperty("grantMinutes", out var gm) && gm.TryGetInt32(out var grant))
            {
                await _walletService.ApplyGrantAsync(
                    familyId, memberId, grant, $"ai_proposal:{row.Id:D}", "AI thưởng phút",
                    cancellationToken);
            }
        }
        else if (row.Kind == FamilyAiProposalKinds.FamilyMode)
        {
            if (root.TryGetProperty("mode", out var modeEl))
            {
                var mode = modeEl.GetString() ?? FamilyModeKinds.Normal;
                DateOnly? start = null;
                DateOnly? end = null;
                if (root.TryGetProperty("startDate", out var sd) && DateOnly.TryParse(sd.GetString(), out var s))
                    start = s;
                if (root.TryGetProperty("endDate", out var ed) && DateOnly.TryParse(ed.GetString(), out var e))
                    end = e;
                await _modes.ActivateAsync(
                    familyId,
                    new FamilyModeActivateRequest(mode, start, end, decidedBy, ConfirmNow: true),
                    cancellationToken);
            }
        }
        else if (row.Kind == FamilyAiProposalKinds.RoutineTrim)
        {
            if (!root.TryGetProperty("routineId", out var ridEl) || !ridEl.TryGetGuid(out var routineId))
                return;
            if (!root.TryGetProperty("deactivateTemplateIds", out var idsEl)
                || idsEl.ValueKind != JsonValueKind.Array)
                return;

            var routine = await _routines.GetRoutineAsync(familyId, routineId, cancellationToken);
            if (routine is null) return;

            foreach (var idEl in idsEl.EnumerateArray())
            {
                if (!idEl.TryGetGuid(out var templateId)) continue;
                var existing = routine.Templates.FirstOrDefault(t => t.Id == templateId);
                if (existing is null || !existing.IsActive) continue;
                await _routines.UpdateTemplateAsync(
                    familyId,
                    routineId,
                    templateId,
                    new UpdateCommitmentTemplateRequest(
                        existing.Title,
                        existing.Description,
                        existing.MemberId,
                        existing.WindowStart,
                        existing.WindowEnd,
                        existing.SortOrder,
                        IsActive: false,
                        existing.Priority,
                        existing.ExpectedDurationMinutes,
                        existing.ContextAnchor,
                        existing.DependsOnTemplateIds,
                        existing.AllowEarlyComplete,
                        existing.EarlyLeadMinutes,
                        existing.OnTimeGraceMinutes,
                        existing.StarReward),
                    cancellationToken);
            }
        }
    }

    private static FamilyAiProposalDto Map(FamilyAiProposalRepository.AiProposalRow r) =>
        new(r.Id, r.FamilyId, r.MemberId, r.MemberName, r.Kind, r.TitleVi, r.BodyVi,
            r.PayloadJson, r.Status, r.CreatedAt, r.DecidedAt);

    private static string ShortName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? name : parts[^1];
    }

    private static bool IsCoreHabitTitle(string title)
    {
        var t = (title ?? "").Trim().ToLowerInvariant();
        return t.Contains("dậy")
               || t.Contains("đánh răng")
               || t.Contains("ăn sáng")
               || t.Contains("ngủ")
               || t.Contains("đi ngủ");
    }
}

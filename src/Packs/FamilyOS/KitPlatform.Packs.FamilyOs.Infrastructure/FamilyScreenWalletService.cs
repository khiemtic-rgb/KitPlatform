using System.Globalization;
using System.Text.Json;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyScreenWalletService : IFamilyScreenWalletService
{
    private readonly FamilyScreenWalletRepository _repo;
    private readonly FamilyAiProposalRepository _proposals;
    private readonly FamilyGraphRepository _families;

    public FamilyScreenWalletService(
        FamilyScreenWalletRepository repo,
        FamilyAiProposalRepository proposals,
        FamilyGraphRepository families)
    {
        _repo = repo;
        _proposals = proposals;
        _families = families;
    }

    public async Task<IReadOnlyList<FamilyScreenWalletDto>> ListWeekAsync(
        Guid familyId,
        int? isoYear = null,
        int? isoWeek = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        var (y, w) = ResolveWeek(family.Timezone, isoYear, isoWeek);
        var rows = await _repo.ListWeekAsync(familyId, y, w, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyScreenWalletDto> ProposeBudgetAsync(
        Guid familyId,
        FamilyScreenWalletProposeRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var child = members.FirstOrDefault(m => m.Id == request.MemberId)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình.");
        if (!string.Equals(child.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Wallet chỉ áp dụng cho con.");

        var (y, w) = ResolveWeek(family.Timezone, null, null);
        var budget = request.BudgetMinutes is > 0
            ? Math.Clamp(request.BudgetMinutes.Value, 30, 600)
            : SuggestBudgetMinutes(null, null);

        var walletId = await _repo.UpsertProposedAsync(
            familyId, request.MemberId, y, w, budget, cancellationToken);

        var shortName = ShortName(child.DisplayName);
        var payload = JsonSerializer.Serialize(new
        {
            walletId,
            memberId = request.MemberId,
            budgetMinutes = budget,
            isoYear = y,
            isoWeek = w,
        });
        await _proposals.TryInsertAsync(
            familyId,
            request.MemberId,
            FamilyAiProposalKinds.ScreenBudget,
            titleVi: $"Tuần này: {budget} phút màn hình cho {shortName}",
            bodyVi: $"AI đề xuất ngân sách thỏa thuận {budget} phút/tuần. Bố mẹ chỉ cần duyệt — không cài Rule theo ngày. (Không đo máy thật.)",
            payloadJson: payload,
            sourceRef: $"screen_budget:{request.MemberId:D}:{y}-W{w}",
            cancellationToken);

        return Map((await _repo.GetAsync(familyId, walletId, cancellationToken))!);
    }

    public async Task<FamilyScreenWalletDto> ActivateAsync(
        Guid familyId,
        Guid walletId,
        Guid decidedByMemberId,
        CancellationToken cancellationToken = default)
    {
        _ = decidedByMemberId;
        var row = await _repo.GetAsync(familyId, walletId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy ví tuần.");
        await _repo.ActivateAsync(familyId, walletId, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        await _repo.TryInsertLedgerAsync(
            familyId,
            walletId,
            row.MemberId,
            today,
            "budget_set",
            row.BudgetMinutes,
            $"Kích hoạt ngân sách {row.BudgetMinutes} phút",
            $"budget_set:{walletId:D}",
            cancellationToken);

        return Map((await _repo.GetAsync(familyId, walletId, cancellationToken))!);
    }

    public async Task<FamilyScreenWalletDto> SpendAsync(
        Guid familyId,
        FamilyScreenWalletSpendRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        if (request.Minutes is < 1 or > 240)
            throw new InvalidOperationException("Số phút tiêu không hợp lệ.");

        var (y, w) = ResolveWeek(family.Timezone, null, null);
        var wallet = await _repo.GetMemberWeekAsync(
            familyId, request.MemberId, y, w, cancellationToken)
            ?? throw new InvalidOperationException("Chưa có ví tuần — bố mẹ cần duyệt ngân sách trước.");

        if (!string.Equals(wallet.Status, "active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ví tuần chưa được bố mẹ kích hoạt.");

        var today = request.FlowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var sourceRef = $"spend:{request.MemberId:D}:{today:yyyy-MM-dd}:{request.Minutes}:{DateTimeOffset.UtcNow.Ticks}";
        var inserted = await _repo.TryInsertLedgerAsync(
            familyId,
            wallet.Id,
            request.MemberId,
            today,
            "spend",
            -request.Minutes,
            request.Note,
            sourceRef,
            cancellationToken);
        if (inserted)
            await _repo.AdjustCountersAsync(familyId, wallet.Id, request.Minutes, 0, 0, cancellationToken);

        return Map((await _repo.GetAsync(familyId, wallet.Id, cancellationToken))!);
    }

    public async Task ApplyGrantAsync(
        Guid familyId,
        Guid memberId,
        int minutes,
        string sourceRef,
        string? noteVi,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        if (family is null) return;

        var (y, w) = ResolveWeek(family.Timezone, null, null);
        var wallet = await _repo.GetMemberWeekAsync(familyId, memberId, y, w, cancellationToken);
        if (wallet is null)
        {
            var id = await _repo.UpsertProposedAsync(
                familyId, memberId, y, w, SuggestBudgetMinutes(null, null), cancellationToken);
            await _repo.ActivateAsync(familyId, id, cancellationToken);
            wallet = await _repo.GetAsync(familyId, id, cancellationToken);
        }

        if (wallet is null) return;

        if (!string.Equals(wallet.Status, "active", StringComparison.OrdinalIgnoreCase))
            await _repo.ActivateAsync(familyId, wallet.Id, cancellationToken);

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var inserted = await _repo.TryInsertLedgerAsync(
            familyId, wallet.Id, memberId, today, "grant", minutes, noteVi, sourceRef,
            cancellationToken);
        if (inserted)
            await _repo.AdjustCountersAsync(familyId, wallet.Id, 0, 0, minutes, cancellationToken);
    }

    public async Task ApplyEarnAsync(
        Guid familyId,
        Guid memberId,
        int minutes,
        string sourceRef,
        string? noteVi,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        if (family is null) return;

        var (y, w) = ResolveWeek(family.Timezone, null, null);
        var wallet = await _repo.GetMemberWeekAsync(familyId, memberId, y, w, cancellationToken);
        if (wallet is null || !string.Equals(wallet.Status, "active", StringComparison.OrdinalIgnoreCase))
            return;

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var inserted = await _repo.TryInsertLedgerAsync(
            familyId, wallet.Id, memberId, today, "earn", minutes, noteVi, sourceRef,
            cancellationToken);
        if (inserted)
            await _repo.AdjustCountersAsync(familyId, wallet.Id, 0, minutes, 0, cancellationToken);
    }

    public int SuggestBudgetMinutes(string? ageBand, string? modeKind)
    {
        var baseMin = ageBand switch
        {
            "4-6" => 120,
            "7-9" => 150,
            "10-12" => 180,
            "13+" => 210,
            _ => 180,
        };
        return modeKind?.ToLowerInvariant() switch
        {
            "summer" or "holiday" => baseMin + 60,
            "exam" => Math.Max(60, baseMin - 60),
            "travel" => baseMin + 120,
            _ => baseMin,
        };
    }

    private static (int Year, int Week) ResolveWeek(string timezone, int? isoYear, int? isoWeek)
    {
        if (isoYear is > 2000 && isoWeek is >= 1 and <= 53)
            return (isoYear.Value, isoWeek.Value);

        var local = FamilyTimeZones.NowIn(timezone).DateTime;
        var week = ISOWeek.GetWeekOfYear(local);
        var year = ISOWeek.GetYear(local);
        return (year, week);
    }

    private static FamilyScreenWalletDto Map(FamilyScreenWalletRepository.WalletRow r)
    {
        var remaining = r.BudgetMinutes + r.EarnedMinutes + r.GrantedMinutes - r.SpentMinutes;
        return new FamilyScreenWalletDto(
            r.Id,
            r.FamilyId,
            r.MemberId,
            r.MemberName,
            r.IsoYear,
            r.IsoWeek,
            r.BudgetMinutes,
            r.SpentMinutes,
            r.EarnedMinutes,
            r.GrantedMinutes,
            Math.Max(0, remaining),
            r.Status);
    }

    private static string ShortName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? name : parts[^1];
    }
}

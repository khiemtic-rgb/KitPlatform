using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyMemoryService : IFamilyMemoryService
{
    private const int MaxLimit = 200;

    private readonly FamilyMemoryRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly ITenantContext _tenant;

    public FamilyMemoryService(
        FamilyMemoryRepository repo,
        FamilyGraphRepository families,
        ITenantContext tenant)
    {
        _repo = repo;
        _families = families;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FamilyMemoryDto>> ListAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        bool favoritesOnly = false,
        int limit = 60,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var rows = await _repo.ListAsync(
            familyId, from, to, favoritesOnly, Math.Clamp(limit, 1, MaxLimit), cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyMemoryDto> CreateAsync(
        Guid familyId,
        FamilyMemoryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var title = (request.TitleVi ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Tiêu đề kỷ niệm bắt buộc.");
        if (title.Length > 200)
            title = title[..200];

        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? FamilyMemoryKinds.Manual
            : request.Kind.Trim().ToLowerInvariant();
        if (!FamilyMemoryKinds.All.Contains(kind))
            throw new InvalidOperationException("Loại kỷ niệm không hợp lệ.");

        if (request.MemberId is Guid memberId)
        {
            var members = await _families.ListMembersAsync(familyId, cancellationToken);
            if (members.All(m => m.Id != memberId))
                throw new InvalidOperationException("Thành viên không thuộc gia đình này.");
        }

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var flowDate = request.FlowDate ?? today;

        var id = await _repo.InsertAsync(
            familyId,
            request.MemberId,
            flowDate,
            kind,
            title,
            Trim(request.NoteVi, 600),
            Trim(request.Icon, 16),
            Trim(request.PhotoUrl, 500),
            sourceRef: null,
            happenedAt: null,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được kỷ niệm.");
        return Map(row);
    }

    public async Task SetFavoriteAsync(
        Guid familyId,
        Guid memoryId,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        if (!await _repo.SetFavoriteAsync(familyId, memoryId, isFavorite, cancellationToken))
            throw new InvalidOperationException("Không tìm thấy kỷ niệm.");
    }

    public async Task DeleteAsync(
        Guid familyId,
        Guid memoryId,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        if (!await _repo.SoftDeleteAsync(familyId, memoryId, cancellationToken))
            throw new InvalidOperationException("Không tìm thấy kỷ niệm.");
    }

    public async Task<FamilyMemoryRecapDto> GetRecapAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var rangeTo = to ?? today;
        if (rangeTo < rangeFrom)
            (rangeFrom, rangeTo) = (rangeTo, rangeFrom);
        if (rangeTo.DayNumber - rangeFrom.DayNumber > 366)
            throw new InvalidOperationException("Khoảng recap tối đa 1 năm.");

        var agg = await _repo.GetRecapAggregateAsync(familyId, rangeFrom, rangeTo, cancellationToken);
        var bestStreak = await _repo.GetBestStreakAsync(familyId, rangeFrom, rangeTo, cancellationToken);

        var highlights = await _repo.ListAsync(
            familyId, rangeFrom, rangeTo, favoritesOnly: false, limit: 12, cancellationToken);

        return new FamilyMemoryRecapDto(
            rangeFrom,
            rangeTo,
            agg.TotalCount,
            agg.BeautifulDays,
            agg.GratitudeCount,
            agg.PhotoCount,
            agg.CelebrationCount,
            bestStreak,
            BuildHeadline(agg, bestStreak),
            highlights.Select(Map).ToList());
    }

    public async Task<bool> TryCaptureAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        string kind,
        string titleVi,
        string? noteVi = null,
        string? icon = null,
        string? photoUrl = null,
        string? sourceRef = null,
        Guid? memberId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = (kind ?? "").Trim().ToLowerInvariant();
        if (!FamilyMemoryKinds.All.Contains(normalized))
            return false;

        var title = (titleVi ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            return false;

        return await _repo.TryInsertForTenantAsync(
            tenantId == Guid.Empty ? _tenant.TenantId : tenantId,
            familyId,
            memberId,
            flowDate,
            normalized,
            title.Length > 200 ? title[..200] : title,
            Trim(noteVi, 600),
            Trim(icon, 16),
            Trim(photoUrl, 500),
            Trim(sourceRef, 120),
            cancellationToken);
    }

    private static string BuildHeadline(
        FamilyMemoryRepository.RecapAggRow agg,
        int bestStreak)
    {
        if (agg.TotalCount == 0)
            return "Chưa có kỷ niệm nào — cứ sống bình thường, nhà mình sẽ tự ghi lại.";

        var parts = new List<string>();
        if (agg.BeautifulDays > 0)
            parts.Add($"{agg.BeautifulDays} ngày không phải nhắc con");
        if (agg.GratitudeCount > 0)
            parts.Add($"{agg.GratitudeCount} lời cảm ơn");
        if (agg.CelebrationCount > 0)
            parts.Add($"{agg.CelebrationCount} lần ăn mừng cùng nhau");
        if (agg.PhotoCount > 0)
            parts.Add($"{agg.PhotoCount} khoảnh khắc có ảnh");

        var body = parts.Count > 0
            ? string.Join(", ", parts)
            : $"{agg.TotalCount} khoảnh khắc";

        return bestStreak >= 3
            ? $"Nhà mình có {body} — chuỗi dài nhất {bestStreak} ngày liên tiếp."
            : $"Nhà mình có {body}.";
    }

    private static string? Trim(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen];
    }

    private static FamilyMemoryDto Map(FamilyMemoryRepository.MemoryRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.MemberId,
            row.MemberName,
            row.FlowDate,
            row.Kind,
            row.TitleVi,
            row.NoteVi,
            row.Icon,
            row.PhotoUrl,
            row.IsFavorite,
            row.HappenedAt);
}

using System.Text.RegularExpressions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyGratitudeService : IFamilyGratitudeService
{
    private static readonly Regex MomNamePattern = new(
        @"^mẹ\b|\bmẹ$|\bmẹ\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly FamilyGratitudeRepository _repo;
    private readonly FamilyGraphRepository _families;

    public FamilyGratitudeService(
        FamilyGratitudeRepository repo,
        FamilyGraphRepository families)
    {
        _repo = repo;
        _families = families;
    }

    public async Task<IReadOnlyList<FamilyChildGratitudeDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        Guid? fromMemberId = null,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var rows = await _repo.ListAsync(familyId, flowDate, fromMemberId, cancellationToken);
        return rows.Select(r => Map(r, alreadySent: false)).ToList();
    }

    public async Task<FamilyChildGratitudeDto> SendAsync(
        Guid familyId,
        FamilyChildGratitudeSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var child = members.FirstOrDefault(m => m.Id == request.FromMemberId)
            ?? throw new InvalidOperationException("Thành viên gửi không thuộc gia đình này.");

        if (!string.Equals(child.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ con mới gửi lời cảm ơn từ thẻ khen.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var flowDate = request.FlowDate ?? today;

        var existing = await _repo.GetByChildDayAsync(
            familyId, request.FromMemberId, flowDate, cancellationToken);
        if (existing is not null)
            return Map(existing, alreadySent: true);

        var recipient = ResolvePrimaryParent(members);
        var childShort = ShortName(child.DisplayName);
        var praiseContext = TrimOptional(request.PraiseContext, 380);
        var messageVi = TrimOptional(request.MessageVi, 380)
            ?? BuildDefaultMessage(childShort, recipient?.DisplayName);

        var id = await _repo.InsertAsync(
            familyId,
            request.FromMemberId,
            recipient?.Id,
            flowDate,
            messageVi,
            praiseContext,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được lời cảm ơn.");

        return Map(row, alreadySent: false);
    }

    public async Task MarkReadAsync(
        Guid familyId,
        Guid gratitudeId,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        if (!await _repo.MarkReadAsync(familyId, gratitudeId, cancellationToken))
            throw new InvalidOperationException("Không tìm thấy lời cảm ơn.");
    }

    internal static FamilyGraphRepository.MembershipRow? ResolvePrimaryParent(
        IReadOnlyList<FamilyGraphRepository.MembershipRow> members)
    {
        var parents = members
            .Where(m =>
                m.RoleCode.Equals(FamilyMembershipRoles.Guardian, StringComparison.OrdinalIgnoreCase)
                || m.RoleCode.Equals(FamilyMembershipRoles.Caregiver, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.SortOrder)
            .ToList();

        var mom = parents.FirstOrDefault(m => MomNamePattern.IsMatch(m.DisplayName.Trim()));
        return mom ?? parents.FirstOrDefault();
    }

    private static string BuildDefaultMessage(string childShort, string? recipientName)
    {
        var who = GreetParent(recipientName);
        return $"{childShort}: Cảm ơn {who}! 💖";
    }

    private static string GreetParent(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "bố mẹ";
        var trimmed = name.Trim();
        if (MomNamePattern.IsMatch(trimmed)) return "mẹ";
        if (trimmed.StartsWith("Bố", StringComparison.OrdinalIgnoreCase)) return "bố";
        return trimmed;
    }

    private static string ShortName(string displayName)
    {
        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : displayName.Trim();
    }

    private static string? TrimOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen];
    }

    private static FamilyChildGratitudeDto Map(
        FamilyGratitudeRepository.GratitudeRow row,
        bool alreadySent) =>
        new(
            row.Id,
            row.FamilyId,
            row.FromMemberId,
            row.FromMemberName,
            row.ToMemberId,
            row.ToMemberName,
            row.FlowDate,
            row.MessageVi,
            row.PraiseContext,
            row.CreatedAt,
            row.ReadAt,
            alreadySent);
}

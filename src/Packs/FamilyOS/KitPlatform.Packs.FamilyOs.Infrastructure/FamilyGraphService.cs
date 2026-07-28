using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyGraphService : IFamilyGraphService
{
    private readonly FamilyGraphRepository _repo;
    private readonly IFamilyCommercialService _commercial;

    public FamilyGraphService(FamilyGraphRepository repo, IFamilyCommercialService commercial)
    {
        _repo = repo;
        _commercial = commercial;
    }

    public async Task<IReadOnlyList<FamilyDto>> ListFamiliesAsync(CancellationToken cancellationToken = default)
    {
        var families = await _repo.ListFamiliesAsync(cancellationToken);
        var result = new List<FamilyDto>(families.Count);
        foreach (var family in families)
        {
            var members = await _repo.ListMembersAsync(family.Id, cancellationToken);
            result.Add(MapFamily(family, members));
        }

        return result;
    }

    public async Task<FamilyDto?> GetFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        var family = await _repo.GetFamilyAsync(familyId, cancellationToken);
        if (family is null) return null;
        var members = await _repo.ListMembersAsync(familyId, cancellationToken);
        return MapFamily(family, members);
    }

    public async Task<FamilyDto> CreateFamilyAsync(
        CreateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.DisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("displayName là bắt buộc.");

        var timezone = string.IsNullOrWhiteSpace(request.Timezone)
            ? "Asia/Ho_Chi_Minh"
            : request.Timezone.Trim();

        var familyId = await _repo.InsertFamilyAsync(name, timezone, cancellationToken);

        if (request.Members is { Count: > 0 })
        {
            var order = 0;
            foreach (var member in request.Members)
            {
                await InsertValidatedMemberAsync(
                    familyId,
                    member.DisplayName,
                    member.RoleCode,
                    member.DateOfBirth,
                    member.AccountId,
                    member.SortOrder ?? order++,
                    cancellationToken);
            }
        }

        return (await GetFamilyAsync(familyId, cancellationToken))!;
    }

    public async Task<FamilyDto> UpdateFamilyAsync(
        Guid familyId,
        UpdateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var name = string.IsNullOrWhiteSpace(request.DisplayName)
            ? existing.DisplayName
            : request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("displayName là bắt buộc.");
        if (name.Length > 120)
            throw new InvalidOperationException("displayName tối đa 120 ký tự.");

        var timezone = string.IsNullOrWhiteSpace(request.Timezone)
            ? existing.Timezone
            : request.Timezone.Trim();
        if (string.IsNullOrWhiteSpace(timezone))
            throw new InvalidOperationException("timezone là bắt buộc.");

        await _repo.UpdateFamilyAsync(familyId, name, timezone, cancellationToken);
        return (await GetFamilyAsync(familyId, cancellationToken))!;
    }

    public async Task<FamilyMembershipDto> AddMemberAsync(
        Guid familyId,
        AddMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _repo.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var role = (request.RoleCode ?? "").Trim().ToLowerInvariant();
        if (role == FamilyMembershipRoles.Child)
            await _commercial.EnsureCanAddChildAsync(familyId, cancellationToken);

        var row = await InsertValidatedMemberAsync(
            family.Id,
            request.DisplayName,
            request.RoleCode,
            request.DateOfBirth,
            request.AccountId,
            request.SortOrder ?? 0,
            cancellationToken);

        return MapMember(row);
    }

    public async Task<FamilyMembershipDto> UpdateMemberAsync(
        Guid familyId,
        Guid memberId,
        UpdateMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _repo.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var existing = await _repo.GetMemberAsync(familyId, memberId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy thành viên.");

        var name = string.IsNullOrWhiteSpace(request.DisplayName)
            ? existing.DisplayName
            : request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("displayName thành viên là bắt buộc.");

        var role = string.IsNullOrWhiteSpace(request.RoleCode)
            ? existing.RoleCode
            : request.RoleCode.Trim().ToLowerInvariant();
        if (!FamilyMembershipRoles.All.Contains(role))
            throw new InvalidOperationException(
                "roleCode phải là guardian | caregiver | child | viewer.");

        var status = string.IsNullOrWhiteSpace(request.Status)
            ? existing.Status
            : request.Status.Trim().ToLowerInvariant();
        if (status is not ("active" or "invited" or "archived"))
            throw new InvalidOperationException("status phải là active | invited | archived.");

        DateOnly? dateOfBirth = existing.DateOfBirth;
        if (request.ClearDateOfBirth)
            dateOfBirth = null;
        else if (request.DateOfBirth.HasValue)
            dateOfBirth = request.DateOfBirth;

        var sortOrder = request.SortOrder ?? existing.SortOrder;

        var row = await _repo.UpdateMemberAsync(
            familyId, memberId, name, role, dateOfBirth, sortOrder, status, cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được thành viên.");

        return MapMember(row);
    }

    private async Task<FamilyGraphRepository.MembershipRow> InsertValidatedMemberAsync(
        Guid familyId,
        string? displayName,
        string? roleCode,
        DateOnly? dateOfBirth,
        Guid? accountId,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var name = (displayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("displayName thành viên là bắt buộc.");

        var role = (roleCode ?? "").Trim().ToLowerInvariant();
        if (!FamilyMembershipRoles.All.Contains(role))
            throw new InvalidOperationException(
                "roleCode phải là guardian | caregiver | child | viewer.");

        return await _repo.InsertMemberAsync(
            familyId, name, role, dateOfBirth, accountId, sortOrder, cancellationToken);
    }

    private static FamilyDto MapFamily(
        FamilyGraphRepository.FamilyRow family,
        IReadOnlyList<FamilyGraphRepository.MembershipRow> members) =>
        new(
            family.Id,
            family.DisplayName,
            family.Timezone,
            family.Status,
            family.CreatedAt,
            members.Select(MapMember).ToList());

    private static FamilyMembershipDto MapMember(FamilyGraphRepository.MembershipRow m) =>
        new(
            m.Id,
            m.FamilyId,
            m.DisplayName,
            m.RoleCode,
            m.DateOfBirth,
            m.AccountId,
            m.SortOrder,
            m.Status);
}

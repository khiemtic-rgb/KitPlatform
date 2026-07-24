using System.Text.Json;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAgreementService : IFamilyAgreementService
{
    private readonly FamilyAgreementRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyAccountabilityOptionRepository _options;

    public FamilyAgreementService(
        FamilyAgreementRepository repo,
        FamilyGraphRepository families,
        FamilyAccountabilityOptionRepository options)
    {
        _repo = repo;
        _families = families;
        _options = options;
    }

    public async Task<IReadOnlyList<FamilyAgreementDto>> ListAsync(
        Guid familyId,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        if (filter is not null && !FamilyAgreementStatuses.All.Contains(filter))
            throw new InvalidOperationException("status không hợp lệ.");

        var rows = await _repo.ListAsync(familyId, filter, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyAgreementDto?> GetAsync(
        Guid familyId,
        Guid agreementId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var row = await _repo.GetAsync(familyId, agreementId, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<FamilyAgreementDto> CreateAsync(
        Guid familyId,
        CreateFamilyAgreementRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        await EnsureDefaultOptionsAsync(familyId, cancellationToken);

        var title = (request.Title ?? "").Trim();
        var body = (request.ProposalBody ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("title và proposalBody là bắt buộc.");

        RejectHarmfulLanguage(title + " " + body + " " + (request.TermsJson ?? ""));

        if (!await MemberExistsAsync(familyId, request.ProposedBy, cancellationToken))
            throw new InvalidOperationException("proposedBy không thuộc gia đình này.");

        var targetType = FamilyAgreementCategories.Normalize(request.TargetType);

        if (request.AppliesToMemberId is Guid mid &&
            !await MemberExistsAsync(familyId, mid, cancellationToken))
            throw new InvalidOperationException("appliesToMemberId không thuộc gia đình này.");

        var purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim();
        var reviewDays = request.ReviewAfterDays;
        if (reviewDays is int d && (d < 1 || d > 730))
            throw new InvalidOperationException("reviewAfterDays phải từ 1–730.");

        var termsJson = await NormalizeTermsJsonAsync(
            familyId, request.TermsJson, targetType, purpose, cancellationToken);

        var inserted = await _repo.InsertAsync(
            familyId,
            request.ProposedBy,
            title,
            body,
            targetType,
            request.TargetId,
            termsJson,
            purpose,
            request.EffectiveOn,
            reviewDays,
            request.AppliesToMemberId,
            cancellationToken);

        return (await GetAsync(familyId, inserted.Id, cancellationToken))!;
    }

    public async Task<FamilyAgreementDto> DecideAsync(
        Guid familyId,
        Guid agreementId,
        DecideFamilyAgreementRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (status is not (
            FamilyAgreementStatuses.Accepted or
            FamilyAgreementStatuses.Rejected or
            FamilyAgreementStatuses.Withdrawn or
            FamilyAgreementStatuses.Discussing))
        {
            throw new InvalidOperationException(
                "status quyết định phải là discussing | accepted | rejected | withdrawn.");
        }

        if (!await MemberExistsAsync(familyId, request.DecidedBy, cancellationToken))
            throw new InvalidOperationException("decidedBy không thuộc gia đình này.");

        var existing = await _repo.GetAsync(familyId, agreementId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy thỏa thuận.");

        if (existing.Status is FamilyAgreementStatuses.Rejected
            or FamilyAgreementStatuses.Withdrawn)
        {
            throw new InvalidOperationException("Thỏa thuận đã kết thúc — không đổi tiếp.");
        }

        // Accepted chỉ được Rút lại (retire); đổi nội dung đi qua đề xuất mới (change).
        if (existing.Status is FamilyAgreementStatuses.Accepted
            && status != FamilyAgreementStatuses.Withdrawn)
        {
            throw new InvalidOperationException(
                "Thỏa thuận đã Đồng ý — chỉ có thể Rút lại, hoặc tạo Đề xuất thay đổi.");
        }

        var note = string.IsNullOrWhiteSpace(request.DecisionNote)
            ? null
            : request.DecisionNote.Trim();
        if (note is not null)
            RejectHarmfulLanguage(note);

        var updated = await _repo.DecideAsync(
            familyId, agreementId, status, request.DecidedBy, note, cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được thỏa thuận.");

        return (await GetAsync(familyId, updated.Id, cancellationToken))!;
    }

    public IReadOnlyList<FamilyConsequenceLibrary.Item> ListConsequenceLibrary() =>
        FamilyAccountabilityDefaults.ConsequenceItems;

    public async Task<IReadOnlyList<FamilyAccountabilityOptionDto>> ListOptionsAsync(
        Guid familyId,
        string? kind = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        await EnsureDefaultOptionsAsync(familyId, cancellationToken);

        string? filter = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            filter = kind.Trim().ToLowerInvariant();
            if (!FamilyAccountabilityOptionKinds.All.Contains(filter))
                throw new InvalidOperationException("kind phải là consequence | reward.");
        }

        var rows = await _options.ListAsync(familyId, filter, cancellationToken);
        return rows.Select(MapOption).ToList();
    }

    public async Task<FamilyAccountabilityOptionDto> CreateOptionAsync(
        Guid familyId,
        CreateAccountabilityOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        await EnsureDefaultOptionsAsync(familyId, cancellationToken);

        var kind = (request.Kind ?? "").Trim().ToLowerInvariant();
        if (!FamilyAccountabilityOptionKinds.All.Contains(kind))
            throw new InvalidOperationException("kind phải là consequence | reward.");

        var code = (request.Code ?? "").Trim().ToLowerInvariant();
        if (!FamilyConsequenceLibrary.IsValidCode(code))
            throw new InvalidOperationException(
                "code phải dạng snake_case (a-z, 0-9, _), dài 3–49).");

        var group = (request.OptionGroup ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(group) || group.Length > 40)
            throw new InvalidOperationException("optionGroup là bắt buộc (≤ 40 ký tự).");

        var label = (request.LabelVi ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label))
            throw new InvalidOperationException("labelVi là bắt buộc.");

        var description = (request.DescriptionVi ?? "").Trim();
        RejectHarmfulLanguage(label + " " + description + " " + code + " " + group);

        if (await _options.CodeExistsAsync(familyId, kind, code, cancellationToken))
            throw new InvalidOperationException("code đã tồn tại trong catalog nhà này.");

        var row = await _options.InsertAsync(
            familyId,
            kind,
            code,
            group,
            label,
            description,
            request.SortOrder ?? 500,
            cancellationToken);

        return MapOption(row);
    }

    public async Task<FamilyAccountabilityOptionDto> UpdateOptionAsync(
        Guid familyId,
        Guid optionId,
        UpdateAccountabilityOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);

        var existing = await _options.GetAsync(familyId, optionId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy mục catalog.");

        var group = string.IsNullOrWhiteSpace(request.OptionGroup)
            ? existing.OptionGroup
            : request.OptionGroup.Trim().ToLowerInvariant();
        var label = string.IsNullOrWhiteSpace(request.LabelVi)
            ? existing.LabelVi
            : request.LabelVi.Trim();
        var description = request.DescriptionVi is null
            ? existing.DescriptionVi
            : request.DescriptionVi.Trim();
        var sortOrder = request.SortOrder ?? existing.SortOrder;
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? existing.Status
            : request.Status.Trim().ToLowerInvariant();

        if (status is not ("active" or "archived"))
            throw new InvalidOperationException("status phải là active | archived.");

        RejectHarmfulLanguage(label + " " + description + " " + group);

        var row = await _options.UpdateAsync(
            familyId, optionId, group, label, description, sortOrder, status, cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được mục catalog.");

        return MapOption(row);
    }

    public async Task DeleteOptionAsync(
        Guid familyId,
        Guid optionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);

        var existing = await _options.GetAsync(familyId, optionId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy mục catalog.");

        if (existing.IsSystem)
            throw new InvalidOperationException(
                "Mục hệ thống không xóa được — dùng Ẩn, hoặc Sửa nhãn nếu cần.");

        await _options.SoftDeleteAsync(familyId, optionId, cancellationToken);
    }

    private async Task EnsureDefaultOptionsAsync(Guid familyId, CancellationToken cancellationToken)
    {
        foreach (var item in FamilyAccountabilityDefaults.All)
            await _options.EnsureDefaultRowAsync(familyId, item, cancellationToken);
    }

    private async Task EnsureFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");
    }

    private async Task<bool> MemberExistsAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        return members.Any(m => m.Id == memberId);
    }

    private async Task<string> NormalizeTermsJsonAsync(
        Guid familyId,
        string? raw,
        string category,
        string? purpose,
        CancellationToken cancellationToken)
    {
        var bag = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("terms phải là JSON object.");

                foreach (var prop in doc.RootElement.EnumerateObject())
                    bag[prop.Name] = prop.Value.Clone();
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("terms JSON không hợp lệ.");
            }
        }

        bag["schemaVersion"] = JsonDocument.Parse("2").RootElement.Clone();
        if (!string.IsNullOrWhiteSpace(purpose))
            bag["purpose"] = JsonDocument.Parse(JsonSerializer.Serialize(purpose)).RootElement.Clone();

        async Task EnsureCodeAsync(string kind, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if (!await _options.ActiveCodeExistsAsync(familyId, kind, code, cancellationToken))
            {
                throw new InvalidOperationException(
                    kind == FamilyAccountabilityOptionKinds.Consequence
                        ? "consequenceCode phải nằm trong catalog hậu quả active của nhà."
                        : "rewardCode phải nằm trong catalog thưởng active của nhà.");
            }
        }

        if (bag.TryGetValue("consequenceCode", out var cEl))
            await EnsureCodeAsync(FamilyAccountabilityOptionKinds.Consequence, cEl.GetString() ?? "");
        if (bag.TryGetValue("rewardCode", out var rEl))
            await EnsureCodeAsync(FamilyAccountabilityOptionKinds.Reward, rEl.GetString() ?? "");

        if (bag.TryGetValue("result", out var resultEl) && resultEl.ValueKind == JsonValueKind.Object)
        {
            var kind = resultEl.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
            var code = resultEl.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
            if (kind.Equals("consequence", StringComparison.OrdinalIgnoreCase))
                await EnsureCodeAsync(FamilyAccountabilityOptionKinds.Consequence, code);
            if (kind.Equals("reward", StringComparison.OrdinalIgnoreCase))
                await EnsureCodeAsync(FamilyAccountabilityOptionKinds.Reward, code);
        }

        // Soft hint: accountability without codes is allowed (free-text proposal)
        _ = category;

        using var stream = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in bag)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void RejectHarmfulLanguage(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var pattern in FamilyConsequenceLibrary.ForbiddenPatterns)
        {
            if (lower.Contains(pattern, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Nội dung chứa hình thức không được hỗ trợ (gây hại / xúc phạm). Hãy chọn hoặc tạo mục trong catalog an toàn.");
            }
        }
    }

    private static FamilyAgreementDto Map(FamilyAgreementRepository.AgreementRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.ProposedBy,
            row.ProposedByName,
            row.Title,
            row.ProposalBody,
            row.TargetType,
            row.TargetId,
            row.Status,
            string.IsNullOrWhiteSpace(row.TermsJson) ? "{}" : row.TermsJson,
            row.DecidedAt,
            row.DecidedBy,
            row.DecisionNote,
            row.CreatedAt,
            row.Purpose,
            row.EffectiveOn,
            row.ReviewAfterDays,
            row.AppliesToMemberId);

    private static FamilyAccountabilityOptionDto MapOption(
        FamilyAccountabilityOptionRepository.OptionRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.Kind,
            row.Code,
            row.OptionGroup,
            row.LabelVi,
            row.DescriptionVi,
            row.IsSystem,
            row.SortOrder,
            row.Status);
}

using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Packs.Clinic.Infrastructure;

internal sealed class ClinicProviderRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ClinicProviderRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectCols = """
        id AS Id,
        provider_code AS ProviderCode,
        display_name AS DisplayName,
        specialty AS Specialty,
        license_no AS LicenseNo,
        status AS Status,
        NULLIF(settings->>'connectDoctorId', '')::uuid AS ConnectDoctorId,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt,
        phone AS Phone,
        email AS Email,
        title AS Title,
        notes AS Notes
        """;

    public async Task<IReadOnlyList<ClinicProviderDto>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectCols}
            FROM pack_clinic.clinic_provider
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND (@IncludeInactive OR status = 1)
            ORDER BY display_name
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ClinicProviderDto>(sql, new
        {
            TenantId,
            IncludeInactive = includeInactive,
        })).ToList();
    }

    public async Task<ClinicProviderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectCols}
            FROM pack_clinic.clinic_provider
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ClinicProviderDto>(sql, new { Id = id, TenantId });
    }

    public async Task<ClinicProviderDto?> FindByConnectDoctorIdAsync(
        Guid connectDoctorId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectCols}
            FROM pack_clinic.clinic_provider
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND settings->>'connectDoctorId' = @DoctorId
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ClinicProviderDto>(sql, new
        {
            TenantId,
            DoctorId = connectDoctorId.ToString(),
        });
    }

    public async Task<ClinicProviderDto?> FindByLicenseAsync(
        string licenseNo,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectCols}
            FROM pack_clinic.clinic_provider
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND lower(trim(license_no)) = lower(trim(@LicenseNo))
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ClinicProviderDto>(sql, new
        {
            TenantId,
            LicenseNo = licenseNo,
        });
    }

    public async Task<Guid> CreateAsync(
        CreateClinicProviderRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_clinic.clinic_provider (
                tenant_id, provider_code, display_name, specialty, license_no, status, settings,
                phone, email, title, notes
            )
            VALUES (
                @TenantId, @ProviderCode, @DisplayName, @Specialty, @LicenseNo, @Status,
                CASE WHEN @ConnectDoctorId IS NULL THEN '{}'::jsonb
                     ELSE jsonb_build_object('connectDoctorId', @ConnectDoctorId::text)
                END,
                @Phone, @Email, @Title, @Notes
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            request.ProviderCode,
            request.DisplayName,
            request.Specialty,
            request.LicenseNo,
            request.Status,
            ConnectDoctorId = request.ConnectDoctorId,
            request.Phone,
            request.Email,
            request.Title,
            request.Notes,
        });
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string displayName,
        string? specialty,
        string? licenseNo,
        short status,
        Guid? connectDoctorId,
        bool clearConnectDoctorId,
        string? phone,
        string? email,
        string? title,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_clinic.clinic_provider
            SET display_name = @DisplayName,
                specialty = @Specialty,
                license_no = @LicenseNo,
                status = @Status,
                phone = @Phone,
                email = @Email,
                title = @Title,
                notes = @Notes,
                settings = CASE
                    WHEN @ClearConnectDoctorId THEN COALESCE(settings, '{}'::jsonb) - 'connectDoctorId'
                    WHEN @ConnectDoctorId IS NOT NULL THEN jsonb_set(
                        COALESCE(settings, '{}'::jsonb),
                        '{connectDoctorId}',
                        to_jsonb(@ConnectDoctorId::text),
                        true)
                    ELSE COALESCE(settings, '{}'::jsonb)
                END,
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            DisplayName = displayName,
            Specialty = specialty,
            LicenseNo = licenseNo,
            Status = status,
            ConnectDoctorId = connectDoctorId,
            ClearConnectDoctorId = clearConnectDoctorId,
            Phone = phone,
            Email = email,
            Title = title,
            Notes = notes,
        });
        return n > 0;
    }

    public async Task<bool> CodeExistsAsync(string providerCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM pack_clinic.clinic_provider
            WHERE tenant_id = @TenantId
              AND provider_code = @Code
              AND deleted_at IS NULL
              AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var found = await conn.QuerySingleOrDefaultAsync<int?>(sql, new
        {
            TenantId,
            Code = providerCode,
            ExcludeId = excludeId,
        });
        return found is not null;
    }

    public async Task<ConnectDoctorSnap?> GetActiveConnectDoctorAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.id AS DoctorId,
                d.full_name AS FullName,
                d.phone AS Phone,
                d.license_number AS License,
                d.specialty AS Specialty
            FROM pack_connect.doctors d
            INNER JOIN pack_connect.doctor_memberships m
                ON m.doctor_id = d.id
               AND m.clinic_tenant_id = @TenantId
               AND m.membership_status = 'active'
               AND m.deleted_at IS NULL
            WHERE d.id = @DoctorId
              AND d.deleted_at IS NULL
              AND d.status = 'active'
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ConnectDoctorSnap>(sql, new
        {
            TenantId,
            DoctorId = doctorId,
        });
    }

    internal sealed class ConnectDoctorSnap
    {
        public Guid DoctorId { get; init; }
        public string FullName { get; init; } = "";
        public string? Phone { get; init; }
        public string? License { get; init; }
        public string? Specialty { get; init; }
    }
}

internal sealed class ClinicProviderService : IClinicProviderService
{
    private readonly ClinicProviderRepository _repo;

    public ClinicProviderService(ClinicProviderRepository repo) => _repo = repo;

    public Task<IReadOnlyList<ClinicProviderDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        _repo.ListAsync(includeInactive, cancellationToken);

    public Task<ClinicProviderDto?> GetAsync(Guid providerId, CancellationToken cancellationToken = default) =>
        _repo.GetAsync(providerId, cancellationToken);

    public async Task<ClinicProviderDto> CreateAsync(
        CreateClinicProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.ProviderCode?.Trim().ToUpperInvariant()
            ?? throw new InvalidOperationException("Mã bác sĩ không được để trống.");
        if (code.Length is < 2 or > 50)
            throw new InvalidOperationException("Mã bác sĩ phải từ 2–50 ký tự.");

        var name = request.DisplayName?.Trim()
            ?? throw new InvalidOperationException("Tên hiển thị không được để trống.");
        if (name.Length < 2)
            throw new InvalidOperationException("Tên hiển thị không hợp lệ.");

        if (await _repo.CodeExistsAsync(code, null, cancellationToken))
            throw new InvalidOperationException("Mã bác sĩ đã tồn tại.");

        var status = request.Status is 0 or 1 ? request.Status : (short)1;
        var id = await _repo.CreateAsync(
            new CreateClinicProviderRequest(
                code,
                name,
                string.IsNullOrWhiteSpace(request.Specialty) ? null : request.Specialty.Trim(),
                string.IsNullOrWhiteSpace(request.LicenseNo) ? null : request.LicenseNo.Trim(),
                status,
                request.ConnectDoctorId,
                string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()),
            cancellationToken);

        return (await _repo.GetAsync(id, cancellationToken))!;
    }

    public async Task<ClinicProviderDto?> UpdateAsync(
        Guid providerId,
        UpdateClinicProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetAsync(providerId, cancellationToken);
        if (existing is null) return null;

        var name = string.IsNullOrWhiteSpace(request.DisplayName)
            ? existing.DisplayName
            : request.DisplayName.Trim();
        if (name.Length < 2)
            throw new InvalidOperationException("Tên hiển thị không hợp lệ.");

        var specialty = request.Specialty is null
            ? existing.Specialty
            : (string.IsNullOrWhiteSpace(request.Specialty) ? null : request.Specialty.Trim());
        var license = request.LicenseNo is null
            ? existing.LicenseNo
            : (string.IsNullOrWhiteSpace(request.LicenseNo) ? null : request.LicenseNo.Trim());
        var status = request.Status is short s && s is 0 or 1 ? s : existing.Status;
        var phone = request.Phone is null
            ? existing.Phone
            : (string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim());
        var email = request.Email is null
            ? existing.Email
            : (string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim());
        var title = request.Title is null
            ? existing.Title
            : (string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim());
        var notes = request.Notes is null
            ? existing.Notes
            : (string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

        Guid? connectId = existing.ConnectDoctorId;
        var clear = request.ClearConnectDoctorId;
        if (clear)
            connectId = null;
        else if (request.ConnectDoctorId.HasValue)
            connectId = request.ConnectDoctorId;

        var ok = await _repo.UpdateAsync(
            providerId,
            name,
            specialty,
            license,
            status,
            clear ? null : connectId,
            clear,
            phone,
            email,
            title,
            notes,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetAsync(providerId, cancellationToken);
    }

    public async Task<ClinicProviderDto> UpsertFromConnectAsync(
        UpsertClinicProviderFromConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _repo.GetActiveConnectDoctorAsync(request.ConnectDoctorId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Không tìm thấy bác sĩ Connect đang active trên phòng khám này.");

        var linked = await _repo.FindByConnectDoctorIdAsync(request.ConnectDoctorId, cancellationToken);
        if (linked is not null)
            return linked;

        var license = string.IsNullOrWhiteSpace(request.LicenseNo)
            ? doctor.License
            : request.LicenseNo.Trim();
        if (!string.IsNullOrWhiteSpace(license))
        {
            var byLicense = await _repo.FindByLicenseAsync(license, cancellationToken);
            if (byLicense is not null)
            {
                await _repo.UpdateAsync(
                    byLicense.Id,
                    byLicense.DisplayName,
                    byLicense.Specialty,
                    byLicense.LicenseNo,
                    byLicense.Status,
                    request.ConnectDoctorId,
                    clearConnectDoctorId: false,
                    string.IsNullOrWhiteSpace(byLicense.Phone) ? doctor.Phone : byLicense.Phone,
                    byLicense.Email,
                    byLicense.Title,
                    byLicense.Notes,
                    cancellationToken);
                return (await _repo.GetAsync(byLicense.Id, cancellationToken))!;
            }
        }

        var name = string.IsNullOrWhiteSpace(request.DisplayName)
            ? doctor.FullName
            : request.DisplayName.Trim();
        var specialty = string.IsNullOrWhiteSpace(request.Specialty)
            ? doctor.Specialty
            : request.Specialty.Trim();

        var code = request.ProviderCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            var suffix = request.ConnectDoctorId.ToString("N")[^4..].ToUpperInvariant();
            code = $"BS-C{suffix}";
        }

        var n = 0;
        var candidate = code;
        while (await _repo.CodeExistsAsync(candidate, null, cancellationToken))
        {
            n++;
            candidate = $"{code}-{n}";
            if (n > 20) throw new InvalidOperationException("Không tạo được mã bác sĩ duy nhất.");
        }

        return await CreateAsync(
            new CreateClinicProviderRequest(
                candidate,
                name,
                specialty,
                string.IsNullOrWhiteSpace(license) ? null : license,
                1,
                request.ConnectDoctorId,
                string.IsNullOrWhiteSpace(doctor.Phone) ? null : doctor.Phone.Trim()),
            cancellationToken);
    }
}

internal sealed class ClinicTenantSettingsService : IClinicTenantSettingsService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ClinicTenantSettingsService(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ClinicTenantSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                settings->'clinic' AS ClinicJson,
                tenant_name AS TenantName
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<ClinicSettingsRow>(sql, new
        {
            TenantId = _tenant.TenantId,
        });
        return Parse(row?.ClinicJson, row?.TenantName);
    }

    public async Task<ClinicTenantSettingsDto> UpdateAsync(
        UpdateClinicTenantSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Tên phòng khám trên đơn không được để trống.");

        var dto = new ClinicTenantSettingsDto(
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            string.IsNullOrWhiteSpace(request.WorkingHours) ? null : request.WorkingHours.Trim());

        var json = JsonSerializer.Serialize(new
        {
            name = dto.Name,
            address = dto.Address,
            phone = dto.Phone,
            workingHours = dto.WorkingHours,
        });

        const string sql = """
            UPDATE tenants
            SET settings = jsonb_set(
                COALESCE(settings, '{}'::jsonb),
                '{clinic}',
                @ClinicJson::jsonb,
                true
            ),
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            ClinicJson = json,
        });
        return dto;
    }

    private static ClinicTenantSettingsDto Parse(string? json, string? tenantName)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "{}" or "null")
        {
            return new ClinicTenantSettingsDto(
                string.IsNullOrWhiteSpace(tenantName) ? "Novixa Clinic" : tenantName.Trim(),
                null,
                null,
                null);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? Get(string a, string b)
            {
                if (root.TryGetProperty(a, out var p) && p.ValueKind == JsonValueKind.String)
                    return p.GetString();
                if (root.TryGetProperty(b, out var q) && q.ValueKind == JsonValueKind.String)
                    return q.GetString();
                return null;
            }

            var name = Get("name", "Name");
            return new ClinicTenantSettingsDto(
                string.IsNullOrWhiteSpace(name)
                    ? (string.IsNullOrWhiteSpace(tenantName) ? "Novixa Clinic" : tenantName.Trim())
                    : name.Trim(),
                Get("address", "Address"),
                Get("phone", "Phone"),
                Get("workingHours", "WorkingHours") ?? Get("working_hours", "working_hours"));
        }
        catch
        {
            return new ClinicTenantSettingsDto(
                string.IsNullOrWhiteSpace(tenantName) ? "Novixa Clinic" : tenantName.Trim(),
                null,
                null,
                null);
        }
    }

    private sealed class ClinicSettingsRow
    {
        public string? ClinicJson { get; init; }
        public string? TenantName { get; init; }
    }
}

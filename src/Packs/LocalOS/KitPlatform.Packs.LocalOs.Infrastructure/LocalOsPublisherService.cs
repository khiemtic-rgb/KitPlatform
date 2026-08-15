using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsPublisherService : ILocalOsPublisherService
{
    private static readonly string[] BlockWords =
    [
        "livestream", "tiktok", "nạp tiền", "nap tien", "đa cấp", "da cap",
        "crypto", "giữ chỗ", "giu cho", "phí ứng tuyển", "phi ung tuyen",
        "triệu/ngày", "trieu/ngay", "triệu / ngày",
    ];

    private readonly IDbConnectionFactory _db;
    private readonly ILocalOsListingService _listings;
    private readonly IHostEnvironment _env;
    private readonly ILogger<LocalOsPublisherService> _log;

    public LocalOsPublisherService(
        IDbConnectionFactory db,
        ILocalOsListingService listings,
        IHostEnvironment env,
        ILogger<LocalOsPublisherService> log)
    {
        _db = db;
        _listings = listings;
        _env = env;
        _log = log;
    }

    public async Task<RequestPublisherOtpResult> RequestOtpAsync(
        string phone,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePhone(phone);
        if (normalized is null)
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var hash = Hash(code);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_local.publisher_otp (id, phone, code_hash, expires_at)
                VALUES (@Id, @Phone, @Hash, NOW() + INTERVAL '10 minutes')
                """,
                new { Id = Guid.CreateVersion7(), Phone = normalized, Hash = hash },
                cancellationToken: cancellationToken));
        _log.LogInformation("Local OS publisher OTP requested for {Phone}", Mask(normalized));
        return new RequestPublisherOtpResult(true, _env.IsDevelopment() ? code : null);
    }

    public async Task<PublisherSessionDto?> VerifyOtpAsync(
        string phone,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePhone(phone);
        if (normalized is null || string.IsNullOrWhiteSpace(code))
            return null;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var otpId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                """
                SELECT id FROM pack_local.publisher_otp
                WHERE phone = @Phone AND code_hash = @Hash AND consumed_at IS NULL AND expires_at > NOW()
                ORDER BY created_at DESC
                LIMIT 1
                """,
                new { Phone = normalized, Hash = Hash(code.Trim()) },
                cancellationToken: cancellationToken));
        if (otpId is null)
            return null;

        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE pack_local.publisher_otp SET consumed_at = NOW() WHERE id = @Id",
                new { Id = otpId.Value },
                cancellationToken: cancellationToken));

        var publisherId = await EnsurePublisherAsync(conn, normalized, cancellationToken);

        var token = Guid.CreateVersion7().ToString("N");
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_local.publisher_session (id, publisher_id, token, expires_at)
                VALUES (@Id, @PublisherId, @Token, NOW() + INTERVAL '2 hours')
                """,
                new { Id = Guid.CreateVersion7(), PublisherId = publisherId, Token = token },
                cancellationToken: cancellationToken));

        var count = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM pack_local.listing WHERE publisher_id = @Id",
                new { Id = publisherId },
                cancellationToken: cancellationToken));

        return new PublisherSessionDto(token, publisherId, normalized, count);
    }

    public async Task<PublishJobResult> PublishJobAsync(
        PublishJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var kind = (request.Kind ?? "job").Trim().ToLowerInvariant();
        if (kind is not ("job" or "event" or "room"))
            throw new InvalidOperationException("Loại tin chưa mở trên form đăng.");

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.PlaceText)
            || string.IsNullOrWhiteSpace(request.ContactName))
            throw new InvalidOperationException("Thiếu tiêu đề, địa điểm hoặc người liên hệ.");

        if (kind == "job" && string.IsNullOrWhiteSpace(request.WorkingTime))
            throw new InvalidOperationException("Việc làm cần thời gian làm việc.");

        var startAt = ParseVn(request.StartAt);
        var endAt = ParseVn(request.EndAt);
        if (kind == "event" && startAt is null)
            throw new InvalidOperationException("Sự kiện cần ngày giờ bắt đầu.");

        var blob = $"{request.Title} {request.Requirements} {request.SalaryText}";
        if (BlockWords.Any(w => blob.Contains(w, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Tin không đạt điều kiện đăng (phí / livestream / đa cấp).");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var session = await ResolvePublisherAsync(conn, request, cancellationToken);

        var today = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*) FROM pack_local.listing
                WHERE publisher_id = @Id AND created_at::date = (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                """,
                new { Id = session.PublisherId },
                cancellationToken: cancellationToken));
        if (today >= 3)
            throw new InvalidOperationException("Mỗi số điện thoại đăng tối đa 3 tin/ngày.");

        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE pack_local.publisher SET name = @Name, updated_at = NOW() WHERE id = @Id AND (name IS NULL OR name = '')",
                new { Id = session.PublisherId, Name = request.ContactName.Trim() },
                cancellationToken: cancellationToken));

        var template = (request.Template ?? "").Trim().ToLowerInvariant();
        if (kind == "job" && template is not ("part_time" or "full_time" or "intern"))
            template = "part_time";
        var roomType = (request.RoomType ?? "").Trim().ToLowerInvariant();
        if (kind == "room" && roomType is not ("private" or "shared" or "transfer"))
            roomType = "private";
        var category = request.Categories is { Count: > 0 }
            ? string.Join(",", request.Categories.Select(c => c.Trim()).Where(c => c.Length > 0).Take(4))
            : kind == "job" ? template : kind == "room" ? roomType : (template.Length > 0 ? template : kind);

        var listing = await _listings.CreateAsync(
            new UpsertLocalListingRequest(
                Kind: kind,
                Title: request.Title.Trim(),
                Summary: BuildSummary(request),
                OrganizationName: request.ContactName.Trim(),
                PlaceText: request.PlaceText.Trim(),
                Audience: ["student"],
                CityCode: LocalOsPackDefinition.DefaultCityCode,
                SourceKind: "submit",
                SourceUrl: string.IsNullOrWhiteSpace(request.RegistrationUrl) ? null : request.RegistrationUrl.Trim(),
                ContactPhone: session.Phone,
                ContactName: request.ContactName.Trim(),
                SalaryText: kind == "job" ? request.SalaryText?.Trim() : null,
                WorkingTime: kind == "job" ? request.WorkingTime?.Trim() : null,
                EmploymentType: kind == "job" ? template : null,
                Category: category,
                Requirements: request.Requirements?.Trim(),
                StartAt: kind == "event" ? startAt : null,
                EndAt: kind == "event" ? endAt : null,
                RegistrationUrl: kind == "event" && !string.IsNullOrWhiteSpace(request.RegistrationUrl)
                    ? request.RegistrationUrl.Trim()
                    : null,
                PriceMonth: null,
                RoomType: kind == "room" ? roomType : null,
                Trust: "UNVERIFIED",
                SafetyFlag: false,
                Status: "NEEDS_REVIEW"),
            cancellationToken);

        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE pack_local.listing SET publisher_id = @Pid WHERE id = @Id",
                new { Pid = session.PublisherId, listing.Id },
                cancellationToken: cancellationToken));

        var groups = await RecommendGroupsAsync(kind, "student", cancellationToken);
        var publicUrl = kind == "event" ? $"/su-kien/{listing.Id}"
            : kind == "room" ? $"/tro/{listing.Id}"
            : $"/viec/{listing.Id}";
        return new PublishJobResult(
            listing,
            BuildShareText(listing, publicUrl),
            publicUrl,
            groups,
            "Tin đã nhận. Lên site sau khi Thái Nguyên Life duyệt. Bạn có thể copy nội dung và tự dán lên group.");
    }

    public async Task<IReadOnlyList<CommunityGroupDto>> RecommendGroupsAsync(
        string? category,
        string audience,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CommunityGroupDto>(
            new CommandDefinition(
                """
                SELECT id AS Id, name AS Name, url AS Url, platform AS Platform,
                       category AS Category, audience AS Audience, geo AS Geo
                FROM pack_local.community_group
                WHERE status = 'active'
                  AND geo = @Geo
                  AND (@Category IS NULL OR category = @Category)
                  AND (audience = @Audience OR audience = 'mixed')
                ORDER BY CASE WHEN audience = @Audience THEN 0 ELSE 1 END, name
                LIMIT 8
                """,
                new
                {
                    Geo = LocalOsPackDefinition.DefaultCityCode,
                    Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant(),
                    Audience = string.IsNullOrWhiteSpace(audience) ? "student" : audience.Trim(),
                },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task TrackShareAsync(TrackShareRequest request, CancellationToken cancellationToken = default)
    {
        var kind = request.EventKind.Trim().ToLowerInvariant();
        if (kind is not ("copy" or "open"))
            kind = "copy";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_local.share_event (id, listing_id, group_id, event_kind)
                VALUES (@Id, @ListingId, @GroupId, @Kind)
                """,
                new
                {
                    Id = Guid.CreateVersion7(),
                    request.ListingId,
                    request.GroupId,
                    Kind = kind,
                },
                cancellationToken: cancellationToken));
    }

    private async Task<PublisherSessionRow> ResolvePublisherAsync(
        System.Data.IDbConnection conn,
        PublishJobRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var session = await conn.QuerySingleOrDefaultAsync<PublisherSessionRow>(
                new CommandDefinition(
                    """
                    SELECT s.publisher_id AS PublisherId, p.phone AS Phone
                    FROM pack_local.publisher_session s
                    JOIN pack_local.publisher p ON p.id = s.publisher_id
                    WHERE s.token = @Token AND s.expires_at > NOW()
                    """,
                    new { request.Token },
                    cancellationToken: cancellationToken));
            if (session is null)
                throw new InvalidOperationException("Phiên đăng tin hết hạn. Gửi lại tin kèm số điện thoại.");
            return session;
        }

        var phone = NormalizePhone(request.Phone);
        if (phone is null)
            throw new InvalidOperationException("Số điện thoại liên hệ không hợp lệ.");

        var publisherId = await EnsurePublisherAsync(conn, phone, cancellationToken);
        return new PublisherSessionRow { PublisherId = publisherId, Phone = phone };
    }

    private static async Task<Guid> EnsurePublisherAsync(
        System.Data.IDbConnection conn,
        string phone,
        CancellationToken cancellationToken)
    {
        var publisherId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT id FROM pack_local.publisher WHERE phone = @Phone",
                new { Phone = phone },
                cancellationToken: cancellationToken));
        if (publisherId is not null)
            return publisherId.Value;

        var id = Guid.CreateVersion7();
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_local.publisher (id, name, phone)
                VALUES (@Id, '', @Phone)
                """,
                new { Id = id, Phone = phone },
                cancellationToken: cancellationToken));
        return id;
    }

    private static string BuildSummary(PublishJobRequest r)
    {
        var qty = string.IsNullOrWhiteSpace(r.Quantity) ? "" : $"Số lượng: {r.Quantity.Trim()}. ";
        var req = string.IsNullOrWhiteSpace(r.Requirements) ? "" : r.Requirements.Trim();
        return $"{qty}{req}".Trim();
    }

    private static DateTimeOffset? ParseVn(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (!DateTime.TryParse(raw, out var dt))
            return null;
        var vn = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
        return vn.ToUniversalTime();
    }

    private static string BuildShareText(LocalListingDto listing, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine(listing.Kind switch
        {
            "event" => "📅 SỰ KIỆN: " + listing.Title.ToUpperInvariant(),
            "room" => "🏠 PHÒNG TRỌ: " + listing.Title.ToUpperInvariant(),
            _ => "📢 TUYỂN " + listing.Title.ToUpperInvariant(),
        });
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(listing.PlaceText))
            sb.AppendLine("📍 Địa điểm: " + listing.PlaceText);
        if (listing.StartAt is { } start)
            sb.AppendLine("⏰ Thời gian: " + start.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm"));
        if (!string.IsNullOrWhiteSpace(listing.WorkingTime))
            sb.AppendLine("⏰ Thời gian: " + listing.WorkingTime);
        if (listing.Kind == "room")
            sb.AppendLine("💰 Giá: liên hệ khi gọi");
        else if (!string.IsNullOrWhiteSpace(listing.SalaryText))
            sb.AppendLine("💰 Thu nhập: " + listing.SalaryText);
        if (!string.IsNullOrWhiteSpace(listing.Requirements))
        {
            sb.AppendLine();
            sb.AppendLine(listing.Kind == "room" ? "📝 Mô tả:" : "👤 Yêu cầu:");
            sb.AppendLine(listing.Requirements);
        }
        if (!string.IsNullOrWhiteSpace(listing.ContactPhone))
        {
            sb.AppendLine();
            sb.AppendLine("📞 Liên hệ: " + listing.ContactPhone);
        }
        sb.AppendLine();
        sb.AppendLine("🔎 Thái Nguyên Life (link chi tiết sau khi duyệt):");
        sb.AppendLine("https://thainguyenlife.vn" + path);
        return sb.ToString().Trim();
    }

    internal static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84") && digits.Length >= 11)
            digits = "0" + digits[2..];
        if (digits.Length is < 9 or > 12)
            return null;
        return digits;
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string Mask(string phone) =>
        phone.Length <= 4 ? "****" : phone[..2] + new string('*', phone.Length - 4) + phone[^2..];

    private sealed class PublisherSessionRow
    {
        public Guid PublisherId { get; set; }
        public string Phone { get; set; } = "";
    }
}

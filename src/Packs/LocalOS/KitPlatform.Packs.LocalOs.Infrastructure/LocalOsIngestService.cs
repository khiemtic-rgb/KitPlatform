using System.Net.Http.Headers;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsIngestService : ILocalOsIngestService
{
    private readonly IDbConnectionFactory _db;
    private readonly ILocalOsListingService _listings;
    private readonly ILocalOsSourceService _sources;

    public LocalOsIngestService(
        IDbConnectionFactory db,
        ILocalOsListingService listings,
        ILocalOsSourceService sources)
    {
        _db = db;
        _listings = listings;
        _sources = sources;
    }

    public async Task<IngestFromSourceResult> IngestAsync(
        IngestFromSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!LocalOsSourceLink.TryParse(request.SourceUrl, out var uri, out var linkKind) || uri is null)
            throw new InvalidOperationException("Link không hợp lệ. Dán URL http/https của một bài, không phải trang chủ.");

        if (linkKind == LocalOsSourceLinkKind.FacebookGroupFeed)
            throw new InvalidOperationException(
                "Đây là link hội nhóm, không phải link một bài. Mở bài viết → copy URL bài. Hệ thống không quét group.");

        var sourceUrl = uri.GetLeftPart(UriPartial.Query).TrimEnd('?');
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existingId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                """
                SELECT id FROM pack_local.listing
                WHERE source_url = @Url
                ORDER BY created_at DESC
                LIMIT 1
                """,
                new { Url = sourceUrl },
                cancellationToken: cancellationToken));
        if (existingId is Guid found)
        {
            var row = await _listings.GetAsync(found, publicOnly: false, cancellationToken);
            if (row is not null)
                return new IngestFromSourceResult(row, "Link này đã có trong hàng chờ / danh sách. Không tạo trùng.", true);
        }

        var pasted = (request.PastedText ?? "").Trim();
        var fetched = "";
        var note = "Đã đưa vào chờ duyệt. Site công khai chỉ hiện sau khi bạn bấm Đăng.";

        if (linkKind == LocalOsSourceLinkKind.FacebookPost)
        {
            if (pasted.Length < 12)
                throw new InvalidOperationException(
                    "Facebook không cho máy đọc bài. Dán nội dung bài (copy chữ trên Facebook) kèm link bài.");
            note = "Đã vào chờ duyệt từ bài Facebook (máy không đọc FB — dùng nội dung bạn dán). Duyệt tay trước khi lên site.";
        }
        else if (pasted.Length < 12)
        {
            fetched = await TryFetchPublicPageAsync(uri, cancellationToken);
            if (fetched.Length < 8)
                throw new InvalidOperationException("Không đọc được trang. Dán thêm nội dung bài, rồi thử lại.");
            note = "Đã lấy tiêu đề/mô tả công khai từ trang (không phải Facebook). Vào chờ duyệt.";
        }

        var blob = pasted.Length >= 12 ? pasted : fetched;
        var kind = LocalOsTextExtract.GuessKind(blob, request.Kind?.Trim().ToLowerInvariant());
        var title = LocalOsTextExtract.GuessTitle(blob);
        var unsafeHit = LocalOsTextExtract.LooksUnsafe(blob);
        var registry = await _sources.ListAsync(cancellationToken);
        var matched = LocalOsSourceMatch.Find(registry, uri, request.SourceId);
        var listingSourceKind = matched?.SourceKind
            ?? (linkKind == LocalOsSourceLinkKind.FacebookPost ? "facebook_post_paste" : "url_paste");
        if (matched is not null)
            note += $" Nguồn: {matched.Name}.";
        if (kind == "room")
            note += " Không lưu giá phòng — site hiện «Giá liên hệ» (giá đổi theo thời điểm).";
        LocalListingDto listing;
        try
        {
            listing = await _listings.CreateAsync(
            new UpsertLocalListingRequest(
                Kind: kind,
                Title: title,
                Summary: blob.Length > 2000 ? blob[..2000] : blob,
                OrganizationName: null,
                PlaceText: LocalOsTextExtract.GuessPlace(blob),
                Audience: matched is { Audience.Length: > 0 } ? [matched.Audience] : ["student"],
                CityCode: LocalOsPackDefinition.DefaultCityCode,
                SourceKind: listingSourceKind,
                SourceUrl: sourceUrl,
                ContactPhone: LocalOsTextExtract.GuessPhone(blob),
                ContactName: null,
                SalaryText: kind == "room" ? null : LocalOsTextExtract.GuessSalary(blob),
                WorkingTime: null,
                EmploymentType: kind == "job" ? "part_time" : null,
                Category: null,
                Requirements: null,
                StartAt: null,
                EndAt: null,
                RegistrationUrl: null,
                PriceMonth: null,
                RoomType: null,
                Trust: "UNVERIFIED",
                SafetyFlag: unsafeHit,
                Status: "NEEDS_REVIEW",
                SourceId: matched?.Id),
            cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("uq_local_listing_source_url", StringComparison.OrdinalIgnoreCase)
                                   || ex.InnerException?.Message.Contains("uq_local_listing_source_url", StringComparison.OrdinalIgnoreCase) == true)
        {
            var again = await conn.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "SELECT id FROM pack_local.listing WHERE source_url = @Url LIMIT 1",
                    new { Url = sourceUrl },
                    cancellationToken: cancellationToken));
            if (again is Guid id)
            {
                var row = await _listings.GetAsync(id, publicOnly: false, cancellationToken);
                if (row is not null)
                    return new IngestFromSourceResult(row, "Link này đã có. Không tạo trùng.", true);
            }
            throw;
        }

        if (unsafeHit)
            note += " Gắn cờ an toàn (phí / livestream / đa cấp) — đọc kỹ trước khi đăng.";
        return new IngestFromSourceResult(listing, note, false);
    }

    private static async Task<string> TryFetchPublicPageAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (LocalOsSourceLink.IsFacebookHost(uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase)))
            return "";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ThaiNguyenLife", "1.0"));
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resp.IsSuccessStatusCode)
                return "";
            var media = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (media.Length > 0 && !media.Contains("html", StringComparison.OrdinalIgnoreCase) && !media.Contains("text", StringComparison.OrdinalIgnoreCase))
                return "";
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (raw.Length > 400_000)
                raw = raw[..400_000];
            return LocalOsTextExtract.StripHtml(raw);
        }
        catch (Exception)
        {
            return "";
        }
    }
}

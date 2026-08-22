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
        var pasted = (request.PastedText ?? "").Trim();
        var rawUrl = (request.SourceUrl ?? "").Trim();
        if (rawUrl.Length == 0)
            rawUrl = LocalOsTextExtract.FirstHttpUrl(pasted) ?? "";

        if (rawUrl.Length == 0)
            return await CreateFromPasteAsync(pasted, request, sourceUrl: null, listingSourceKind: "group_paste", cancellationToken);

        if (!LocalOsSourceLink.TryParse(rawUrl, out var uri, out var linkKind) || uri is null)
        {
            if (pasted.Length >= 12)
                return await CreateFromPasteAsync(pasted, request, sourceUrl: null, listingSourceKind: "group_paste", cancellationToken);
            throw new InvalidOperationException("Dán nội dung bài, hoặc kèm link bài http/https.");
        }

        if (linkKind == LocalOsSourceLinkKind.FacebookGroupFeed)
        {
            if (pasted.Length >= 12)
                return await CreateFromPasteAsync(pasted, request, sourceUrl: null, listingSourceKind: "group_paste", cancellationToken);
            throw new InvalidOperationException("Dán nội dung bài (copy chữ trên bài). Link hội nhóm không đủ.");
        }

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

        var fetched = "";
        if (linkKind == LocalOsSourceLinkKind.FacebookPost && pasted.Length < 12)
            throw new InvalidOperationException("Dán nội dung bài kèm link, nếu có.");
        if (pasted.Length < 12)
        {
            fetched = await TryFetchPublicPageAsync(uri, cancellationToken);
            if (fetched.Length < 8)
                throw new InvalidOperationException("Không đọc được trang. Dán thêm nội dung bài.");
        }

        var blob = pasted.Length >= 12 ? pasted : fetched;
        var listingSourceKind = linkKind == LocalOsSourceLinkKind.FacebookPost ? "facebook_post_paste" : "url_paste";
        try
        {
            return await CreateFromPasteAsync(blob, request, sourceUrl, listingSourceKind, cancellationToken, uri);
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
                    return new IngestFromSourceResult(row, "Link này đã có trong danh sách.", true);
            }
            throw;
        }
    }

    private async Task<IngestFromSourceResult> CreateFromPasteAsync(
        string blob,
        IngestFromSourceRequest request,
        string? sourceUrl,
        string listingSourceKind,
        CancellationToken cancellationToken,
        Uri? uri = null)
    {
        if (blob.Trim().Length < 12)
            throw new InvalidOperationException("Dán nội dung bài (vài dòng chữ).");

        var kind = LocalOsTextExtract.GuessKind(blob, request.Kind?.Trim().ToLowerInvariant());
        var title = LocalOsTextExtract.GuessTitle(blob);
        var unsafeHit = LocalOsTextExtract.LooksUnsafe(blob);
        var registry = await _sources.ListAsync(cancellationToken);
        var matched = request.SourceId is not null || uri is not null
            ? LocalOsSourceMatch.Find(registry, uri ?? new Uri("https://thainguyenlife.vn/"), request.SourceId)
            : null;
        var sourceKind = matched?.SourceKind ?? listingSourceKind;
        var place = LocalOsTextExtract.GuessPlace(blob);
        var phone = LocalOsTextExtract.GuessPhone(blob);
        var existing = await _listings.FindDuplicateAsync(
            kind, title, place, phone, blob, sourceUrl, excludeId: null, onlyActive: false, cancellationToken);
        if (existing is not null)
            return new IngestFromSourceResult(existing, "Tin này đã có trong danh sách. Không thêm trùng.", true);

        var trustedWatch = request.FromWatch
            && matched is not null
            && matched.SourceKind is "official_web" or "partner" or "rss"
            && !string.Equals(matched.Platform, "facebook", StringComparison.OrdinalIgnoreCase)
            && !unsafeHit;
        var listing = await _listings.CreateAsync(
            new UpsertLocalListingRequest(
                Kind: kind,
                Title: title,
                Summary: blob.Length > 2000 ? blob[..2000] : blob,
                OrganizationName: null,
                PlaceText: place,
                Audience: matched is { Audience.Length: > 0 } ? [matched.Audience] : ["student"],
                CityCode: LocalOsPackDefinition.DefaultCityCode,
                SourceKind: sourceKind,
                SourceUrl: sourceUrl,
                ContactPhone: phone,
                ContactName: null,
                SalaryText: kind == "room" ? null : LocalOsTextExtract.GuessSalary(blob),
                WorkingTime: null,
                EmploymentType: kind == "job" ? "part_time" : null,
                Category: kind == "event" ? LocalOsTextExtract.GuessEventCategory(blob) : null,
                Requirements: null,
                StartAt: null,
                EndAt: null,
                RegistrationUrl: null,
                PriceMonth: null,
                RoomType: null,
                Trust: trustedWatch ? "SOURCE_TRUSTED" : "UNVERIFIED",
                SafetyFlag: unsafeHit,
                Status: trustedWatch ? "ACTIVE" : "NEEDS_REVIEW",
                SourceId: matched?.Id),
            cancellationToken);
        var note = trustedWatch
            ? "Đã đăng từ nguồn tin cậy."
            : "Đã thêm vào danh sách. Viết lại rồi duyệt trước khi đăng.";
        if (matched is not null)
            note += $" Nguồn: {matched.Name}.";
        if (unsafeHit)
            note += " Đọc kỹ trước khi đăng.";
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

using System.Text.Json;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Api.LocalOs;

/// <summary>KIT_MKT publish → pack_local article → thainguyenlife.vn. No Novixa/Famixa CTA.</summary>
public sealed class ContentLocalOsPublisher : IContentLocalOsPublisher
{
    public const string SourceKind = "kit_mkt";

    private readonly ILocalOsListingService _listings;
    private readonly ILocalOsHomepagePush _homepage;
    private readonly ILogger<ContentLocalOsPublisher> _log;

    public ContentLocalOsPublisher(
        ILocalOsListingService listings,
        ILocalOsHomepagePush homepage,
        ILogger<ContentLocalOsPublisher> log)
    {
        _listings = listings;
        _homepage = homepage;
        _log = log;
    }

    public static string TopicSourceUrl(Guid topicId) =>
        $"content://kit-mkt/topic/{topicId:N}";

    public async Task<ContentLocalOsPublishResult> PublishArticleAsync(
        ContentLocalOsPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = (request.Title ?? "").Trim();
        if (title.Length < 4)
            throw new InvalidOperationException("Bài chưa có tiêu đề — không đẩy Thái Nguyên Life.");
        var body = (request.BodyMarkdown ?? "").Trim();
        if (body.Length < 40)
            throw new InvalidOperationException("Bài quá ngắn — cần web_long trước khi đẩy Thái Nguyên Life.");

        var sourceUrl = TopicSourceUrl(request.TopicId);
        var org = string.IsNullOrWhiteSpace(request.BrandName) ? "Thái Nguyên Life" : request.BrandName.Trim();
        var upsert = new UpsertLocalListingRequest(
            Kind: "article",
            Title: title,
            Summary: body,
            OrganizationName: org,
            PlaceText: "Thái Nguyên",
            Audience: ["student"],
            CityCode: LocalOsPackDefinition.DefaultCityCode,
            SourceKind: SourceKind,
            SourceUrl: sourceUrl,
            ContactPhone: null,
            ContactName: null,
            SalaryText: request.SeoDescription,
            WorkingTime: null,
            EmploymentType: null,
            Category: "news",
            Requirements: null,
            StartAt: null,
            EndAt: null,
            RegistrationUrl: null,
            PriceMonth: null,
            RoomType: null,
            Trust: "SOURCE_TRUSTED",
            SafetyFlag: false,
            Status: "ACTIVE");

        var existing = await _listings.FindBySourceUrlAsync(sourceUrl, cancellationToken);
        LocalListingDto row;
        if (existing is null)
        {
            row = await _listings.CreateAsync(upsert, cancellationToken);
        }
        else
        {
            row = await _listings.UpdateAsync(existing.Id, upsert, cancellationToken)
                  ?? throw new InvalidOperationException("Không cập nhật được bài Thái Nguyên Life.");
            if (!string.Equals(row.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                row = await _listings.SetStatusAsync(row.Id, "ACTIVE", cancellationToken) ?? row;
        }

        var path = $"/tin/{row.Id:D}";
        try
        {
            await _homepage.PushAfterTrustedPublishAsync(1, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Published TNL article {Id} but homepage feed push failed", row.Id);
        }

        var json = JsonSerializer.Serialize(new
        {
            listingId = row.Id,
            path,
            url = "https://thainguyenlife.vn" + path,
        });
        return new ContentLocalOsPublishResult(row.Id, path, json);
    }
}

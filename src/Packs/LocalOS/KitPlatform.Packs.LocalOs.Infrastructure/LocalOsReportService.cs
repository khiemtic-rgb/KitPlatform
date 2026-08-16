using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsReportService : ILocalOsReportService
{
    private static readonly HashSet<string> Reasons = ["wrong_phone", "gone", "no_answer", "other"];

    private readonly IDbConnectionFactory _db;
    private readonly ILocalOsListingService _listings;

    public LocalOsReportService(IDbConnectionFactory db, ILocalOsListingService listings)
    {
        _db = db;
        _listings = listings;
    }

    public async Task<LocalListingReportDto?> SubmitAsync(
        Guid listingId,
        SubmitLocalListingReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetAsync(listingId, publicOnly: true, cancellationToken);
        if (listing is null) return null;

        var reason = (request.Reason ?? "").Trim().ToLowerInvariant();
        if (!Reasons.Contains(reason))
            throw new InvalidOperationException("Chọn lý do báo tin.");
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 280 }) note = note[..280];

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var recent = await conn.QuerySingleOrDefaultAsync<ReportRow>(
            new CommandDefinition(
                """
                SELECT r.id AS Id, r.listing_id AS ListingId, r.reason AS Reason, r.note AS Note,
                       r.created_at AS CreatedAt
                FROM pack_local.listing_report r
                WHERE r.listing_id = @ListingId
                  AND r.reason = @Reason
                  AND r.created_at > NOW() - INTERVAL '20 minutes'
                ORDER BY r.created_at DESC
                LIMIT 1
                """,
                new { ListingId = listingId, Reason = reason },
                cancellationToken: cancellationToken));
        if (recent is not null)
            return Map(recent, listing);

        var dayCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*) FROM pack_local.listing_report
                WHERE listing_id = @ListingId AND created_at > NOW() - INTERVAL '1 day'
                """,
                new { ListingId = listingId },
                cancellationToken: cancellationToken));
        if (dayCount >= 8)
        {
            var last = await conn.QuerySingleOrDefaultAsync<ReportRow>(
                new CommandDefinition(
                    """
                    SELECT r.id AS Id, r.listing_id AS ListingId, r.reason AS Reason, r.note AS Note,
                           r.created_at AS CreatedAt
                    FROM pack_local.listing_report r
                    WHERE r.listing_id = @ListingId
                    ORDER BY r.created_at DESC
                    LIMIT 1
                    """,
                    new { ListingId = listingId },
                    cancellationToken: cancellationToken));
            return last is null ? null : Map(last, listing);
        }

        var id = Guid.CreateVersion7();
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_local.listing_report (id, listing_id, reason, note)
                VALUES (@Id, @ListingId, @Reason, @Note)
                """,
                new { Id = id, ListingId = listingId, Reason = reason, Note = note },
                cancellationToken: cancellationToken));
        return new LocalListingReportDto(
            id, listingId, reason, note, DateTimeOffset.UtcNow,
            listing.Title, listing.Kind, listing.Status);
    }

    public async Task<IReadOnlyList<LocalListingReportDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ReportRow>(
            new CommandDefinition(
                """
                SELECT r.id AS Id, r.listing_id AS ListingId, r.reason AS Reason, r.note AS Note,
                       r.created_at AS CreatedAt,
                       l.title AS ListingTitle, l.kind AS ListingKind, l.status AS ListingStatus
                FROM pack_local.listing_report r
                JOIN pack_local.listing l ON l.id = r.listing_id
                ORDER BY r.created_at DESC
                LIMIT 300
                """,
                cancellationToken: cancellationToken));
        return rows.Select(r => new LocalListingReportDto(
            r.Id, r.ListingId, r.Reason, r.Note, r.CreatedAt,
            r.ListingTitle, r.ListingKind, r.ListingStatus)).ToList();
    }

    private static LocalListingReportDto Map(ReportRow r, LocalListingDto listing) =>
        new(r.Id, r.ListingId, r.Reason, r.Note, r.CreatedAt, listing.Title, listing.Kind, listing.Status);

    private sealed class ReportRow
    {
        public Guid Id { get; set; }
        public Guid ListingId { get; set; }
        public string Reason { get; set; } = "";
        public string? Note { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? ListingTitle { get; set; }
        public string? ListingKind { get; set; }
        public string? ListingStatus { get; set; }
    }
}

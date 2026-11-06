using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Read-only look at assessment_submission. Survey's repository is internal to that pack,
/// so Sales does not take a dependency and does not change Assessment.
/// </summary>
internal sealed class KitSalesAssessmentLookup
{
    private readonly IDbConnectionFactory _db;

    public KitSalesAssessmentLookup(IDbConnectionFactory db) => _db = db;

    public async Task<AssessmentLinkRow?> FindAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AssessmentLinkRow>(
            new CommandDefinition(
                """
                SELECT s.id AS Id,
                       s.tenant_id AS TenantId,
                       s.status AS Status,
                       s.respondent_phone AS Phone,
                       s.report_ready_at AS ReportReadyAt,
                       t.code AS TemplateCode
                FROM assessment_submission s
                JOIN assessment_template t ON t.id = s.template_id
                WHERE s.id = @Id
                """,
                new { Id = submissionId },
                cancellationToken: cancellationToken));
    }

    public static string? Deny(
        AssessmentLinkRow? row,
        Guid salesTenantId,
        IReadOnlyCollection<string> allowedTemplates,
        string? leadPhone)
    {
        if (row is null)
            return "Bài PHC không tồn tại.";
        if (row.Status == "draft")
            return "Bài PHC còn nháp.";
        if (row.TenantId is { } tenant && tenant != salesTenantId)
            return "Bài PHC thuộc tenant khác.";
        if (allowedTemplates.Count == 0)
            return "Product profile chưa có phcTemplateCodes.";
        if (!allowedTemplates.Contains(row.TemplateCode))
            return "Template PHC không nằm trong allowlist của sản phẩm.";
        var leadDigits = Digits(leadPhone);
        var submissionDigits = Digits(row.Phone);
        if (leadDigits.Length > 0 && submissionDigits.Length > 0 && leadDigits != submissionDigits)
            return "Số điện thoại lead và bài PHC không khớp.";
        if (row.Status is not ("completed" or "lead_captured" or "report_ready"))
            return "Trạng thái bài PHC không gắn được.";
        return null;
    }

    public static bool ReportReady(AssessmentLinkRow row) =>
        row.ReportReadyAt is not null || row.Status == "report_ready";

    public static string PhoneMatch(AssessmentLinkRow row, string? leadPhone)
    {
        var leadDigits = Digits(leadPhone);
        var submissionDigits = Digits(row.Phone);
        if (leadDigits.Length == 0 || submissionDigits.Length == 0)
            return "unknown";
        return leadDigits == submissionDigits ? "match" : "mismatch";
    }

    public static string Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return new string(value.Where(char.IsDigit).ToArray());
    }

    internal sealed class AssessmentLinkRow
    {
        public Guid Id { get; init; }
        public Guid? TenantId { get; init; }
        public string Status { get; init; } = "";
        public string? Phone { get; init; }
        public DateTime? ReportReadyAt { get; init; }
        public string TemplateCode { get; init; } = "";
    }
}

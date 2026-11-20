using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Pharmacy.Knowledge;

public static class PharmacyKnowledgeSeparationOfDuties
{
    public const string SelfApprovalMessage =
        "Người soạn hoặc người gửi duyệt không được tự duyệt phiên bản này. Cần một quản trị khác duyệt.";

    /// <summary>Approver must not be anyone who created, edited or submitted this version.</summary>
    public static void EnsureApproverIsNotAuthor(Guid approverUserId, IEnumerable<string?> authorUserIds)
    {
        foreach (var author in authorUserIds)
        {
            if (Guid.TryParse(author, out var id) && id == approverUserId)
                throw new InvalidOperationException(SelfApprovalMessage);
        }
    }
}

public static class PharmacyKnowledgeReviewDue
{
    public const int DefaultMonths = 12;
    public const int MaxMonths = 36;

    /// <summary>Calendar date only. Kind is Unspecified so Npgsql binds it as a plain timestamp, not timestamptz.</summary>
    public static DateTime TodayInVietnam(DateTime utcNow) =>
        DateTime.SpecifyKind(utcNow.AddHours(7).Date, DateTimeKind.Unspecified);

    public static DateTime Resolve(DateTime? requested, DateTime today)
    {
        var start = DateTime.SpecifyKind(today.Date, DateTimeKind.Unspecified);
        if (requested is not DateTime value)
            return start.AddMonths(DefaultMonths);
        var date = DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
        if (date <= start)
            throw new InvalidOperationException("Hạn rà soát phải sau hôm nay.");
        if (date > start.AddMonths(MaxMonths))
            throw new InvalidOperationException($"Hạn rà soát không quá {MaxMonths} tháng.");
        return date;
    }

    public static bool IsOverdue(DateTime? dueOn, DateTime today) =>
        dueOn is DateTime due && due.Date < today.Date;
}

public static class PharmacyKnowledgeSearchText
{
    public static readonly string SqlFrom;
    public static readonly string SqlTo;

    static PharmacyKnowledgeSearchText()
    {
        var from = new StringBuilder();
        var to = new StringBuilder();
        foreach (var (start, end) in new[] { (0x00C0, 0x024F), (0x1E00, 0x1EFF) })
        {
            for (var code = start; code <= end; code++)
            {
                var c = (char)code;
                var folded = Fold(c.ToString());
                if (folded.Length == 1 && folded[0] is >= 'a' and <= 'z')
                {
                    from.Append(c);
                    to.Append(folded[0]);
                }
            }
        }

        for (var mark = 0x0300; mark <= 0x036F; mark++)
            from.Append((char)mark);
        SqlFrom = from.ToString();
        SqlTo = to.ToString();
    }

    /// <summary>Lowercase, no Vietnamese tone or vowel marks, đ as d.</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(c switch
            {
                'đ' or 'Đ' => 'd',
                _ => char.ToLowerInvariant(c),
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>ILIKE pattern on folded text. Null when the query is blank.</summary>
    public static string? Pattern(string? query)
    {
        var text = Fold(query?.Trim())
            .Replace("%", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("\\", "", StringComparison.Ordinal)
            .Trim();
        if (text.Length == 0)
            return null;
        if (text.Length > 200)
            text = text[..200];
        return "%" + text + "%";
    }
}

public static class PharmacyKnowledgeFeedbackKinds
{
    public const string Open = "open";
    public const string Missing = "missing";
    public const string NotHelpful = "not_helpful";

    public static string NormalizeSubmitted(string? kind)
    {
        var value = kind?.Trim().ToLowerInvariant();
        return value is Missing or NotHelpful
            ? value
            : throw new InvalidOperationException("Loại phản hồi không hợp lệ.");
    }

    public static string? NormalizeQuery(string? query, string kind)
    {
        var text = query?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            if (kind == Missing)
                throw new InvalidOperationException("Cần ghi nội dung đang thiếu.");
            return null;
        }

        if (text.Length > 500)
            throw new InvalidOperationException("Nội dung phản hồi tối đa 500 ký tự.");
        return text;
    }
}

public sealed record PharmacyKnowledgeFeedbackRequest(
    Guid? KnowledgeId,
    string? Kind,
    string? Query);

public sealed record PharmacyKnowledgeFeedbackCountDto(
    Guid KnowledgeId,
    int OpenCount,
    int NotHelpfulCount,
    int MissingCount);

public sealed record PharmacyKnowledgeMissingDto(
    string Query,
    DateTime CreatedAt);

public sealed record PharmacyKnowledgeFeedbackSummaryDto(
    IReadOnlyList<PharmacyKnowledgeFeedbackCountDto> Items,
    IReadOnlyList<PharmacyKnowledgeMissingDto> RecentMissing,
    bool StoreReady);

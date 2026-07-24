using System.Text.RegularExpressions;

namespace KitPlatform.Packs.FamilyOs;

/// <summary>Mirror family-app ParentBoardView — which done items need parent check before stars post.</summary>
public static partial class FamilyCommitmentReview
{
    [GeneratedRegex(
        @"đánh răng|ăn sáng|ăn trưa|ăn tối|uống sữa|đi ngủ|ngủ|đi học|mặc|đồng phục|tắm|rửa mặt|rửa tay",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrustChildPattern();

    [GeneratedRegex(
        @"bài tập|học|dọn|phòng|luyện|đàn|piano|gấp|quần áo|đọc sách|viết|ôn|balo|cặp|chơi đàn",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NeedApprovalPattern();

    public static bool NeedsParentApproval(string? title, string? evidenceUrl)
    {
        var t = (title ?? "").Trim();
        if (NeedApprovalPattern().IsMatch(t))
            return true;
        if (TrustChildPattern().IsMatch(t))
            return false;
        return !string.IsNullOrWhiteSpace(evidenceUrl);
    }
}

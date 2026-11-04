using KitPlatform.Packs.Pharmacy.Knowledge;
using Xunit;

namespace KitPlatform.Application.Tests.Pharmacy;

public sealed class PharmacyKnowledgeOperationsTests
{
    private static readonly Guid Author = Guid.Parse("11111111-1111-4111-8111-1111111111a1");
    private static readonly Guid Approver = Guid.Parse("11111111-1111-4111-8111-1111111111a3");

    [Fact]
    public void Author_or_submitter_cannot_approve_own_version()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeSeparationOfDuties.EnsureApproverIsNotAuthor(Author, [Author.ToString("D")]));
        Assert.Equal(PharmacyKnowledgeSeparationOfDuties.SelfApprovalMessage, ex.Message);
        Assert.Contains("không được tự duyệt", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Author_match_ignores_guid_case()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeSeparationOfDuties.EnsureApproverIsNotAuthor(
                Author, [Approver.ToString(), Author.ToString("D").ToUpperInvariant()]));
    }

    [Fact]
    public void Different_admin_may_approve()
    {
        PharmacyKnowledgeSeparationOfDuties.EnsureApproverIsNotAuthor(
            Approver, [Author.ToString("D"), null, "", "not-a-guid"]);
    }

    [Fact]
    public void Review_due_defaults_to_twelve_months()
    {
        var today = new DateTime(2026, 11, 4);
        Assert.Equal(new DateTime(2027, 11, 4), PharmacyKnowledgeReviewDue.Resolve(null, today));
        Assert.Equal(DateTimeKind.Unspecified, PharmacyKnowledgeReviewDue.Resolve(null, today).Kind);
    }

    [Fact]
    public void Review_due_override_must_be_future_and_bounded()
    {
        var today = new DateTime(2026, 11, 4);
        Assert.Equal(new DateTime(2027, 5, 1), PharmacyKnowledgeReviewDue.Resolve(new DateTime(2027, 5, 1, 13, 0, 0), today));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeReviewDue.Resolve(today, today));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeReviewDue.Resolve(today.AddMonths(37), today));
    }

    [Fact]
    public void Overdue_only_after_due_date()
    {
        var today = new DateTime(2026, 11, 4);
        Assert.False(PharmacyKnowledgeReviewDue.IsOverdue(null, today));
        Assert.False(PharmacyKnowledgeReviewDue.IsOverdue(today, today));
        Assert.True(PharmacyKnowledgeReviewDue.IsOverdue(today.AddDays(-1), today));
    }

    [Fact]
    public void Vietnam_today_rolls_over_at_17_utc()
    {
        Assert.Equal(new DateTime(2026, 11, 5), PharmacyKnowledgeReviewDue.TodayInVietnam(new DateTime(2026, 11, 4, 17, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 11, 4), PharmacyKnowledgeReviewDue.TodayInVietnam(new DateTime(2026, 11, 4, 16, 59, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("Đau đầu", "dau dau")]
    [InlineData("HẠ SỐT", "ha sot")]
    [InlineData("thuốc ho trẻ em", "thuoc ho tre em")]
    [InlineData("Ưu tiên người già", "uu tien nguoi gia")]
    public void Fold_removes_vietnamese_marks(string input, string expected)
    {
        Assert.Equal(expected, PharmacyKnowledgeSearchText.Fold(input));
        Assert.Equal(expected, PharmacyKnowledgeSearchText.Fold(input.Normalize(System.Text.NormalizationForm.FormD)));
    }

    [Fact]
    public void Sql_translate_map_matches_csharp_fold()
    {
        var from = PharmacyKnowledgeSearchText.SqlFrom;
        var to = PharmacyKnowledgeSearchText.SqlTo;
        Assert.True(from.Length > to.Length, "combining marks are deleted by translate");
        Assert.Equal(from.Length, from.Distinct().Count());
        foreach (var c in "ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵĐÁỰ")
        {
            var index = from.IndexOf(c);
            Assert.True(index >= 0 && index < to.Length, $"missing {c}");
            Assert.Equal(PharmacyKnowledgeSearchText.Fold(c.ToString()), to[index].ToString());
        }
    }

    [Fact]
    public void Search_pattern_strips_wildcards_and_blank()
    {
        Assert.Null(PharmacyKnowledgeSearchText.Pattern("   "));
        Assert.Null(PharmacyKnowledgeSearchText.Pattern("%_"));
        Assert.Equal("%dau bung%", PharmacyKnowledgeSearchText.Pattern(" Đau bụng% "));
        Assert.Equal("%daubung%", PharmacyKnowledgeSearchText.Pattern("Đau_bụng"));
    }

    [Fact]
    public void Feedback_kinds_are_closed()
    {
        Assert.Equal("missing", PharmacyKnowledgeFeedbackKinds.NormalizeSubmitted("MISSING"));
        Assert.Equal("not_helpful", PharmacyKnowledgeFeedbackKinds.NormalizeSubmitted("not_helpful"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeFeedbackKinds.NormalizeSubmitted("open"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeFeedbackKinds.NormalizeQuery(" ", "missing"));
        Assert.Null(PharmacyKnowledgeFeedbackKinds.NormalizeQuery(null, "not_helpful"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeFeedbackKinds.NormalizeQuery(new string('a', 501), "missing"));
    }

    [Fact]
    public void Owned_and_regimen_approve_paths_enforce_separation_of_duties()
    {
        var root = RepoRoot();
        var owned = File.ReadAllText(Path.Combine(root, "src", "Packs", "Pharmacy", "KitPlatform.Packs.Pharmacy.Infrastructure", "Knowledge", "PharmacyOwnedKnowledgeGovernance.cs"));
        var regimen = File.ReadAllText(Path.Combine(root, "src", "Packs", "Pharmacy", "KitPlatform.Packs.Pharmacy.Infrastructure", "Knowledge", "PharmacyLocalRegimenService.cs"));
        Assert.Contains("PharmacyKnowledgeSeparationOfDuties.EnsureApproverIsNotAuthor", owned, StringComparison.Ordinal);
        Assert.Contains("PharmacyKnowledgeSeparationOfDuties.EnsureApproverIsNotAuthor", regimen, StringComparison.Ordinal);
    }

    [Fact]
    public void Permission_seed_never_inserts_platform_permissions()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "migrations", "432_pharmacy_knowledge_permissions.sql"));
        var body = string.Join('\n', sql.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        Assert.DoesNotContain(PharmacyKnowledgeApprovalAccess.PlatformPublishPermission, body, StringComparison.Ordinal);
        Assert.DoesNotContain(PharmacyKnowledgeApprovalAccess.ReviewPermission, body, StringComparison.Ordinal);
        Assert.DoesNotContain(PharmacyKnowledgeApprovalAccess.VerifySourcePermission, body, StringComparison.Ordinal);
        Assert.DoesNotContain("STAFF", body, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (permission_code) DO NOTHING", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Knowledge_manifest_is_isolated_from_prod()
    {
        var root = RepoRoot();
        var manifest = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.pharmacy-knowledge-v1.txt"));
        Assert.Contains("Do NOT merge into migration-files.prod.txt", manifest, StringComparison.Ordinal);
        var prod = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.prod.txt"));
        foreach (var file in new[] { "432_pharmacy_knowledge_permissions.sql", "433_pharmacy_owned_knowledge_review_due.sql" })
        {
            Assert.Contains(file, manifest, StringComparison.Ordinal);
            Assert.DoesNotContain(file, prod, StringComparison.Ordinal);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx")) && !Directory.Exists(Path.Combine(dir.FullName, "migrations")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root");
    }
}

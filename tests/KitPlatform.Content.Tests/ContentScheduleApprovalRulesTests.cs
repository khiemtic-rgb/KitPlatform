using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ContentScheduleApprovalRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Approve_ok_when_proposed_and_future()
        => Assert.Null(ContentScheduleApprovalRules.CheckApprove("Proposed", Now.AddHours(5), Now));

    [Fact]
    public void Approve_rejects_non_proposed()
        => Assert.NotNull(ContentScheduleApprovalRules.CheckApprove("Approved", Now.AddHours(5), Now));

    [Fact]
    public void Approve_rejects_past_time()
        => Assert.NotNull(ContentScheduleApprovalRules.CheckApprove("Proposed", Now.AddMinutes(-1), Now));

    [Fact]
    public void Approve_rejects_too_close()
        => Assert.NotNull(ContentScheduleApprovalRules.CheckApprove("Proposed", Now.AddMinutes(1), Now));

    [Fact]
    public void Cancel_ok_only_for_approved_with_queued_job()
        => Assert.Null(ContentScheduleApprovalRules.CheckCancel("Approved", "Queued"));

    [Theory]
    [InlineData("Proposed", "Queued")]
    [InlineData("Approved", "Running")]
    [InlineData("Approved", "Succeeded")]
    [InlineData("Approved", null)]
    public void Cancel_rejected_otherwise(string status, string? job)
        => Assert.NotNull(ContentScheduleApprovalRules.CheckCancel(status, job));
}

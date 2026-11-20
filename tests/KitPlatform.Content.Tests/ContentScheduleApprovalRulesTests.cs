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

    [Fact]
    public void Cancel_ok_for_approved_with_failed_job()
        => Assert.Null(ContentScheduleApprovalRules.CheckCancel("Approved", "Failed"));

    [Theory]
    [InlineData("Proposed", "Queued")]
    [InlineData("Approved", "Running")]
    [InlineData("Approved", "Succeeded")]
    [InlineData("Approved", null)]
    public void Cancel_rejected_otherwise(string status, string? job)
        => Assert.NotNull(ContentScheduleApprovalRules.CheckCancel(status, job));

    [Theory]
    [InlineData("Failed")]
    [InlineData("Queued")]
    public void Retry_ok_for_failed_or_waiting_job(string job)
        => Assert.Null(ContentScheduleApprovalRules.CheckRetry("Approved", job, Now.AddHours(-30), Now));

    [Fact]
    public void Retry_ok_for_job_stuck_running()
        => Assert.Null(ContentScheduleApprovalRules.CheckRetry("Approved", "Running", Now.AddHours(-2), Now));

    [Fact]
    public void Retry_rejects_job_running_now()
        => Assert.NotNull(ContentScheduleApprovalRules.CheckRetry("Approved", "Running", Now.AddMinutes(-5), Now));

    [Theory]
    [InlineData("Approved", "Succeeded")]
    [InlineData("Approved", "Cancelled")]
    [InlineData("Approved", null)]
    [InlineData("Proposed", "Failed")]
    public void Retry_rejected_otherwise(string status, string? job)
        => Assert.NotNull(ContentScheduleApprovalRules.CheckRetry(status, job, Now.AddHours(-2), Now));
}

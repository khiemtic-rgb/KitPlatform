namespace KitPlatform.Application.Customers;

public interface ICustomerMergeService
{
    Task<MergeCustomersResult> MergeAsync(
        MergeCustomersRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record MergeCustomersRequest(
    Guid KeeperCustomerId,
    Guid SourceCustomerId,
    string? Reason = null);

public sealed record MergeCustomersResult(
    Guid MergeId,
    Guid KeeperCustomerId,
    Guid SourceCustomerId,
    bool SourceSoftDeleted,
    int OrdersMoved,
    int PaymentsMoved,
    int LoyaltyProgramsMerged,
    int VouchersMoved,
    int ConsentsMoved);

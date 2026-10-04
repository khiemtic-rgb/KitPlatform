using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Workflow;

/// <summary>
/// Pharmacy workflow (POL-WF). Pilot: POS discount override via seeded <c>pos_discount_override</c>.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Validates discount against policy; if exceeded, requires an approved workflow task or creates a pending approval request.
    /// </summary>
    Task EnforcePosDiscountApprovalAsync(
        SaleOrderPricingResult pricing,
        SalesDiscountPolicy policy,
        Guid? approvedWorkflowTaskId = null,
        Guid? salesOrderId = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowTaskDecisionDto> ApprovePosDiscountTaskAsync(
        Guid taskId,
        bool approved,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTaskListItemDto>> ListPendingPosDiscountTasksAsync(
        CancellationToken cancellationToken = default);

    Task<bool> HasPendingPurchaseOrderTaskAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<Guid> SubmitPurchaseOrderApprovalAsync(
        Guid purchaseOrderId,
        string poNumber,
        string supplierName,
        decimal totalAmount,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetPurchaseOrderIdForPendingTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<WorkflowTaskDecisionDto> DecidePurchaseOrderTaskAsync(
        Guid taskId,
        bool approved,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTaskListItemDto>> ListPendingPurchaseOrderTasksAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Thrown when staff discount exceeds policy and manager approval is required.</summary>
public sealed class DiscountApprovalRequiredException : InvalidOperationException
{
    public DiscountApprovalRequiredException(Guid workflowTaskId, string message)
        : base(message)
    {
        WorkflowTaskId = workflowTaskId;
    }

    public Guid WorkflowTaskId { get; }
}

namespace KitPlatform.Application.CustomerApp;

public interface ICustomerAppLoginAdminService
{
    Task<IReadOnlyList<CustomerAppLoginRequestDto>> ListAsync(
        string? status,
        CancellationToken cancellationToken = default);

    Task<ApproveCustomerAppLoginResult> ApproveAsync(
        Guid requestId,
        Guid reviewedByUserId,
        CancellationToken cancellationToken = default);

    Task RejectAsync(
        Guid requestId,
        Guid reviewedByUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<CustomerAppAuthSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<CustomerAppAuthSettingsDto> UpdateSettingsAsync(
        UpdateCustomerAppAuthSettingsRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default);

    Task<IssueCounterPilotOtpResult> IssueCounterOtpAsync(
        IssueCounterPilotOtpRequest request,
        CancellationToken cancellationToken = default);
}

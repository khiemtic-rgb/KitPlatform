namespace KitPlatform.Application.Customers;

public interface ICustomerAdminService
{
    Task<PagedCustomersResult> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SimilarCustomerClustersResult> GetSimilarClustersAsync(
        double similarityThreshold = 0.8,
        CancellationToken cancellationToken = default);

    Task<SimilarCustomerNamesResult> FindSimilarNamesAsync(
        string fullName,
        Guid? excludeCustomerId = null,
        double similarityThreshold = 0.8,
        CancellationToken cancellationToken = default);

    Task<CustomerDetailDto?> GetAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<PagedCustomerOrdersResult> GetOrdersAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CustomerDetailDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerDetailDto?> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<string> GetNextCustomerCodeAsync(CancellationToken cancellationToken = default);
}

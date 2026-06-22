using PharmaCore.Application.Catalog;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class BrandService : IBrandService
{
    private readonly BrandRepository _repository;

    public BrandService(BrandRepository repository) => _repository = repository;

    public Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<BrandDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var id = await _repository.CreateAsync(request, cancellationToken);
        return (await _repository.GetAsync(id, cancellationToken))!;
    }

    public async Task<BrandDto?> UpdateAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var updated = await _repository.UpdateAsync(id, request, cancellationToken);
        return updated ? await _repository.GetAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.CountProductsAsync(id, cancellationToken) > 0)
            return (false, "Không xóa được: thương hiệu đang được sản phẩm sử dụng.");

        var deleted = await _repository.SoftDeleteAsync(id, cancellationToken);
        return deleted ? (true, null) : (false, "Thương hiệu không tồn tại.");
    }
}

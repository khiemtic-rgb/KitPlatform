using PharmaCore.Application.Catalog;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class CategoryService : ICategoryService
{
    private readonly CategoryRepository _repository;

    public CategoryService(CategoryRepository repository) => _repository = repository;

    public Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<CategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ParentId is not null)
        {
            var parent = await _repository.GetAsync(request.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new InvalidOperationException("Danh mục cha không tồn tại.");
        }

        var id = await _repository.CreateAsync(request, cancellationToken);
        return (await _repository.GetAsync(id, cancellationToken))!;
    }

    public async Task<CategoryDto?> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ParentId == id)
            throw new InvalidOperationException("Danh mục không thể là cha của chính nó.");

        if (request.ParentId is not null)
        {
            var parent = await _repository.GetAsync(request.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new InvalidOperationException("Danh mục cha không tồn tại.");
        }

        var updated = await _repository.UpdateAsync(id, request, cancellationToken);
        return updated ? await _repository.GetAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.CountChildCategoriesAsync(id, cancellationToken) > 0)
            return (false, "Không xóa được: danh mục còn danh mục con.");

        if (await _repository.CountProductsAsync(id, cancellationToken) > 0)
            return (false, "Không xóa được: danh mục đang được sản phẩm sử dụng.");

        var deleted = await _repository.SoftDeleteAsync(id, cancellationToken);
        return deleted ? (true, null) : (false, "Danh mục không tồn tại.");
    }
}

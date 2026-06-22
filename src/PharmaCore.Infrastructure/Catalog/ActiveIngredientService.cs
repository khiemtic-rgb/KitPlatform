using PharmaCore.Application.Catalog;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class ActiveIngredientService : IActiveIngredientService
{
    private readonly ActiveIngredientRepository _repository;

    public ActiveIngredientService(ActiveIngredientRepository repository) => _repository = repository;

    public Task<IReadOnlyList<ActiveIngredientDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<ActiveIngredientDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<ActiveIngredientDto> CreateAsync(
        CreateActiveIngredientRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.IngredientCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Mã hoạt chất không được để trống.");
        if (string.IsNullOrWhiteSpace(request.IngredientName))
            throw new InvalidOperationException("Tên hoạt chất không được để trống.");

        if (await _repository.CodeExistsAsync(code, null, cancellationToken))
            throw new InvalidOperationException($"Mã hoạt chất '{code}' đã tồn tại.");

        var id = await _repository.CreateAsync(request, cancellationToken);
        return (await _repository.GetAsync(id, cancellationToken))!;
    }

    public async Task<ActiveIngredientDto?> UpdateAsync(
        Guid id,
        UpdateActiveIngredientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IngredientName))
            throw new InvalidOperationException("Tên hoạt chất không được để trống.");

        var updated = await _repository.UpdateAsync(id, request, cancellationToken);
        return updated ? await _repository.GetAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.CountProductUsagesAsync(id, cancellationToken) > 0)
            return (false, "Không xóa được: hoạt chất đang được sản phẩm sử dụng.");

        var deactivated = await _repository.DeactivateAsync(id, cancellationToken);
        return deactivated ? (true, null) : (false, "Hoạt chất không tồn tại hoặc đã ngừng.");
    }
}

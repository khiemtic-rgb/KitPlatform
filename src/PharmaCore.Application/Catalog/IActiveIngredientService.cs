namespace PharmaCore.Application.Catalog;

public interface IActiveIngredientService
{
    Task<IReadOnlyList<ActiveIngredientDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ActiveIngredientDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ActiveIngredientDto> CreateAsync(CreateActiveIngredientRequest request, CancellationToken cancellationToken = default);
    Task<ActiveIngredientDto?> UpdateAsync(Guid id, UpdateActiveIngredientRequest request, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

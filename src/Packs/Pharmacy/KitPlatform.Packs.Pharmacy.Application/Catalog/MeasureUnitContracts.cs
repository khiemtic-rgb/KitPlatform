namespace KitPlatform.Packs.Pharmacy.Catalog;

public sealed record MeasureUnitDto(
    Guid Id,
    string UnitName,
    int SortOrder,
    short Status);

public sealed record CreateMeasureUnitRequest(
    string UnitName,
    int? SortOrder);

public sealed record UpdateMeasureUnitRequest(
    string UnitName,
    int SortOrder,
    short Status);

public interface IMeasureUnitService
{
    Task<IReadOnlyList<MeasureUnitDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MeasureUnitDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MeasureUnitDto> CreateAsync(CreateMeasureUnitRequest request, CancellationToken cancellationToken = default);
    Task<MeasureUnitDto?> UpdateAsync(Guid id, UpdateMeasureUnitRequest request, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

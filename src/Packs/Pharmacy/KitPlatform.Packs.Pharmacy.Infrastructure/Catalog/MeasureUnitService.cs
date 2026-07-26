using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class MeasureUnitService : IMeasureUnitService
{
    private readonly MeasureUnitRepository _repository;

    public MeasureUnitService(MeasureUnitRepository repository) => _repository = repository;

    public Task<IReadOnlyList<MeasureUnitDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<MeasureUnitDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<MeasureUnitDto> CreateAsync(CreateMeasureUnitRequest request, CancellationToken cancellationToken = default)
    {
        var unitName = request.UnitName?.Trim();
        if (string.IsNullOrEmpty(unitName))
            throw new InvalidOperationException("Tên đơn vị tính không được để trống.");
        if (unitName.Length > 50)
            throw new InvalidOperationException("Tên đơn vị tính tối đa 50 ký tự.");
        if (await _repository.ExistsByNameAsync(unitName, excludeId: null, cancellationToken))
            throw new InvalidOperationException($"Đơn vị tính \"{unitName}\" đã tồn tại.");

        var id = await _repository.CreateAsync(unitName, request.SortOrder ?? 0, cancellationToken);
        return (await _repository.GetAsync(id, cancellationToken))!;
    }

    public async Task<MeasureUnitDto?> UpdateAsync(Guid id, UpdateMeasureUnitRequest request, CancellationToken cancellationToken = default)
    {
        var unitName = request.UnitName?.Trim();
        if (string.IsNullOrEmpty(unitName))
            throw new InvalidOperationException("Tên đơn vị tính không được để trống.");
        if (unitName.Length > 50)
            throw new InvalidOperationException("Tên đơn vị tính tối đa 50 ký tự.");
        if (await _repository.ExistsByNameAsync(unitName, excludeId: id, cancellationToken))
            throw new InvalidOperationException($"Đơn vị tính \"{unitName}\" đã tồn tại.");

        var updated = await _repository.UpdateAsync(id, request with { UnitName = unitName }, cancellationToken);
        return updated ? await _repository.GetAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usages = await _repository.CountProductUsagesAsync(id, cancellationToken);
        if (usages > 0)
            return (false, $"Không xóa được: đơn vị tính đang được {usages} sản phẩm sử dụng. Hãy chuyển sang trạng thái Ngừng.");

        var deleted = await _repository.SoftDeleteAsync(id, cancellationToken);
        return deleted ? (true, null) : (false, "Đơn vị tính không tồn tại.");
    }
}

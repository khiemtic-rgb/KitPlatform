namespace PharmaCore.Application.Catalog;

public sealed record NationalDrugConnectionStatusDto(
    string Mode,
    string ModeLabel,
    bool IsLive,
    string? Message);

public sealed record NationalDrugFieldMapDto(
    string NationalField,
    string NationalLabel,
    string ProductField,
    string ProductLabel,
    string Notes);

public sealed record NationalDrugListItemDto(
    string DrugId,
    string RegistrationNumber,
    string ProductName,
    string? ActiveIngredient,
    string? Strength,
    string? DosageForm,
    string? UnitName,
    string? Manufacturer,
    string DrugCategoryLabel);

public sealed record PagedNationalDrugListResult(
    IReadOnlyList<NationalDrugListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record NationalDrugDetailDto(
    string DrugId,
    string RegistrationNumber,
    string ProductName,
    string? ActiveIngredient,
    string? Strength,
    string? DosageForm,
    string? Packaging,
    string? UnitName,
    string? Manufacturer,
    string? CountryOfOrigin,
    string DrugCategoryCode,
    string DrugCategoryLabel,
    string? Barcode,
    string? AtcCode,
    string? RouteOfAdministration,
    DateOnly? RegistrationExpiryDate);

public sealed record NationalDrugProductPrefillDto(
    string DrugId,
    string RegistrationNumber,
    string ProductName,
    string? GenericName,
    short DrugType,
    string SaleUnitName,
    string? Description,
    string? SuggestedBarcode);

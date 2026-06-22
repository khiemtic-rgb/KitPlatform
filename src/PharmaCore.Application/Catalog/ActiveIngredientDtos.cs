namespace PharmaCore.Application.Catalog;

public sealed record ActiveIngredientDto(
    Guid Id,
    string IngredientCode,
    string IngredientName,
    string? Description,
    short Status);

public sealed class CreateActiveIngredientRequest
{
    public required string IngredientCode { get; init; }
    public required string IngredientName { get; init; }
    public string? Description { get; init; }
}

public sealed class UpdateActiveIngredientRequest
{
    public required string IngredientName { get; init; }
    public string? Description { get; init; }
    public short Status { get; init; }
}

using System.Text.Json.Serialization;

namespace PharmaCore.Application.Catalog;

public sealed record ProductIngredientDto(
    Guid Id,
    Guid IngredientId,
    string IngredientCode,
    string IngredientName,
    decimal? StrengthValue,
    string? StrengthUnit);

public sealed record ProductIngredientSyncItem(
    [property: JsonPropertyName("ingredientId")] Guid IngredientId,
    [property: JsonPropertyName("strengthValue")] decimal? StrengthValue,
    [property: JsonPropertyName("strengthUnit")] string? StrengthUnit);

public class SyncProductIngredientsRequest
{
    [JsonPropertyName("ingredients")]
    public IReadOnlyList<ProductIngredientSyncItem>? Ingredients { get; init; }
}

public sealed class SyncProductIngredientsBodyRequest : SyncProductIngredientsRequest
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; init; }
}

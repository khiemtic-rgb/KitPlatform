using System.Text.Json.Serialization;

namespace PharmaCore.Application.Catalog;

public sealed record ProductUnitSyncItem(
    [property: JsonPropertyName("id")] Guid? Id,
    [property: JsonPropertyName("unitName")] string UnitName,
    [property: JsonPropertyName("conversionFactor")] decimal ConversionFactor,
    [property: JsonPropertyName("isBaseUnit")] bool IsBaseUnit,
    [property: JsonPropertyName("isSaleUnit")] bool IsSaleUnit);

public class SyncProductUnitsRequest
{
    [JsonPropertyName("units")]
    public IReadOnlyList<ProductUnitSyncItem>? Units { get; init; }
}

public sealed class SyncProductUnitsBodyRequest : SyncProductUnitsRequest
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; init; }
}

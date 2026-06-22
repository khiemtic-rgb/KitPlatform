using System.Text.Json.Serialization;

namespace PharmaCore.Application.Catalog;

public sealed record ProductImageItem(
    [property: JsonPropertyName("imageUrl")] string ImageUrl,
    [property: JsonPropertyName("isPrimary")] bool IsPrimary = false,
    [property: JsonPropertyName("sortOrder")] int SortOrder = 0);

public sealed record ProductBarcodeItem(
    [property: JsonPropertyName("barcode")] string Barcode,
    [property: JsonPropertyName("barcodeType")] short BarcodeType = 1);

public sealed record ProductBarcodeSyncItem(
    [property: JsonPropertyName("barcode")] string Barcode,
    [property: JsonPropertyName("isPrimary")] bool IsPrimary = false,
    [property: JsonPropertyName("barcodeType")] short BarcodeType = 1);

public sealed record ProductPriceItem(
    [property: JsonPropertyName("priceType")] short PriceType,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("productUnitId")] Guid? ProductUnitId = null);

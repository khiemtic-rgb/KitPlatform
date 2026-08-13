using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class CsdlDuocStockOutRequest
{
    [JsonPropertyName("transaction_date")]
    public string TransactionDate { get; init; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "sale-retail";

    [JsonPropertyName("reference_number")]
    public string ReferenceNumber { get; init; } = "";

    [JsonPropertyName("practice_license_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PracticeLicenseCode { get; init; }

    [JsonPropertyName("note")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }

    [JsonPropertyName("items")]
    public List<CsdlDuocStockOutItem> Items { get; init; } = [];
}

/// <summary>Phiếu nhập (stock-in) — Mục 5.4.1. Dùng chung items với stock-out.</summary>
internal sealed class CsdlDuocStockInRequest
{
    [JsonPropertyName("transaction_date")]
    public string TransactionDate { get; init; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "opening-balance";

    [JsonPropertyName("supplier_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SupplierId { get; init; }

    [JsonPropertyName("reference_number")]
    public string ReferenceNumber { get; init; } = "";

    [JsonPropertyName("practice_license_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PracticeLicenseCode { get; init; }

    [JsonPropertyName("note")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }

    [JsonPropertyName("items")]
    public List<CsdlDuocStockOutItem> Items { get; init; } = [];
}

internal sealed class CsdlDuocStockOutItem
{
    [JsonPropertyName("drug_id")]
    public string DrugId { get; init; } = "";

    [JsonPropertyName("unit_id")]
    public string UnitId { get; init; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    [JsonPropertyName("batch_no")]
    public string BatchNo { get; init; } = "";

    [JsonPropertyName("packaging_specifications")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PackagingSpecifications { get; init; }

    [JsonPropertyName("expiry_date")]
    public string ExpiryDate { get; init; } = "";

    [JsonPropertyName("manufacturer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CsdlDuocStockOutManufacturer? Manufacturer { get; init; }

    [JsonPropertyName("gtin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Gtin { get; init; }

    [JsonPropertyName("price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Price { get; init; }
}

internal sealed class CsdlDuocStockOutManufacturer
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("country")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Country { get; init; }
}

internal sealed class CsdlDuocTransactionCreateResponse
{
    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; init; }
}

internal sealed class CsdlDuocTransactionStatusResponse
{
    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("messages")]
    public List<string>? Messages { get; init; }

    [JsonPropertyName("submitted_at")]
    public DateTime? SubmittedAt { get; init; }
}

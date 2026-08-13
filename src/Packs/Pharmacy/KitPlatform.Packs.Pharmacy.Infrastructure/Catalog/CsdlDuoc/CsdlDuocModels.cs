using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class CsdlDuocTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }
}

internal sealed class CsdlDuocPagedDrugsResponse
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("data")]
    public List<CsdlDuocDrugDto>? Data { get; init; }
}

internal sealed class CsdlDuocDrugDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("drug_group_id")]
    public string? DrugGroupId { get; init; }

    [JsonPropertyName("registration_number")]
    public string? RegistrationNumber { get; init; }

    [JsonPropertyName("old_registration_number")]
    public string? OldRegistrationNumber { get; init; }

    [JsonPropertyName("active_pharmaceutical_ingredient")]
    public string? ActivePharmaceuticalIngredient { get; init; }

    [JsonPropertyName("strength")]
    public string? Strength { get; init; }

    [JsonPropertyName("routes")]
    public List<CsdlDuocNamedRefDto>? Routes { get; init; }

    [JsonPropertyName("prescription_status")]
    public int? PrescriptionStatus { get; init; }

    [JsonPropertyName("special_control_type")]
    public int? SpecialControlType { get; init; }

    [JsonPropertyName("packagings")]
    public List<CsdlDuocPackagingDto>? Packagings { get; init; }

    [JsonPropertyName("manufacturer")]
    public CsdlDuocManufacturerDto? Manufacturer { get; init; }

    [JsonPropertyName("approval_date")]
    public DateTime? ApprovalDate { get; init; }

    [JsonPropertyName("expiry_date")]
    public DateTime? ExpiryDate { get; init; }

    [JsonPropertyName("last_update_time")]
    public DateTime? LastUpdateTime { get; init; }
}

internal sealed class CsdlDuocNamedRefDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed class CsdlDuocPackagingDto
{
    [JsonPropertyName("unit_id")]
    public string? UnitId { get; init; }

    [JsonPropertyName("unit_name")]
    public string? UnitName { get; init; }

    [JsonPropertyName("gtin")]
    public string? Gtin { get; init; }
}

internal sealed class CsdlDuocManufacturerDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("address")]
    public string? Address { get; init; }
}

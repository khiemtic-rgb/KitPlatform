using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Sales;

/// <summary>
/// Customer context at POS — Display-only MVP
/// </summary>
public sealed record CustomerPosContextDto(
    CustomerPosProfileDto Profile,
    IReadOnlyList<CustomerPosOrderDto> RecentOrders,
    IReadOnlyList<CustomerPosActiveMedicationDto> RecentLikelyActiveMedications,
    IReadOnlyList<CustomerPosConsultationDto> RecentConsultations,
    IReadOnlyList<CustomerPosPrescriptionDto> RecentPrescriptions,
    IReadOnlyList<CustomerPosFrequentProductDto> FrequentPurchases,
    IReadOnlyList<CustomerKnowledgeEntryDto> KnowledgeEntries,
    bool KnowledgeStoreReady);

public sealed record CustomerPosProfileDto(
    Guid CustomerId,
    string FullName,
    string CustomerCode,
    int? AgeYears,
    string? Gender,
    string? ClinicalNotes,
    DateTimeOffset? LastPurchaseDate,
    IReadOnlyList<string> ProfileGaps)
{
    /// <summary>Clinical notes are customer-provided free-text — NOT validated clinical facts</summary>
    public const string ClinicalNotesDisclaimer = "Customer-provided free-text - NOT validated clinical facts";
}

public sealed record CustomerPosOrderDto(
    DateTimeOffset OrderDate,
    string OrderNumber,
    decimal TotalAmount,
    IReadOnlyList<CustomerPosOrderProductDto> TopProducts);

public sealed record CustomerPosOrderProductDto(
    string ProductName,
    decimal Quantity);

public sealed record CustomerPosActiveMedicationDto(
    string ProductName,
    DateOnly? LastPurchaseDate,
    int? DaysSupply,
    DateOnly? SupplyEndDate,
    bool IsNearEnd)
{
    /// <summary>Inferred from purchase history — NOT confirmed clinical fact</summary>
    public const string Disclaimer = "Inferred from purchase history - NOT confirmed clinical fact";
}

public sealed record CustomerPosConsultationDto(
    DateTimeOffset ConsultationDate,
    string SafetyLevel,
    string? SafetyMessage,
    IReadOnlyList<string> Symptoms);

/// <summary>
/// A recorded prescription for this customer. History only — not a current order.
/// </summary>
public sealed record CustomerPosPrescriptionDto(
    string PrescriptionCode,
    DateTimeOffset CreatedAt,
    string Status,
    string? PrescriberName,
    string? ProductNames);

/// <summary>Products that appear often on completed sales. A purchase is not confirmed use.</summary>
public sealed record CustomerPosFrequentProductDto(
    string ProductName,
    int OrderCount,
    DateTimeOffset LastPurchaseDate);

public interface ICustomerPosContextService
{
    /// <summary>
    /// Get customer context for POS display.
    /// Returns null when customer does not exist or is outside current tenant.
    /// </summary>
    Task<CustomerPosContextDto?> GetContextAsync(
        Guid tenantId,
        Guid customerId,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
}

namespace KitPlatform.Packs.Pharmacy.CustomerApp;

public sealed record ActiveMedicationTimelineEventDto(
    string EventType,
    DateTimeOffset OccurredAt,
    string Label);

public sealed record ActiveMedicationDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string? DosageNote,
    Guid? FamilyMemberId,
    Guid? MedicationReminderId,
    string? RemindTime,
    int? DaysRemaining,
    DateOnly? SupplyEndDate,
    string? LastOrderNumber,
    DateTimeOffset? LastOrderDate,
    int? RepurchaseSuggestionCount,
    IReadOnlyList<ActiveMedicationTimelineEventDto> Timeline,
    int? DaysSupply = null);

public sealed record ActiveMedicationListResult(IReadOnlyList<ActiveMedicationDto> Items);

public interface ICustomerActiveMedicationService
{
    Task<ActiveMedicationListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        Guid? familyMemberId = null,
        bool forSelfOnly = false,
        CancellationToken cancellationToken = default);
}

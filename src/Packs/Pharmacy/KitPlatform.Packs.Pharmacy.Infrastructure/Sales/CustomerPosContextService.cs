using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Customers;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Packs.Pharmacy.CustomerApp;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Sales;

/// <summary>
/// Aggregates customer context for POS display (display-only MVP).
/// Service belongs to Sales module (POS responsibility).
/// Uses public Application contracts for cross-module access.
/// </summary>
internal sealed class CustomerPosContextService : ICustomerPosContextService
{
    private readonly ITenantContext _tenant;
    private readonly ICustomerAdminService _customerAdmin;
    private readonly ICustomerActiveMedicationService _activeMeds;
    private readonly IPharmacyConsultationService _consultation;
    private readonly IPharmacyCustomerKnowledgeService _knowledge;
    private readonly SalesRepository _sales;

    public CustomerPosContextService(
        ITenantContext tenant,
        ICustomerAdminService customerAdmin,
        ICustomerActiveMedicationService activeMeds,
        IPharmacyConsultationService consultation,
        IPharmacyCustomerKnowledgeService knowledge,
        SalesRepository sales)
    {
        _tenant = tenant;
        _customerAdmin = customerAdmin;
        _activeMeds = activeMeds;
        _consultation = consultation;
        _knowledge = knowledge;
        _sales = sales;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<CustomerPosContextDto?> GetContextAsync(
        Guid tenantId,
        Guid customerId,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        // Use tenant from context (ignore parameter — retained for interface compatibility)
        var actualTenantId = TenantId;

        // Parallel queries via public Application contracts + internal Sales repository
        var profileTask = _customerAdmin.GetAsync(customerId, cancellationToken);
        var ordersTask = _sales.GetRecentOrdersWithProductsAsync(actualTenantId, customerId, limit: 5, topProductsPerOrder: 5, cancellationToken);
        var activeMedsTask = _activeMeds.ListAsync(actualTenantId, customerId, familyMemberId: null, forSelfOnly: false, cancellationToken);
        var consultationsTask = _consultation.ListRecentByCustomerAsync(customerId, limit: 3, cancellationToken);
        var prescriptionsTask = _sales.GetRecentPrescriptionsAsync(actualTenantId, customerId, limit: 5, cancellationToken);
        var frequentTask = _sales.GetFrequentPurchasesAsync(actualTenantId, customerId, limit: 5, cancellationToken);
        var knowledgeTask = _knowledge.ListAsync(actualTenantId, customerId, cancellationToken);

        await Task.WhenAll(profileTask, ordersTask, activeMedsTask, consultationsTask, prescriptionsTask, frequentTask, knowledgeTask);

        var profile = await profileTask;
        
        // Return null if customer does not exist or is outside current tenant
        if (profile is null)
            return null;

        var orders = await ordersTask;
        var activeMeds = await activeMedsTask;
        var consultations = await consultationsTask;
        var prescriptions = await prescriptionsTask;
        var frequent = await frequentTask;
        var knowledge = await knowledgeTask;

        // Map to DTO with disclaimers
        return new CustomerPosContextDto(
            Profile: MapProfile(profile, LatestPurchaseDate(orders)),
            RecentOrders: orders,
            RecentLikelyActiveMedications: MapActiveMeds(activeMeds),
            RecentConsultations: MapConsultations(consultations),
            RecentPrescriptions: prescriptions,
            FrequentPurchases: frequent,
            KnowledgeEntries: knowledge.Entries,
            KnowledgeStoreReady: knowledge.StoreReady);
    }

    /// <summary>
    /// Newest completed order already loaded for this panel.
    /// That list is status 2 and the last 90 days, so an older sale stays blank here.
    /// </summary>
    internal static DateTimeOffset? LatestPurchaseDate(IReadOnlyList<CustomerPosOrderDto> orders)
    {
        DateTimeOffset? latest = null;
        foreach (var order in orders)
        {
            if (latest is null || order.OrderDate > latest)
                latest = order.OrderDate;
        }

        return latest;
    }

    private static CustomerPosProfileDto MapProfile(
        Application.Customers.CustomerDetailDto profile,
        DateTimeOffset? lastPurchaseDate)
    {
        var ageYears = profile.DateOfBirth.HasValue
            ? CalculateAge(profile.DateOfBirth.Value)
            : (int?)null;

        var gender = profile.Gender switch
        {
            1 => "Nam",
            2 => "Nữ",
            _ => null
        };

        var gaps = GetProfileGaps(profile);

        return new CustomerPosProfileDto(
            CustomerId: profile.Id,
            FullName: profile.FullName,
            CustomerCode: profile.CustomerCode,
            AgeYears: ageYears,
            Gender: gender,
            ClinicalNotes: profile.ClinicalNotes,
            LastPurchaseDate: lastPurchaseDate,
            ProfileGaps: gaps);
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
            age--;
        return age;
    }

    private static IReadOnlyList<string> GetProfileGaps(Application.Customers.CustomerDetailDto profile)
    {
        var gaps = new List<string>();
        if (!profile.DateOfBirth.HasValue)
            gaps.Add("Ngày sinh");
        if (string.IsNullOrWhiteSpace(profile.ClinicalNotes))
            gaps.Add("Dị ứng/Bệnh nền");
        if (string.IsNullOrWhiteSpace(profile.AddressLine))
            gaps.Add("Địa chỉ");
        return gaps;
    }

    private static IReadOnlyList<CustomerPosActiveMedicationDto> MapActiveMeds(ActiveMedicationListResult result)
    {
        return result.Items.Select(MapActiveMedication).ToList();
    }

    /// <summary>
    /// DaysSupply is the purchased course length. DaysRemaining only drives the near-end flag.
    /// </summary>
    internal static CustomerPosActiveMedicationDto MapActiveMedication(ActiveMedicationDto medication)
    {
        var isNearEnd = medication.DaysRemaining is <= 7;
        return new CustomerPosActiveMedicationDto(
            ProductName: medication.ProductName,
            LastPurchaseDate: medication.LastOrderDate.HasValue
                ? DateOnly.FromDateTime(medication.LastOrderDate.Value.UtcDateTime)
                : null,
            DaysSupply: medication.DaysSupply,
            SupplyEndDate: medication.SupplyEndDate,
            IsNearEnd: isNearEnd);
    }

    private static IReadOnlyList<CustomerPosConsultationDto> MapConsultations(IReadOnlyList<PharmacyConsultationSessionSummaryDto> consultations)
    {
        return consultations
            .Select(c => new CustomerPosConsultationDto(
                ConsultationDate: c.ConfirmedAt,
                SafetyLevel: c.SafetyLevel,
                SafetyMessage: c.PreliminaryHeadlineVi,
                Symptoms: c.SymptomCodes))
            .ToList();
    }
}

using KitPlatform.Packs.Pharmacy.CustomerApp;
using KitPlatform.Packs.Pharmacy.Infrastructure.Sales;
using Xunit;

namespace KitPlatform.Tests;

public class CustomerPosContextMedicationTests
{
    [Fact]
    public void MapActiveMedication_ShowsPurchasedSupply_NotDaysRemaining()
    {
        var medication = new ActiveMedicationDto(
            Guid.NewGuid(),
            "PARA500",
            "Paracetamol 500",
            null,
            null,
            null,
            null,
            DaysRemaining: 3,
            SupplyEndDate: new DateOnly(2026, 11, 1),
            LastOrderNumber: "SO-1",
            LastOrderDate: new DateTimeOffset(2026, 10, 2, 3, 0, 0, TimeSpan.Zero),
            RepurchaseSuggestionCount: 0,
            Timeline: [],
            DaysSupply: 30);

        var mapped = CustomerPosContextService.MapActiveMedication(medication);

        Assert.Equal(30, mapped.DaysSupply);
        Assert.True(mapped.IsNearEnd);
        Assert.Equal(new DateOnly(2026, 10, 2), mapped.LastPurchaseDate);
        Assert.Equal(new DateOnly(2026, 11, 1), mapped.SupplyEndDate);
    }

    [Fact]
    public void MapActiveMedication_WithoutSupply_DoesNotInventACourseLength()
    {
        var medication = new ActiveMedicationDto(
            Guid.NewGuid(),
            "PARA500",
            "Paracetamol 500",
            DosageNote: "1 viên",
            FamilyMemberId: null,
            MedicationReminderId: Guid.NewGuid(),
            RemindTime: "08:00",
            DaysRemaining: null,
            SupplyEndDate: null,
            LastOrderNumber: null,
            LastOrderDate: null,
            RepurchaseSuggestionCount: 0,
            Timeline: []);

        var mapped = CustomerPosContextService.MapActiveMedication(medication);

        Assert.Null(mapped.DaysSupply);
        Assert.False(mapped.IsNearEnd);
    }
}

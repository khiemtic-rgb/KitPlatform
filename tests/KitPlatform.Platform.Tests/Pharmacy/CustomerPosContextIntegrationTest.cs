using Xunit;
using KitPlatform.Packs.Pharmacy.Knowledge;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Platform.Tests.Pharmacy;

/// <summary>
/// Integration tests for Customer POS Context service interface contracts and DTOs.
/// These tests verify contract structure without requiring database connectivity or full DI setup.
/// </summary>
public class CustomerPosContextIntegrationTest
{
    [Fact]
    public void CustomerPosContextService_InterfaceExists()
    {
        // ARRANGE & ACT: Verify the interface type exists and is accessible
        var interfaceType = typeof(ICustomerPosContextService);

        // ASSERT: Interface should exist and be public
        Assert.NotNull(interfaceType);
        Assert.True(interfaceType.IsInterface);
        Assert.True(interfaceType.IsPublic);
        
        // Verify the GetContextAsync method exists
        var method = interfaceType.GetMethod("GetContextAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void CustomerPosContextDto_ContainsExpectedProperties()
    {
        // ARRANGE & ACT: Create a DTO instance with all expected fields
        var dto = new CustomerPosContextDto(
            Profile: new CustomerPosProfileDto(
                CustomerId: Guid.NewGuid(),
                FullName: "Test Customer",
                CustomerCode: "CUS001",
                AgeYears: 45,
                Gender: "Nam",
                ClinicalNotes: "Test notes",
                LastPurchaseDate: null,
                ProfileGaps: new[] { "Địa chỉ" }),
            RecentOrders: new List<CustomerPosOrderDto>
            {
                new CustomerPosOrderDto(
                    OrderDate: DateTimeOffset.Now,
                    OrderNumber: "SO-001",
                    TotalAmount: 100000,
                    TopProducts: new List<CustomerPosOrderProductDto>
                    {
                        new CustomerPosOrderProductDto("Product 1", 2)
                    })
            },
            RecentLikelyActiveMedications: new List<CustomerPosActiveMedicationDto>(),
            RecentConsultations: new List<CustomerPosConsultationDto>(),
            RecentPrescriptions: new List<CustomerPosPrescriptionDto>(),
            FrequentPurchases: new List<CustomerPosFrequentProductDto>(),
            KnowledgeEntries: Array.Empty<CustomerKnowledgeEntryDto>(),
            KnowledgeStoreReady: false);

        // ASSERT: Verify structure
        Assert.NotNull(dto.Profile);
        Assert.Equal("Test Customer", dto.Profile.FullName);
        Assert.Equal(45, dto.Profile.AgeYears);
        Assert.Single(dto.RecentOrders);
        Assert.Empty(dto.RecentLikelyActiveMedications);
        Assert.Empty(dto.RecentConsultations);
        Assert.Empty(dto.RecentPrescriptions);
    }
}

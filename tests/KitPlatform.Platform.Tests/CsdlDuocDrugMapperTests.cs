using KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class CsdlDuocDrugMapperTests
{
    [Fact]
    public void ToDetail_maps_rx_and_packaging()
    {
        var dto = new CsdlDuocDrugDto
        {
            Id = "893110130226",
            Name = "TBZemitin 500",
            RegistrationNumber = "893110130226",
            ActivePharmaceuticalIngredient = "Azithromycin",
            Strength = "500 mg",
            PrescriptionStatus = 1,
            SpecialControlType = 0,
            Manufacturer = new CsdlDuocManufacturerDto { Name = "Pharbaco", Country = "VN" },
            Packagings =
            [
                new CsdlDuocPackagingDto { UnitId = "U31", UnitName = "Viên", Gtin = "8930001" },
            ],
            ExpiryDate = new DateTime(2031, 6, 16),
        };

        var detail = CsdlDuocDrugMapper.ToDetail(dto);
        Assert.Equal("893110130226", detail.DrugId);
        Assert.Equal("RX", detail.DrugCategoryCode);
        Assert.Equal("Kê đơn", detail.DrugCategoryLabel);
        Assert.Equal("Viên", detail.UnitName);
        Assert.Equal("8930001", detail.Barcode);
        Assert.Equal("Pharbaco", detail.Manufacturer);
        Assert.Equal("VN", detail.CountryOfOrigin);

        var prefill = CsdlDuocDrugMapper.ToPrefill(dto);
        Assert.Equal((short)2, prefill.DrugType);
        Assert.Contains("Azithromycin", prefill.GenericName);
    }

    [Fact]
    public void ToDetail_special_control_overrides_rx()
    {
        var dto = new CsdlDuocDrugDto
        {
            Id = "1",
            Name = "X",
            PrescriptionStatus = 0,
            SpecialControlType = 2,
        };
        var detail = CsdlDuocDrugMapper.ToDetail(dto);
        Assert.Equal("CONTROLLED", detail.DrugCategoryCode);
        Assert.Equal((short)3, CsdlDuocDrugMapper.ToPrefill(dto).DrugType);
    }
}

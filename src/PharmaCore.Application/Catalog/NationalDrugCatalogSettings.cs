namespace PharmaCore.Application.Catalog;

public sealed class NationalDrugCatalogSettings
{
    public const string SectionName = "NationalDrugCatalog";

    /// <summary>mock | sandbox | live — MVP dùng mock khi chưa có tài khoản liên thông.</summary>
    public string Mode { get; init; } = "mock";
}

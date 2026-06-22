namespace PharmaCore.Application.Configuration;

public enum TenantBatchMode
{
    Off,
    Suggest,
    LabelOptional,
    LabelRequired,
}

public static class TenantBatchModeParser
{
    public static TenantBatchMode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "off" => TenantBatchMode.Off,
        "suggest" or null or "" => TenantBatchMode.Suggest,
        "label_optional" => TenantBatchMode.LabelOptional,
        "label_required" => TenantBatchMode.LabelRequired,
        _ => TenantBatchMode.Suggest,
    };

    public static string ToSettingValue(TenantBatchMode mode) => mode switch
    {
        TenantBatchMode.Off => "off",
        TenantBatchMode.Suggest => "suggest",
        TenantBatchMode.LabelOptional => "label_optional",
        TenantBatchMode.LabelRequired => "label_required",
        _ => "suggest",
    };
}

public static class StockSourceLabels
{
    public const string SystemBook = "Tồn theo hệ thống";
}

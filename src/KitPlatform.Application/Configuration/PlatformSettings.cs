namespace KitPlatform.Application.Configuration;

public sealed class PlatformSettings
{
    public const string SectionName = "Platform";

    /// <summary>Mã bí mật để tạo nhà thuốc mới qua /setup (header X-Platform-Key).</summary>
    public string ProvisioningKey { get; set; } = "";

    public string BrandName { get; set; } = "Novixa";

    public string ProductName { get; set; } = "ERP Nhà thuốc";

    public string AdminUrl { get; set; } = "https://admin.novixa.vn";

    public string CustomerAppUrl { get; set; } = "https://novixa.vn";

    public string ApiUrl { get; set; } = "https://api.novixa.vn";

    /// <summary>URL portal bác sĩ (OTP / kê đơn).</summary>
    public string PrescriberPortalUrl { get; set; } = "https://prescriber.novixa.vn";

    /// <summary>Bật màn admin xem lead khảo sát public (legacy flag).</summary>
    public bool EnableAssessmentLeadsAdmin { get; set; }

    /// <summary>Bật module KAP (KIT Assessment Platform) trên admin.</summary>
    public bool EnableKapAdmin { get; set; }

    /// <summary>URL public KAP / assessment web.</summary>
    public string KapPublicUrl { get; set; } = "https://survey.novixa.vn";

    public bool IsKapAdminEnabled => EnableKapAdmin || EnableAssessmentLeadsAdmin;
}

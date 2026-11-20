namespace KitPlatform.Packs.Sales;

/// <summary>
/// PHC submission linking stays closed until the product owner approves template codes.
/// Do not infer codes from qualification_json or from Assessment.
/// </summary>
public static class PhcLinkLock
{
    public const bool Enabled = false;

    public const string Reason = "Liên kết PHC đang khóa. Chưa có phcTemplateCodes được duyệt.";

    public static void EnsureOpen()
    {
        if (!Enabled)
            throw new InvalidOperationException(Reason);
    }
}

namespace KitPlatform.Packs.Pharmacy.Inventory;

/// <summary>
/// Stored on <c>tenants.settings.low_stock_notification_email</c>.
/// Delivery of the alert is not implemented; this only keeps a valid address.
/// </summary>
public static class LowStockNotificationEmail
{
    public const int MaxLength = 200;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new InvalidOperationException("Email thông báo tồn thấp vượt quá 200 ký tự.");
        if (trimmed.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Email thông báo tồn thấp không hợp lệ.");

        var at = trimmed.IndexOf('@');
        if (at <= 0 || at != trimmed.LastIndexOf('@') || at >= trimmed.Length - 1)
            throw new InvalidOperationException("Email thông báo tồn thấp không hợp lệ.");

        var domain = trimmed[(at + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains(' '))
            throw new InvalidOperationException("Email thông báo tồn thấp không hợp lệ.");

        return trimmed;
    }
}

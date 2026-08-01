namespace KitPlatform.Application.Customers;

/// <summary>Shared phone normalize for admin create, OTP, and conflict checks.</summary>
public static class CustomerPhoneNormalizer
{
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length >= 11)
            digits = "0" + digits[2..];
        return digits;
    }

    public static bool IsValid(string? phone) => Normalize(phone).Length >= 9;
}

/// <summary>How the customer row was first created. Immutable after insert.</summary>
public static class CustomerAcquisitionSources
{
    public const string Counter = "counter";
    public const string AppSelf = "app_self";
    public const string QrClaim = "qr_claim";
    public const string Import = "import";
    public const string Admin = "admin";

    public static bool IsKnown(string? value) =>
        value is Counter or AppSelf or QrClaim or Import or Admin;
}

/// <summary>Pharmacy membership relation (server source of truth for soft-gate).</summary>
public static class CustomerPharmacyRelations
{
    public const string Prospect = "prospect";
    public const string Member = "member";
    public const string Revoked = "revoked";

    public static bool IsKnown(string? value) => value is Prospect or Member or Revoked;

    public static bool IsMember(string? value) =>
        string.Equals(value, Member, StringComparison.OrdinalIgnoreCase);
}

public static class CustomerPharmacyVerifiedVia
{
    public const string StaffMark = "staff_mark";
    public const string QrScan = "qr_scan";
    public const string FirstSale = "first_sale";
    public const string Invite = "invite";
}

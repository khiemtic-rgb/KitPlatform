using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Sales;

public static class SalesConsent
{
    public static readonly string[] Bases = ["form_opt_in", "verbal_recorded", "written"];

    public static string? Reject(string actor, string basis, string? purpose, bool hasExpiresKey, string? evidenceRef)
    {
        if (string.Equals(actor, "agent", StringComparison.Ordinal))
            return "agent_cannot_grant_consent";
        if (!Bases.Contains(basis))
            return "basis_not_allowed";
        if (purpose != "marketing")
            return "purpose_required";
        if (!hasExpiresKey)
            return "expires_at_required";
        if (basis == "verbal_recorded" && string.IsNullOrWhiteSpace(evidenceRef))
            return "evidence_ref_required";
        return null;
    }
}

public static class SalesSubjectHash
{
    public static string ForMessenger(string psid)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(psid.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class SalesCallLog
{
    public static readonly string[] ResultCodes =
        ["reached", "no_answer", "callback", "not_interested", "booked_phc", "wrong_number"];

    public static bool HasResult(string? code) => code is not null && ResultCodes.Contains(code);
}

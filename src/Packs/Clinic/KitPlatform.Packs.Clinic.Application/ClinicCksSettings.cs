namespace KitPlatform.Packs.Clinic;

/// <summary>CL2 Soft-CKS config — mock first; CA/USB via Provider later.</summary>
public sealed class ClinicCksSettings
{
    public const string SectionName = "Clinic:Cks";

    /// <summary>When false, POST /sign returns 400.</summary>
    public bool Enabled { get; set; }

    /// <summary><c>mock</c> (CL2.0) or <c>usb_ca</c> (later).</summary>
    public string Provider { get; set; } = ClinicSignatureProviders.Mock;

    /// <summary>HMAC key for mock signer — Dev/pilot only.</summary>
    public string MockSigningKey { get; set; } = "dev-only-change-me-min-32-chars!!";

    /// <summary>When true (Hard-CKS), send-to-pharmacy requires signed.</summary>
    public bool RequireSignedBeforeSend { get; set; }
}

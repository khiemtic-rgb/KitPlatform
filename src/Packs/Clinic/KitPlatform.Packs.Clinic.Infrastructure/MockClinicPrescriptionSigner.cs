using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Packs.Clinic.Infrastructure;

/// <summary>CL2.0 Soft-CKS — HMAC over pdf_sha256. Not legal CA.</summary>
internal sealed class MockClinicPrescriptionSigner : IClinicPrescriptionSigner
{
    private readonly ClinicCksSettings _settings;

    public MockClinicPrescriptionSigner(IOptions<ClinicCksSettings> options) =>
        _settings = options.Value;

    public string ProviderCode => ClinicSignatureProviders.Mock;

    public Task<ClinicSignatureResult> SignAsync(
        ClinicSignRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = _settings.MockSigningKey;
        if (string.IsNullOrWhiteSpace(key) || key.Length < 16)
            throw new InvalidOperationException("Clinic:Cks:MockSigningKey chưa cấu hình (tối thiểu 16 ký tự).");

        var payload = string.Join('|',
            request.PrescriptionId.ToString("N"),
            request.PrescriptionCode,
            request.PdfSha256.Trim().ToLowerInvariant(),
            request.SignerUserId?.ToString("N") ?? "");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        return Task.FromResult(new ClinicSignatureResult(
            SignatureAlg: "HMAC-SHA256",
            SignatureValue: sig,
            SignatureProvider: ClinicSignatureProviders.Mock,
            SignerCertThumbprint: null));
    }
}

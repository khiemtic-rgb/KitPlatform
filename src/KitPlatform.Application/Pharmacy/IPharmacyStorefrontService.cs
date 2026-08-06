using System.Text.Json;

namespace KitPlatform.Application.Pharmacy;

public sealed record PharmacyStorefrontProfileDto(
    string Slug,
    bool IsPublished,
    string PublicHostHint,
    JsonElement Content,
    DateTimeOffset UpdatedAt);

public sealed record UpdatePharmacyStorefrontProfileRequest(
    string Slug,
    bool IsPublished,
    JsonElement Content);

public sealed record PublicPharmacyStorefrontDto(
    string Slug,
    string TenantCode,
    string TenantName,
    JsonElement Content);

public interface IPharmacyStorefrontService
{
    Task<PharmacyStorefrontProfileDto> GetProfileAsync(CancellationToken cancellationToken);

    Task<PharmacyStorefrontProfileDto> UpsertProfileAsync(
        UpdatePharmacyStorefrontProfileRequest request,
        CancellationToken cancellationToken);

    Task<PublicPharmacyStorefrontDto?> GetPublishedBySlugAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<PublicPharmacyStorefrontDto?> GetPublishedByTenantCodeAsync(
        string tenantCode,
        CancellationToken cancellationToken);
}

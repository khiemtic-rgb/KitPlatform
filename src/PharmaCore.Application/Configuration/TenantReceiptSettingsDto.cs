namespace PharmaCore.Application.Configuration;

public sealed record TenantReceiptSettingsDto(
    string Name,
    string? Tagline,
    string? Phone,
    string? Address);

public sealed record UpdateTenantReceiptSettingsRequest(
    string Name,
    string? Tagline,
    string? Phone,
    string? Address);

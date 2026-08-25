namespace KitPlatform.Application.Configuration;

public sealed record TenantPharmacyConsultationAiSettingsDto(
    string? GeminiApiKeySecretRef,
    bool GeminiApiKeyConfigured,
    string TextModel,
    bool EnvFallbackAvailable,
    bool ContentFallbackAvailable);

public sealed record UpdateTenantPharmacyConsultationAiSettingsRequest(
    string? GeminiApiKeySecretRef,
    /// <summary>Null = giữ key cũ. Chuỗi rỗng = xóa key lưu DB.</summary>
    string? GeminiApiKey,
    string? TextModel);

public sealed record PharmacyConsultationAiResolvedDto(
    string? GeminiApiKey,
    string? GeminiApiKeySecretRef,
    string TextModel);

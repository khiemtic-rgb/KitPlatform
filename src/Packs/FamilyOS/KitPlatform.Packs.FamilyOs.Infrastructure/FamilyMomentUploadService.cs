using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;
using Microsoft.Extensions.Hosting;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

/// <summary>
/// Relation-moment media (photo / short voice). No study-evidence hard gates.
/// </summary>
internal sealed class FamilyMomentUploadService : IFamilyMomentUploadService
{
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webm", ".m4a", ".mp3", ".ogg", ".aac", ".wav",
    };

    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const long MaxAudioBytes = 2 * 1024 * 1024;

    private readonly IFamilyGraphService _families;
    private readonly ITenantContext _tenant;
    private readonly IHostEnvironment _environment;

    public FamilyMomentUploadService(
        IFamilyGraphService families,
        ITenantContext tenant,
        IHostEnvironment environment)
    {
        _families = families;
        _tenant = tenant;
        _environment = environment;
    }

    public async Task<FamilyMomentUploadResult> SaveAsync(
        Guid familyId,
        Guid? memberId,
        Stream content,
        string originalFileName,
        string? contentType,
        long declaredLength,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        _ = memberId;

        if (declaredLength <= 0)
            throw new InvalidOperationException("Chọn ảnh hoặc ghi âm để tải lên.");

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = GuessExtension(contentType);

        var isPhoto = PhotoExtensions.Contains(extension);
        var isAudio = AudioExtensions.Contains(extension);
        if (!isPhoto && !isAudio)
            throw new InvalidOperationException(
                "Chỉ hỗ trợ ảnh JPG/PNG/WebP hoặc audio WebM/M4A/MP3/OGG.");

        var maxBytes = isPhoto ? MaxPhotoBytes : MaxAudioBytes;
        if (declaredLength > maxBytes)
            throw new InvalidOperationException(
                isPhoto ? "Ảnh tối đa 5 MB." : "Giọng nói tối đa 2 MB (~30 giây).");

        await using var ms = new MemoryStream(capacity: (int)Math.Min(declaredLength, maxBytes));
        await content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            throw new InvalidOperationException("Chọn ảnh hoặc ghi âm để tải lên.");
        if (bytes.Length > maxBytes)
            throw new InvalidOperationException(
                isPhoto ? "Ảnh tối đa 5 MB." : "Giọng nói tối đa 2 MB (~30 giây).");

        var tenantFolder = _tenant.TenantId.ToString("N");
        var directory = Path.Combine(_environment.ContentRootPath, "uploads", "family-os", tenantFolder);
        Directory.CreateDirectory(directory);
        var fileName = $"mom_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
        var url = $"/uploads/family-os/{tenantFolder}/{fileName}";

        return new FamilyMomentUploadResult(
            url,
            isAudio ? FamilyMomentMediaKinds.Audio : FamilyMomentMediaKinds.Photo);
    }

    private static string GuessExtension(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return "";
        var ct = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return ct switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "audio/webm" => ".webm",
            "audio/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
            "audio/mpeg" => ".mp3",
            "audio/ogg" => ".ogg",
            "audio/aac" => ".aac",
            "audio/wav" or "audio/wave" or "audio/x-wav" => ".wav",
            _ => "",
        };
    }
}

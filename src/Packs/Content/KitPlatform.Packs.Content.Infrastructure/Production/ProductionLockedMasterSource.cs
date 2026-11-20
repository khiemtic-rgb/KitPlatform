using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

public sealed class ProductionLockedMasterSource : IProductionLockedMasterSource
{
    readonly ICharacterStudioOrchestrator _studio;

    public ProductionLockedMasterSource(ICharacterStudioOrchestrator studio)
    {
        _studio = studio;
    }

    public async Task<GeminiStillReference?> ReadOfficialAsync(
        string characterId, string name, CancellationToken ct = default)
    {
        var image = await _studio.ReadOfficialLockedMasterAsync(characterId, "ERA-01", ct);
        if (image is null || string.IsNullOrWhiteSpace(image.Value.Sha256)) return null;
        var mime = string.IsNullOrWhiteSpace(image.Value.Mime) ? "image/jpeg" : image.Value.Mime;
        return new GeminiStillReference(characterId.Trim().ToUpperInvariant(), name, mime, image.Value.Sha256, image.Value.Bytes);
    }
}

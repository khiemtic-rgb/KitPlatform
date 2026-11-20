using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// CK18B live host. Wires existing repositories only. Does not Start Vertex.
/// </summary>
public static class OfficialVeoLiveOfficialContractMaterializationLiveV1
{
    public static async Task<OfficialVeoLiveOfficialContractMaterializationResult> MaterializeDevelopmentAsync(
        string connectionString,
        string videoRoot,
        CancellationToken ct = default)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var store = new ProductionWorkRepository(db);
        var permits = new ProductionExecutionPermitRepository(db);
        var selections = new ProductionAiSelectionRepository(db);
        var art = await store.GetArtifactAsync(OfficialCreateVideoFreezeV1.OfficialKeyframeId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);
        var bytes = ReadExistingKeyframeBytes(videoRoot, art.StorageKey)
            ?? throw new InvalidOperationException(ProductionFailureReasons.KeyframeBytesMissing);
        return await OfficialVeoLiveOfficialContractMaterializationV1.MaterializeAsync(
            new OfficialVeoLiveOfficialContractMaterializationRequest(
                store,
                permits,
                Selection: null,
                bytes,
                selections,
                OfficialVeoHttpCompositionV1.DefaultComposeProject,
                DryRun: false,
                RequireOfficialLiveKeyframeHash: true),
            ct);
    }

    static byte[]? ReadExistingKeyframeBytes(string videoRoot, string? storageKey)
    {
        var raw = (storageKey ?? "").Trim().Replace('\\', '/');
        var i = raw.LastIndexOf('/');
        if (i <= 0 || i >= raw.Length - 1) return null;
        var folder = raw[..i];
        var fileId = raw[(i + 1)..];
        var path = Path.Combine(videoRoot, folder, fileId + ".mp4");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}

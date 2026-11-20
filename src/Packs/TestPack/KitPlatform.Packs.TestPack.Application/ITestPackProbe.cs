namespace KitPlatform.Packs.TestPack;

public interface ITestPackProbe
{
    Task RememberAsync(Guid tenantId, string marker, CancellationToken cancellationToken = default);

    Task<string?> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record TestPackPing(string Pack, string Module, Guid TenantId, string? Marker);

public sealed record TestPackMarkerWrite(string Marker);

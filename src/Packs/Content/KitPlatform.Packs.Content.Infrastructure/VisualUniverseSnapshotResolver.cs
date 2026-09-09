namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Single read-only resolver for the current Visual Universe snapshot.
/// Does not promote, lock, persist, or generate.
/// </summary>
internal sealed class VisualUniverseSnapshotResolver : IVisualUniverseSnapshotResolver
{
    private readonly IVisualUniverseAuthority _universe;
    private readonly IProjectVisualStyleAuthority _pvs;
    private readonly KitVideoVisualSystemRepository _repo;

    public VisualUniverseSnapshotResolver(
        IVisualUniverseAuthority universe,
        IProjectVisualStyleAuthority pvs,
        KitVideoVisualSystemRepository repo)
    {
        _universe = universe;
        _pvs = pvs;
        _repo = repo;
    }

    public IReadOnlyList<string> RunRegression() => VisualUniverseSnapshotResolverV1Regression.Run();

    public async Task<VisualUniverseSnapshot> GetCurrentAsync(
        string projectId, CancellationToken cancellationToken = default)
    {
        var project = VisualUniverseSnapshotV1Rules.NormalizeProject(projectId);
        var universe = await _universe.GetAsync(project, cancellationToken);
        var pvs = await _pvs.GetActiveAsync(project, cancellationToken);
        var row = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var cal = row is null ? null : VisualCalibrationPackV1Rules.ReadSnapshot(row.RulesJson);
        return VisualUniverseSnapshotV1Rules.FromAuthorities(
            project,
            universe,
            pvs.Sha,
            cal?.PackId,
            cal is null ? null : VisualCalibrationPackV1Rules.PackSha(cal));
    }
}

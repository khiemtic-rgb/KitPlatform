using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal static class ContentQualityRunner
{
    public static async Task<ContentQualityGateDto> EvaluateAsync(
        ContentRepository repo,
        ContentRepository.PackageRow package,
        CancellationToken cancellationToken)
    {
        var brand = await repo.GetBrandAsync(package.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand not found");
        var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
        var (core, _) = ContentPackageExtra.Parse(package.ExtraJson);
        var brief = ContentPackageExtra.ParseBrief(package.ExtraJson);
        var variants = await repo.ListVariantsAsync(package.TopicId, cancellationToken);
        return ContentQualityGate.Evaluate(
            knowledge,
            core,
            package.Angle,
            variants.Select(v => (v.Kind, v.BodyMarkdown)).ToList(),
            brand.Name,
            brief);
    }

    public static async Task PersistAsync(
        ContentRepository repo,
        Guid packageId,
        string? extraJson,
        ContentQualityGateDto gate,
        CancellationToken cancellationToken)
    {
        await repo.UpdatePackageExtraJsonAsync(
            packageId,
            ContentPackageExtra.MergeGate(extraJson, gate),
            cancellationToken);
    }

    public static void ThrowIfCannotApprove(ContentQualityGateDto gate)
    {
        if (gate.CanApprove) return;
        var issues = gate.ApproveIssues is { Count: > 0 } ? gate.ApproveIssues : gate.Issues;
        throw new InvalidOperationException(ContentQualityGate.RefuseApprove(issues));
    }

    public static void ThrowIfCannotPublish(ContentQualityGateDto gate, string? connectorType = null)
    {
        var issues = ContentQualityGate.SelectPublishBlocking(
            gate.BlockingIssues is { Count: > 0 } ? gate.BlockingIssues : gate.Issues,
            connectorType);
        if (issues.Count == 0) return;
        throw new InvalidOperationException(ContentQualityGate.RefusePublish(issues));
    }
}

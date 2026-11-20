using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal static class ContentFactCheck
{
    public static async Task<ContentFactGateResult> EvaluateAsync(
        ContentRepository repo,
        Guid topicId,
        CancellationToken ct)
    {
        var topic = await repo.GetTopicAsync(topicId, ct);
        if (topic is null) return ContentFactGateResult.NotASeries;
        var episode = await repo.FindEpisodeForTopicAsync(topicId, ct);
        if (episode is null) return ContentFactGateResult.NotASeries;

        var variants = await repo.ListVariantsAsync(topicId, ct);
        var recent = await repo.ListRecentWebThesesAsync(topic.BrandId, topicId, 3, ct);
        var brands = await repo.ListBrandsAsync(true, ct);
        var others = brands
            .Where(b => b.Id != topic.BrandId)
            .SelectMany(b => new[] { b.Name, b.Code })
            .ToList();
        return ContentFactGate.Evaluate(
            ContentFactGate.ReadCard(episode.ContinuityJson),
            topic.Title,
            variants.Select(v => v.BodyMarkdown).ToList(),
            recent,
            others);
    }
}

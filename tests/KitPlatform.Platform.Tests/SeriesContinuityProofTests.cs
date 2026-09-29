using System.Net;
using System.Text;
using System.Text.Json;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace KitPlatform.Platform.Tests;

/// <summary>
/// Opt-in local proof. Set SERIES_CONTINUITY_PROOF=1.
/// Generates Episode 02 only. Does not regenerate Episode 01 and does not publish.
/// </summary>
public sealed class SeriesContinuityProofTests
{
    private const string SeriesId = "01a0e0b4-e428-7b26-ad6b-46c207ca7019";
    private const string Episode1Id = "01a0e0b4-e494-7820-8288-770134d73272";
    private const string Episode2Id = "01a0e0b4-e49d-7734-9bd3-e975639bb450";
    private const string Episode1TopicId = "01a0e0be-8a19-76d6-b616-94d7dc8d42df";
    private const string Angle = "Hàng quá hạn là tiền nằm im trong kho, không phải doanh thu kém.";
    private const string OpenLoop = "Doanh thu vẫn đều nên chủ nhà thuốc chưa thấy tiền đang kẹt ở hạn dùng.";
    private const string E2Key = "Doanh thu đều không chứng minh kho đang khỏe.";
    private const string E2Next = "Lần ra điểm nhập hàng và tồn kho nào tạo ra hàng quá hạn.";
    private const string TerritoryId = "novixa.core.inventory-fefo";

    private readonly ITestOutputHelper _out;

    public SeriesContinuityProofTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Local_episode_02_continues_episode_01()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SERIES_CONTINUITY_PROOF"), "1", StringComparison.Ordinal))
        {
            _out.WriteLine("Skipped. Set SERIES_CONTINUITY_PROOF=1 to run the series continuity proof.");
            return;
        }

        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "continuity-proof-mock");
        var probe = new PromptProbe();
        await using var provider = Build(probe);
        var seriesApi = provider.GetRequiredService<IContentArticleSeriesService>();
        var work = provider.GetRequiredService<IContentWorkQueueService>();
        var packages = provider.GetRequiredService<IContentPackageService>();
        var db = provider.GetRequiredService<IDbConnectionFactory>();

        Assert.Equal(0, await Scalar(db, "SELECT count(*) FROM pack_content.work_job WHERE status = 'Queued'"));
        var publishBefore = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var e1HashesBefore = await VariantHashes(db, Episode1TopicId);
        var e1 = (await seriesApi.GetEpisodeDetailAsync(Guid.Parse(SeriesId), Guid.Parse(Episode1Id)))!.Episode;
        var e2 = (await seriesApi.GetEpisodeDetailAsync(Guid.Parse(SeriesId), Guid.Parse(Episode2Id)))!.Episode;
        var e1Continuity = ContentArticleSeriesRules.ParseContinuity(e1.Continuity.GetRawText());
        var e2Continuity = ContentArticleSeriesRules.ParseContinuity(e2.Continuity.GetRawText());
        var e1Canon = Canon(e1);
        var e2Canon = Canon(e2);

        Assert.Equal(Guid.Parse(Episode1Id), e2.PreviousEpisodeId);
        Assert.Contains(OpenLoop, e2Continuity.MustContinueFrom);
        Assert.Contains(OpenLoop, e1Continuity.OpenLoops);
        Assert.Contains(Angle, e2Continuity.MustNotRepeat);
        Assert.False(string.IsNullOrWhiteSpace(e2Continuity.PreviousSummary));
        Assert.Equal(E2Next, e2Continuity.NextEpisodeDirection);

        var enqueued = await seriesApi.GenerateContentAsync(
            Guid.Parse(SeriesId),
            Guid.Parse(Episode2Id),
            new GenerateContentRequest(SkipImages: true));
        Assert.Equal(ContentWorkKinds.GenerateTopic, enqueued.Work?.Job.Kind);
        Assert.True(await work.ProcessNextAsync());

        var detail = (await seriesApi.GetEpisodeDetailAsync(Guid.Parse(SeriesId), Guid.Parse(Episode2Id)))!;
        var episode = detail.Episode;
        var topic = detail.TopicDetail;
        Assert.NotNull(topic);
        var packageId = await ScalarGuid(db, "SELECT id FROM pack_content.content_package WHERE topic_id = '" + episode.ContentTopicId + "'");
        var package = (await packages.GetAsync(packageId))!;
        var fit = package.BrandFits!.Single(f => f.BrandId == package.BrandId);
        var web = topic!.Variants.Single(v => v.Kind == "web_long");
        var body = web.BodyMarkdown ?? "";
        var e1After = (await seriesApi.GetEpisodeDetailAsync(Guid.Parse(SeriesId), Guid.Parse(Episode1Id)))!.Episode;
        var e1HashesAfter = await VariantHashes(db, Episode1TopicId);
        var publishAfter = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");

        var report = new
        {
            episode2Id = episode.Id,
            topicId = topic.Topic.Id,
            packageId,
            episode.Title,
            episode.Objective,
            episode.KeyMessage,
            previous = episode.PreviousEpisodeId,
            fit.TerritoryId,
            package.Angle,
            brief = package.CreativeBrief,
            variants = topic.Variants.Select(v => new { v.Kind, len = (v.BodyMarkdown ?? "").Length }).ToArray(),
            gate = package.QualityGate?.Passed,
            issues = package.QualityGate?.Issues,
            e1Topic = e1After.ContentTopicId,
            e1HashesSame = e1HashesBefore == e1HashesAfter,
            publishBefore,
            publishAfter,
            prompt = probe,
        };
        _out.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(Guid.Parse(Episode2Id), episode.Id);
        Assert.Equal(2, episode.EpisodeNo);
        Assert.Equal(Guid.Parse(Episode1Id), episode.PreviousEpisodeId);
        Assert.NotEqual(Guid.Parse(Episode1TopicId), episode.ContentTopicId);
        Assert.Equal(Guid.Parse(Episode1TopicId), e1After.ContentTopicId);
        Assert.Equal(e1Canon, Canon(e1After));
        Assert.Equal(e2Canon, Canon(episode));
        Assert.Equal(e1HashesBefore, e1HashesAfter);
        Assert.Equal(Angle, package.Angle);
        Assert.Equal(TerritoryId, fit.TerritoryId);
        Assert.Equal("core", fit.Territory);
        Assert.Equal(E2Key, episode.KeyMessage);
        Assert.Contains(OpenLoop, body);
        Assert.Contains(E2Key, body);
        Assert.Contains(E2Next, body);
        Assert.Contains("Bài này chưa đi vào thao tác nhập", body);
        Assert.DoesNotContain(Angle, body);
        Assert.DoesNotContain("các bước FEFO", body);
        Assert.True(probe.SawSequence);
        Assert.True(probe.SawPrevious);
        Assert.True(probe.SawOpenLoop);
        Assert.True(probe.SawMustNotRepeat);
        Assert.True(probe.SawNext);
        Assert.True(probe.SawSeries);
        Assert.True(probe.SawCta);
        Assert.True(probe.SawAngleLock);
        Assert.True(probe.SawTerritory);
        Assert.True(probe.Order);
        Assert.NotNull(package.QualityGate);
        Assert.True(package.QualityGate!.Passed, string.Join(" | ", package.QualityGate.Issues));
        Assert.True(body.Length >= 2200);
        Assert.Equal(120, publishBefore);
        Assert.Equal(publishBefore, publishAfter);
        Assert.Equal(5, topic.Variants.Count);
    }

    private static string Canon(ContentArticleEpisodeDto episode) =>
        episode.Title + "\n" + episode.Objective + "\n" + episode.KeyMessage + "\n" + episode.Continuity.GetRawText();

    private static async Task<string> VariantHashes(IDbConnectionFactory db, string topicId)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT string_agg(kind || ':' || md5(body_markdown), ',' ORDER BY kind) FROM pack_content.variant WHERE topic_id = '" + topicId + "'";
        var value = await cmd.ExecuteScalarAsync();
        return value?.ToString() ?? "";
    }

    private static ServiceProvider Build(PromptProbe probe)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHostEnvironment>(new ProofHost());
        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(
            "Host=localhost;Port=5432;Database=kitplatform;Username=kitplatform;Password=kitplatform_dev_2026"));
        services.AddContentPack();
        foreach (var name in new[]
                 {
                     "ContentGeminiClient",
                     "KitPlatform.Packs.Content.Infrastructure.ContentGeminiClient",
                 })
        {
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new ProofHandler(probe));
        }

        return services.BuildServiceProvider();
    }

    private static async Task<long> Scalar(IDbConnectionFactory db, string sql)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task<Guid> ScalarGuid(IDbConnectionFactory db, string sql)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private sealed class ProofHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "series-continuity-proof";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class PromptProbe
    {
        public bool SawSequence { get; set; }
        public bool SawPrevious { get; set; }
        public bool SawOpenLoop { get; set; }
        public bool SawMustNotRepeat { get; set; }
        public bool SawNext { get; set; }
        public bool SawSeries { get; set; }
        public bool SawCta { get; set; }
        public bool SawAngleLock { get; set; }
        public bool SawTerritory { get; set; }
        public bool Order { get; set; }
    }

    private sealed class ProofHandler : HttpMessageHandler
    {
        private readonly PromptProbe _probe;

        public ProofHandler(PromptProbe probe) => _probe = probe;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var raw = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var prompt = Decode(raw);
            Note(prompt);
            var inner = prompt.Contains("Write the article now", StringComparison.Ordinal)
                        || prompt.Contains("flagship article", StringComparison.Ordinal)
                ? Web()
                : prompt.Contains("Variant kinds required", StringComparison.Ordinal)
                    ? Pack(prompt)
                    : """{"ok":false}""";
            var envelope = JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = inner } } } },
                },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "application/json"),
            };
        }

        private void Note(string prompt)
        {
            _probe.SawSequence |= prompt.Contains("Sequence: 2", StringComparison.Ordinal);
            _probe.SawPrevious |= prompt.Contains("Previous summary:", StringComparison.Ordinal);
            _probe.SawOpenLoop |= prompt.Contains(OpenLoop, StringComparison.Ordinal);
            _probe.SawMustNotRepeat |= prompt.Contains(Angle, StringComparison.Ordinal)
                                       && prompt.Contains("mustNotRepeat:", StringComparison.Ordinal);
            _probe.SawNext |= prompt.Contains(E2Next, StringComparison.Ordinal);
            _probe.SawSeries |= prompt.Contains("[SERIES CONTEXT]", StringComparison.Ordinal)
                                && prompt.Contains("Narrative direction:", StringComparison.Ordinal);
            _probe.SawCta |= prompt.Contains("CTA strategy:", StringComparison.Ordinal);
            _probe.SawAngleLock |= prompt.Contains("Do not replace this Brand Angle.", StringComparison.Ordinal);
            _probe.SawTerritory |= prompt.Contains(TerritoryId, StringComparison.Ordinal);
            var brand = prompt.IndexOf("[BRAND IDENTITY]", StringComparison.Ordinal);
            var narrative = prompt.IndexOf("[NARRATIVE CONTEXT — AUTHORITATIVE]", StringComparison.Ordinal);
            var territory = prompt.IndexOf("[TERRITORY]", StringComparison.Ordinal);
            var angle = prompt.IndexOf("[BRAND ANGLE]", StringComparison.Ordinal);
            var series = prompt.IndexOf("[SERIES CONTEXT]", StringComparison.Ordinal);
            var episode = prompt.IndexOf("[EPISODE CONTEXT]", StringComparison.Ordinal);
            var brief = prompt.IndexOf("[CREATIVE BRIEF]", StringComparison.Ordinal);
            var format = prompt.IndexOf("[FORMAT CONTRACT]", StringComparison.Ordinal);
            var supplemental = prompt.IndexOf("=== SUPPLEMENTAL EXECUTION CONTEXT ===", StringComparison.Ordinal);
            if (brand >= 0 && brand < narrative && narrative < territory && territory < angle
                && angle < series && series < episode && episode < brief && brief < format && format < supplemental)
                _probe.Order = true;
        }

        private static string Pack(string prompt)
        {
            var marker = "Variant kinds required (ONLY these): ";
            var i = prompt.IndexOf(marker, StringComparison.Ordinal);
            var kinds = i < 0
                ? new[] { "fb_page", "fb_short", "seo_meta", "social_caption" }
                : prompt[(i + marker.Length)..].Split('\n')[0]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var variants = kinds.Select(kind => new
            {
                kind,
                title = "Vì sao doanh thu đều mà hạn dùng vẫn quá",
                bodyMarkdown = kind == "seo_meta"
                    ? "Doanh thu đo hàng đã bán. Hàng quá hạn không đi vào hóa đơn đó."
                    : E2Key + " " + OpenLoop,
                meta = new { },
            });
            return JsonSerializer.Serialize(new { variants, imagePrompt = "Pharmacy counter and storeroom shelf, no text" });
        }

        private static string Web()
        {
            var beat =
                "Hóa đơn cuối ngày cộng những hộp đã bán. Hộp nằm quá hạn không có dòng trên hóa đơn đó, nên nó không kéo doanh thu xuống. " +
                E2Key + " " + OpenLoop + " " +
                "Hai con số đang đo hai việc khác nhau: một việc là hàng đã ra khỏi kệ, một việc là hàng còn nằm sau hạn. ";
            var body = new StringBuilder();
            body.Append("Cuối ngày quỹ trông đều. Trên kệ vẫn còn hộp đã qua hạn.\n\n");
            while (body.Length < 2600)
            {
                body.Append("## Doanh thu chỉ đếm hàng đã bán\n\n");
                body.Append(beat).Append('\n').Append('\n');
                body.Append("## Hạn dùng không hiện thành doanh thu kém\n\n");
                body.Append(beat).Append('\n').Append('\n');
                body.Append("## Câu hỏi còn để tập sau\n\n");
                body.Append(E2Next).Append(' ');
                body.Append("Bài này chưa đi vào thao tác nhập hay cách xếp kệ. Nó chỉ chỉ ra vì sao doanh thu vẫn đều.\n\n");
            }

            return JsonSerializer.Serialize(new
            {
                title = "Vì sao doanh thu đều mà hạn dùng vẫn quá",
                bodyMarkdown = body.ToString(),
            });
        }

        private static string Decode(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var sb = new StringBuilder();
                foreach (var name in new[] { "systemInstruction", "contents" })
                {
                    if (!doc.RootElement.TryGetProperty(name, out var box)) continue;
                    var target = box.ValueKind == JsonValueKind.Array ? box[0] : box;
                    if (!target.TryGetProperty("parts", out var parts)) continue;
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text))
                            sb.AppendLine(text.GetString());
                    }
                }

                return sb.ToString();
            }
            catch (JsonException)
            {
                return body;
            }
        }
    }
}

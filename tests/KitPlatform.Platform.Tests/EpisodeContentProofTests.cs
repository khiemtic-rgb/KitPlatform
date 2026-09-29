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
/// Opt-in local proof. Set EPISODE_CONTENT_PROOF=1. Mocks only the Gemini HTTP call.
/// Generates Episode 01 of NOVIXA-S002. Does not publish and does not rewrite Episode 02.
/// </summary>
public sealed class EpisodeContentProofTests
{
    private const string SeriesId = "01a0e0b4-e428-7b26-ad6b-46c207ca7019";
    private const string Episode1Id = "01a0e0b4-e494-7820-8288-770134d73272";
    private const string Episode2Id = "01a0e0b4-e49d-7734-9bd3-e975639bb450";
    private const string CoreId = "019ff037-c7f6-7907-9c51-4526696a91b0";
    private const string AnglePackageId = "01a0e0a5-93bc-726e-aa22-215449784267";
    private const string Angle = "Hàng quá hạn là tiền nằm im trong kho, không phải doanh thu kém.";
    private const string OpenLoop = "Doanh thu vẫn đều nên chủ nhà thuốc chưa thấy tiền đang kẹt ở hạn dùng.";
    private const string NextDirection = "Giải thích vì sao doanh thu đều mà hạn dùng vẫn quá.";
    private const string DoNot = "Không mở đầu bằng giới thiệu phần mềm.";

    private readonly ITestOutputHelper _out;

    public EpisodeContentProofTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Local_episode_01_to_quality_gate()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("EPISODE_CONTENT_PROOF"), "1", StringComparison.Ordinal))
        {
            _out.WriteLine("Skipped. Set EPISODE_CONTENT_PROOF=1 to run the episode content proof.");
            return;
        }

        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "episode-proof-mock");
        var probe = new PromptProbe();
        await using var provider = Build(probe);
        var seriesApi = provider.GetRequiredService<IContentArticleSeriesService>();
        var work = provider.GetRequiredService<IContentWorkQueueService>();
        var packages = provider.GetRequiredService<IContentPackageService>();
        var db = provider.GetRequiredService<IDbConnectionFactory>();

        var queued = await Scalar(db, "SELECT count(*) FROM pack_content.work_job WHERE status = 'Queued'");
        Assert.Equal(0, queued);
        var publishBefore = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var before = await seriesApi.GetEpisodeDetailAsync(Guid.Parse(SeriesId), Guid.Parse(Episode2Id));
        Assert.NotNull(before);
        var e2Before = before!.Episode;

        await seriesApi.ApproveAsync(Guid.Parse(SeriesId));
        var enqueued = await seriesApi.GenerateContentAsync(
            Guid.Parse(SeriesId),
            Guid.Parse(Episode1Id),
            new GenerateContentRequest(SkipImages: true));
        Assert.Equal("generate_topic", enqueued.Work?.Job.Kind);
        Assert.True(await work.ProcessNextAsync());

        var detail = await seriesApi.GetEpisodeDetailAsync(Guid.Parse(SeriesId), Guid.Parse(Episode1Id));
        Assert.NotNull(detail);
        var episode = detail!.Episode;
        var topic = detail.TopicDetail;
        Assert.NotNull(topic);
        var packageId = await ScalarGuid(db, "SELECT id FROM pack_content.content_package WHERE topic_id = '" + episode.ContentTopicId + "'");
        var package = await packages.GetAsync(packageId);
        Assert.NotNull(package);
        var fit = package!.BrandFits!.Single(f => f.BrandId == package.BrandId);
        var web = topic!.Variants.Single(v => v.Kind == "web_long");
        var body = web.BodyMarkdown ?? "";
        var e2After = (await seriesApi.GetEpisodeDetailAsync(Guid.Parse(SeriesId), Guid.Parse(Episode2Id)))!.Episode;
        var publishAfter = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var episodeCount = await Scalar(db, "SELECT count(*) FROM pack_content.content_episode WHERE series_id = '" + SeriesId + "' AND status <> 'CANCELLED'");

        var report = new
        {
            seriesId = SeriesId,
            episodeId = episode.Id,
            episode.EpisodeNo,
            episode.Title,
            episode.Objective,
            episode.KeyMessage,
            topicId = topic.Topic.Id,
            topic.Topic.Status,
            packageId,
            brief = package.CreativeBrief,
            territory = fit.TerritoryId,
            angle = package.Angle,
            variants = topic.Variants.Select(v => new { v.Kind, len = (v.BodyMarkdown ?? "").Length }).ToArray(),
            gate = package.QualityGate,
            prompt = new
            {
                probe.SawObjective,
                probe.SawKeyMessage,
                probe.SawMustNotRepeat,
                probe.SawOpenLoop,
                probe.SawNext,
                probe.SawAngle,
                probe.SawTerritory,
                probe.SawSeriesBeforeEpisode,
                probe.SawEpisodeBeforeBrief,
            },
            e2Unchanged = e2Before.Title == e2After.Title
                && e2Before.KeyMessage == e2After.KeyMessage
                && e2Before.ContentTopicId == e2After.ContentTopicId,
            publishBefore,
            publishAfter,
            episodeCount,
        };
        _out.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(episode.Objective, package.CreativeBrief?.Objective);
        Assert.Equal("web_long", package.CreativeBrief?.Format);
        Assert.True(probe.SawObjective);
        Assert.True(probe.SawKeyMessage);
        Assert.True(probe.SawMustNotRepeat);
        Assert.True(probe.SawOpenLoop);
        Assert.True(probe.SawNext);
        Assert.True(probe.SawAngle);
        Assert.True(probe.SawTerritory);
        Assert.True(probe.SawSeriesBeforeEpisode);
        Assert.True(probe.SawEpisodeBeforeBrief);
        Assert.Equal(Angle, package.Angle);
        Assert.Equal(Angle, episode.KeyMessage);
        Assert.Equal("novixa.core.inventory-fefo", fit.TerritoryId);
        Assert.Equal("core", fit.Territory);
        Assert.Equal(Guid.Parse(Episode1Id), episode.Id);
        Assert.Equal(1, episode.EpisodeNo);
        Assert.Null(e2After.ContentTopicId);
        Assert.Equal(e2Before.Title, e2After.Title);
        Assert.Equal(e2Before.Objective, e2After.Objective);
        Assert.Equal(e2Before.KeyMessage, e2After.KeyMessage);
        Assert.Equal(e2Before.Continuity.GetRawText(), e2After.Continuity.GetRawText());
        Assert.Equal(2, episodeCount);
        Assert.Contains(Angle, body);
        Assert.Contains(OpenLoop, body);
        Assert.Contains(NextDirection, body);
        Assert.Contains("Bài này chưa trả lời câu đó", body);
        Assert.DoesNotContain("các bước FEFO", body);
        Assert.DoesNotContain("nguyên nhân là khâu nhập", body);
        Assert.NotNull(package.QualityGate);
        Assert.True(package.QualityGate!.Passed, string.Join(" | ", package.QualityGate.Issues));
        Assert.True(body.Length >= 2200);
        Assert.Equal(publishBefore, publishAfter);
        Assert.Equal(Guid.Parse(CoreId), detail.Series.CorePackageId);
        Assert.Equal(Guid.Parse(AnglePackageId), detail.Series.SourcePackageId);
    }

    private const string episodeObjective = "Mở câu chuyện: nhận ra hàng quá hạn là tiền nằm trong kho.";

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
        var value = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(value);
    }

    private static async Task<Guid> ScalarGuid(IDbConnectionFactory db, string sql)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var value = await cmd.ExecuteScalarAsync();
        return (Guid)value!;
    }

    private sealed class ProofHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "episode-content-proof";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class PromptProbe
    {
        public bool SawObjective;
        public bool SawKeyMessage;
        public bool SawMustNotRepeat;
        public bool SawOpenLoop;
        public bool SawNext;
        public bool SawAngle;
        public bool SawTerritory;
        public bool SawSeriesBeforeEpisode;
        public bool SawEpisodeBeforeBrief;
    }

    private sealed class ProofHandler : HttpMessageHandler
    {
        private readonly PromptProbe _probe;

        public ProofHandler(PromptProbe probe) => _probe = probe;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var prompt = Decode(body);
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
            _probe.SawObjective |= prompt.Contains(episodeObjective, StringComparison.Ordinal);
            _probe.SawKeyMessage |= prompt.Contains(Angle, StringComparison.Ordinal);
            _probe.SawMustNotRepeat |= prompt.Contains(DoNot, StringComparison.Ordinal);
            _probe.SawOpenLoop |= prompt.Contains(OpenLoop, StringComparison.Ordinal);
            _probe.SawNext |= prompt.Contains(NextDirection, StringComparison.Ordinal);
            _probe.SawAngle |= prompt.Contains("Do not replace this Brand Angle.", StringComparison.Ordinal);
            _probe.SawTerritory |= prompt.Contains("novixa.core.inventory-fefo", StringComparison.Ordinal);
            var seriesAt = prompt.IndexOf("[SERIES CONTEXT]", StringComparison.Ordinal);
            var episodeAt = prompt.IndexOf("[EPISODE CONTEXT]", StringComparison.Ordinal);
            var briefAt = prompt.IndexOf("[CREATIVE BRIEF]", StringComparison.Ordinal);
            if (seriesAt >= 0 && episodeAt > seriesAt) _probe.SawSeriesBeforeEpisode = true;
            if (episodeAt >= 0 && briefAt > episodeAt) _probe.SawEpisodeBeforeBrief = true;
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
                title = "Hàng quá hạn là tiền nằm trong kho",
                bodyMarkdown = kind == "fb_page"
                    ? Angle + "\n\nDoanh thu đều chưa cho thấy hộp đã quá hạn. " + OpenLoop + " Câu đó để tập sau."
                    : kind == "seo_meta"
                        ? "Hàng quá hạn là tiền nằm trong kho nhà thuốc. Novixa nhìn từ hạn dùng."
                        : Angle + " " + OpenLoop,
                meta = new { },
            });
            return JsonSerializer.Serialize(new { variants, imagePrompt = "Pharmacy storeroom, no text" });
        }

        private static string Web()
        {
            var beat =
                "Cuối ngày chủ nhà thuốc thấy một hộp đã quá hạn còn nằm trên kệ. Hóa đơn bán ngày hôm đó vẫn đều. " +
                Angle + " Tiền mua hộp đó không quay lại quỹ, và báo cáo bán hàng không gọi đó là doanh thu kém. " +
                "Bài này dừng ở chỗ nhận ra. ";
            var body = new StringBuilder();
            body.Append("Một lô quá hạn vẫn nằm trên kệ. Doanh thu ngày hôm đó không nói hộp đó đã mất tiền.\n\n");
            while (body.Length < 2600)
            {
                body.Append("## Hàng quá hạn là tiền đang nằm im\n\n");
                body.Append(beat).Append('\n').Append('\n');
                body.Append("## Doanh thu không kể hộp đã hết hạn\n\n");
                body.Append(beat).Append('\n').Append('\n');
                body.Append("## Câu hỏi còn mở\n\n");
                body.Append(OpenLoop).Append(' ');
                body.Append(NextDirection);
                body.Append(" Bài này chưa trả lời câu đó. Tập sau mới đi vào vì sao.\n\n");
            }

            return JsonSerializer.Serialize(new
            {
                title = "Hàng quá hạn là tiền nằm trong kho",
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

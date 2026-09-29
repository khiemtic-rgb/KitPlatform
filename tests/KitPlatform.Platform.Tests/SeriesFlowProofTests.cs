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
/// Opt-in local proof. Set SERIES_FLOW_PROOF=1. Mocks only the Gemini HTTP call.
/// Does not publish and does not rewrite NOVIXA-S001.
/// </summary>
public sealed class SeriesFlowProofTests
{
    private const string AnglePackageId = "01a0e0a5-93bc-726e-aa22-215449784267";
    private const string CoreId = "019ff037-c7f6-7907-9c51-4526696a91b0";
    private const string LegacyPackageId = "01a0228a-a352-7a1b-a076-5c2526d6ff90";
    private const string OffBrandPackageId = "01a0e0a5-945f-770d-b8c3-9394c0825fd0";
    private const string SeriesFixtureId = "01a0aac1-30f6-7c77-8a41-654d21b04629";
    private const string Angle = "Hàng quá hạn là tiền nằm im trong kho, không phải doanh thu kém.";
    private const string TerritoryId = "novixa.core.inventory-fefo";
    private const string OpenLoop = "Doanh thu vẫn đều nên chủ nhà thuốc chưa thấy tiền đang kẹt ở hạn dùng.";

    private readonly ITestOutputHelper _out;

    public SeriesFlowProofTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Local_brand_angle_to_episode_02()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SERIES_FLOW_PROOF"), "1", StringComparison.Ordinal))
        {
            _out.WriteLine("Skipped. Set SERIES_FLOW_PROOF=1 to run the local series flow proof.");
            return;
        }

        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "series-proof-mock");
        var probe = new PromptProbe();
        await using var provider = Build(probe);
        var seriesApi = provider.GetRequiredService<IContentArticleSeriesService>();
        var packages = provider.GetRequiredService<IContentPackageService>();
        var db = provider.GetRequiredService<IDbConnectionFactory>();

        var publishBefore = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var fixtureBefore = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE id = '" + SeriesFixtureId + "'");
        var legacySeriesBefore = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE source_package_id = '" + LegacyPackageId + "'");
        var offSeriesBefore = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE source_package_id = '" + OffBrandPackageId + "'");

        var listed = await seriesApi.ListAsync(null, null, Guid.Parse(CoreId), null, null);
        var existing = listed.FirstOrDefault(s =>
            s.SourcePackageId == Guid.Parse(AnglePackageId) && s.Code != "NOVIXA-S001");
        var created = existing ?? await seriesApi.CreateAsync(new CreateContentArticleSeriesRequest(
            Guid.Parse(AnglePackageId),
            "Hàng quá hạn là tiền nằm trong kho",
            "Dẫn chủ nhà thuốc từ chỗ nhận ra hàng quá hạn là tiền nằm im, tới chỗ thấy vì sao kho vẫn tạo ra số hàng đó.",
            10));
        var planned = await seriesApi.GenerateBlueprintAsync(
            created.Id,
            new GenerateArticleSeriesBlueprintRequest(10, 2, false));

        var detail = await seriesApi.GetDetailAsync(created.Id);
        Assert.NotNull(detail);
        var series = detail!.Series;
        var blueprint = ContentArticleSeriesRules.ParseBlueprint(series.Blueprint.GetRawText());
        var episodes = detail.Episodes.OrderBy(e => e.EpisodeNo).ToList();
        var e1 = episodes.Single(e => e.EpisodeNo == 1);
        var e2 = episodes.Single(e => e.EpisodeNo == 2);
        var c1 = ContentArticleSeriesRules.ParseContinuity(e1.Continuity.GetRawText());
        var c2 = ContentArticleSeriesRules.ParseContinuity(e2.Continuity.GetRawText());
        var source = await packages.GetAsync(series.SourcePackageId);
        var fit = source!.BrandFits!.Single(f => f.BrandId == source.BrandId);

        var legacyEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seriesApi.CreateAsync(new CreateContentArticleSeriesRequest(Guid.Parse(LegacyPackageId))));
        var offEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seriesApi.CreateAsync(new CreateContentArticleSeriesRequest(Guid.Parse(OffBrandPackageId))));

        var publishAfter = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var fixtureAfter = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE id = '" + SeriesFixtureId + "'");
        var legacySeriesAfter = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE source_package_id = '" + LegacyPackageId + "'");
        var offSeriesAfter = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE source_package_id = '" + OffBrandPackageId + "'");
        var topics = await Scalar(db, "SELECT count(*) FROM pack_content.content_episode WHERE series_id = '" + series.Id + "' AND content_topic_id IS NOT NULL");

        var report = new
        {
            series = new
            {
                series.Id,
                series.Code,
                series.Name,
                series.Status,
                series.BrandCode,
                series.SourcePackageId,
                series.CorePackageId,
                series.CoreIdeaTitle,
                series.Objective,
                series.Audience,
                series.CoreMessage,
                blueprint.Territory,
                blueprint.TerritoryId,
                blueprint.BrandAngle,
                blueprint.CtaStrategy,
                blueprint.SeriesCta,
                arc = blueprint.NarrativeArc.Count,
                series.EpisodeCount,
                materialized = episodes.Count,
            },
            e1 = new { e1.Title, e1.Objective, e1.KeyMessage, e1.PreviousEpisodeId, c1.OpenLoops, c1.NextEpisodeDirection },
            e2 = new
            {
                e2.Title,
                e2.Objective,
                e2.KeyMessage,
                c2.PreviousSummary,
                c2.MustContinueFrom,
                c2.MustNotRepeat,
                c2.NextEpisodeDirection,
            },
            promptSawTerritory = probe.SawTerritory,
            promptSawAngle = probe.SawAngle,
            legacy = legacyEx.Message,
            offBrand = offEx.Message,
            topics,
            untouched = new { publishBefore, publishAfter, fixtureBefore, fixtureAfter },
        };
        _out.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal("novixa", series.BrandCode);
        Assert.Equal(Guid.Parse(AnglePackageId), series.SourcePackageId);
        Assert.Equal(Guid.Parse(CoreId), series.CorePackageId);
        Assert.NotEqual("NOVIXA-S001", series.Code);
        Assert.Equal(TerritoryId, fit.TerritoryId);
        Assert.Equal("core", fit.Territory);
        Assert.Equal(Angle, fit.BrandAngle);
        Assert.Equal(Angle, source.Angle);
        Assert.Equal(TerritoryId, blueprint.TerritoryId);
        Assert.Equal("core", blueprint.Territory);
        Assert.Equal(Angle, blueprint.BrandAngle);
        Assert.Contains(Angle, series.CoreMessage);
        Assert.True(probe.SawTerritory);
        Assert.True(probe.SawAngle);
        Assert.InRange(blueprint.NarrativeArc.Count, 5, 10);
        Assert.Equal(10, series.EpisodeCount);
        Assert.Equal(2, episodes.Count);
        Assert.Equal(1, e1.EpisodeNo);
        Assert.Equal(2, e2.EpisodeNo);
        Assert.Null(e1.PreviousEpisodeId);
        Assert.Equal(e1.Id, e2.PreviousEpisodeId);
        Assert.Null(e1.ContentTopicId);
        Assert.Null(e2.ContentTopicId);
        Assert.Contains(OpenLoop, c1.OpenLoops);
        Assert.Contains(OpenLoop, c2.MustContinueFrom);
        Assert.Contains(e1.KeyMessage, c2.MustNotRepeat);
        Assert.False(string.IsNullOrWhiteSpace(c2.PreviousSummary));
        Assert.False(string.IsNullOrWhiteSpace(c2.NextEpisodeDirection));
        Assert.Contains("NarrativeFitRequired", legacyEx.Message);
        Assert.Contains("OFF-BRAND", offEx.Message);
        Assert.Equal(legacySeriesBefore, legacySeriesAfter);
        Assert.Equal(offSeriesBefore, offSeriesAfter);
        Assert.Equal(0, topics);
        Assert.Equal(publishBefore, publishAfter);
        Assert.Equal(1, fixtureBefore);
        Assert.Equal(1, fixtureAfter);
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
        var value = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(value);
    }

    private sealed class ProofHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "series-flow-proof";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class PromptProbe
    {
        public bool SawTerritory;
        public bool SawAngle;
    }

    private sealed class ProofHandler : HttpMessageHandler
    {
        private readonly PromptProbe _probe;

        public ProofHandler(PromptProbe probe) => _probe = probe;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var prompt = DecodePrompt(body);
            _probe.SawTerritory |= prompt.Contains(TerritoryId, StringComparison.Ordinal);
            _probe.SawAngle |= prompt.Contains(Angle, StringComparison.Ordinal);
            var envelope = JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = Blueprint() } } } },
                },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "application/json"),
            };
        }

        private static string DecodePrompt(string body)
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

        private static string Blueprint()
        {
            var beats = new (int No, string Title, string Beat, string Objective)[]
            {
                (1, "Hàng quá hạn là tiền nằm trong kho", "Mở câu chuyện: nhận ra hạn dùng là tiền.", "Mở câu chuyện: nhận ra hàng quá hạn là tiền nằm trong kho."),
                (2, "Doanh thu đều chưa có nghĩa kho đang khỏe", "Đi sâu vì sao bán vẫn đều mà hạn vẫn quá.", "Đi sâu nguyên nhân: vì sao doanh thu vẫn đều mà hạn dùng vẫn quá."),
                (3, "Chỗ nhập và tồn tạo ra hạn quá", "Lần ra điểm trong nhập hàng và tồn kho.", "Đi tới cơ chế: điểm nào trong nhập và tồn tạo ra hàng quá hạn."),
                (4, "FEFO gỡ được phần nào của số tiền đó", "FEFO xử lý phần nào, không phải toàn bộ.", "Đi tới phương pháp: FEFO giải quyết phần nào."),
                (5, "Biết FEFO mà tiền vẫn kẹt", "Biết quy tắc chưa có nghĩa ca đang làm.", "Đi tới vận hành: vì sao biết FEFO vẫn còn thất thoát."),
                (6, "Ca nào không ghi hàng cận date", "Thất thoát nằm ở ca không ghi nhận.", "Đi tới hiện trường: ca nào bỏ sót hàng cận date."),
                (7, "Một việc trong tuần để thấy tiền đang nằm", "Một việc đo được, không phải khẩu hiệu.", "Đi tới việc làm: một lần kiểm trong tuần."),
                (8, "Kho ngừng tạo thêm hàng quá hạn", "Đích của chuỗi: kho không sinh thêm số hàng đó.", "Đích: chủ nhà thuốc thấy kho ngừng tạo thêm hàng quá hạn."),
            };
            return JsonSerializer.Serialize(new
            {
                name = "Hàng quá hạn là tiền nằm trong kho",
                description = "Một đường đi từ chỗ nhận ra tiền đang nằm im, tới chỗ thấy vì sao kho vẫn tạo ra hàng quá hạn.",
                objective = "Dẫn chủ nhà thuốc từ nhận ra hàng quá hạn là tiền nằm im, tới chỗ thấy cơ chế kho tạo ra số hàng đó.",
                audience = "Chủ nhà thuốc / người vận hành nhà thuốc",
                coreMessage = Angle,
                blueprint = new
                {
                    version = 1,
                    narrativeArc = beats.Select(b => new
                    {
                        episodeNo = b.No,
                        title = b.Title,
                        beat = b.Beat,
                        objective = b.Objective,
                        angle = Angle,
                        keyMessage = b.No == 1 ? Angle : b.Title,
                    }),
                    contentPillars = new[] { "Inventory & FEFO" },
                    audienceNeed = "Thấy tiền đang kẹt ở hạn dùng, không chỉ nhìn doanh thu.",
                    coreMessage = Angle,
                    episodeCount = 10,
                    continuityRules = new[] { "Tập sau tiếp tục nút chưa đóng của tập trước." },
                    avoidRepetition = new[] { "Không lặp lại việc giải thích hàng quá hạn là tiền." },
                    ctaStrategy = "Tập đầu không bán. CTA chỉ khi chủ nhà thuốc đã thấy tiền đang kẹt.",
                    seriesCta = "https://novixa.vn/",
                    successDefinition = "Chủ nhà thuốc chỉ ra được một lô quá hạn là tiền, rồi chỉ ra vì sao doanh thu vẫn đều.",
                },
                episodes = new object[]
                {
                    new
                    {
                        episodeNo = 1,
                        title = beats[0].Title,
                        objective = beats[0].Objective,
                        angle = Angle,
                        keyMessage = Angle,
                        continuity = new
                        {
                            previousSummary = (string?)null,
                            previousKeyPoints = Array.Empty<string>(),
                            mustContinueFrom = Array.Empty<string>(),
                            mustNotRepeat = new[] { "Không mở đầu bằng giới thiệu phần mềm." },
                            nextEpisodeDirection = "Giải thích vì sao doanh thu đều mà hạn dùng vẫn quá.",
                            openLoops = new[] { OpenLoop },
                            references = new[] { "novixa.core.inventory-fefo" },
                        },
                    },
                    new
                    {
                        episodeNo = 2,
                        title = beats[1].Title,
                        objective = beats[1].Objective,
                        angle = Angle,
                        keyMessage = "Doanh thu đều không chứng minh kho đang khỏe.",
                        continuity = new
                        {
                            previousSummary = "Tập 1 đã cho chủ nhà thuốc thấy hàng quá hạn là tiền nằm im trong kho.",
                            previousKeyPoints = new[] { "Hàng quá hạn không hiện thành doanh thu kém." },
                            mustContinueFrom = new[] { OpenLoop },
                            mustNotRepeat = new[] { Angle },
                            nextEpisodeDirection = "Lần ra điểm nhập hàng và tồn kho nào tạo ra hàng quá hạn.",
                            openLoops = new[] { "Chưa chỉ ra thao tác nhập hay xếp kệ nào đang tạo hàng quá hạn." },
                            references = new[] { "novixa.core.inventory-fefo", "E01" },
                        },
                    },
                },
            });
        }
    }
}

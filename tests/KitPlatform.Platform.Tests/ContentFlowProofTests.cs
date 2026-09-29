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
/// Opt-in local proof. Set CONTENT_FLOW_PROOF=1. Mocks only the Gemini HTTP call.
/// Does not publish and does not touch NOVIXA-S001.
/// </summary>
public sealed class ContentFlowProofTests
{
    private const string CoreId = "019ff037-c7f6-7907-9c51-4526696a91b0";
    private const string NovixaId = "019fec28-586c-7c7c-907e-38e11152dcf7";
    private const string FamixaId = "019ff475-4641-724c-ac3b-21c7eff6d784";
    private const string SeriesSourceId = "01a0228a-a352-7a1b-a076-5c2526d6ff90";
    private const string Elderly = "7 thói quen tốt cho người cao tuổi.";

    private readonly ITestOutputHelper _out;

    public ContentFlowProofTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Local_pipeline_core_idea_to_quality_gate()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CONTENT_FLOW_PROOF"), "1", StringComparison.Ordinal))
        {
            _out.WriteLine("Skipped. Set CONTENT_FLOW_PROOF=1 to run the local content flow proof.");
            return;
        }

        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "flow-proof-mock");
        var probe = new PromptProbe();
        await using var provider = Build(probe);
        var packages = provider.GetRequiredService<IContentPackageService>();
        var work = provider.GetRequiredService<IContentWorkQueueService>();
        var db = provider.GetRequiredService<IDbConnectionFactory>();

        var publishBefore = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var seriesBefore = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE code = 'NOVIXA-S001'");

        var core = await packages.GetAsync(Guid.Parse(CoreId));
        Assert.NotNull(core);
        Assert.Equal("novixa", core!.BrandCode);

        var queued = await Scalar(db, "SELECT count(*) FROM pack_content.work_job WHERE status = 'Queued'");
        string fitPath;
        if (queued == 0)
        {
            var enqueued = await work.EnqueueBrandAdaptBatchAsync(new AnalyzePoolRequest(
                [core.Id],
                [Guid.Parse(NovixaId)],
                IncludeMaybe: true));
            Assert.Single(enqueued.Jobs);
            var ran = await work.ProcessNextAsync();
            Assert.True(ran);
            fitPath = "POST packages/pool/analyze → work queue BrandAdapt → ProcessNext → AnalyzeAndAdaptAsync";
        }
        else
        {
            await packages.AnalyzeAndAdaptAsync(core.Id, new AnalyzeAdaptRequest(
                [Guid.Parse(NovixaId)],
                IncludeMaybe: true,
                GenerateFits: false,
                CreatePackages: false,
                IncludeSourceBrand: true));
            fitPath = "AnalyzeAndAdaptAsync with the same request pool/analyze enqueues (queue was not empty)";
        }

        var scored = await packages.GetAsync(core.Id);
        var novixaFit = scored!.BrandFits!.Single(f => f.BrandCode == "novixa");
        var applied = await packages.ApplyPoolFitsAsync(new ApplyPoolFitsRequest(
            [new ApplyPoolFitItem(core.Id, Guid.Parse(NovixaId))],
            GenerateFits: false));
        Assert.Equal(1, applied.Created);
        var angleId = applied.Fits[0].PackageId!.Value;
        var angle = await packages.GetAsync(angleId);
        Assert.NotNull(angle);
        Assert.Equal(core.Id, angle!.SourcePackageId);
        var stamped = angle.BrandFits!.Single(f => f.BrandId == angle.BrandId);

        var generated = await packages.GenerateAllAsync(angleId, new GenerateContentRequest(SkipImages: true));
        var after = await packages.GetAsync(angleId);

        var famixaHome = Guid.Parse(FamixaId);
        var negative = await packages.CreatePoolAsync(new CreatePoolIdeasRequest(
            famixaHome,
            [new PoolIdeaDraft(Elderly, Elderly, "Nội dung chỉ về người cao tuổi.", Elderly)]));
        var negativeId = negative.Packages[0].Id;
        var negativeQueued = await Scalar(db, "SELECT count(*) FROM pack_content.work_job WHERE status = 'Queued'");
        if (negativeQueued == 0)
        {
            await work.EnqueueBrandAdaptBatchAsync(new AnalyzePoolRequest([negativeId], [famixaHome]));
            Assert.True(await work.ProcessNextAsync());
        }
        else
        {
            await packages.AnalyzeAndAdaptAsync(negativeId, new AnalyzeAdaptRequest(
                [famixaHome], true, false, false, true));
        }

        var negativeScored = await packages.GetAsync(negativeId);
        var famixaFit = negativeScored!.BrandFits!.Single(f => f.BrandCode == "famixa");
        var negativeApply = await packages.ApplyPoolFitsAsync(new ApplyPoolFitsRequest(
            [new ApplyPoolFitItem(negativeId, famixaHome)]));
        var negativeChildren = await Scalar(
            db,
            "SELECT count(*) FROM pack_content.content_package WHERE source_package_id = '" + negativeId + "'");

        var publishAfter = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var seriesAfter = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE code = 'NOVIXA-S001'");
        var seriesSource = await Scalar(db, "SELECT count(*) FROM pack_content.content_package WHERE id = '" + SeriesSourceId + "'");

        var report = new
        {
            fitPath,
            core = new { core.Id, core.Title, core.BrandCode },
            dnaInPrompt = probe.FitSawAuthoritative,
            supplementalInPrompt = probe.FitSawSupplemental,
            fit = novixaFit,
            anglePackage = new
            {
                angle.Id,
                angle.SourcePackageId,
                angle.Title,
                angle.Angle,
                angle.BrandCode,
                stamped.Territory,
                stamped.TerritoryId,
                stamped.NarrativeFit,
                stamped.Reason,
                stamped.BrandAngle,
            },
            generation = new
            {
                generated.BudgetBlocked,
                generated.Message,
                generated.Topic.Id,
                generated.Topic.Status,
                variants = generated.Variants.Select(v => new { v.Kind, len = (v.BodyMarkdown ?? "").Length }).ToArray(),
                promptSawNovixa = probe.GenerateSawNovixa,
                promptSawAngle = probe.GenerateSawAngle,
                promptSawAuthoritativeDna = probe.GenerateSawAuthoritative,
            },
            gate = after!.QualityGate,
            negative = new
            {
                negativeId,
                famixaFit.NarrativeFit,
                famixaFit.Territory,
                famixaFit.BrandAngle,
                negativeApply.Created,
                negativeApply.Skipped,
                negativeChildren,
            },
            untouched = new { publishBefore, publishAfter, seriesBefore, seriesAfter, seriesSource },
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(@"E:\KitPlatform-kitmkt\tmp-flow-proof-result.json", json);
        _out.WriteLine(json);

        Assert.True(probe.FitSawAuthoritative);
        Assert.True(novixaFit.NarrativeFit);
        Assert.Contains(novixaFit.Territory, new[] { "core", "supporting" });
        Assert.False(string.IsNullOrWhiteSpace(novixaFit.TerritoryId));
        Assert.False(string.IsNullOrWhiteSpace(stamped.BrandAngle ?? angle.Angle));
        Assert.Equal(novixaFit.Territory, stamped.Territory);
        Assert.False(generated.BudgetBlocked);
        Assert.NotEmpty(generated.Variants);
        Assert.False(famixaFit.NarrativeFit);
        Assert.Equal("off-brand", famixaFit.Territory);
        Assert.Null(famixaFit.BrandAngle);
        Assert.Equal(0, negativeApply.Created);
        Assert.Equal(0, negativeChildren);
        Assert.Equal(publishBefore, publishAfter);
        Assert.Equal(seriesBefore, seriesAfter);
        Assert.Equal(1, seriesSource);
    }

    [Fact]
    public async Task Local_generation_contract_on_proven_novixa_package()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CONTENT_FLOW_PROOF"), "1", StringComparison.Ordinal))
        {
            _out.WriteLine("Skipped. Set CONTENT_FLOW_PROOF=1 to run the generation contract proof.");
            return;
        }

        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "flow-proof-mock");
        var probe = new PromptProbe();
        await using var provider = Build(probe);
        var packages = provider.GetRequiredService<IContentPackageService>();
        var db = provider.GetRequiredService<IDbConnectionFactory>();
        var angleId = Guid.Parse("01a0e0a5-93bc-726e-aa22-215449784267");
        var before = await packages.GetAsync(angleId);
        Assert.NotNull(before);
        Assert.Equal("novixa", before!.BrandCode);
        Assert.Equal("Hàng quá hạn là tiền nằm im trong kho, không phải doanh thu kém.", before.Angle);
        var fit = before.BrandFits!.Single(f => f.BrandId == before.BrandId);
        Assert.Equal("novixa.core.inventory-fefo", fit.TerritoryId);
        Assert.Equal("core", fit.Territory);

        const string objective =
            "Giúp chủ nhà thuốc nhận ra hàng quá hạn là một dạng thất thoát tồn kho có thể nhìn thấy và đo được.";
        const string audience = "Chủ nhà thuốc / người vận hành nhà thuốc";
        var stamped = await packages.UpdateAsync(angleId, new UpsertContentPackageRequest(
            before.BrandId,
            before.Title,
            before.Angle,
            audience,
            before.ContentType,
            before.Pillar,
            before.Goal,
            before.Priority,
            null,
            before.DisplayAt,
            null,
            CreativeBrief: new ContentCreativeBriefDto(objective, null, "web_long")));
        Assert.NotNull(stamped);
        Assert.Equal(before.Angle, stamped!.Angle);
        Assert.Equal("web_long", stamped.CreativeBrief?.Format);
        Assert.Equal(objective, stamped.CreativeBrief?.Objective);
        Assert.Equal("novixa.core.inventory-fefo", stamped.BrandFits!.Single(f => f.BrandId == stamped.BrandId).TerritoryId);

        probe.Angle = stamped.Angle;
        var publishBefore = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var seriesBefore = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE code = 'NOVIXA-S001'");
        var generated = await packages.GenerateAllAsync(angleId, new GenerateContentRequest(SkipImages: true));
        var after = await packages.GetAsync(angleId);
        var publishAfter = await Scalar(db, "SELECT count(*) FROM pack_content.publish_job");
        var seriesAfter = await Scalar(db, "SELECT count(*) FROM pack_content.content_series WHERE code = 'NOVIXA-S001'");
        var web = generated.Variants.Single(v => v.Kind == "web_long");

        var report = new
        {
            packageId = angleId,
            topicId = generated.Topic.Id,
            status = generated.Topic.Status,
            budgetBlocked = generated.BudgetBlocked,
            message = generated.Message,
            angle = after!.Angle,
            brief = after.CreativeBrief,
            territory = after.BrandFits!.Single(f => f.BrandId == after.BrandId).TerritoryId,
            variants = generated.Variants.Select(v => new { v.Kind, len = (v.BodyMarkdown ?? "").Length }).ToArray(),
            webLength = (web.BodyMarkdown ?? "").Length,
            gate = after.QualityGate,
            prompt = new
            {
                probe.GenerateSawNovixa,
                probe.GenerateSawAngle,
                probe.GenerateSawAuthoritative,
                probe.GenerateSawTerritory,
                probe.GenerateSawSupplemental,
                probe.GenerateSawBrief,
                probe.GenerateSawWebContract,
                probe.NarrativeBeforeSupplemental,
                probe.SawBrandBrainDump,
            },
            untouched = new { publishBefore, publishAfter, seriesBefore, seriesAfter },
        };
        _out.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Assert.False(generated.BudgetBlocked);
        Assert.Equal(before.Angle, after.Angle);
        Assert.Contains(before.Angle!, web.BodyMarkdown);
        Assert.True((web.BodyMarkdown ?? "").Length >= 2200);
        Assert.True(ContentQualityGate.CountMarkdownH2(web.BodyMarkdown) >= 2);
        Assert.True(probe.GenerateSawAuthoritative);
        Assert.True(probe.GenerateSawTerritory);
        Assert.True(probe.GenerateSawSupplemental);
        Assert.True(probe.GenerateSawBrief);
        Assert.True(probe.GenerateSawWebContract);
        Assert.True(probe.NarrativeBeforeSupplemental);
        Assert.True(probe.GenerateSawAngle);
        Assert.False(probe.SawBrandBrainDump);
        Assert.NotNull(after.QualityGate);
        Assert.True(after.QualityGate!.Passed, string.Join(" | ", after.QualityGate.Issues));
        Assert.Equal(publishBefore, publishAfter);
        Assert.Equal(seriesBefore, seriesAfter);
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
        public string ApplicationName { get; set; } = "content-flow-proof";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class PromptProbe
    {
        public bool FitSawAuthoritative;
        public bool FitSawSupplemental;
        public bool GenerateSawNovixa;
        public bool GenerateSawAngle;
        public bool GenerateSawAuthoritative;
        public bool GenerateSawSupplemental;
        public bool GenerateSawTerritory;
        public bool GenerateSawWebContract;
        public bool GenerateSawBrief;
        public bool NarrativeBeforeSupplemental;
        public bool SawBrandBrainDump;
        public string? Angle;
    }

    private sealed class ProofHandler : HttpMessageHandler
    {
        private readonly PromptProbe _probe;

        public ProofHandler(PromptProbe probe) => _probe = probe;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var user = UserText(body);
            var system = SystemText(body);
            var joined = system + "\n" + user;
            string inner;
            if (joined.Contains("BRANDS TO SCORE", StringComparison.Ordinal))
            {
                _probe.FitSawAuthoritative |= joined.Contains("AUTHORITATIVE NARRATIVE SOURCE", StringComparison.Ordinal);
                _probe.FitSawSupplemental |= joined.Contains("SUPPLEMENTAL EXECUTION CONTEXT", StringComparison.Ordinal);
                inner = joined.Contains(Elderly, StringComparison.Ordinal)
                    ? FamixaHostile()
                    : NovixaFit();
            }
            else if (joined.Contains("Write the article now", StringComparison.Ordinal)
                     || joined.Contains("flagship article", StringComparison.Ordinal))
            {
                NoteGenerate(joined);
                inner = Web(joined);
            }
            else if (joined.Contains("Variant kinds required", StringComparison.Ordinal))
            {
                NoteGenerate(joined);
                inner = Pack(user);
            }
            else
            {
                inner = """{"ok":true}""";
            }

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

        private void NoteGenerate(string prompt)
        {
            _probe.GenerateSawNovixa |= prompt.Contains("Novixa", StringComparison.Ordinal);
            _probe.GenerateSawAuthoritative |= prompt.Contains("AUTHORITATIVE NARRATIVE CONTEXT", StringComparison.Ordinal);
            _probe.GenerateSawSupplemental |= prompt.Contains("NOT NARRATIVE AUTHORITY", StringComparison.Ordinal);
            _probe.GenerateSawTerritory |= prompt.Contains("novixa.core.inventory-fefo", StringComparison.Ordinal);
            _probe.GenerateSawWebContract |= prompt.Contains("long-form article, not a short post", StringComparison.Ordinal);
            _probe.GenerateSawBrief |= prompt.Contains("Giúp chủ nhà thuốc nhận ra hàng quá hạn", StringComparison.Ordinal);
            _probe.SawBrandBrainDump |= prompt.Contains("=== BRAND BRAIN ===", StringComparison.Ordinal)
                                        || prompt.Contains("FULL Brand Brain", StringComparison.Ordinal);
            var authority = prompt.IndexOf("AUTHORITATIVE NARRATIVE CONTEXT", StringComparison.Ordinal);
            var supplemental = prompt.IndexOf("SUPPLEMENTAL EXECUTION CONTEXT", StringComparison.Ordinal);
            if (authority >= 0 && supplemental > authority)
                _probe.NarrativeBeforeSupplemental = true;
            if (!string.IsNullOrWhiteSpace(_probe.Angle))
                _probe.GenerateSawAngle |= prompt.Contains(_probe.Angle, StringComparison.Ordinal);
        }

        private string NovixaFit()
        {
            _probe.Angle = "Hàng quá hạn là tiền nằm im trong kho, không phải doanh thu kém.";
            return JsonSerializer.Serialize(new
            {
                fits = new[]
                {
                    new
                    {
                        brandCode = "novixa",
                        fit = true,
                        territory = "core",
                        territoryId = "novixa.core.inventory-fefo",
                        reason = "Hàng quá hạn là chuyện tồn kho và thất thoát mà Novixa có lý do để kể.",
                        brandAngle = _probe.Angle,
                        confidence = 0.86,
                        boundaryWarnings = Array.Empty<string>(),
                        verdict = "fit",
                        score = 91,
                        title = "Hàng quá hạn đang làm mất của nhà thuốc bao nhiêu tiền?",
                        angle = _probe.Angle,
                    },
                },
            });
        }

        private static string FamixaHostile() =>
            JsonSerializer.Serialize(new
            {
                fits = new[]
                {
                    new
                    {
                        brandCode = "famixa",
                        fit = true,
                        territory = "core",
                        territoryId = "famixa.core.habits",
                        reason = "Thói quen nên là chuyện Famixa.",
                        brandAngle = "Góc ông bà sống khỏe",
                        confidence = 0.9,
                        boundaryWarnings = Array.Empty<string>(),
                        verdict = "fit",
                        score = 90,
                    },
                },
            });

        private string Pack(string user)
        {
            var kinds = Kinds(user);
            var angle = _probe.Angle ?? "Hàng quá hạn là tiền nằm im trong kho.";
            var variants = kinds.Select(kind => new
            {
                kind,
                title = "Hàng quá hạn trong kho nhà thuốc",
                bodyMarkdown = kind switch
                {
                    "fb_page" => Fb(angle),
                    "seo_meta" => "Hàng quá hạn là tiền nằm trong kho nhà thuốc. Novixa nhìn từ vận hành và hạn dùng.",
                    _ => angle + " Chủ nhà thuốc kiểm lô và hạn dùng trước khi nhìn doanh thu.",
                },
                meta = new { },
            });
            return JsonSerializer.Serialize(new { variants, imagePrompt = "Pharmacy storeroom, soft daylight, shelves of boxes, no text" });
        }

        private static string Web(string prompt)
        {
            var thesis = "Hàng quá hạn là tiền nằm im trong kho, không phải doanh thu kém.";
            if (prompt.Contains(thesis, StringComparison.Ordinal))
                thesis = prompt.Split('\n').FirstOrDefault(l => l.Contains(thesis, StringComparison.Ordinal))?.Trim() ?? thesis;
            var beat =
                "Cuối ngày chủ nhà thuốc mở ngăn hàng cận date. Hóa đơn bán vẫn đều. " +
                "Hộp đã quá hạn không còn là doanh thu. Đó là tiền mua hàng không quay lại quỹ. " +
                "Việc nhìn thấy là đối chiếu lô, hạn dùng và hàng bán chậm trong tuần. " +
                "Đây là vận hành kho nhà thuốc và ngăn thất thoát. " +
                thesis + " ";
            var body = new StringBuilder();
            body.Append("Một lô quá hạn vẫn nằm trên kệ. Doanh thu ngày hôm đó không nói hộp đó đã mất tiền.\n\n");
            while (body.Length < 2600)
            {
                body.Append("## Hạn dùng là tiền đang nằm trong kho\n\n");
                body.Append(beat).Append('\n').Append('\n');
                body.Append("## FEFO lệch thì thất thoát không hiện trên doanh thu\n\n");
                body.Append(beat).Append('\n').Append('\n');
                body.Append("## Việc trong tuần là soi lô, không soi khẩu hiệu\n\n");
                body.Append(beat).Append('\n').Append('\n');
            }

            return JsonSerializer.Serialize(new
            {
                title = "Hàng quá hạn đang giữ tiền của nhà thuốc",
                bodyMarkdown = body.ToString(),
            });
        }

        private static string Fb(string angle) =>
            angle + "\n\nDoanh thu đều chưa có nghĩa là kho đang khỏe. " +
            "Một lô quá hạn không đứng trong báo cáo bán hàng, nhưng tiền mua lô đó không quay lại. " +
            "Cuối tuần, đối chiếu hạn dùng với hàng bán chậm. Đó là việc vận hành, không phải khẩu hiệu.";

        private static IReadOnlyList<string> Kinds(string user)
        {
            var marker = "Variant kinds required (ONLY these): ";
            var i = user.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return ["fb_page", "fb_short", "social_caption", "seo_meta"];
            var line = user[(i + marker.Length)..].Split('\n')[0];
            return line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string UserText(string body) => Part(body, "contents");

        private static string SystemText(string body) => Part(body, "systemInstruction");

        private static string Part(string body, string node)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty(node, out var box)) return "";
                var target = box.ValueKind == JsonValueKind.Array ? box[0] : box;
                if (!target.TryGetProperty("parts", out var parts)) return "";
                var sb = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                        sb.AppendLine(text.GetString());
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

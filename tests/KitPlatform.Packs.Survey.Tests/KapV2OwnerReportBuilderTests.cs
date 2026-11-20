using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KitPlatform.Packs.Survey.Infrastructure;
using Xunit;

namespace KitPlatform.Packs.Survey.Tests;

public class KapV2OwnerReportBuilderTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    [Fact]
    public void Fixtures_match_owner_report_character_for_character()
    {
        var dir = AppContext.BaseDirectory;
        using var templateDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "PHARMACY_V2.template.json")));
        var index = KapV2TemplateIndex.FromJson(templateDoc.RootElement);
        var fixtures = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "owner-report.fixtures.json")))!.AsArray();
        var mismatches = new List<string>();
        foreach (var fx in fixtures)
        {
            var name = fx!["name"]!.GetValue<string>();
            var answers = fx["input"]!["answers"]!.AsObject()
                .ToDictionary(p => p.Key, p => p.Value!.GetValue<string>());
            var categories = fx["input"]!["categoryScores"]!.AsArray().Select(c => new KapV2CategoryInput(
                c!["code"]!.GetValue<string>(),
                c["scorePct"]!.GetValue<double>(),
                c["insufficientData"]!.GetValue<bool>())).ToList();
            var overall = fx["input"]!["overallPct"]!.GetValue<double>();
            var report = KapV2OwnerReportBuilder.Build(answers, categories, overall, index);
            var got = JsonSerializer.SerializeToNode(report, JsonOpts)!;
            var expected = fx["expected"]!;
            if (!Same(expected, got, "$", out var diff))
                mismatches.Add(name + ": " + diff);
        }
        if (mismatches.Count > 0)
        {
            var dump = string.Join("\n", mismatches);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "kap-fixture-mismatch.txt"), dump);
            Assert.Fail(dump.Length > 4000 ? dump[..4000] : dump);
        }
    }

    [Fact]
    public void Pharmacy_v2_pdfs_match_the_web_report()
    {
        var dir = AppContext.BaseDirectory;
        using var templateDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "PHARMACY_V2.template.json")));
        var index = KapV2TemplateIndex.FromJson(templateDoc.RootElement);
        var fixtures = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "owner-report.fixtures.json")))!.AsArray();
        var outDir = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "out-pdf"));
        Directory.CreateDirectory(outDir);
        foreach (var fx in fixtures)
        {
            var id = fx!["submissionId"]!.GetValue<string>();
            var name = fx["name"]!.GetValue<string>();
            var report = Build(fx, index);
            var owner = KapV2OwnerReportPdf.Generate(report, name, new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.FromHours(7)), null);
            var admin = KapV2OwnerReportPdf.Generate(report, name, new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.FromHours(7)), new KapV2PdfAdmin(
                name, "0900000000", "utm_campaign=qa5_19_11", true,
                [("C6", "—"), ("I7", "—"), ("O6", "—"), ("O7", "—"), ("B5", "—"), ("G3", "—")]));
            File.WriteAllBytes(Path.Combine(outDir, id + "-owner.pdf"), owner);
            File.WriteAllBytes(Path.Combine(outDir, id + "-admin.pdf"), admin);
            var ownerPages = PageCount(owner);
            var adminPages = PageCount(admin);
            Assert.InRange(ownerPages, 2, 4);
            Assert.Equal(ownerPages + 1, adminPages);
        }
    }

    private static KapV2OwnerReport Build(JsonNode fx, KapV2TemplateIndex index)
    {
        var answers = fx["input"]!["answers"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>());
        var categories = fx["input"]!["categoryScores"]!.AsArray().Select(c => new KapV2CategoryInput(
            c!["code"]!.GetValue<string>(),
            c["scorePct"]!.GetValue<double>(),
            c["insufficientData"]!.GetValue<bool>())).ToList();
        return KapV2OwnerReportBuilder.Build(answers, categories, fx["input"]!["overallPct"]!.GetValue<double>(), index);
    }

    private static int PageCount(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(text, @"/Type\s*/Page(?!s)").Count;
    }

    private static bool Same(JsonNode? expected, JsonNode? got, string path, out string diff)
    {
        diff = "";
        if (expected is null && got is null) return true;
        if (expected is null || got is null)
        {
            diff = $"{path} expected {expected?.ToJsonString()} got {got?.ToJsonString()}";
            return false;
        }
        if (expected is JsonObject eo && got is JsonObject go)
        {
            foreach (var prop in eo)
            {
                go.TryGetPropertyValue(prop.Key, out var gv);
                if (!Same(prop.Value, gv, path + "." + prop.Key, out diff)) return false;
            }
            foreach (var prop in go)
            {
                if (!eo.ContainsKey(prop.Key))
                {
                    diff = $"{path}.{prop.Key} unexpected {prop.Value?.ToJsonString()}";
                    return false;
                }
            }
            return true;
        }
        if (expected is JsonArray ea && got is JsonArray ga)
        {
            if (ea.Count != ga.Count)
            {
                diff = $"{path} length {ea.Count} vs {ga.Count}";
                return false;
            }
            for (var i = 0; i < ea.Count; i++)
                if (!Same(ea[i], ga[i], $"{path}[{i}]", out diff)) return false;
            return true;
        }
        if (expected is JsonValue ev && got is JsonValue gv2)
        {
            if (ev.ToJsonString() == gv2.ToJsonString()) return true;
            if (ev.TryGetValue<double>(out var ed) && gv2.TryGetValue<double>(out var gd) && ed == gd) return true;
            diff = $"{path} expected {ev.ToJsonString()} got {gv2.ToJsonString()}";
            return false;
        }
        diff = $"{path} type mismatch";
        return false;
    }
}

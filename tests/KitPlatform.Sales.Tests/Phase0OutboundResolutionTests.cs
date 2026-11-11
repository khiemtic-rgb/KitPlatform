using KitPlatform.Packs.Sales;
using Xunit;

namespace KitPlatform.Sales.Tests;

/// <summary>04 §10.2: outbound_unknown is resolved by a person, never by a resend.</summary>
public sealed class Phase0OutboundResolutionTests
{
    [Theory]
    [InlineData("outbound_unknown", false, false, true, "Thấy tin", null)]
    [InlineData("outbound_unknown", false, false, false, null, null)]
    [InlineData("outbound_unknown", false, false, true, null, "note_required")]
    [InlineData("outbound_unknown", false, false, true, "   ", "note_required")]
    [InlineData("outbound_unknown", true, false, false, null, "already_resolved")]
    [InlineData("outbound_unknown", false, true, true, "Thấy tin", "already_sent")]
    [InlineData("outbound_sent", false, false, true, "Thấy tin", "not_unknown")]
    [InlineData("outbound_failed", false, false, false, null, "not_unknown")]
    [InlineData(null, false, false, false, null, "not_unknown")]
    public void Resolve_rules(string? outcome, bool resolved, bool sentAt, bool delivered, string? note, string? expected) =>
        Assert.Equal(expected, FacebookOutboundRules.DenyResolve(outcome, resolved, sentAt, delivered, note));

    [Fact]
    public void Resolve_note_is_bounded()
    {
        var max = new string('a', FacebookOutboundRules.ResolutionNoteMax);
        Assert.Null(FacebookOutboundRules.DenyResolve("outbound_unknown", false, false, true, max));
        Assert.Equal("note_too_long", FacebookOutboundRules.DenyResolve("outbound_unknown", false, false, true, max + "a"));
    }

    [Fact]
    public void Resolve_path_makes_no_http_call_and_closes_the_check_task()
    {
        var gate = File.ReadAllText(Path.Combine(SalesInfra(), "FacebookOutboundGate.cs"));
        var resolve = Slice(gate, "public async Task<KitSalesInteractionDto> ResolveUnknownAsync(", "public async Task<KitSalesInteractionDto> SendApprovedAsync(");
        Assert.DoesNotContain("_graph", resolve, StringComparison.Ordinal);
        Assert.DoesNotContain("SendOnceAsync", resolve, StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE", gate[..gate.IndexOf("private sealed record Reservation", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("FacebookOutboundRules.DenyResolve(", resolve, StringComparison.Ordinal);
        Assert.Contains("FacebookOutboundReconciler.TaskAction", resolve, StringComparison.Ordinal);
        Assert.Contains("confirmedBy = \"human\"", resolve, StringComparison.Ordinal);
        Assert.Contains("approved_by_user_id = NULL", resolve, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_endpoint_requires_write_policy()
    {
        var controller = File.ReadAllText(Path.Combine(
            FindRepo(), "src", "KitPlatform.Api", "Controllers", "KitSales", "KitSalesController.cs"));
        var endpoint = Slice(controller, "[HttpPost(\"chat/drafts/{draftId:guid}/outbound-resolution\")]", "public async Task");
        Assert.Contains("[Authorize(Policy = KitSalesPolicies.Write)]", endpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void Kpi_keeps_sent_and_unknown_apart()
    {
        var agent = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesAgentService.cs"));
        Assert.Contains("InPeriod(\"outbound_sent\")", agent, StringComparison.Ordinal);
        Assert.Contains("InPeriod(\"outbound_unknown\")", agent, StringComparison.Ordinal);
        Assert.Contains("u.event_type = 'outbound_unknown'", agent, StringComparison.Ordinal);
    }

    private static string Slice(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Missing marker: {start}");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"Missing end marker: {end}");
        return source[from..to];
    }

    private static string SalesInfra() =>
        Path.Combine(FindRepo(), "src", "Packs", "Sales", "KitPlatform.Packs.Sales.Infrastructure");

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.kit-sales.txt")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}

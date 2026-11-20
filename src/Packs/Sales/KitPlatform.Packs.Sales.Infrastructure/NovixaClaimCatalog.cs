using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

public static class NovixaClaimCatalog
{
    private static readonly string[] Stages = ["discover", "contact", "engage", "phc", "qualify", "demo", "pilot"];
    private static readonly string[] Channels = ["messenger", "phone_script"];
    private static readonly string[] Audience = ["pharmacy"];

    public static IReadOnlyList<SalesClaim> Active(string phcUrl) =>
        NovixaSalesKnowledge.Rows(phcUrl).Select(row => new SalesClaim(
            row.Intent == "unknown" ? "novixa.escalate" : "novixa." + row.Intent,
            "novixa",
            row.Intent,
            row.Reply,
            row.Intent is "no_revenue_promise" or "no_unlimited_trial" or "not_in_phase1" ? "deny" : "affirm",
            SalesOutputValidator.Numbers(row.Reply),
            Stages,
            Channels,
            Audience,
            row.Citation)).ToList();

    public static SalesSentence Sentence(string intent, string phcUrl)
    {
        var claim = Active(phcUrl).Single(item => item.Intent == intent);
        return new SalesSentence(claim.Text, claim.Id, false);
    }
}

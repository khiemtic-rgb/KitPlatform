using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Sales;

public sealed record SalesClaim(
    string Id,
    string ProductCode,
    string Intent,
    string Text,
    string Polarity,
    IReadOnlyList<long> Slots,
    IReadOnlyList<string> Stages,
    IReadOnlyList<string> Channels,
    IReadOnlyList<string> Audience,
    string Citation);

public sealed record SalesSentence(string Text, string? ClaimId, bool NonClaim);

public sealed record SalesValidationRequest(
    string ProductCode,
    string Stage,
    string Channel,
    string Audience,
    string Intent,
    bool FirstMessage,
    bool Autonomous,
    bool RequireAssistantDisclosure,
    IReadOnlyList<string> AllowedHosts,
    IReadOnlyList<SalesClaim> Claims,
    IReadOnlyList<SalesSentence> Sentences);

public sealed record SalesValidationResult(string Decision, IReadOnlyList<string> Failures);

public static partial class SalesOutputValidator
{
    private static readonly string[] PromiseWords =
        ["cam kết", "đảm bảo", "chắc chắn", "giúp tăng", "tích hợp", "có sẵn", "miễn phí", "giảm"];

    private static readonly string[] Forbidden =
        ["tăng doanh thu", "dùng thử 14", "hđđt có sẵn", "tư vấn điều trị", "đạt gpp", "đối thủ"];

    public static SalesValidationResult Check(SalesValidationRequest request)
    {
        var failures = new List<string>();
        var decision = "pass";
        if (request.Sentences.Count == 0)
            return new SalesValidationResult("fallback_catalog", ["empty"]);

        var claims = request.Claims.ToDictionary(claim => claim.Id, StringComparer.Ordinal);
        foreach (var sentence in request.Sentences)
        {
            if (sentence.NonClaim)
            {
                if (HasPromise(sentence.Text) || Numbers(sentence.Text).Count > 0)
                    Worse(ref decision, "fallback_catalog", failures, "non_claim");
                continue;
            }

            if (sentence.ClaimId is null || !claims.TryGetValue(sentence.ClaimId, out var claim))
            {
                Worse(ref decision, "fallback_catalog", failures, "unknown_claim");
                continue;
            }

            if (!string.Equals(claim.ProductCode, request.ProductCode, StringComparison.OrdinalIgnoreCase))
                Worse(ref decision, "fallback_catalog", failures, "other_product");
            if (!claim.Stages.Contains(request.Stage) || !claim.Channels.Contains(request.Channel) || !claim.Audience.Contains(request.Audience))
                Worse(ref decision, "fallback_catalog", failures, "context");

            var exact = string.Equals(Fold(sentence.Text), Fold(claim.Text), StringComparison.Ordinal);
            if (!exact)
            {
                var numbers = Numbers(sentence.Text);
                if (numbers.Any(number => !claim.Slots.Contains(number)))
                    Worse(ref decision, "block", failures, "number");
                if (claim.Polarity == "deny" && !sentence.Text.Contains("không", StringComparison.OrdinalIgnoreCase))
                    Worse(ref decision, "block", failures, "polarity");
                if (Forbidden.Any(word => sentence.Text.Contains(word, StringComparison.OrdinalIgnoreCase))
                    && claim.Polarity != "deny")
                    Worse(ref decision, "block", failures, "forbidden");
            }

            if (HasPhoneOrEmail(sentence.Text) || HasForeignUrl(sentence.Text, request.AllowedHosts))
                Worse(ref decision, "block", failures, "private_or_url");
        }

        if (request.Intent == "unknown")
        {
            var onlyEscalate = request.Sentences.All(sentence => sentence.ClaimId == "novixa.escalate");
            if (!onlyEscalate)
                Worse(ref decision, "escalate", failures, "unknown_intent");
        }
        else if (request.Sentences.All(sentence => sentence.NonClaim || !claims.TryGetValue(sentence.ClaimId!, out var claim) || claim.Intent != request.Intent))
        {
            Worse(ref decision, "escalate", failures, "intent");
        }

        var joined = string.Join(" ", request.Sentences.Select(sentence => sentence.Text));
        if (request.FirstMessage && !joined.Contains("Novixa", StringComparison.OrdinalIgnoreCase) && !joined.Contains("KIT", StringComparison.Ordinal))
            Worse(ref decision, "block", failures, "identity");
        if (request.Autonomous && request.RequireAssistantDisclosure && !joined.Contains("trợ lý tự động", StringComparison.Ordinal))
            Worse(ref decision, "block", failures, "disclosure");
        if (joined.Contains("em là người", StringComparison.OrdinalIgnoreCase))
            Worse(ref decision, "block", failures, "human_claim");
        if (joined.Count(ch => ch == '?') > 1 || joined.Length > 800 || UrlCount(joined) > 1)
            Worse(ref decision, "fallback_catalog", failures, "shape");
        if (joined == joined.ToUpperInvariant() && joined.Any(char.IsLetter))
            Worse(ref decision, "fallback_catalog", failures, "shouting");

        return new SalesValidationResult(decision, failures);
    }

    private static void Worse(ref string decision, string next, List<string> failures, string code)
    {
        failures.Add(code);
        if (Rank(next) > Rank(decision))
            decision = next;
    }

    private static int Rank(string decision) => decision switch
    {
        "block" => 4,
        "escalate" => 3,
        "fallback_catalog" => 2,
        _ => 1,
    };

    private static bool HasPromise(string text) =>
        PromiseWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static bool HasPhoneOrEmail(string text) =>
        PhonePattern().IsMatch(text) || text.Contains('@', StringComparison.Ordinal);

    private static bool HasForeignUrl(string text, IReadOnlyList<string> allowedHosts)
    {
        foreach (Match match in UrlPattern().Matches(text))
        {
            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
                return true;
            if (!allowedHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static int UrlCount(string text) => UrlPattern().Matches(text).Count;

    public static IReadOnlyList<long> Numbers(string text) =>
        NumberPattern().Matches(text).Select(match => long.Parse(match.Value.Replace(".", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture)).ToList();

    private static string Fold(string text) => text.Trim();

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})+|\d+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"https?://[^\s]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"(?<!\d)0\d{8,10}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}

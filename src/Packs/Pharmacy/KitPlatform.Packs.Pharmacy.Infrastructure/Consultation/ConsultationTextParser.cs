using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal static class ConsultationTextParser
{
    internal static PharmacyConsultationFactsDto EnrichFromNaturalLanguage(
        PharmacyConsultationFactsDto facts,
        string? naturalLanguage,
        IReadOnlyList<string> quickSymptoms)
    {
        var text = naturalLanguage?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            return facts;

        var ageYears = facts.AgeYears;
        if (ageYears is null)
        {
            var ageMatch = Regex.Match(text, @"(\d{1,3})\s*tuổi", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (ageMatch.Success
                && int.TryParse(ageMatch.Groups[1].Value, out var years)
                && years is >= 0 and <= 120)
            {
                ageYears = years;
            }
            else
            {
                var compactAge = Regex.Match(text, @"\b(\d{1,3})\s*t\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (compactAge.Success
                    && int.TryParse(compactAge.Groups[1].Value, out years)
                    && years is >= 0 and <= 120)
                {
                    ageYears = years;
                }
            }
        }

        var gender = facts.Gender;
        if (string.IsNullOrWhiteSpace(gender))
        {
            if (Regex.IsMatch(text, @"\b(nam|ông|anh|bác|chú|male)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                gender = "male";
            else if (Regex.IsMatch(text, @"\b(nữ|bà|chị|cô|female)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                gender = "female";
            else if (Regex.IsMatch(text, @"(?:,\s*|\s+)(nam|nữ|nu)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var tail = Regex.Match(text, @"(?:,\s*|\s+)(nam|nữ|nu)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                gender = tail.Groups[1].Value.Equals("nữ", StringComparison.OrdinalIgnoreCase)
                           || tail.Groups[1].Value.Equals("nu", StringComparison.OrdinalIgnoreCase)
                    ? "female"
                    : "male";
            }
        }

        var durationDays = facts.DurationDays;
        if (durationDays is null)
        {
            var dayMatch = Regex.Match(text, @"(\d{1,3})\s*ngày", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dayMatch.Success && int.TryParse(dayMatch.Groups[1].Value, out var days) && days is >= 0 and <= 365)
                durationDays = days;
        }

        var negation = ScanNegations(text);
        var noFeverEnglish = Regex.IsMatch(text, @"\bno\s+fever\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var feverStated = Regex.IsMatch(negation.MaskedText, @"\b(sot|fever)\b", RegexOptions.CultureInvariant)
                          && !noFeverEnglish;
        var feverDenied = negation.NegatedCodes.Contains(PharmacyConsultationSymptoms.Fever) || noFeverEnglish;
        var feverPicked = quickSymptoms.Contains(PharmacyConsultationSymptoms.Fever, StringComparer.OrdinalIgnoreCase);
        var hasFever = facts.HasFever;
        if (feverStated || feverPicked)
            hasFever = true;
        else if (feverDenied)
            hasFever = false;

        var isPregnant = facts.IsPregnant;
        if (isPregnant is not true
            && Regex.IsMatch(text, @"\b(mang thai|bầu|bau|pregnant)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            && !Regex.IsMatch(text, @"(không\s+mang\s+thai|khong\s+mang\s+thai|not\s+pregnant)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            isPregnant = true;
        }

        var isBreastfeeding = facts.IsBreastfeeding;
        if (isBreastfeeding is not true
            && Regex.IsMatch(text, @"\b(cho con bú|dang cho con bu|đang cho con bú|breastfeeding)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            isBreastfeeding = true;
        }

        var symptoms = facts.Symptoms.ToList();
        if (quickSymptoms.Count > 0)
        {
            symptoms.AddRange(quickSymptoms);
        }

        MergeSymptomsFromText(symptoms, negation.MaskedText);

        symptoms = symptoms
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ConsultationSafetyRules.NormalizeFacts(facts with
        {
            AgeYears = ageYears,
            Gender = gender,
            DurationDays = durationDays,
            HasFever = hasFever,
            IsPregnant = isPregnant,
            IsBreastfeeding = isBreastfeeding,
            Symptoms = symptoms,
        });
    }

    internal static bool ShouldUseGemini(string? naturalLanguage, PharmacyConsultationFactsDto localFacts)
    {
        var text = naturalLanguage?.Trim() ?? "";
        if (text.Length == 0)
            return false;

        if (text.Length > 150)
            return true;

        if (MayHaveRedFlags(text))
            return true;

        if (localFacts.DurationDays is null
            && Regex.IsMatch(text, @"\b(\d+\s*(ngày|tuan|tuần|tháng|thang)|hôm qua|hom qua|mấy ngày|may ngay)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        return false;
    }

    internal static bool MayHaveRedFlags(string text) =>
        Regex.IsMatch(
            text,
            @"\b(khó thở|kho tho|đau ngực|dau nguc|choáng|ngất|ngat|co giật|co giat|chảy máu|chay mau|"
            + @"nuốt khó|nuot kho|tím tái|tim tai|li bì|li bi)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void MergeSymptomsFromText(List<string> symptoms, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        void TryAdd(string code, string pattern)
        {
            if (symptoms.Contains(code, StringComparer.OrdinalIgnoreCase))
                return;
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                symptoms.Add(code);
        }

        TryAdd(PharmacyConsultationSymptoms.CoughDry, @"\bho\s*khan\b|\bho\s*k\b");
        TryAdd(PharmacyConsultationSymptoms.Cough, @"\b(?:bi\s+)?ho\b|\bho\s+\d");
        TryAdd(PharmacyConsultationSymptoms.SoreThroat, @"\b(?:dau|đau)\s*(?:hong|họng)\b");
        TryAdd(PharmacyConsultationSymptoms.RunnyNose, @"\b(?:so|sổ)\s*mui\b");
        TryAdd(PharmacyConsultationSymptoms.NasalCongestion, @"\b(?:nghet|nghẹt)\s*mui\b");
        TryAdd(PharmacyConsultationSymptoms.Fever, @"\b(?:sot|sốt)\b");
        TryAdd(PharmacyConsultationSymptoms.Diarrhea, @"\b(?:tieu|tiêu)\s*chay\b|\b(?:tieu|tiêu)\s*chảy\b|\b(?:ia|ỉa)\s*(?:chay|chảy|long|lỏng)\b|\b(?:di|đi)\s*(?:ngoai|ngoài|ta|tả)\b|\btiêu\s*lỏng\b|\btieu\s*long\b");
        TryAdd(PharmacyConsultationSymptoms.Headache, @"\b(?:dau|đau)\s*(?:dau|đầu)\b");
        TryAdd(PharmacyConsultationSymptoms.CommonCold, @"\b(?:cam|cảm)\s*(?:lanh|lạnh|cúm|cum)\b");
        TryAdd(PharmacyConsultationSymptoms.RedEye, @"\b(?:dau|đau)\s*mắt\s*đỏ\b|\b(?:do|đỏ)\s*mắt\b|\bmat\s*do\b");
        TryAdd(PharmacyConsultationSymptoms.ItchyEye, @"\b(?:ngua|ngứa)\s*mắt\b|\bngua\s*mat\b");
        TryAdd(PharmacyConsultationSymptoms.WateryEye, @"\b(?:chảy|chay)\s*nước\s*mắt\b|\bchay\s*nuoc\s*mat\b");
        TryAdd("dry_eye", @"\b(?:kho|khô)\s*mắt\b|\bkho\s*mat\b");
    }

    internal static PharmacyConsultationFactsDto ApplySymptomAliases(
        PharmacyConsultationFactsDto facts,
        string? naturalLanguage,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? aliasesByCode)
    {
        var matched = MatchAliasSymptoms(naturalLanguage, aliasesByCode);
        if (matched.Count == 0)
            return facts;

        var symptoms = facts.Symptoms
            .Concat(matched)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return facts with { Symptoms = symptoms };
    }

    internal static IReadOnlyList<string> MatchAliasSymptoms(
        string? naturalLanguage,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? aliasesByCode)
    {
        if (aliasesByCode is null || string.IsNullOrWhiteSpace(naturalLanguage))
            return [];

        var tokens = Tokenize(naturalLanguage);
        if (tokens.Count == 0)
            return [];

        var codes = new List<string>();
        foreach (var (code, aliases) in aliasesByCode)
        {
            foreach (var alias in aliases)
            {
                var aliasTokens = Tokenize(alias);
                if (aliasTokens.Count == 0 || aliasTokens.Any(token => token.Length < 2))
                    continue;
                if (aliasTokens.Count == 1 && aliasTokens[0].Length < 3)
                    continue;
                if (!ContainsSequence(tokens, aliasTokens))
                    continue;
                codes.Add(code);
                break;
            }
        }

        return codes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal sealed record NegationScan(string MaskedText, IReadOnlySet<string> NegatedCodes);

    private const string MaskedToken = "xneg";

    private static readonly HashSet<string> NegatorTokens = new(StringComparer.Ordinal)
    {
        "khong", "ko", "kg", "khg", "k", "chua", "het", "khoi",
    };

    private static readonly HashSet<string> NegationFillers = new(StringComparer.Ordinal)
    {
        "bi", "co", "con", "thay", "he",
    };

    private static readonly HashSet<string> NegationChains = new(StringComparer.Ordinal)
    {
        "hay", "hoac",
    };

    // Red-flag phrases (khó thở, đau ngực…) are deliberately absent: a misread
    // negation there must over-refer, never hide the flag.
    private static readonly (string Code, string Phrase)[] ExtraNegatablePhrases =
    [
        (PharmacyConsultationSymptoms.SoreThroat, "dau hong"),
        (PharmacyConsultationSymptoms.RunnyNose, "so mui"),
        (PharmacyConsultationSymptoms.NasalCongestion, "nghet mui"),
        (PharmacyConsultationSymptoms.Diarrhea, "tieu chay"),
        (PharmacyConsultationSymptoms.Headache, "dau dau"),
        (PharmacyConsultationSymptoms.CommonCold, "cam lanh"),
        (PharmacyConsultationSymptoms.RedEye, "mat do"),
        (PharmacyConsultationSymptoms.ItchyEye, "ngua mat"),
        (PharmacyConsultationSymptoms.WateryEye, "chay nuoc mat"),
        ("dry_eye", "kho mat"),
        (PharmacyConsultationSymptoms.Vomiting, "non"),
        (PharmacyConsultationSymptoms.Chills, "ret run"),
        (PharmacyConsultationSymptoms.Sneezing, "hat hoi"),
        (PharmacyConsultationSymptoms.Fever, "fever"),
    ];

    private static readonly (string Code, string[] Tokens)[] NegatablePhrases =
        ConsultationPhraseBook.All
            .Concat(ExtraNegatablePhrases)
            .Select(p => (p.Code, p.Phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .OrderByDescending(p => p.Item2.Length)
            .ToArray();

    /// <summary>
    /// "không sốt", "chưa bị ho", "hết sốt", "không sốt hay ho": the symptom
    /// phrase right after the negator is masked so no matcher sees it.
    /// </summary>
    internal static NegationScan ScanNegations(string? text)
    {
        var negated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return new NegationScan("", negated);

        var tokens = Tokenize(text);
        var masked = tokens.ToArray();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!IsNegator(tokens, i))
                continue;

            var cursor = i + 1;
            while (cursor < tokens.Count)
            {
                while (cursor < tokens.Count && NegationFillers.Contains(tokens[cursor]))
                    cursor++;
                var hit = LongestNegatablePhrase(tokens, cursor);
                if (hit is null)
                    break;
                negated.Add(hit.Value.Code);
                for (var k = cursor; k < cursor + hit.Value.Length; k++)
                    masked[k] = MaskedToken;
                cursor += hit.Value.Length;
                if (cursor < tokens.Count && NegationChains.Contains(tokens[cursor]))
                {
                    cursor++;
                    continue;
                }
                break;
            }
        }

        return new NegationScan(string.Join(' ', masked), negated);
    }

    /// <summary>
    /// Alias + phrase-book symptoms on the negation-masked text, then drops what the text denies.
    /// </summary>
    internal static PharmacyConsultationFactsDto ApplyTextSymptoms(
        PharmacyConsultationFactsDto facts,
        string? naturalLanguage,
        IReadOnlyList<string> quickSymptoms,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? aliasesByCode)
    {
        var masked = ScanNegations(naturalLanguage).MaskedText;
        facts = ApplySymptomAliases(facts, masked, aliasesByCode);
        facts = ConsultationPhraseBook.Apply(facts, masked);
        facts = DropNegatedSymptoms(facts, naturalLanguage, quickSymptoms, aliasesByCode);
        return ConsultationSafetyRules.NormalizeFacts(facts);
    }

    /// <summary>
    /// Removes symptoms the text explicitly denies, unless the text also states
    /// them plainly or staff picked them as quick symptoms.
    /// </summary>
    internal static PharmacyConsultationFactsDto DropNegatedSymptoms(
        PharmacyConsultationFactsDto facts,
        string? naturalLanguage,
        IReadOnlyList<string> quickSymptoms,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? aliasesByCode)
    {
        var scan = ScanNegations(naturalLanguage);
        if (scan.NegatedCodes.Count == 0)
            return facts;

        var stated = new List<string>();
        MergeSymptomsFromText(stated, scan.MaskedText);
        stated.AddRange(ConsultationPhraseBook.Match(scan.MaskedText));
        stated.AddRange(MatchAliasSymptoms(scan.MaskedText, aliasesByCode));
        var keep = new HashSet<string>(stated.Concat(quickSymptoms), StringComparer.OrdinalIgnoreCase);

        var symptoms = facts.Symptoms
            .Where(code => !scan.NegatedCodes.Contains(code) || keep.Contains(code))
            .ToList();
        return symptoms.Count == facts.Symptoms.Count ? facts : facts with { Symptoms = symptoms };
    }

    private static bool IsNegator(IReadOnlyList<string> tokens, int index)
    {
        var token = tokens[index];
        if (!NegatorTokens.Contains(token))
            return false;
        // "ho k" is shorthand for "ho khan".
        return !(token == "k" && index > 0 && tokens[index - 1] == "ho");
    }

    private static (string Code, int Length)? LongestNegatablePhrase(IReadOnlyList<string> tokens, int start)
    {
        if (start >= tokens.Count)
            return null;
        foreach (var (code, phraseTokens) in NegatablePhrases)
        {
            if (start + phraseTokens.Length > tokens.Count)
                continue;
            var matches = true;
            for (var offset = 0; offset < phraseTokens.Length; offset++)
            {
                if (!tokens[start + offset].Equals(phraseTokens[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
                return (code, phraseTokens.Length);
        }
        return null;
    }

    internal static List<string> Tokenize(string text)
    {
        var folded = Fold(text);
        return Regex.Split(folded, @"[^a-z0-9]+")
            .Where(token => token.Length > 0)
            .ToList();
    }

    private static string Fold(string text)
    {
        var lower = text.Trim().ToLowerInvariant().Replace('đ', 'd');
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsSequence(IReadOnlyList<string> tokens, IReadOnlyList<string> aliasTokens)
    {
        if (aliasTokens.Count > tokens.Count)
            return false;
        for (var start = 0; start <= tokens.Count - aliasTokens.Count; start++)
        {
            var matches = true;
            for (var offset = 0; offset < aliasTokens.Count; offset++)
            {
                if (!tokens[start + offset].Equals(aliasTokens[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
                return true;
        }
        return false;
    }
}

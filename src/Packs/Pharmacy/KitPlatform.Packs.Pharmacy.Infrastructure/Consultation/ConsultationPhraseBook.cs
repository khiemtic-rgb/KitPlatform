using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

/// <summary>
/// Câu nhân viên quầy hay gõ, gắn vào mã triệu chứng đã có.
/// Câu dài hơn thắng câu ngắn nằm trong nó: "ho đờm" không còn là mỗi "ho".
/// </summary>
internal static class ConsultationPhraseBook
{
    private static readonly (string Code, string Phrase)[] Phrases =
    [
        ("cough_phlegm", "ho co dom"),
        ("cough_phlegm", "ho dom"),
        ("cough_phlegm", "dom"),
        ("cough_dry", "ho khan"),
        ("cough", "ho"),
        ("diarrhea", "dau bung di ngoai"),
        ("diarrhea", "di long"),
        ("diarrhea", "ia chay"),
        ("diarrhea", "ia long"),
        ("diarrhea", "di ta"),
        ("diarrhea", "tieu long"),
        ("heartburn", "dau thuong vi"),
        ("heartburn", "viem da day"),
        ("heartburn", "nong rat thuong vi"),
        ("heartburn", "o chua"),
        ("heartburn", "o nong"),
        ("heartburn", "on nong"),
        ("itchy_eye", "ngua mat"),
        ("itchy_skin", "ngua da"),
        ("allergy", "man ngua"),
        ("itchy_skin", "ngua"),
        ("rash", "phat ban"),
        ("rash", "man"),
        ("hives", "me day"),
        ("runny_nose", "cam cum"),
        ("runny_nose", "cam lanh"),
        ("runny_nose", "hat hoi"),
        ("runny_nose", "viem mui"),
        ("runny_nose", "so mui"),
        ("nasal_congestion", "ngat mui"),
        ("nasal_congestion", "nghet mui"),
        ("sore_throat", "viem hong"),
        ("sore_throat", "rat hong"),
        ("sore_throat", "dau hong"),
        ("allergy", "noi man"),
        ("allergy", "di ung"),
        ("hives", "noi me day"),
        ("red_eye", "viem ket mac"),
        ("red_eye", "mat do"),
        ("red_eye", "dau mat do"),
        ("fatigue", "suy nhuoc"),
        ("fatigue", "ue oai"),
        ("fatigue", "met moi"),
        ("insomnia", "ngu khong ngon"),
        ("insomnia", "mat ngu"),
        ("back_pain", "dau vai gay"),
        ("back_pain", "dau lung"),
        ("joint_pain", "nhuc moi"),
        ("joint_pain", "dau khop"),
        ("nausea", "non oi"),
        ("nausea", "buon non"),
        ("nausea", "oi"),
        ("toothache", "nhuc rang"),
        ("toothache", "dau rang"),
        ("dizziness", "chong mat"),
        ("dizziness", "hoa mat"),
        ("constipation", "bi dai tien"),
        ("constipation", "kho di ngoai"),
        ("constipation", "tao bon"),
        ("constipation", "bi tao"),
        ("bloating", "an khong tieu"),
        ("bloating", "day bung"),
        ("bloating", "hoi bung"),
        ("bloating", "chuong bung"),
        ("abdominal_pain", "dau bung"),
        ("fatigue", "met"),
        ("insomnia", "kho ngu"),
        ("headache", "nhuc dau"),
        ("headache", "dau dau"),
        ("body_ache", "dau nhuc"),
        ("fever", "nong sot"),
        ("fever", "sot cao"),
        ("fever", "sot"),
    ];

    internal static IReadOnlyList<(string Code, string Phrase)> All => Phrases;

    internal static PharmacyConsultationFactsDto Apply(PharmacyConsultationFactsDto facts, string? naturalLanguage)
    {
        var matched = Match(naturalLanguage);
        var symptoms = facts.Symptoms
            .Concat(matched)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symptoms.Contains("cough_phlegm") || symptoms.Contains("cough_dry"))
            symptoms.RemoveAll(code => code.Equals("cough", StringComparison.OrdinalIgnoreCase));

        if (symptoms.Count == facts.Symptoms.Count
            && symptoms.All(code => facts.Symptoms.Contains(code, StringComparer.OrdinalIgnoreCase)))
        {
            return facts;
        }

        return facts with { Symptoms = symptoms };
    }

    internal static IReadOnlyList<string> Match(string? naturalLanguage)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
            return [];

        var tokens = ConsultationTextParser.Tokenize(naturalLanguage);
        if (tokens.Count == 0)
            return [];

        var hits = new List<(string Code, int Start, int Length)>();
        foreach (var (code, phrase) in Phrases)
        {
            var phraseTokens = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (phraseTokens.Length == 0 || phraseTokens.Length > tokens.Count)
                continue;
            for (var start = 0; start <= tokens.Count - phraseTokens.Length; start++)
            {
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
                    hits.Add((code, start, phraseTokens.Length));
            }
        }

        return hits
            .Where(hit => !hits.Any(other =>
                other.Length > hit.Length
                && other.Start <= hit.Start
                && other.Start + other.Length >= hit.Start + hit.Length))
            .Select(hit => hit.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

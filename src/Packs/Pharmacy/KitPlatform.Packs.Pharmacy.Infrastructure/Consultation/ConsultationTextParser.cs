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

        var hasFever = facts.HasFever;
        if (hasFever is not true
            && Regex.IsMatch(text, @"\b(sốt|sot|fever)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            && !Regex.IsMatch(text, @"(không\s+sốt|khong\s+sot|không\s+sot|no\s+fever)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            hasFever = true;
        }

        if (hasFever is null
            && Regex.IsMatch(text, @"(không\s+sốt|khong\s+sot|không\s+sot|no\s+fever)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            hasFever = false;
        }

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

        MergeSymptomsFromText(symptoms, text);

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
        TryAdd(PharmacyConsultationSymptoms.Diarrhea, @"\b(?:tieu|tiêu)\s*chay\b|\b(?:tieu|tiêu)\s*chảy\b");
        TryAdd(PharmacyConsultationSymptoms.Headache, @"\b(?:dau|đau)\s*(?:dau|đầu)\b");
        TryAdd(PharmacyConsultationSymptoms.CommonCold, @"\b(?:cam|cảm)\s*(?:lanh|lạnh|cúm|cum)\b");
    }
}

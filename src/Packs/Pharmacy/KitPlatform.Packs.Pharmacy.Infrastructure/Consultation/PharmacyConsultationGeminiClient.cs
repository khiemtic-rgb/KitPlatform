using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal sealed class PharmacyConsultationGeminiClient
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";

    private static readonly string[] TextFallbacks =
    [
        "gemini-3.6-flash",
        "gemini-flash-latest",
        "gemini-2.5-flash-lite",
    ];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<PharmacyConsultationGeminiClient> _logger;

    public PharmacyConsultationGeminiClient(
        HttpClient http,
        ILogger<PharmacyConsultationGeminiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(PharmacyConsultationFactsDto Facts, string Model)> ExtractFactsAsync(
        string deidentifiedText,
        IReadOnlyList<string> quickSymptoms,
        IReadOnlyList<PharmacyConsultationSymptomOptionDto> symptomOptions,
        PharmacyConsultationAiConfigProvider.ResolvedConfig aiConfig,
        CancellationToken ct)
    {
        var key = aiConfig.ApiKey
                  ?? throw new InvalidOperationException(
                      "Chưa cấu hình Gemini — vào Cài đặt POS → AI tư vấn quầy, hoặc dùng key Content Park.");

        var symptomCodes = string.Join(
            ", ",
            symptomOptions.Select(o => o.Code));

        var system = """
            Bạn trích xuất thông tin tư vấn quầy thuốc OTC (không chẩn đoán, không kê đơn).
            Trả về JSON duy nhất với schema:
            {
              "ageYears": number|null,
              "ageMonths": number|null,
              "gender": "male"|"female"|"other"|null,
              "symptoms": string[],
              "durationDays": number|null,
              "hasFever": boolean|null,
              "isPregnant": boolean|null,
              "isBreastfeeding": boolean|null,
              "redFlags": string[],
              "notes": string|null
            }
            symptoms codes (chọn từ danh sách): 
            """ + symptomCodes + """

            redFlags codes when present: difficulty_breathing, chest_pain, severe_bleeding, unconscious, seizure,
              swallowing_difficulty, high_fever_infant, shortness_of_breath
            Chỉ dùng thông tin trong câu. Không suy diễn chẩn đoán.
            """;

        var quick = quickSymptoms.Count > 0
            ? $"Quick symptoms selected: {string.Join(", ", quickSymptoms)}"
            : "";
        var user = string.IsNullOrWhiteSpace(deidentifiedText)
            ? quick
            : $"{deidentifiedText}\n{quick}".Trim();

        var model = string.IsNullOrWhiteSpace(aiConfig.TextModel)
            ? TextFallbacks[0]
            : aiConfig.TextModel.Trim();

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = user } } } },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json",
                maxOutputTokens = 1024,
                thinkingConfig = new { thinkingBudget = 0 },
            },
        };

        var url = $"{ApiBase}/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(key)}";
        using var res = await _http.PostAsJsonAsync(url, body, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini {model} failed ({(int)res.StatusCode})");

        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Gemini {model} returned no candidates");
        }

        var text = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini returned empty JSON");

        var parsed = JsonSerializer.Deserialize<GeminiFactsPayload>(text, JsonOpts)
                     ?? throw new InvalidOperationException("Gemini JSON parse failed");

        var facts = new PharmacyConsultationFactsDto(
            parsed.AgeYears,
            parsed.AgeMonths,
            parsed.Gender,
            MergeSymptoms(parsed.Symptoms, quickSymptoms),
            parsed.DurationDays,
            parsed.HasFever,
            parsed.IsPregnant,
            parsed.IsBreastfeeding,
            parsed.RedFlags ?? [],
            parsed.Notes);

        return (ConsultationSafetyRules.NormalizeFacts(facts), model);
    }

    internal static string Deidentify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var s = text.Trim();
        s = Regex.Replace(s, @"\b0\d{8,10}\b", "[phone]");
        s = Regex.Replace(s, @"\b\d{9,12}\b", "[phone]");
        s = Regex.Replace(s, @"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", "[email]", RegexOptions.IgnoreCase);
        return s;
    }

    private static IReadOnlyList<string> MergeSymptoms(
        IReadOnlyList<string>? fromAi,
        IReadOnlyList<string> quick)
    {
        return (fromAi ?? [])
            .Concat(quick)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static PharmacyConsultationFactsDto FactsFromQuickOnly(IReadOnlyList<string> quickSymptoms) =>
        ConsultationSafetyRules.NormalizeFacts(new PharmacyConsultationFactsDto(
            null,
            null,
            null,
            quickSymptoms.ToList(),
            null,
            quickSymptoms.Any(s => s.Contains("fever", StringComparison.OrdinalIgnoreCase)),
            null,
            null,
            [],
            null));

    private sealed class GeminiFactsPayload
    {
        public int? AgeYears { get; set; }
        public int? AgeMonths { get; set; }
        public string? Gender { get; set; }
        public List<string>? Symptoms { get; set; }
        public int? DurationDays { get; set; }
        public bool? HasFever { get; set; }
        public bool? IsPregnant { get; set; }
        public bool? IsBreastfeeding { get; set; }
        public List<string>? RedFlags { get; set; }
        public string? Notes { get; set; }
    }
}

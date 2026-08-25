using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Consultation;
using Npgsql;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal sealed class SymptomTaxonomyRepository
{
    private readonly IDbConnectionFactory _db;

    public SymptomTaxonomyRepository(IDbConnectionFactory db) => _db = db;

    public sealed class SymptomRow
    {
        public string Code { get; set; } = "";
        public string NameVi { get; set; } = "";
        public string CategoryCode { get; set; } = "";
        public string CategoryLabel { get; set; } = "";
        public int CategorySort { get; set; }
        public string ConsultationMode { get; set; } = "capture_only";
        public int SortOrder { get; set; }
    }

    public sealed class KnowledgeRuleRow
    {
        public string RuleCode { get; set; } = "";
        public string? SymptomCode { get; set; }
        public string[] CategoryCodes { get; set; } = [];
        public string[] Keywords { get; set; } = [];
        public string[] ExcludeKeywords { get; set; } = [];
        public string ReasonVi { get; set; } = "";
        public int Priority { get; set; }
    }

    public sealed class SymptomRiskRow
    {
        public string FlagCode { get; set; } = "";
        public string MessageVi { get; set; } = "";
        public string SafetyLevel { get; set; } = "caution";
        public string Severity { get; set; } = "caution";
    }

    public async Task<PharmacyConsultationSymptomCatalogDto?> TryGetCatalogAsync(CancellationToken ct)
    {
        try
        {
            const string sql = """
                SELECT
                    s.code AS Code,
                    s.name_vi AS NameVi,
                    s.category_code AS CategoryCode,
                    c.label_vi AS CategoryLabel,
                    c.sort_order AS CategorySort,
                    s.consultation_mode AS ConsultationMode,
                    s.sort_order AS SortOrder
                FROM pharmacy_symptom s
                INNER JOIN pharmacy_symptom_category c ON c.code = s.category_code
                WHERE s.is_active = TRUE AND c.is_active = TRUE
                ORDER BY c.sort_order, s.sort_order, s.name_vi
                """;

            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            var rows = (await conn.QueryAsync<SymptomRow>(sql)).ToList();
            if (rows.Count == 0)
                return null;

            const string aliasSql = """
                SELECT s.code AS Code, a.alias AS Alias
                FROM pharmacy_symptom_alias a
                INNER JOIN pharmacy_symptom s ON s.id = a.symptom_id
                WHERE s.is_active = TRUE
                ORDER BY s.code, a.alias
                """;

            var aliasRows = (await conn.QueryAsync<(string Code, string Alias)>(aliasSql)).ToList();
            var aliasesByCode = aliasRows
                .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<string>)g.Select(x => x.Alias).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var groups = rows
                .GroupBy(r => r.CategoryCode, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.First().CategorySort)
                .Select(g =>
                {
                    var first = g.First();
                    return new PharmacyConsultationSymptomGroupDto(
                        g.Key,
                        first.CategoryLabel,
                        g.Select(s => new PharmacyConsultationSymptomOptionDto(s.Code, s.NameVi)).ToList());
                })
                .ToList();

            var flat = rows.Select(r => new PharmacyConsultationSymptomOptionDto(r.Code, r.NameVi)).ToList();
            return new PharmacyConsultationSymptomCatalogDto(groups, flat, "taxonomy_v1", aliasesByCode);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetConsultationModesAsync(
        IReadOnlyList<string> symptomCodes,
        CancellationToken ct)
    {
        if (symptomCodes.Count == 0)
            return [];

        const string sql = """
            SELECT consultation_mode
            FROM pharmacy_symptom
            WHERE is_active = TRUE AND code = ANY(@Codes)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var modes = await conn.QueryAsync<string>(sql, new { Codes = symptomCodes.ToArray() });
        return modes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<SymptomRiskRow>> GetSymptomRiskFlagsAsync(
        IReadOnlyList<string> symptomCodes,
        CancellationToken ct)
    {
        if (symptomCodes.Count == 0)
            return [];

        const string sql = """
            SELECT
                rf.code AS FlagCode,
                rf.message_vi AS MessageVi,
                rf.safety_level AS SafetyLevel,
                rf.severity AS Severity
            FROM pharmacy_consultation_risk_flag rf
            WHERE rf.is_active = TRUE
              AND rf.id IN (
                  SELECT DISTINCT sr.risk_flag_id
                  FROM pharmacy_symptom s
                  INNER JOIN pharmacy_symptom_risk_rule sr
                      ON sr.symptom_id = s.id AND sr.is_active = TRUE
                  WHERE s.is_active = TRUE AND s.code = ANY(@Codes)
              )
            ORDER BY rf.sort_order
            """;

        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            var rows = await conn.QueryAsync<SymptomRiskRow>(sql, new { Codes = symptomCodes.ToArray() });
            return rows.ToList();
        }
        catch (PostgresException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<KnowledgeRuleRow>> GetKnowledgeRulesAsync(
        IReadOnlyList<string> symptomCodes,
        CancellationToken ct)
    {
        if (symptomCodes.Count == 0)
            return [];

        const string sql = """
            SELECT
                kr.rule_code AS RuleCode,
                s.code AS SymptomCode,
                kr.category_codes AS CategoryCodes,
                kr.keywords AS Keywords,
                kr.exclude_keywords AS ExcludeKeywords,
                kr.reason_vi AS ReasonVi,
                kr.priority AS Priority
            FROM pharmacy_knowledge_rule kr
            LEFT JOIN pharmacy_symptom s ON s.id = kr.symptom_id
            WHERE kr.is_active = TRUE
              AND (kr.effective_to IS NULL OR kr.effective_to >= CURRENT_DATE)
              AND kr.effective_from <= CURRENT_DATE
              AND (s.code IS NULL OR s.code = ANY(@Codes))
            ORDER BY kr.priority, kr.rule_code
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<KnowledgeRuleRow>(sql, new { Codes = symptomCodes.ToArray() });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> GetOtcAssistSymptomCodesAsync(
        IReadOnlyList<string> symptomCodes,
        CancellationToken ct)
    {
        if (symptomCodes.Count == 0)
            return [];

        const string sql = """
            SELECT code
            FROM pharmacy_symptom
            WHERE is_active = TRUE
              AND consultation_mode = 'otc_assist'
              AND code = ANY(@Codes)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var codes = await conn.QueryAsync<string>(sql, new { Codes = symptomCodes.ToArray() });
        return codes.ToList();
    }

    public sealed class QuestionRow
    {
        public string Code { get; set; } = "";
        public string QuestionVi { get; set; } = "";
        public string AnswerType { get; set; } = "text";
        public bool Required { get; set; }
        public int Priority { get; set; }
    }

    public async Task<IReadOnlyList<QuestionRow>> GetQuestionsForSymptomsAsync(
        IReadOnlyList<string> symptomCodes,
        CancellationToken ct)
    {
        if (symptomCodes.Count == 0)
            return [];

        const string sql = """
            SELECT DISTINCT ON (q.code)
                q.code AS Code,
                q.question_vi AS QuestionVi,
                q.answer_type AS AnswerType,
                sqr.required AS Required,
                sqr.priority AS Priority
            FROM pharmacy_symptom s
            INNER JOIN pharmacy_symptom_question_rule sqr
                ON sqr.symptom_id = s.id AND sqr.is_active = TRUE
            INNER JOIN pharmacy_consultation_question q
                ON q.id = sqr.question_id AND q.is_active = TRUE
            WHERE s.is_active = TRUE AND s.code = ANY(@Codes)
            ORDER BY q.code, sqr.priority
            """;

        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            var rows = await conn.QueryAsync<QuestionRow>(sql, new { Codes = symptomCodes.ToArray() });
            return rows.OrderBy(r => r.Priority).ToList();
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable)
        {
            return [];
        }
    }
}

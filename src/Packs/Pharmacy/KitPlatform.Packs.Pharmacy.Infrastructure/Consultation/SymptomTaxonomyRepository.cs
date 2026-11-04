using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Knowledge;
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
        public bool ProductMatchingEnabled { get; set; } = true;
        public string[] ExtraExcludeKeywords { get; set; } = [];
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
            WHERE rf.publication_status = 'PUBLISHED'
              AND rf.id IN (
                  SELECT DISTINCT sr.risk_flag_id
                  FROM pharmacy_symptom s
                  INNER JOIN pharmacy_symptom_risk_rule sr
                      ON sr.symptom_id = s.id
                  WHERE s.is_active = TRUE
                    AND s.code = ANY(@Codes)
                    AND sr.risk_flag_id IN (
                        SELECT id
                        FROM pharmacy_consultation_risk_flag
                        WHERE publication_status = 'PUBLISHED'
                    )
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
        Guid tenantId,
        IReadOnlyList<string> symptomCodes,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty || symptomCodes.Count == 0)
            return [];

        var sql = """
            SELECT
                v.rule_code AS RuleCode,
                s.code AS SymptomCode,
                v.category_codes AS CategoryCodes,
                v.keywords AS Keywords,
                v.exclude_keywords AS ExcludeKeywords,
                v.reason_vi AS ReasonVi,
                GREATEST(
                    v.priority,
                    CASE
                        WHEN p.rule_version = a.rule_version THEN COALESCE(p.priority_override, v.priority)
                        ELSE v.priority
                    END
                ) AS Priority,
                CASE
                    WHEN p.rule_version = a.rule_version THEN COALESCE(p.product_matching_enabled, TRUE)
                    ELSE TRUE
                END AS ProductMatchingEnabled,
                CASE
                    WHEN p.rule_version = a.rule_version THEN COALESCE(p.extra_exclude_keywords, '{}')
                    ELSE '{}'::text[]
                END AS ExtraExcludeKeywords
            FROM pharmacy_knowledge_adoption a
            INNER JOIN pharmacy_knowledge_rule_version v
                ON v.rule_code = a.rule_code AND v.version = a.rule_version
            LEFT JOIN pharmacy_symptom s ON s.id = v.symptom_id
            LEFT JOIN pharmacy_tenant_knowledge_policy p ON p.adoption_id = a.id
            WHERE 
            """ + PharmacyKnowledgeCopilotQuery.TenantPublishedPin + """
              AND 
            """ + PharmacyKnowledgeCopilotQuery.ExcludeUnverifiedCatchAll + """
              AND (v.effective_to IS NULL OR v.effective_to >= CURRENT_DATE)
              AND v.effective_from <= CURRENT_DATE
              AND (s.code IS NULL OR s.code = ANY(@Codes))
            ORDER BY 7, v.rule_code
            """;

        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            var rows = await conn.QueryAsync<KnowledgeRuleRow>(sql, new
            {
                TenantId = tenantId,
                Codes = symptomCodes.ToArray(),
            });
            var pinned = rows.ToList();
            var covered = pinned
                .Select(row => row.SymptomCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = symptomCodes
                .Where(code => !string.IsNullOrWhiteSpace(code) && !covered.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missing.Length == 0)
                return pinned;

            var baseline = await conn.QueryAsync<KnowledgeRuleRow>(
                """
                SELECT
                    r.rule_code AS RuleCode,
                    s.code AS SymptomCode,
                    r.category_codes AS CategoryCodes,
                    r.keywords AS Keywords,
                    r.exclude_keywords AS ExcludeKeywords,
                    r.reason_vi AS ReasonVi,
                    r.priority AS Priority,
                    TRUE AS ProductMatchingEnabled,
                    '{}'::text[] AS ExtraExcludeKeywords
                FROM pharmacy_knowledge_rule r
                INNER JOIN pharmacy_symptom s ON s.id = r.symptom_id
                WHERE r.is_active = TRUE
                  AND r.approval_status = 'APPROVED'
                  AND r.rule_code <> 'KR_OTHER'
                  AND cardinality(r.keywords) > 0
                  AND (r.effective_to IS NULL OR r.effective_to >= CURRENT_DATE)
                  AND r.effective_from <= CURRENT_DATE
                  AND s.code = ANY(@Missing)
                ORDER BY r.priority, r.rule_code
                """,
                new { Missing = missing });
            pinned.AddRange(baseline);
            return pinned;
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedColumn or PostgresErrorCodes.UndefinedTable)
        {
            return [];
        }
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

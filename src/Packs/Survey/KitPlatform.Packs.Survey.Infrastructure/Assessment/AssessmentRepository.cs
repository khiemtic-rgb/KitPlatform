using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Event;
using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal sealed class AssessmentRepository
{
    private readonly IDbConnectionFactory _db;

    public AssessmentRepository(IDbConnectionFactory db) => _db = db;

    public async Task<AssessmentTemplateRow?> GetTemplateHeaderAsync(
        string code,
        string? version,
        CancellationToken cancellationToken)
    {
        const string sqlLatest = """
            SELECT id AS Id, code AS Code, name AS Name, version AS Version
            FROM assessment_template
            WHERE code = @Code AND status = 'active'
            ORDER BY version DESC
            LIMIT 1
            """;

        const string sqlVersion = """
            SELECT id AS Id, code AS Code, name AS Name, version AS Version
            FROM assessment_template
            WHERE code = @Code AND version = @Version AND status = 'active'
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(version)
            ? await conn.QuerySingleOrDefaultAsync<AssessmentTemplateRow>(sqlLatest, new { Code = code })
            : await conn.QuerySingleOrDefaultAsync<AssessmentTemplateRow>(sqlVersion, new { Code = code, Version = version });
    }

    public async Task<IReadOnlyList<AssessmentTemplateTreeRow>> GetTemplateTreeAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.code AS CategoryCode,
                c.name AS CategoryName,
                c.sort_order AS CategorySort,
                d.code AS DimensionCode,
                d.name AS DimensionName,
                d.sort_order AS DimensionSort,
                q.id AS QuestionId,
                q.code AS QuestionCode,
                q.title AS QuestionTitle,
                q.help_text AS QuestionHelpText,
                q.question_type AS QuestionType,
                q.scorable AS Scorable,
                q.required AS Required,
                q.sort_order AS QuestionSort,
                q.metadata::text AS QuestionMetadataJson,
                o.id AS OptionId,
                o.code AS OptionCode,
                o.label AS OptionLabel,
                o.score AS OptionScore,
                o.sort_order AS OptionSort
            FROM assessment_category c
            JOIN assessment_dimension d ON d.category_id = c.id
            JOIN assessment_question q ON q.dimension_id = d.id
            LEFT JOIN assessment_option o ON o.question_id = q.id
            WHERE c.template_id = @TemplateId
            ORDER BY c.sort_order, d.sort_order, q.sort_order, o.sort_order NULLS LAST
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentTemplateTreeRow>(sql, new { TemplateId = templateId });
        return rows.ToList();
    }

    public async Task<AssessmentSubmissionRow?> GetSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s.id AS Id,
                s.template_id AS TemplateId,
                s.template_version AS TemplateVersion,
                s.status AS Status,
                s.session_token AS SessionToken,
                s.overall_score AS OverallScore,
                s.overall_pct AS OverallPct,
                s.started_at AS StartedAt,
                s.completed_at AS CompletedAt,
                s.lead_captured_at AS LeadCapturedAt,
                s.report_ready_at AS ReportReadyAt,
                s.respondent_name AS RespondentName,
                s.respondent_phone AS RespondentPhone,
                s.respondent_email AS RespondentEmail,
                s.respondent_org_name AS RespondentOrgName,
                s.respondent_note AS RespondentNote,
                s.consent_marketing AS ConsentMarketing,
                t.code AS TemplateCode
            FROM assessment_submission s
            JOIN assessment_template t ON t.id = s.template_id
            WHERE s.id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AssessmentSubmissionRow>(sql, new { Id = submissionId });
    }

    public async Task<Guid> InsertSubmissionAsync(
        Guid templateId,
        string templateVersion,
        string sessionToken,
        string source,
        string? ipAddress,
        string? userAgent,
        string? locale,
        Guid? partnerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO assessment_submission (
                template_id, template_version, tenant_id, status, session_token, source,
                ip_address, user_agent, metadata, partner_id
            )
            VALUES (
                @TemplateId, @TemplateVersion, NULL, 'draft', @SessionToken, @Source,
                @IpAddress::inet, @UserAgent, @Metadata::jsonb, @PartnerId
            )
            RETURNING id
            """;

        var metadata = string.IsNullOrWhiteSpace(locale)
            ? "{}"
            : JsonSerializer.Serialize(new { locale });

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var submissionId = await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            SessionToken = sessionToken,
            Source = source,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Metadata = metadata,
            PartnerId = partnerId,
        }, tx);

        await SurveySubmissionWriter.InsertDraftAsync(
            conn,
            tx,
            submissionId,
            templateId,
            source,
            cancellationToken: cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return submissionId;
    }

    public async Task<IReadOnlyList<AssessmentResponseRow>> GetResponsesAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.question_id AS QuestionId,
                q.code AS QuestionCode,
                r.option_id AS OptionId,
                o.code AS OptionCode,
                r.text_value AS TextValue
            FROM assessment_response r
            JOIN assessment_question q ON q.id = r.question_id
            LEFT JOIN assessment_option o ON o.id = r.option_id
            WHERE r.submission_id = @SubmissionId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentResponseRow>(sql, new { SubmissionId = submissionId });
        return rows.ToList();
    }

    public async Task UpsertResponsesAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid submissionId,
        IReadOnlyList<AssessmentResponseUpsert> responses)
    {
        const string sql = """
            INSERT INTO assessment_response (submission_id, question_id, option_id, text_value)
            VALUES (@SubmissionId, @QuestionId, @OptionId, @TextValue)
            ON CONFLICT (submission_id, question_id)
            DO UPDATE SET
                option_id = EXCLUDED.option_id,
                text_value = EXCLUDED.text_value,
                updated_at = NOW()
            """;

        foreach (var item in responses)
        {
            await conn.ExecuteAsync(sql, new
            {
                SubmissionId = submissionId,
                item.QuestionId,
                item.OptionId,
                item.TextValue,
            }, tx);
        }
    }

    public async Task<AssessmentRequiredCountsRow> GetRequiredCountsAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(*) FILTER (WHERE q.required) AS TotalRequired,
                COUNT(*) FILTER (
                    WHERE q.required
                      AND EXISTS (
                          SELECT 1 FROM assessment_response r
                          WHERE r.submission_id = @SubmissionId
                            AND r.question_id = q.id
                            AND (r.option_id IS NOT NULL OR NULLIF(TRIM(COALESCE(r.text_value, '')), '') IS NOT NULL)
                      )
                ) AS AnsweredRequired
            FROM assessment_submission s
            JOIN assessment_template t ON t.id = s.template_id
            JOIN assessment_category c ON c.template_id = t.id
            JOIN assessment_dimension d ON d.category_id = c.id
            JOIN assessment_question q ON q.dimension_id = d.id
            WHERE s.id = @SubmissionId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<AssessmentRequiredCountsRow>(sql, new { SubmissionId = submissionId });
    }

    public async Task<IReadOnlyList<AssessmentScoringRow>> GetScoringDataAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id AS CategoryId,
                c.code AS CategoryCode,
                c.name AS CategoryName,
                c.weight AS CategoryWeight,
                d.id AS DimensionId,
                d.code AS DimensionCode,
                d.name AS DimensionName,
                d.weight AS DimensionWeight,
                q.id AS QuestionId,
                q.code AS QuestionCode,
                q.title AS QuestionTitle,
                q.scorable AS Scorable,
                q.weight AS QuestionWeight,
                q.required AS Required,
                o.id AS OptionId,
                o.code AS OptionCode,
                o.label AS OptionLabel,
                o.score AS OptionScore,
                o.metadata::text AS OptionMetadataJson,
                r.option_id AS ResponseOptionId,
                r.text_value AS ResponseTextValue
            FROM assessment_submission s
            JOIN assessment_template t ON t.id = s.template_id
            JOIN assessment_category c ON c.template_id = t.id
            JOIN assessment_dimension d ON d.category_id = c.id
            JOIN assessment_question q ON q.dimension_id = d.id
            LEFT JOIN assessment_response r ON r.submission_id = s.id AND r.question_id = q.id
            LEFT JOIN assessment_option o ON o.id = r.option_id
            WHERE s.id = @SubmissionId
            ORDER BY c.sort_order, d.sort_order, q.sort_order
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentScoringRow>(sql, new { SubmissionId = submissionId });
        return rows.ToList();
    }

    public async Task SaveScoresAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid submissionId,
        decimal overallScore,
        decimal overallPct,
        IReadOnlyList<AssessmentDimensionScoreWrite> dimensionScores,
        IReadOnlyList<AssessmentCategoryScoreWrite> categoryScores)
    {
        const string updateSubmission = """
            UPDATE assessment_submission
            SET overall_score = @OverallScore,
                overall_pct = @OverallPct,
                status = 'completed',
                completed_at = NOW()
            WHERE id = @SubmissionId
            """;

        await conn.ExecuteAsync(updateSubmission, new
        {
            SubmissionId = submissionId,
            OverallScore = overallScore,
            OverallPct = overallPct,
        }, tx);

        const string dimSql = """
            INSERT INTO assessment_dimension_score (submission_id, dimension_id, score, score_pct)
            VALUES (@SubmissionId, @DimensionId, @Score, @ScorePct)
            ON CONFLICT (submission_id, dimension_id)
            DO UPDATE SET score = EXCLUDED.score, score_pct = EXCLUDED.score_pct
            """;

        foreach (var item in dimensionScores)
        {
            await conn.ExecuteAsync(dimSql, new
            {
                SubmissionId = submissionId,
                item.DimensionId,
                item.Score,
                item.ScorePct,
            }, tx);
        }

        const string catSql = """
            INSERT INTO assessment_category_score (submission_id, category_id, score, score_pct)
            VALUES (@SubmissionId, @CategoryId, @Score, @ScorePct)
            ON CONFLICT (submission_id, category_id)
            DO UPDATE SET score = EXCLUDED.score, score_pct = EXCLUDED.score_pct
            """;

        foreach (var item in categoryScores)
        {
            await conn.ExecuteAsync(catSql, new
            {
                SubmissionId = submissionId,
                item.CategoryId,
                item.Score,
                item.ScorePct,
            }, tx);
        }

        await SurveySubmissionWriter.SyncCompletedAsync(
            conn, tx, submissionId, overallScore, overallPct, CancellationToken.None);
    }

    public async Task<IReadOnlyList<AssessmentRuleRow>> GetActiveRulesAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                code AS Code,
                expression AS Expression,
                action_type AS ActionType,
                action_payload::text AS ActionPayloadJson,
                priority AS Priority
            FROM assessment_rule
            WHERE template_id = @TemplateId AND is_active = TRUE
            ORDER BY priority DESC, code
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentRuleRow>(sql, new { TemplateId = templateId });
        return rows.ToList();
    }

    public async Task DeleteInsightsAndRecommendationsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid submissionId)
    {
        await conn.ExecuteAsync(
            "DELETE FROM assessment_insight WHERE submission_id = @SubmissionId",
            new { SubmissionId = submissionId }, tx);
        await conn.ExecuteAsync(
            "DELETE FROM assessment_recommendation WHERE submission_id = @SubmissionId",
            new { SubmissionId = submissionId }, tx);
    }

    public async Task InsertInsightAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid submissionId,
        Guid? ruleId,
        string title,
        string body,
        string severity,
        string? scopeType,
        string? scopeCode,
        int sortOrder)
    {
        const string sql = """
            INSERT INTO assessment_insight (
                submission_id, rule_id, scope_type, scope_code, title, body, severity, sort_order
            )
            VALUES (
                @SubmissionId, @RuleId, @ScopeType, @ScopeCode, @Title, @Body, @Severity, @SortOrder
            )
            """;

        await conn.ExecuteAsync(sql, new
        {
            SubmissionId = submissionId,
            RuleId = ruleId,
            ScopeType = scopeType,
            ScopeCode = scopeCode,
            Title = title,
            Body = body,
            Severity = severity,
            SortOrder = sortOrder,
        }, tx);
    }

    public async Task InsertRecommendationAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid submissionId,
        Guid? ruleId,
        string title,
        string body,
        int priority,
        string? productArea,
        string? estimateHint,
        int sortOrder)
    {
        const string sql = """
            INSERT INTO assessment_recommendation (
                submission_id, rule_id, title, body, priority, product_area, estimate_hint, sort_order
            )
            VALUES (
                @SubmissionId, @RuleId, @Title, @Body, @Priority, @ProductArea, @EstimateHint, @SortOrder
            )
            """;

        await conn.ExecuteAsync(sql, new
        {
            SubmissionId = submissionId,
            RuleId = ruleId,
            Title = title,
            Body = body,
            Priority = priority,
            ProductArea = productArea,
            EstimateHint = estimateHint,
            SortOrder = sortOrder,
        }, tx);
    }

    public async Task CaptureLeadAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid submissionId,
        string name,
        string phone,
        string email,
        string orgName,
        string? note,
        bool consentMarketing)
    {
        const string sql = """
            UPDATE assessment_submission
            SET status = 'lead_captured',
                lead_captured_at = NOW(),
                report_ready_at = NOW(),
                respondent_name = @Name,
                respondent_phone = @Phone,
                respondent_email = @Email,
                respondent_org_name = @OrgName,
                respondent_note = @Note,
                consent_marketing = @ConsentMarketing
            WHERE id = @SubmissionId
            """;

        await conn.ExecuteAsync(sql, new
        {
            SubmissionId = submissionId,
            Name = name,
            Phone = phone,
            Email = email,
            OrgName = orgName,
            Note = note,
            ConsentMarketing = consentMarketing,
        }, tx);

        await SurveySubmissionWriter.SyncLeadCapturedAsync(
            conn, tx, submissionId, name, phone, email, orgName, CancellationToken.None);
    }

    public async Task<IReadOnlyList<AssessmentCategoryScoreReadRow>> GetCategoryScoresAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.code AS Code, c.name AS Name, cs.score AS Score, cs.score_pct AS ScorePct
            FROM assessment_category_score cs
            JOIN assessment_category c ON c.id = cs.category_id
            WHERE cs.submission_id = @SubmissionId
            ORDER BY c.sort_order
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentCategoryScoreReadRow>(sql, new { SubmissionId = submissionId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AssessmentDimensionScoreReadRow>> GetDimensionScoresAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.code AS Code,
                d.name AS Name,
                c.code AS CategoryCode,
                ds.score AS Score,
                ds.score_pct AS ScorePct
            FROM assessment_dimension_score ds
            JOIN assessment_dimension d ON d.id = ds.dimension_id
            JOIN assessment_category c ON c.id = d.category_id
            WHERE ds.submission_id = @SubmissionId
            ORDER BY c.sort_order, d.sort_order
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentDimensionScoreReadRow>(sql, new { SubmissionId = submissionId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AssessmentInsightReadRow>> GetInsightsAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT title AS Title, body AS Body, severity AS Severity, sort_order AS SortOrder
            FROM assessment_insight
            WHERE submission_id = @SubmissionId
            ORDER BY sort_order, created_at
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentInsightReadRow>(sql, new { SubmissionId = submissionId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AssessmentRecommendationReadRow>> GetRecommendationsAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT title AS Title, body AS Body, priority AS Priority,
                   product_area AS ProductArea, estimate_hint AS EstimateHint, sort_order AS SortOrder
            FROM assessment_recommendation
            WHERE submission_id = @SubmissionId
            ORDER BY priority DESC, sort_order, created_at
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentRecommendationReadRow>(sql, new { SubmissionId = submissionId });
        return rows.ToList();
    }

    public async Task UpsertReportAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid submissionId,
        string storageKey,
        string fileName,
        long byteSize,
        string format)
    {
        const string sql = """
            INSERT INTO assessment_report (
                submission_id, format, storage_key, file_name, byte_size, expires_at
            )
            VALUES (
                @SubmissionId, @Format, @StorageKey, @FileName, @ByteSize, NOW() + INTERVAL '24 hours'
            )
            """;

        await conn.ExecuteAsync(sql, new
        {
            SubmissionId = submissionId,
            Format = format,
            StorageKey = storageKey,
            FileName = fileName,
            ByteSize = byteSize,
        }, tx);
    }

    public async Task<AssessmentReportRow?> GetLatestReportAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT storage_key AS StorageKey, file_name AS FileName, format AS Format
            FROM assessment_report
            WHERE submission_id = @SubmissionId
            ORDER BY generated_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AssessmentReportRow>(sql, new { SubmissionId = submissionId });
    }

    public async Task<Guid?> GetTenantIdByCodeAsync(
        string tenantCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id FROM tenants
            WHERE tenant_code = @TenantCode AND deleted_at IS NULL AND status = 1
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TenantCode = tenantCode });
    }

    public Task WritePlatformEventAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        string eventType,
        Guid aggregateId,
        object data,
        CancellationToken cancellationToken = default) =>
        PlatformEventDualWrite.WriteAsync(
            conn,
            tx,
            tenantId,
            eventType,
            aggregateType: PlatformEventAggregateTypes.AssessmentSubmission,
            aggregateId,
            data,
            SurveyPackDefinition.EventSource,
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<AssessmentQuestionValidationRow>> GetQuestionsForValidationAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                q.id AS QuestionId,
                q.required AS Required,
                q.question_type AS QuestionType,
                o.id AS OptionId,
                o.question_id AS OptionQuestionId
            FROM assessment_submission s
            JOIN assessment_template t ON t.id = s.template_id
            JOIN assessment_category c ON c.template_id = t.id
            JOIN assessment_dimension d ON d.category_id = c.id
            JOIN assessment_question q ON q.dimension_id = d.id
            LEFT JOIN assessment_option o ON o.question_id = q.id
            WHERE s.id = @SubmissionId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AssessmentQuestionValidationRow>(sql, new { SubmissionId = submissionId });
        return rows.ToList();
    }

    public static string GenerateSessionToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<IReadOnlyList<KapTemplateListRow>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, code AS Code, name AS Name, version AS Version,
                   status AS Status, description AS Description, updated_at AS UpdatedAt
            FROM assessment_template
            ORDER BY code, version DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<KapTemplateListRow>(sql)).ToList();
    }

    public async Task<KapTemplateHeaderRow?> GetTemplateHeaderByIdAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, code AS Code, name AS Name, version AS Version,
                   status AS Status, description AS Description,
                   verticals AS Verticals
            FROM assessment_template
            WHERE id = @Id
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<KapTemplateHeaderRow>(sql, new { Id = templateId });
    }

    public async Task<bool> UpdateTemplateHeaderAsync(
        Guid templateId,
        string name,
        string? description,
        string status,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE assessment_template
            SET name = @Name, description = @Description, status = @Status, updated_at = NOW()
            WHERE id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = templateId, Name = name, Description = description, Status = status }) > 0;
    }

    public async Task<IReadOnlyList<KapRuleListRow>> ListRulesForTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, template_id AS TemplateId, code AS Code, name AS Name,
                   expression AS Expression, action_type AS ActionType,
                   action_payload::text AS ActionPayloadJson, priority AS Priority, is_active AS IsActive
            FROM assessment_rule
            WHERE template_id = @TemplateId
            ORDER BY priority DESC, code
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<KapRuleListRow>(sql, new { TemplateId = templateId })).ToList();
    }

    public async Task<Guid> InsertRuleAsync(
        Guid templateId,
        string code,
        string name,
        string expression,
        string actionType,
        string actionPayloadJson,
        int priority,
        bool isActive,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO assessment_rule (
                template_id, code, name, expression, action_type, action_payload, priority, is_active
            )
            VALUES (
                @TemplateId, @Code, @Name, @Expression, @ActionType, @ActionPayloadJson::jsonb, @Priority, @IsActive
            )
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TemplateId = templateId,
            Code = code,
            Name = name,
            Expression = expression,
            ActionType = actionType,
            ActionPayloadJson = actionPayloadJson,
            Priority = priority,
            IsActive = isActive,
        });
    }

    public async Task<KapRuleListRow?> GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, template_id AS TemplateId, code AS Code, name AS Name,
                   expression AS Expression, action_type AS ActionType,
                   action_payload::text AS ActionPayloadJson, priority AS Priority, is_active AS IsActive
            FROM assessment_rule
            WHERE id = @Id
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<KapRuleListRow>(sql, new { Id = ruleId });
    }

    public async Task<bool> UpdateRuleAsync(
        Guid ruleId,
        string name,
        string expression,
        string actionType,
        string actionPayloadJson,
        int priority,
        bool isActive,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE assessment_rule
            SET name = @Name, expression = @Expression, action_type = @ActionType,
                action_payload = @ActionPayloadJson::jsonb, priority = @Priority, is_active = @IsActive
            WHERE id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = ruleId,
            Name = name,
            Expression = expression,
            ActionType = actionType,
            ActionPayloadJson = actionPayloadJson,
            Priority = priority,
            IsActive = isActive,
        }) > 0;
    }

    public async Task<bool> DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM assessment_rule WHERE id = @Id";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = ruleId }) > 0;
    }

    public async Task<int> CountLeadCapturesByPhoneTodayAsync(
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int
            FROM assessment_submission
            WHERE respondent_phone = @Phone
              AND lead_captured_at >= date_trunc('day', NOW() AT TIME ZONE 'UTC')
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { Phone = phone });
    }

    public async Task<AssessmentSubmissionRow?> GetSubmissionByIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken) =>
        await GetSubmissionAsync(submissionId, cancellationToken);

    public async Task<Guid?> GetCrmWorkspaceIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id
            FROM kit_workspace.workspace_workspace
            WHERE tenant_id = @TenantId
              AND package_code = 'clinic_crm'
              AND deleted_at IS NULL
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TenantId = tenantId });
    }

    public async Task<Guid?> InsertCrmLeadFromAssessmentAsync(
        Guid tenantId,
        Guid? workspaceId,
        string leadCode,
        string fullName,
        string? phone,
        string? email,
        string notes,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_crm.crm_lead (
                tenant_id, workspace_id, lead_code, full_name, phone, email, source, notes, metadata
            )
            VALUES (
                @TenantId, @WorkspaceId, @LeadCode, @FullName, @Phone, @Email, 'kap_assessment', @Notes, @Metadata::jsonb
            )
            ON CONFLICT (tenant_id, lead_code) DO NOTHING
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            LeadCode = leadCode,
            FullName = fullName,
            Phone = phone,
            Email = email,
            Notes = notes,
            Metadata = metadataJson,
        });
    }

    public async Task<(IReadOnlyList<AssessmentSubmissionListRow> Items, int Total)> ListSubmissionsAsync(
        int page,
        int pageSize,
        string? status,
        bool? hasLead,
        Guid? partnerId,
        string? leadPipelineStatus,
        bool archived,
        CancellationToken cancellationToken)
    {
        var offset = Math.Max(0, (Math.Max(1, page) - 1) * Math.Max(1, pageSize));
        var limit = Math.Clamp(pageSize, 1, 100);

        var where = new List<string> { "1=1" };
        if (!string.IsNullOrWhiteSpace(status))
            where.Add("s.status = @Status");
        if (hasLead == true)
            where.Add("s.respondent_phone IS NOT NULL");
        else if (hasLead == false)
            where.Add("s.respondent_phone IS NULL");
        if (partnerId.HasValue)
            where.Add("s.partner_id = @PartnerId");
        if (!string.IsNullOrWhiteSpace(leadPipelineStatus))
            where.Add("s.lead_pipeline_status = @LeadPipelineStatus");
        where.Add(archived ? "s.archived_at IS NOT NULL" : "s.archived_at IS NULL");

        var whereSql = string.Join(" AND ", where);

        var countSql = $"""
            SELECT COUNT(*)::int
            FROM assessment_submission s
            WHERE {whereSql}
            """;

        var listSql = $"""
            SELECT
                s.id AS Id,
                s.status AS Status,
                t.code AS TemplateCode,
                s.template_version AS TemplateVersion,
                s.started_at AS StartedAt,
                s.completed_at AS CompletedAt,
                s.lead_captured_at AS LeadCapturedAt,
                s.overall_score AS OverallScore,
                s.overall_pct AS OverallPct,
                s.respondent_name AS RespondentName,
                s.respondent_phone AS RespondentPhone,
                s.respondent_email AS RespondentEmail,
                s.respondent_org_name AS RespondentOrgName,
                s.partner_id AS PartnerId,
                p.code AS PartnerCode,
                p.name AS PartnerName,
                s.lead_pipeline_status AS LeadPipelineStatus,
                s.commission_status AS CommissionStatus,
                s.archived_at AS ArchivedAt,
                (SELECT COUNT(*)::int FROM assessment_response r WHERE r.submission_id = s.id) AS ResponseCount
            FROM assessment_submission s
            JOIN assessment_template t ON t.id = s.template_id
            LEFT JOIN assessment_partner p ON p.id = s.partner_id
            WHERE {whereSql}
            ORDER BY COALESCE(s.lead_captured_at, s.completed_at, s.started_at) DESC
            LIMIT @Limit OFFSET @Offset
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var args = new
        {
            Status = status,
            PartnerId = partnerId,
            LeadPipelineStatus = leadPipelineStatus,
            Limit = limit,
            Offset = offset,
        };
        var total = await conn.ExecuteScalarAsync<int>(countSql, args);
        var items = (await conn.QueryAsync<AssessmentSubmissionListRow>(listSql, args)).ToList();
        return (items, total);
    }

    public async Task<(string? CommissionStatus, DateTimeOffset? ArchivedAt)?> GetArchiveStateAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT commission_status AS CommissionStatus, archived_at AS ArchivedAt
            FROM assessment_submission
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<ArchiveStateRow>(sql, new { Id = submissionId });
        if (row is null)
            return null;
        return (row.CommissionStatus, row.ArchivedAt);
    }

    public async Task<bool> SetArchivedAtAsync(
        Guid submissionId,
        DateTimeOffset? archivedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE assessment_submission
            SET archived_at = @ArchivedAt
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new { Id = submissionId, ArchivedAt = archivedAt });
        return n > 0;
    }

    public async Task<bool> UpdateLeadPipelineAsync(
        Guid submissionId,
        string leadPipelineStatus,
        string? commissionStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE assessment_submission SET
                lead_pipeline_status = @LeadPipelineStatus,
                commission_status = COALESCE(@CommissionStatus, commission_status)
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new
        {
            Id = submissionId,
            LeadPipelineStatus = leadPipelineStatus,
            CommissionStatus = commissionStatus,
        });
        return n > 0;
    }
}

internal sealed record AssessmentResponseUpsert(Guid QuestionId, Guid? OptionId, string? TextValue);

internal sealed record AssessmentDimensionScoreWrite(Guid DimensionId, decimal Score, decimal ScorePct);

internal sealed record AssessmentCategoryScoreWrite(Guid CategoryId, decimal Score, decimal ScorePct);

internal sealed class AssessmentTemplateRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
}

internal sealed class AssessmentTemplateTreeRow
{
    public string CategoryCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int CategorySort { get; set; }
    public string DimensionCode { get; set; } = "";
    public string DimensionName { get; set; } = "";
    public int DimensionSort { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionCode { get; set; } = "";
    public string QuestionTitle { get; set; } = "";
    public string? QuestionHelpText { get; set; }
    public string QuestionType { get; set; } = "";
    public bool Scorable { get; set; }
    public bool Required { get; set; }
    public int QuestionSort { get; set; }
    public string? QuestionMetadataJson { get; set; }
    public Guid? OptionId { get; set; }
    public string? OptionCode { get; set; }
    public string? OptionLabel { get; set; }
    public short? OptionScore { get; set; }
    public int? OptionSort { get; set; }
}

internal sealed class AssessmentSubmissionRow
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateVersion { get; set; } = "";
    public string Status { get; set; } = "";
    public string SessionToken { get; set; } = "";
    public decimal? OverallScore { get; set; }
    public decimal? OverallPct { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? LeadCapturedAt { get; set; }
    public DateTimeOffset? ReportReadyAt { get; set; }
    public string? RespondentName { get; set; }
    public string? RespondentPhone { get; set; }
    public string? RespondentEmail { get; set; }
    public string? RespondentOrgName { get; set; }
    public string? RespondentNote { get; set; }
    public bool ConsentMarketing { get; set; }
    public string TemplateCode { get; set; } = "";
}

internal sealed class AssessmentResponseRow
{
    public Guid QuestionId { get; set; }
    public string QuestionCode { get; set; } = "";
    public Guid? OptionId { get; set; }
    public string? OptionCode { get; set; }
    public string? TextValue { get; set; }
}

internal sealed class AssessmentRequiredCountsRow
{
    public int TotalRequired { get; set; }
    public int AnsweredRequired { get; set; }
}

internal sealed class AssessmentScoringRow
{
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public decimal CategoryWeight { get; set; }
    public Guid DimensionId { get; set; }
    public string DimensionCode { get; set; } = "";
    public string DimensionName { get; set; } = "";
    public decimal DimensionWeight { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionCode { get; set; } = "";
    public string QuestionTitle { get; set; } = "";
    public bool Scorable { get; set; }
    public decimal QuestionWeight { get; set; }
    public bool Required { get; set; }
    public Guid? OptionId { get; set; }
    public string? OptionCode { get; set; }
    public string? OptionLabel { get; set; }
    public short? OptionScore { get; set; }
    public string? OptionMetadataJson { get; set; }
    public Guid? ResponseOptionId { get; set; }
    public string? ResponseTextValue { get; set; }
}

internal sealed class AssessmentRuleRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Expression { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string ActionPayloadJson { get; set; } = "{}";
    public int Priority { get; set; }
}

internal sealed class AssessmentCategoryScoreReadRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Score { get; set; }
    public decimal ScorePct { get; set; }
}

internal sealed class AssessmentDimensionScoreReadRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public decimal Score { get; set; }
    public decimal ScorePct { get; set; }
}

internal sealed class AssessmentInsightReadRow
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Severity { get; set; } = "";
    public int SortOrder { get; set; }
}

internal sealed class AssessmentRecommendationReadRow
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int Priority { get; set; }
    public string? ProductArea { get; set; }
    public string? EstimateHint { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class AssessmentReportRow
{
    public string StorageKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Format { get; set; } = "";
}

internal sealed class AssessmentQuestionValidationRow
{
    public Guid QuestionId { get; set; }
    public bool Required { get; set; }
    public string QuestionType { get; set; } = "";
    public Guid? OptionId { get; set; }
    public Guid? OptionQuestionId { get; set; }
}

internal sealed class AssessmentSubmissionListRow
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "";
    public string TemplateCode { get; set; } = "";
    public string TemplateVersion { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? LeadCapturedAt { get; set; }
    public decimal? OverallScore { get; set; }
    public decimal? OverallPct { get; set; }
    public string? RespondentName { get; set; }
    public string? RespondentPhone { get; set; }
    public string? RespondentEmail { get; set; }
    public string? RespondentOrgName { get; set; }
    public int ResponseCount { get; set; }
    public Guid? PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public string LeadPipelineStatus { get; set; } = "new";
    public string CommissionStatus { get; set; } = "none";
    public DateTimeOffset? ArchivedAt { get; set; }
}

internal sealed class ArchiveStateRow
{
    public string CommissionStatus { get; set; } = "none";
    public DateTimeOffset? ArchivedAt { get; set; }
}

internal sealed class KapTemplateListRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class KapTemplateHeaderRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Description { get; set; }
    public string[] Verticals { get; set; } = [];
}

internal sealed class KapRuleListRow
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Expression { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string ActionPayloadJson { get; set; } = "{}";
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

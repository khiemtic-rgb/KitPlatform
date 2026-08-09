using System.Data;
using System.Text.Json;
using Dapper;

namespace KitPlatform.Packs.Survey.Infrastructure;

/// <summary>Phase D dual-write — <c>pack_survey.survey_submission</c> on assessment lifecycle.</summary>
internal static class SurveySubmissionWriter
{
    public static async Task InsertDraftAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid assessmentSubmissionId,
        Guid templateId,
        string source,
        Guid? tenantId = null,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO pack_survey.survey_submission (
                tenant_id, workspace_id, assessment_submission_id, template_id,
                submission_status, session_source, metadata
            )
            VALUES (
                @TenantId, @WorkspaceId, @AssessmentSubmissionId, @TemplateId,
                'draft', @Source, @Metadata::jsonb
            )
            ON CONFLICT (assessment_submission_id) DO NOTHING
            """;

        var metadata = JsonSerializer.Serialize(new { source = "survey_write_cutover" });

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            AssessmentSubmissionId = assessmentSubmissionId,
            TemplateId = templateId,
            Source = source,
            Metadata = metadata,
        }, tx, cancellationToken: cancellationToken));
    }

    public static async Task SyncCompletedAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid assessmentSubmissionId,
        decimal overallScore,
        decimal overallPct,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE pack_survey.survey_submission
            SET submission_status = 'completed',
                overall_score = @OverallScore,
                overall_pct = @OverallPct,
                completed_at = NOW(),
                updated_at = NOW()
            WHERE assessment_submission_id = @AssessmentSubmissionId
            """;

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AssessmentSubmissionId = assessmentSubmissionId,
            OverallScore = overallScore,
            OverallPct = overallPct,
        }, tx, cancellationToken: cancellationToken));
    }

    public static async Task SyncLeadCapturedAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid assessmentSubmissionId,
        string name,
        string phone,
        string email,
        string? orgName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE pack_survey.survey_submission
            SET submission_status = 'lead_captured',
                lead_name = @Name,
                lead_phone = @Phone,
                lead_email = @Email,
                lead_company = @OrgName,
                lead_captured_at = NOW(),
                updated_at = NOW()
            WHERE assessment_submission_id = @AssessmentSubmissionId
            """;

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AssessmentSubmissionId = assessmentSubmissionId,
            Name = name,
            Phone = phone,
            Email = email,
            OrgName = orgName,
        }, tx, cancellationToken: cancellationToken));
    }
}

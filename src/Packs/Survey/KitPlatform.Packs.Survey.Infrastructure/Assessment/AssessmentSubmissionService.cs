using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Survey;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal sealed class AssessmentSubmissionService : IAssessmentSubmissionService
{
    private static readonly Regex VnPhoneRegex = new(@"^0[0-9]{9}$", RegexOptions.Compiled);

    private readonly AssessmentRepository _repo;
    private readonly AssessmentPartnerRepository _partners;
    private readonly AssessmentIntelligenceRepository _intel;
    private readonly AssessmentAnalysisPipeline _pipeline;
    private readonly IDbConnectionFactory _db;
    private readonly AssessmentSettings _settings;
    private readonly ILogger<AssessmentSubmissionService> _logger;

    public AssessmentSubmissionService(
        AssessmentRepository repo,
        AssessmentPartnerRepository partners,
        AssessmentIntelligenceRepository intel,
        AssessmentAnalysisPipeline pipeline,
        IDbConnectionFactory db,
        IOptions<AssessmentSettings> settings,
        ILogger<AssessmentSubmissionService> logger)
    {
        _repo = repo;
        _partners = partners;
        _intel = intel;
        _pipeline = pipeline;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CreateAssessmentSubmissionResult> CreateAsync(
        CreateAssessmentSubmissionRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var template = await _repo.GetTemplateHeaderAsync(
            request.TemplateCode,
            request.TemplateVersion,
            cancellationToken)
            ?? throw new AssessmentException(
                AssessmentErrorCodes.NotFound,
                "Không tìm thấy template khảo sát.",
                statusCode: 404);

        Guid? partnerId = null;
        if (!string.IsNullOrWhiteSpace(request.PartnerCode))
        {
            var partner = await _partners.GetByCodeAsync(request.PartnerCode, cancellationToken);
            if (partner is null || !string.Equals(partner.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                throw new AssessmentException(
                    AssessmentErrorCodes.ValidationError,
                    "Mã đối tác không hợp lệ hoặc đã bị khóa.",
                    statusCode: 400);
            }

            partnerId = partner.Id;
        }

        var source = string.IsNullOrWhiteSpace(request.Source) ? "public_web" : request.Source.Trim();
        if (partnerId.HasValue && source is "public_web")
            source = "partner";

        if (source is not ("public_web" or "admin" or "embed" or "sales" or "partner"))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                "source không hợp lệ.",
                statusCode: 400);
        }

        var sessionToken = AssessmentRepository.GenerateSessionToken();
        var id = await _repo.InsertSubmissionAsync(
            template.Id,
            template.Version,
            sessionToken,
            source,
            ipAddress,
            userAgent,
            request.Locale,
            partnerId,
            cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.AddDays(_settings.SessionMaxAgeDays);

        return new CreateAssessmentSubmissionResult(
            id,
            sessionToken,
            "draft",
            template.Code,
            template.Version,
            expiresAt);
    }

    public async Task<AssessmentSubmissionDetailDto> GetAsync(
        Guid submissionId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var submission = await RequireSubmissionAsync(submissionId, sessionToken, cancellationToken);
        var responses = await _repo.GetResponsesAsync(submissionId, cancellationToken);

        var map = responses.ToDictionary(
            r => r.QuestionId.ToString(),
            r => new AssessmentResponseItemDto(r.OptionId, r.TextValue));

        if (submission.Status is "completed" or "lead_captured" or "report_ready")
        {
            var categoryScores = await _repo.GetCategoryScoresAsync(submissionId, cancellationToken);
            var insights = await _repo.GetInsightsAsync(submissionId, cancellationToken);

            return new AssessmentSubmissionDetailDto(
                submission.Id,
                submission.Status,
                submission.TemplateCode,
                submission.TemplateVersion,
                submission.StartedAt,
                submission.CompletedAt,
                map,
                submission.OverallScore,
                submission.OverallPct,
                categoryScores.Select(c => new AssessmentCategoryScoreDto(c.Code, c.Name, c.Score, c.ScorePct)).ToList(),
                insights.Take(2).Select(i => new AssessmentInsightDto(i.Title, i.Body, i.Severity)).ToList());
        }

        return new AssessmentSubmissionDetailDto(
            submission.Id,
            submission.Status,
            submission.TemplateCode,
            submission.TemplateVersion,
            submission.StartedAt,
            submission.CompletedAt,
            map);
    }

    public async Task<SaveAssessmentResponsesResult> SaveResponsesAsync(
        Guid submissionId,
        string sessionToken,
        SaveAssessmentResponsesRequest request,
        CancellationToken cancellationToken = default)
    {
        var submission = await RequireSubmissionAsync(submissionId, sessionToken, cancellationToken);
        EnsureDraft(submission);

        if (request.Responses.Count == 0)
        {
            var countsEmpty = await _repo.GetRequiredCountsAsync(submissionId, cancellationToken);
            return new SaveAssessmentResponsesResult(0, countsEmpty.AnsweredRequired, countsEmpty.TotalRequired);
        }

        var validationRows = await _repo.GetQuestionsForValidationAsync(submissionId, cancellationToken);
        var optionToQuestion = validationRows
            .Where(r => r.OptionId.HasValue)
            .ToDictionary(r => r.OptionId!.Value, r => r.OptionQuestionId ?? r.QuestionId);

        var upserts = new List<AssessmentResponseUpsert>();
        foreach (var item in request.Responses)
        {
            if (item.OptionId.HasValue)
            {
                if (!optionToQuestion.TryGetValue(item.OptionId.Value, out var ownerQuestionId)
                    || ownerQuestionId != item.QuestionId)
                {
                    throw new AssessmentException(
                        AssessmentErrorCodes.ValidationError,
                        "optionId không thuộc questionId.",
                        statusCode: 400);
                }
            }

            upserts.Add(new AssessmentResponseUpsert(item.QuestionId, item.OptionId, item.TextValue));
        }

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await _repo.UpsertResponsesAsync(conn, tx, submissionId, upserts);
        await tx.CommitAsync(cancellationToken);

        var counts = await _repo.GetRequiredCountsAsync(submissionId, cancellationToken);
        return new SaveAssessmentResponsesResult(
            upserts.Count,
            counts.AnsweredRequired,
            counts.TotalRequired);
    }

    public async Task<CompleteAssessmentResult> CompleteAsync(
        Guid submissionId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var submission = await RequireSubmissionAsync(submissionId, sessionToken, cancellationToken);

        if (submission.Status != "draft")
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AlreadyCompleted,
                "Submission đã hoàn thành.",
                statusCode: 409);
        }

        var counts = await _repo.GetRequiredCountsAsync(submissionId, cancellationToken);
        if (counts.AnsweredRequired < counts.TotalRequired)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                $"Còn {counts.TotalRequired - counts.AnsweredRequired} câu bắt buộc chưa trả lời.",
                statusCode: 400);
        }

        var scoringRows = await _repo.GetScoringDataAsync(submissionId, cancellationToken);
        var scoring = AssessmentScoringEngine.Compute(scoringRows);
        var rules = await _repo.GetActiveRulesAsync(submission.TemplateId, cancellationToken);
        var responseCodes = BuildResponseOptionCodes(scoringRows);
        var questionScores = BuildQuestionScores(scoringRows);

        var matches = AssessmentRuleEngine.Evaluate(
            rules,
            scoring.OverallScore,
            scoring.CategoryScoreByCode,
            scoring.DimensionScoreByCode,
            responseCodes,
            questionScores);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await _repo.SaveScoresAsync(
            conn,
            tx,
            submissionId,
            scoring.OverallScore,
            scoring.OverallPct,
            scoring.DimensionScores,
            scoring.CategoryScores);

        await _repo.DeleteInsightsAndRecommendationsAsync(conn, tx, submissionId);

        var insightMatches = matches
            .Where(m => string.Equals(m.Rule.ActionType, "insight", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        var sort = 0;
        foreach (var match in insightMatches)
        {
            await PersistInsightAsync(_repo, conn, tx, submissionId, match, sort++);
        }

        await TryWriteCompletedEventAsync(conn, tx, submissionId, scoring.OverallScore, scoring.OverallPct, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        var categoryScores = await _repo.GetCategoryScoresAsync(submissionId, cancellationToken);
        var insights = await _repo.GetInsightsAsync(submissionId, cancellationToken);

        return new CompleteAssessmentResult(
            "completed",
            scoring.OverallScore,
            scoring.OverallPct,
            categoryScores.Select(c => new AssessmentCategoryScoreDto(c.Code, c.Name, c.Score, c.ScorePct)).ToList(),
            insights.Select(i => new AssessmentInsightDto(i.Title, i.Body, i.Severity)).ToList(),
            ReportLocked: true,
            LeadCaptureRequired: true);
    }

    public async Task<CaptureAssessmentLeadResult> CaptureLeadAsync(
        Guid submissionId,
        string sessionToken,
        CaptureAssessmentLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var submission = await RequireSubmissionAsync(submissionId, sessionToken, cancellationToken);

        if (submission.Status != "completed")
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                submission.Status == "draft"
                    ? "Hoàn thành khảo sát trước khi gửi thông tin."
                    : "Lead đã được ghi nhận.",
                statusCode: 400);
        }

        ValidateLeadRequest(request);

        var phone = NormalizePhone(request.RespondentPhone);
        var leadAttempts = await _repo.CountLeadCapturesByPhoneTodayAsync(phone, cancellationToken);
        if (leadAttempts >= _settings.CaptureLeadPerPhonePerDay)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.RateLimited,
                "Đã vượt giới hạn gửi thông tin liên hệ trong ngày.",
                statusCode: 429);
        }

        var scoringRows = await _repo.GetScoringDataAsync(submissionId, cancellationToken);
        var scoring = AssessmentScoringEngine.Compute(scoringRows);
        var rules = await _repo.GetActiveRulesAsync(submission.TemplateId, cancellationToken);
        var responseCodes = BuildResponseOptionCodes(scoringRows);
        var questionScores = BuildQuestionScores(scoringRows);

        var matches = AssessmentRuleEngine.Evaluate(
            rules,
            scoring.OverallScore,
            scoring.CategoryScoreByCode,
            scoring.DimensionScoreByCode,
            responseCodes,
            questionScores);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await _repo.CaptureLeadAsync(
            conn,
            tx,
            submissionId,
            request.RespondentName.Trim(),
            phone,
            string.IsNullOrWhiteSpace(request.RespondentEmail) ? null : request.RespondentEmail.Trim(),
            string.IsNullOrWhiteSpace(request.RespondentOrgName) ? null : request.RespondentOrgName.Trim(),
            request.RespondentNote?.Trim(),
            request.ConsentMarketing);

        await _repo.DeleteInsightsAndRecommendationsAsync(conn, tx, submissionId);

        var insightSort = 0;
        var recSort = 0;
        foreach (var match in matches)
        {
            if (string.Equals(match.Rule.ActionType, "insight", StringComparison.OrdinalIgnoreCase))
                await PersistInsightAsync(_repo, conn, tx, submissionId, match, insightSort++);
            else if (string.Equals(match.Rule.ActionType, "recommendation", StringComparison.OrdinalIgnoreCase))
                await PersistRecommendationAsync(conn, tx, submissionId, match, recSort++);
        }

        await TryWriteLeadCapturedEventAsync(conn, tx, submissionId, phone, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        KapReportArtifactDto? artifact = null;
        try
        {
            artifact = await _pipeline.RunAsync(submissionId, "lead_captured", NormalizeOrgScale(request.OrgScale), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KAP pipeline failed after lead capture for submission {SubmissionId}", submissionId);
        }

        if (_settings.SyncLeadsToCrm && !string.IsNullOrWhiteSpace(_settings.EventTenantCode))
        {
            var tenantId = await _repo.GetTenantIdByCodeAsync(_settings.EventTenantCode, cancellationToken);
            if (tenantId.HasValue)
            {
                await Events.AssessmentCrmBridge.SyncSubmissionLeadAsync(
                    _repo,
                    tenantId.Value,
                    submissionId,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                    cancellationToken);
            }
        }

        return new CaptureAssessmentLeadResult(
            artifact is not null ? "report_ready" : "lead_captured",
            sessionToken,
            "Cảm ơn. Báo cáo đã sẵn sàng.");
    }

    public async Task<AssessmentFullReportDto> GetReportAsync(
        Guid submissionId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var submission = await RequireSubmissionAsync(submissionId, sessionToken, cancellationToken);
        EnsureReportUnlocked(submission);

        return await BuildFullReportAsync(submission, cancellationToken);
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> GetReportPdfAsync(
        Guid submissionId,
        string sessionToken,
        KapReportPdfKind kind = KapReportPdfKind.Consulting,
        CancellationToken cancellationToken = default)
    {
        var submission = await RequireSubmissionAsync(submissionId, sessionToken, cancellationToken);
        EnsureReportUnlocked(submission);

        var fileName = BuildPdfFileName(submissionId, kind);
        if (TryReadCachedPdf(submissionId, kind, out var cachedBytes))
        {
            _logger.LogInformation("KAP PDF cache hit for submission {SubmissionId}", submissionId);
            return (cachedBytes, fileName, "application/pdf");
        }

        _logger.LogInformation("KAP PDF cache miss — generating for submission {SubmissionId}", submissionId);
        var report = await BuildFullReportAsync(submission, cancellationToken);
        var orgName = submission.RespondentOrgName?.Trim()
            ?? submission.RespondentName?.Trim()
            ?? submission.Id.ToString();

        return await GenerateAndStorePdfAsync(submissionId, report, orgName, kind, cancellationToken);
    }

    public async Task<AssessmentFullReportDto> GetReportForAdminAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var submission = await _repo.GetSubmissionAsync(submissionId, cancellationToken)
            ?? throw new AssessmentException(AssessmentErrorCodes.NotFound, "Không tìm thấy submission.", 404);

        EnsureReportUnlocked(submission);
        return await BuildFullReportAsync(submission, cancellationToken);
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> GetReportPdfForAdminAsync(
        Guid submissionId,
        KapReportPdfKind kind = KapReportPdfKind.Consulting,
        CancellationToken cancellationToken = default)
    {
        var submission = await _repo.GetSubmissionAsync(submissionId, cancellationToken)
            ?? throw new AssessmentException(AssessmentErrorCodes.NotFound, "Không tìm thấy submission.", 404);

        EnsureReportUnlocked(submission);

        var fileName = BuildPdfFileName(submissionId, kind);
        if (TryReadCachedPdf(submissionId, kind, out var cachedBytes))
            return (cachedBytes, fileName, "application/pdf");

        var report = await BuildFullReportAsync(submission, cancellationToken);
        var orgName = submission.RespondentOrgName?.Trim()
            ?? submission.RespondentName?.Trim()
            ?? submission.Id.ToString();

        return await GenerateAndStorePdfAsync(submissionId, report, orgName, kind, cancellationToken);
    }

    private async Task<(byte[] Content, string FileName, string ContentType)> GenerateAndStorePdfAsync(
        Guid submissionId,
        AssessmentFullReportDto report,
        string orgName,
        KapReportPdfKind kind,
        CancellationToken cancellationToken)
    {
        byte[] pdfBytes;
        try
        {
            pdfBytes = AssessmentReportPdfGenerator.Generate(report, orgName, kind);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KAP PDF generation failed for submission {SubmissionId}", submissionId);
            throw new AssessmentException(
                AssessmentErrorCodes.InternalError,
                "Không tạo được PDF. Vui lòng thử lại sau.",
                statusCode: 500);
        }

        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "assessment-reports");
        Directory.CreateDirectory(reportsDir);
        var fileName = BuildPdfFileName(submissionId, kind);
        var filePath = Path.Combine(reportsDir, fileName);
        await File.WriteAllBytesAsync(filePath, pdfBytes, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await _repo.UpsertReportAsync(conn, tx, submissionId, filePath, fileName, pdfBytes.LongLength, "pdf");
        await tx.CommitAsync(cancellationToken);

        return (pdfBytes, fileName, "application/pdf");
    }

    private static bool TryReadCachedPdf(Guid submissionId, KapReportPdfKind kind, out byte[] content)
    {
        content = [];
        var fileName = BuildPdfFileName(submissionId, kind);
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "assessment-reports", fileName);
        if (!File.Exists(filePath))
            return false;

        try
        {
            content = File.ReadAllBytes(filePath);
            return content.Length >= 512
                && content[0] == (byte)'%'
                && content[1] == (byte)'P'
                && content[2] == (byte)'D'
                && content[3] == (byte)'F';
        }
        catch
        {
            content = [];
            return false;
        }
    }

    private static string BuildPdfFileName(Guid submissionId, KapReportPdfKind kind) =>
        kind switch
        {
            KapReportPdfKind.Executive => $"kap-executive-{submissionId:N}.pdf",
            KapReportPdfKind.Appendix => $"kap-appendix-{submissionId:N}.pdf",
            KapReportPdfKind.Owner => $"kap-owner-{submissionId:N}.pdf",
            _ => $"kap-consulting-{submissionId:N}.pdf",
        };

    private async Task<AssessmentFullReportDto> BuildFullReportAsync(
        AssessmentSubmissionRow submission,
        CancellationToken cancellationToken)
    {
        await EnsureArtifactAsync(submission.Id, cancellationToken);

        var artifactRow = await _intel.GetCurrentArtifactAsync(submission.Id, cancellationToken);
        if (artifactRow is not null)
        {
            var artifact = AssessmentAnalysisPipeline.DeserializeArtifact(artifactRow.ArtifactJson);
            if (artifact is not null)
            {
                var mapped = MapFullReportFromArtifact(submission, artifact);
                if (mapped.Intelligence is null)
                    return mapped;

                var orgName = submission.RespondentOrgName?.Trim()
                    ?? submission.RespondentName?.Trim()
                    ?? "Doanh nghiệp";
                var orgScale = artifact.Meta.OrgScale
                    ?? await _intel.GetSubmissionOrgScaleAsync(submission.Id, cancellationToken);
                var enriched = KapReportIntelligenceEnricher.Enrich(mapped, mapped.Intelligence, orgName, orgScale);
                return mapped with { Intelligence = enriched, OrgScale = orgScale };
            }
        }

        var categoryScores = await _repo.GetCategoryScoresAsync(submission.Id, cancellationToken);
        var dimensionScores = await _repo.GetDimensionScoresAsync(submission.Id, cancellationToken);
        var insights = await _repo.GetInsightsAsync(submission.Id, cancellationToken);
        var recommendations = await _repo.GetRecommendationsAsync(submission.Id, cancellationToken);
        var scoringRows = await _repo.GetScoringDataAsync(submission.Id, cancellationToken);
        var qualitative = AssessmentScoringEngine.Compute(scoringRows).Qualitative;

        return new AssessmentFullReportDto(
            submission.Id,
            submission.TemplateCode,
            submission.CompletedAt,
            submission.OverallScore ?? 0m,
            submission.OverallPct ?? 0m,
            categoryScores.Select(c => new AssessmentCategoryScoreDto(c.Code, c.Name, c.Score, c.ScorePct)).ToList(),
            dimensionScores.Select(d => new AssessmentDimensionScoreDto(
                d.Code, d.Name, d.CategoryCode, d.Score, d.ScorePct)).ToList(),
            insights.Select(i => new AssessmentInsightDto(i.Title, i.Body, i.Severity)).ToList(),
            recommendations.Select(r => new AssessmentRecommendationDto(
                r.Title, r.Body, r.Priority, r.ProductArea, r.EstimateHint)).ToList(),
            new AssessmentQualitativeTagsDto(qualitative.PainPointTag, qualitative.PriorityNeedTag),
            new AssessmentReportPdfDto(
                true,
                $"/api/public/assessment/submissions/{submission.Id}/report.pdf"));
    }

    private static AssessmentFullReportDto MapFullReportFromArtifact(
        AssessmentSubmissionRow submission,
        KapReportArtifactDto artifact) =>
        new(
            submission.Id,
            submission.TemplateCode,
            submission.CompletedAt,
            artifact.Scores.OverallScore,
            artifact.Scores.OverallPct,
            artifact.Scores.Categories,
            artifact.Scores.Dimensions,
            artifact.Insights,
            artifact.Recommendations,
            artifact.Scores.Qualitative,
            new AssessmentReportPdfDto(
                true,
                $"/api/public/assessment/submissions/{submission.Id}/report.pdf"),
            AssessmentAnalysisPipeline.ToIntelligenceDto(artifact),
            artifact.Meta.OrgScale);

    private async Task EnsureArtifactAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var existing = await _intel.GetCurrentArtifactAsync(submissionId, cancellationToken);
        if (existing is not null)
        {
            var artifact = AssessmentAnalysisPipeline.DeserializeArtifact(existing.ArtifactJson);
            if (artifact is not null && !KapReportIntelligenceEnricher.NeedsArtifactRebuild(artifact, _settings))
                return;
        }

        var orgScale = await _intel.GetSubmissionOrgScaleAsync(submissionId, cancellationToken);
        try
        {
            await _pipeline.RunAsync(submissionId, "manual_refresh", orgScale, cancellationToken);
            _logger.LogInformation("KAP artifact rebuilt for submission {SubmissionId}", submissionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KAP artifact rebuild failed for submission {SubmissionId}", submissionId);
        }
    }

    private static Dictionary<string, string> BuildResponseOptionCodes(IReadOnlyList<AssessmentScoringRow> rows) =>
        rows
            .Where(r => !string.IsNullOrWhiteSpace(r.OptionCode))
            .GroupBy(r => r.QuestionCode)
            .ToDictionary(g => g.Key, g => g.First().OptionCode!);

    private static Dictionary<string, decimal> BuildQuestionScores(IReadOnlyList<AssessmentScoringRow> rows) =>
        rows
            .Where(r => r.Scorable && r.OptionScore.HasValue)
            .GroupBy(r => r.QuestionCode)
            .ToDictionary(g => g.Key, g => (decimal)g.First().OptionScore!.Value);

    private async Task<AssessmentSubmissionRow> RequireSubmissionAsync(
        Guid submissionId,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.SessionInvalid,
                "Thiếu session token.",
                statusCode: 401);
        }

        var submission = await _repo.GetSubmissionAsync(submissionId, cancellationToken);
        if (submission is null)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.NotFound,
                "Không tìm thấy submission.",
                statusCode: 404);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(submission.SessionToken),
                Encoding.UTF8.GetBytes(sessionToken)))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.SessionInvalid,
                "Session không hợp lệ.",
                statusCode: 401);
        }

        return submission;
    }

    private static void EnsureDraft(AssessmentSubmissionRow submission)
    {
        if (submission.Status != "draft")
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AlreadyCompleted,
                "Không thể sửa submission đã hoàn thành.",
                statusCode: 409);
        }
    }

    private static void EnsureReportUnlocked(AssessmentSubmissionRow submission)
    {
        if (submission.Status is "draft" or "completed")
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ReportLocked,
                "Báo cáo bị khóa. Vui lòng nhập thông tin liên hệ.",
                statusCode: 403);
        }
    }

    private static void ValidateLeadRequest(CaptureAssessmentLeadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RespondentName) || request.RespondentName.Trim().Length < 2)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                "respondentName bắt buộc (tối thiểu 2 ký tự).",
                statusCode: 400);
        }

        if (!string.IsNullOrWhiteSpace(request.RespondentOrgName) && request.RespondentOrgName.Trim().Length < 2)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                "respondentOrgName tối thiểu 2 ký tự (nếu điền).",
                statusCode: 400);
        }

        var phone = NormalizePhone(request.RespondentPhone);
        if (!VnPhoneRegex.IsMatch(phone))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                "respondentPhone không hợp lệ (10 số, bắt đầu 0).",
                statusCode: 400);
        }

        if (!string.IsNullOrWhiteSpace(request.RespondentEmail)
            && (!request.RespondentEmail.Contains('@') || request.RespondentEmail.Trim().Length < 5))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                "respondentEmail không hợp lệ.",
                statusCode: 400);
        }

        if (!string.IsNullOrWhiteSpace(request.OrgScale) && !IsValidOrgScale(request.OrgScale))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ValidationError,
                "orgScale không hợp lệ (micro, small, medium, large, chain).",
                statusCode: 400);
        }
    }

    private static bool IsValidOrgScale(string scale)
    {
        var normalized = scale.Trim().ToLowerInvariant();
        return normalized is "micro" or "small" or "medium" or "large" or "chain";
    }

    private static string NormalizeOrgScale(string? scale) =>
        string.IsNullOrWhiteSpace(scale) ? "small" : scale.Trim().ToLowerInvariant();

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84") && digits.Length == 11)
            digits = "0" + digits[2..];
        return digits;
    }

    private static async Task PersistInsightAsync(
        AssessmentRepository repo,
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid submissionId,
        AssessmentRuleEngine.Match match,
        int sortOrder)
    {
        var root = match.Payload.RootElement;
        var title = root.GetProperty("title").GetString() ?? match.Rule.Code;
        var body = root.GetProperty("body").GetString() ?? "";
        var severity = root.TryGetProperty("severity", out var sev) ? sev.GetString() ?? "info" : "info";
        var scopeType = root.TryGetProperty("scopeType", out var st) ? st.GetString() : null;
        var scopeCode = root.TryGetProperty("scopeCode", out var sc) ? sc.GetString() : null;

        await repo.InsertInsightAsync(
            conn, tx, submissionId, match.Rule.Id, title, body, severity, scopeType, scopeCode, sortOrder);
    }

    private async Task PersistRecommendationAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid submissionId,
        AssessmentRuleEngine.Match match,
        int sortOrder)
    {
        var root = match.Payload.RootElement;
        var title = root.GetProperty("title").GetString() ?? match.Rule.Code;
        var body = root.GetProperty("body").GetString() ?? "";
        var priority = root.TryGetProperty("priority", out var p) && p.TryGetInt32(out var pv)
            ? pv
            : match.Rule.Priority;
        var productArea = root.TryGetProperty("productArea", out var pa) ? pa.GetString() : null;
        var estimateHint = root.TryGetProperty("estimateHint", out var eh) ? eh.GetString() : null;

        await _repo.InsertRecommendationAsync(
            conn, tx, submissionId, match.Rule.Id, title, body, priority, productArea, estimateHint, sortOrder);
    }

    private async Task TryWriteLeadCapturedEventAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid submissionId,
        string phone,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.EventTenantCode))
            return;

        var tenantId = await _repo.GetTenantIdByCodeAsync(_settings.EventTenantCode, cancellationToken);
        if (tenantId is null)
            return;

        var phoneHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(phone))).ToLowerInvariant();

        await _repo.WritePlatformEventAsync(
            conn,
            tx,
            tenantId.Value,
            PlatformEventTypes.AssessmentSubmissionLeadCaptured,
            submissionId,
            new { submissionId, phoneHash },
            cancellationToken);
    }

    private async Task TryWriteCompletedEventAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid submissionId,
        decimal overallScore,
        decimal overallPct,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.EventTenantCode))
            return;

        var tenantId = await _repo.GetTenantIdByCodeAsync(_settings.EventTenantCode, cancellationToken);
        if (tenantId is null)
            return;

        await _repo.WritePlatformEventAsync(
            conn,
            tx,
            tenantId.Value,
            PlatformEventTypes.AssessmentSubmissionCompleted,
            submissionId,
            new { submissionId, overallScore, overallPct },
            cancellationToken);
    }
}

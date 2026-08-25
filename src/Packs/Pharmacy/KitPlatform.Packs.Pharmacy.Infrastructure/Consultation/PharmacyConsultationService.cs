using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal sealed class PharmacyConsultationService : IPharmacyConsultationService
{
    private static readonly TimeSpan GeminiBudget = TimeSpan.FromSeconds(12);

    private readonly ConsultationRepository _repo;
    private readonly SymptomTaxonomyRepository _taxonomy;
    private readonly PharmacyConsultationGeminiClient _gemini;
    private readonly PharmacyConsultationAiConfigProvider _aiConfig;
    private readonly IBranchAccessService _branchAccess;
    private readonly ITenantContext _tenant;

    public PharmacyConsultationService(
        ConsultationRepository repo,
        SymptomTaxonomyRepository taxonomy,
        PharmacyConsultationGeminiClient gemini,
        PharmacyConsultationAiConfigProvider aiConfig,
        IBranchAccessService branchAccess,
        ITenantContext tenant)
    {
        _repo = repo;
        _taxonomy = taxonomy;
        _gemini = gemini;
        _aiConfig = aiConfig;
        _branchAccess = branchAccess;
        _tenant = tenant;
    }

    public async Task<PharmacyConsultationSymptomCatalogDto> GetSymptomCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var fromDb = await _taxonomy.TryGetCatalogAsync(cancellationToken);
        if (fromDb is not null)
            return fromDb;

        var tenantCode = await _repo.GetTenantCodeAsync(cancellationToken);
        return PharmacyConsultationSymptomCatalogBuilder.Build(tenantCode);
    }

    private async Task<ConsultationSafetyRules.Evaluation> EvaluateFactsAsync(
        PharmacyConsultationFactsDto facts,
        CancellationToken cancellationToken)
    {
        var eval = ConsultationSafetyRules.Evaluate(facts);
        try
        {
            var symptomRisks = await _taxonomy.GetSymptomRiskFlagsAsync(facts.Symptoms, cancellationToken);
            return ConsultationSafetyRules.MergeSymptomRiskFlags(eval, symptomRisks);
        }
        catch
        {
            return eval;
        }
    }

    public async Task<ExtractPharmacyConsultationResultDto> ExtractAsync(
        ExtractPharmacyConsultationRequest request,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetSymptomCatalogAsync(cancellationToken);
        var quick = NormalizeQuick(request.QuickSymptoms);
        var natural = request.NaturalLanguage?.Trim() ?? "";
        var deidentified = PharmacyConsultationGeminiClient.Deidentify(natural);

        if (string.IsNullOrWhiteSpace(deidentified) && quick.Count == 0)
            throw new InvalidOperationException("Nhập mô tả khách hoặc chọn ít nhất một triệu chứng.");

        var aiResolved = await _aiConfig.ResolveAsync(cancellationToken);
        var localFacts = BuildLocalFacts(quick, natural);

        PharmacyConsultationFactsDto facts;
        string source;
        string? model = null;

        var useGemini = aiResolved.IsConfigured
                        && !string.IsNullOrWhiteSpace(deidentified)
                        && ConsultationTextParser.ShouldUseGemini(natural, localFacts);

        if (!useGemini)
        {
            facts = localFacts;
            source = quick.Count > 0 || !string.IsNullOrWhiteSpace(natural) ? "local_fast" : "manual";
            if (string.IsNullOrWhiteSpace(deidentified) && quick.Count == 0)
                facts = ConsultationSafetyRules.EmptyFacts();
        }
        else
        {
            using var geminiBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            geminiBudget.CancelAfter(GeminiBudget);
            try
            {
                (facts, model) = await _gemini.ExtractFactsAsync(
                    deidentified,
                    quick,
                    catalog.Flat,
                    aiResolved,
                    geminiBudget.Token);
                facts = ConsultationTextParser.EnrichFromNaturalLanguage(facts, natural, quick);
                source = "gemini";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                facts = localFacts;
                source = "local_fast";
            }
            catch
            {
                facts = localFacts;
                source = "local_fast";
            }
        }

        if (request.ConfirmedFacts is not null)
            facts = MergeExtractedWithConfirmed(facts, request.ConfirmedFacts);

        var eval = await EvaluateFactsAsync(facts, cancellationToken);
        var assessment = ConsultationPreliminaryAssessment.Build(facts, eval);
        return new ExtractPharmacyConsultationResultDto(
            facts,
            eval.Flags,
            eval.SafetyLevel,
            source,
            model,
            aiResolved.IsConfigured,
            assessment);
    }

    private static PharmacyConsultationFactsDto BuildLocalFacts(
        IReadOnlyList<string> quick,
        string naturalLanguage) =>
        ConsultationTextParser.EnrichFromNaturalLanguage(
            PharmacyConsultationGeminiClient.FactsFromQuickOnly(quick),
            naturalLanguage,
            quick);

    private static PharmacyConsultationFactsDto MergeExtractedWithConfirmed(
        PharmacyConsultationFactsDto extracted,
        PharmacyConsultationFactsDto confirmed)
    {
        var e = ConsultationSafetyRules.NormalizeFacts(extracted);
        var c = ConsultationSafetyRules.NormalizeFacts(confirmed);
        var symptoms = c.Symptoms
            .Concat(e.Symptoms)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var redFlags = c.RedFlags
            .Concat(e.RedFlags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ConsultationSafetyRules.NormalizeFacts(new PharmacyConsultationFactsDto(
            c.AgeYears ?? e.AgeYears,
            c.AgeMonths ?? e.AgeMonths,
            c.Gender ?? e.Gender,
            symptoms,
            c.DurationDays ?? e.DurationDays,
            c.HasFever ?? e.HasFever,
            c.IsPregnant ?? e.IsPregnant,
            c.IsBreastfeeding ?? e.IsBreastfeeding,
            redFlags,
            string.IsNullOrWhiteSpace(c.Notes) ? e.Notes : c.Notes));
    }

    public async Task<SuggestPharmacyConsultationResultDto> SuggestAsync(
        SuggestPharmacyConsultationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);

        var facts = ConsultationSafetyRules.NormalizeFacts(request.ConfirmedFacts);
        var eval = await EvaluateFactsAsync(facts, cancellationToken);

        if (eval.SafetyLevel is "stop_sale")
        {
            return new SuggestPharmacyConsultationResultDto(
                true,
                eval.Flags.FirstOrDefault()?.Message ?? "Dừng bán — gọi cấp cứu / chuyển cấp cứu ngay.",
                eval.SafetyLevel,
                eval.Flags,
                []);
        }

        if (facts.Symptoms.Count == 0)
        {
            return new SuggestPharmacyConsultationResultDto(
                true,
                "Chọn ít nhất một triệu chứng để gợi ý thuốc.",
                eval.SafetyLevel,
                eval.Flags,
                []);
        }

        var otcSymptoms = await _taxonomy.GetOtcAssistSymptomCodesAsync(facts.Symptoms, cancellationToken);
        if (otcSymptoms.Count == 0)
        {
            return new SuggestPharmacyConsultationResultDto(
                true,
                "Triệu chứng đã chọn chưa có quy tắc gợi ý OTC tự động. "
                + "Vẫn lưu được phiên tư vấn + cảnh báo an toàn. "
                + "Thử chọn thêm triệu chứng liên quan (VD: Ho, Đau họng) hoặc ghi rõ trong mô tả (VD: “ho 2 ngày”).",
                eval.SafetyLevel,
                eval.Flags,
                []);
        }

        var (categoryCodes, keywords, excludeKeywords, reasonByRule) =
            await ResolveKnowledgeAsync(otcSymptoms, facts, cancellationToken);

        if (categoryCodes.Count == 0 && keywords.Count == 0)
        {
            return new SuggestPharmacyConsultationResultDto(
                true,
                "Chưa có quy tắc gợi ý cho triệu chứng đã chọn.",
                eval.SafetyLevel,
                eval.Flags,
                []);
        }

        var exclude = excludeKeywords
            .Concat(ConsultationOtcRules.GetExcludeKeywords(facts))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var limit = request.Limit is > 0 and <= 12 ? request.Limit.Value : 6;

        var rows = await _repo.SearchOtcSuggestionsAsync(
            request.WarehouseId,
            SalesPriceTypes.Retail,
            categoryCodes,
            keywords,
            exclude,
            limit,
            cancellationToken);

        if (rows.Count == 0 && keywords.Count > 0)
        {
            rows = await _repo.SearchOtcSuggestionsAsync(
                request.WarehouseId,
                SalesPriceTypes.Retail,
                [],
                keywords,
                exclude,
                limit,
                cancellationToken);
        }

        var suggestions = rows
            .Select(row =>
            {
                var reason = PickReason(reasonByRule, row.CategoryCode, row.ProductName, row.GenericName)
                             ?? "Gợi ý OTC theo triệu chứng";
                var matchSource = !string.IsNullOrWhiteSpace(row.CategoryCode)
                                && categoryCodes.Contains(row.CategoryCode, StringComparer.OrdinalIgnoreCase)
                    ? "category"
                    : "keyword";
                return new PharmacyConsultationProductSuggestionDto(
                    row.ProductId,
                    row.ProductCode,
                    row.ProductName,
                    row.GenericName,
                    row.LookupCode,
                    row.ProductUnitId,
                    row.UnitName,
                    row.UnitPrice,
                    row.StockAvailable,
                    reason,
                    matchSource);
            })
            .ToList();

        if (suggestions.Count == 0)
        {
            return new SuggestPharmacyConsultationResultDto(
                false,
                "Không tìm thấy thuốc OTC còn hàng phù hợp — thử tìm tay trên POS hoặc chuyển dược sĩ.",
                eval.SafetyLevel,
                eval.Flags,
                []);
        }

        return new SuggestPharmacyConsultationResultDto(
            false,
            BuildSuggestionAdvisory(eval.SafetyLevel),
            eval.SafetyLevel,
            eval.Flags,
            suggestions);
    }

    private static string? BuildSuggestionAdvisory(string safetyLevel) => safetyLevel switch
    {
        "refer_medical" =>
            "Có cảnh báo cần khám — gợi ý OTC chỉ tham khảo cho triệu chứng phù hợp; dược sĩ quyết định trước khi bán.",
        "refer_pharmacist" =>
            "Nên có dược sĩ xác nhận trước khi bán — danh sách bên dưới chỉ mang tính gợi ý.",
        "caution" =>
            "Có điểm cần lưu ý — xác nhận lại với khách trước khi bán.",
        _ => null,
    };

    private async Task<(List<string> CategoryCodes, List<string> Keywords, List<string> ExcludeKeywords, Dictionary<string, string> ReasonByRule)> ResolveKnowledgeAsync(
        IReadOnlyList<string> otcSymptoms,
        PharmacyConsultationFactsDto facts,
        CancellationToken cancellationToken)
    {
        var dbRules = await _taxonomy.GetKnowledgeRulesAsync(otcSymptoms, cancellationToken);
        if (dbRules.Count > 0)
        {
            var categories = dbRules
                .SelectMany(r => r.CategoryCodes ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var keywords = dbRules
                .SelectMany(r => r.Keywords ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var exclude = dbRules
                .SelectMany(r => r.ExcludeKeywords ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var reasons = dbRules.ToDictionary(r => r.RuleCode, r => r.ReasonVi, StringComparer.OrdinalIgnoreCase);
            return (categories, keywords, exclude, reasons);
        }

        var matches = ConsultationOtcRules.ResolveMatches(otcSymptoms);
        var legacyCategories = matches.SelectMany(m => m.CategoryCodes).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var legacyKeywords = matches.SelectMany(m => m.Keywords).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var legacyReasons = matches.ToDictionary(m => m.SymptomCode, m => m.Reason, StringComparer.OrdinalIgnoreCase);
        return (legacyCategories, legacyKeywords, [], legacyReasons);
    }

    private static string? PickReason(
        IReadOnlyDictionary<string, string> reasonByRule,
        string? categoryCode,
        string productName,
        string? genericName)
    {
        if (reasonByRule.Count == 0)
            return null;

        var haystack = $"{productName} {genericName}".ToLowerInvariant();
        foreach (var reason in reasonByRule.Values)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                return reason;
        }

        return reasonByRule.Values.FirstOrDefault();
    }

    public async Task<PharmacyConsultationSessionDto> ConfirmAsync(
        ConfirmPharmacyConsultationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.UserId == Guid.Empty)
            throw new InvalidOperationException("Phiên đăng nhập không hợp lệ.");

        var quick = NormalizeQuick(request.QuickSymptoms);
        var facts = ConsultationSafetyRules.NormalizeFacts(request.ConfirmedFacts);
        var eval = await EvaluateFactsAsync(facts, cancellationToken);
        var assessment = request.PreliminaryAssessment
                           ?? ConsultationPreliminaryAssessment.Build(facts, eval);
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var row = new ConsultationRepository.SessionRow
        {
            Id = id,
            TenantId = _tenant.TenantId,
            CustomerId = request.CustomerId,
            StaffUserId = _tenant.UserId,
            ConsultationLevel = request.ConsultationLevel,
            Status = "confirmed",
            QuickSymptomsJson = ConsultationRepository.ToJson(quick),
            NaturalLanguageInput = string.IsNullOrWhiteSpace(request.NaturalLanguage)
                ? null
                : request.NaturalLanguage.Trim(),
            ExtractedJson = ConsultationRepository.ToJson(facts),
            ConfirmedFactsJson = ConsultationRepository.ToJson(facts),
            SafetyFlagsJson = ConsultationRepository.ToJson(eval.Flags),
            SafetyLevel = eval.SafetyLevel,
            PreliminaryAssessmentJson = ConsultationRepository.ToJson(assessment),
            CustomerProfileSnapshotJson = request.CustomerProfileSnapshot is null
                ? null
                : ConsultationRepository.ToJson(request.CustomerProfileSnapshot),
            ExtractionSource = string.IsNullOrWhiteSpace(request.ExtractionSource)
                ? "manual"
                : request.ExtractionSource.Trim(),
            AiModel = string.IsNullOrWhiteSpace(request.AiModel) ? null : request.AiModel.Trim(),
            ConfirmedAt = now,
        };

        await _repo.InsertAsync(row, cancellationToken);
        return Map(row);
    }

    public async Task<PharmacyConsultationSessionDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetAsync(id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<PharmacyConsultationSessionSummaryDto>> ListRecentByCustomerAsync(
        Guid customerId,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListRecentByCustomerAsync(customerId, limit, cancellationToken);
        var summaries = new List<PharmacyConsultationSessionSummaryDto>();

        foreach (var row in rows)
        {
            var facts = ConsultationSafetyRules.DeserializeFacts(row.ConfirmedFactsJson);
            var quick = DeserializeList(row.QuickSymptomsJson);
            var symptoms = facts.Symptoms
                .Concat(quick)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? headline = null;
            if (!string.IsNullOrWhiteSpace(row.PreliminaryAssessmentJson))
            {
                var assessment = DeserializePreliminaryAssessment(row.PreliminaryAssessmentJson);
                headline = assessment?.HeadlineVi;
            }

            IReadOnlyList<string> purchased = [];
            if (row.SalesOrderId is Guid orderId)
                purchased = await _repo.GetOrderProductNamesAsync(orderId, cancellationToken);

            var excerpt = row.NaturalLanguageInput?.Trim();
            if (excerpt?.Length > 120)
                excerpt = excerpt[..117] + "...";

            summaries.Add(new PharmacyConsultationSessionSummaryDto(
                row.Id,
                row.ConfirmedAt,
                row.Status,
                row.SalesOrderId,
                symptoms,
                headline,
                row.SafetyLevel,
                excerpt,
                row.SalesOrderId.HasValue,
                purchased));
        }

        return summaries;
    }

    public async Task<PharmacyConsultationSessionDto?> LinkOrderAsync(
        Guid sessionId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repo.OrderBelongsToTenantAsync(salesOrderId, cancellationToken))
            throw new InvalidOperationException("Đơn bán không tồn tại.");

        if (!await _repo.LinkOrderAsync(sessionId, salesOrderId, cancellationToken))
            return null;

        var row = await _repo.GetAsync(sessionId, cancellationToken);
        return row is null ? null : Map(row);
    }

    private static IReadOnlyList<string> NormalizeQuick(IReadOnlyList<string>? quick) =>
        (quick ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static PharmacyConsultationSessionDto Map(ConsultationRepository.SessionRow row)
    {
        var quick = DeserializeList(row.QuickSymptomsJson);
        var facts = ConsultationSafetyRules.DeserializeFacts(row.ConfirmedFactsJson);
        var flags = DeserializeFlags(row.SafetyFlagsJson);
        var assessment = DeserializePreliminaryAssessment(row.PreliminaryAssessmentJson);
        var profileSnapshot = DeserializeCustomerProfileSnapshot(row.CustomerProfileSnapshotJson);

        return new PharmacyConsultationSessionDto(
            row.Id,
            row.CustomerId,
            row.SalesOrderId,
            row.ConsultationLevel,
            row.Status,
            row.NaturalLanguageInput,
            quick,
            facts,
            flags,
            row.SafetyLevel,
            row.ExtractionSource,
            row.AiModel,
            row.CreatedAt,
            row.ConfirmedAt,
            assessment,
            profileSnapshot);
    }

    public async Task<IReadOnlyList<PharmacyConsultationQuestionDto>> GetQuestionsAsync(
        IReadOnlyList<string> symptomCodes,
        CancellationToken cancellationToken = default)
    {
        var codes = symptomCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = await _taxonomy.GetQuestionsForSymptomsAsync(codes, cancellationToken);
        return rows
            .Select(r => new PharmacyConsultationQuestionDto(
                r.Code,
                r.QuestionVi,
                r.AnswerType,
                r.Required,
                r.Priority))
            .ToList();
    }

    private static IReadOnlyList<string> DeserializeList(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<PharmacyConsultationSafetyFlagDto> DeserializeFlags(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<PharmacyConsultationSafetyFlagDto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static ConsultationPreliminaryAssessmentDto? DeserializePreliminaryAssessment(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<ConsultationPreliminaryAssessmentDto>(json);
        }
        catch
        {
            return null;
        }
    }

    private static ConsultationCustomerProfileSnapshotDto? DeserializeCustomerProfileSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<ConsultationCustomerProfileSnapshotDto>(json);
        }
        catch
        {
            return null;
        }
    }
}

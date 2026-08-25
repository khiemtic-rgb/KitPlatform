namespace KitPlatform.Packs.Pharmacy.Consultation;

public sealed record PharmacyConsultationFactsDto(
    int? AgeYears,
    int? AgeMonths,
    string? Gender,
    IReadOnlyList<string> Symptoms,
    int? DurationDays,
    bool? HasFever,
    bool? IsPregnant,
    bool? IsBreastfeeding,
    IReadOnlyList<string> RedFlags,
    string? Notes);

public sealed record PharmacyConsultationSafetyFlagDto(
    string Code,
    string Level,
    string Message);

public sealed record ConsultationCustomerProfileSnapshotDto(
    Guid? CustomerId,
    string? FullName,
    string? CustomerCode,
    int? AgeYears,
    int? AgeMonths,
    string? Gender,
    string? DateOfBirth,
    string? ClinicalNotes,
    DateTimeOffset CapturedAt);

public sealed record ExtractPharmacyConsultationRequest(
    string? NaturalLanguage,
    IReadOnlyList<string>? QuickSymptoms,
    PharmacyConsultationFactsDto? ConfirmedFacts = null);

public sealed record ExtractPharmacyConsultationResultDto(
    PharmacyConsultationFactsDto ProposedFacts,
    IReadOnlyList<PharmacyConsultationSafetyFlagDto> SafetyFlags,
    string SafetyLevel,
    string ExtractionSource,
    string? AiModel,
    bool GeminiConfigured,
    ConsultationPreliminaryAssessmentDto? PreliminaryAssessment = null);

public sealed record ConsultationPreliminaryHypothesisDto(
    string Code,
    string FitLevel,
    string LabelVi,
    string RationaleVi);

public sealed record ConsultationPreliminaryAssessmentDto(
    string Level,
    string HeadlineVi,
    string SummaryVi,
    string DisclaimerVi,
    IReadOnlyList<string> SupportingFactLines,
    IReadOnlyList<ConsultationPreliminaryHypothesisDto> Hypotheses,
    IReadOnlyList<string> MissingInfoHints,
    string? AdvisoryVi);

public sealed record ConfirmPharmacyConsultationRequest(
    Guid? CustomerId,
    short ConsultationLevel,
    string? NaturalLanguage,
    IReadOnlyList<string>? QuickSymptoms,
    PharmacyConsultationFactsDto ConfirmedFacts,
    string? ExtractionSource = null,
    string? AiModel = null,
    ConsultationPreliminaryAssessmentDto? PreliminaryAssessment = null,
    ConsultationCustomerProfileSnapshotDto? CustomerProfileSnapshot = null);

public sealed record PharmacyConsultationSessionDto(
    Guid Id,
    Guid? CustomerId,
    Guid? SalesOrderId,
    short ConsultationLevel,
    string Status,
    string? NaturalLanguage,
    IReadOnlyList<string> QuickSymptoms,
    PharmacyConsultationFactsDto ConfirmedFacts,
    IReadOnlyList<PharmacyConsultationSafetyFlagDto> SafetyFlags,
    string SafetyLevel,
    string ExtractionSource,
    string? AiModel,
    DateTimeOffset CreatedAt,
    DateTimeOffset ConfirmedAt,
    ConsultationPreliminaryAssessmentDto? PreliminaryAssessment = null,
    ConsultationCustomerProfileSnapshotDto? CustomerProfileSnapshot = null);

public sealed record PharmacyConsultationSessionSummaryDto(
    Guid Id,
    DateTimeOffset ConfirmedAt,
    string Status,
    Guid? SalesOrderId,
    IReadOnlyList<string> SymptomCodes,
    string? PreliminaryHeadlineVi,
    string SafetyLevel,
    string? NaturalLanguageExcerpt,
    bool OrderLinked,
    IReadOnlyList<string> PurchasedProductNames);

public sealed record LinkPharmacyConsultationOrderRequest(Guid SalesOrderId);

public sealed record PharmacyConsultationProductSuggestionDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string? GenericName,
    string LookupCode,
    Guid ProductUnitId,
    string UnitName,
    decimal UnitPrice,
    decimal StockAvailable,
    string Reason,
    string MatchSource);

public sealed record SuggestPharmacyConsultationRequest(
    PharmacyConsultationFactsDto ConfirmedFacts,
    Guid WarehouseId,
    int? Limit = null);

public sealed record SuggestPharmacyConsultationResultDto(
    bool Blocked,
    string? BlockReason,
    string SafetyLevel,
    IReadOnlyList<PharmacyConsultationSafetyFlagDto> SafetyFlags,
    IReadOnlyList<PharmacyConsultationProductSuggestionDto> Suggestions);

public sealed record PharmacyConsultationQuestionDto(
    string Code,
    string QuestionVi,
    string AnswerType,
    bool Required,
    int Priority);

public interface IPharmacyConsultationService
{
    Task<PharmacyConsultationSymptomCatalogDto> GetSymptomCatalogAsync(
        CancellationToken cancellationToken = default);

    Task<ExtractPharmacyConsultationResultDto> ExtractAsync(
        ExtractPharmacyConsultationRequest request,
        CancellationToken cancellationToken = default);

    Task<PharmacyConsultationSessionDto> ConfirmAsync(
        ConfirmPharmacyConsultationRequest request,
        CancellationToken cancellationToken = default);

    Task<PharmacyConsultationSessionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PharmacyConsultationSessionSummaryDto>> ListRecentByCustomerAsync(
        Guid customerId,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<PharmacyConsultationSessionDto?> LinkOrderAsync(
        Guid sessionId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<SuggestPharmacyConsultationResultDto> SuggestAsync(
        SuggestPharmacyConsultationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PharmacyConsultationQuestionDto>> GetQuestionsAsync(
        IReadOnlyList<string> symptomCodes,
        CancellationToken cancellationToken = default);
}

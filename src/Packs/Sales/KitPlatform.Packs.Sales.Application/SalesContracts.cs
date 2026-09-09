namespace KitPlatform.Packs.Sales;

public sealed record KitSalesHealthDto(string Pack, string Version, bool Ok);

public sealed record KitSalesProductDto(string Code, string DisplayName, string Status);

public sealed record KitSalesBusinessDto(
    Guid Id,
    string Name,
    string BusinessType,
    string? Province,
    string? Phone,
    string Status,
    string? Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateKitSalesBusinessRequest(
    string Name,
    string BusinessType = "pharmacy",
    string? Province = null,
    string? District = null,
    string? Phone = null,
    string? Email = null,
    string? Source = null,
    string? Notes = null);

public sealed record KitSalesLeadDto(
    Guid Id,
    Guid BusinessId,
    string BusinessName,
    string ProductCode,
    string LeadStatus,
    string LeadTemperature,
    decimal TotalScore,
    string? Source,
    string? Province,
    string? Phone,
    string? Notes,
    Guid? OwnerUserId,
    string? NextActionCode,
    DateTimeOffset? NextActionAt,
    DateTimeOffset? LastInteractionAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? FacebookKind = null,
    string? FacebookUrl = null);

public sealed record CreateKitSalesLeadRequest(
    Guid BusinessId,
    string ProductCode = "novixa",
    string? Source = null,
    string LeadStatus = "discovered",
    string LeadTemperature = "cold",
    string? Notes = null);

public sealed record UpdateKitSalesLeadRequest(
    string? BusinessName = null,
    string? Province = null,
    string? Phone = null,
    string? Source = null,
    string? LeadStatus = null,
    string? LeadTemperature = null,
    string? Notes = null,
    string? NextActionCode = null,
    DateTimeOffset? NextActionAt = null,
    bool ClearNextAction = false,
    string? FacebookKind = null);

public sealed record KitSalesInteractionDto(
    Guid Id,
    Guid LeadId,
    string Channel,
    string Direction,
    string InteractionType,
    string? Content,
    string? Outcome,
    DateTimeOffset OccurredAt,
    string ReviewStatus = "sent",
    bool AiGenerated = false,
    Guid? ApprovedByUserId = null,
    DateTimeOffset? SentAt = null,
    string? Classification = null,
    string? SentByName = null,
    string? ApprovedByName = null);

public sealed record CreateKitSalesInteractionRequest(
    string Channel = "phone",
    string Direction = "outbound",
    string InteractionType = "note",
    string? Content = null,
    string? Outcome = null,
    string ReviewStatus = "sent",
    bool AiGenerated = false,
    string? Classification = null,
    bool MarkSent = true);

public sealed record KitSalesTaskDto(
    Guid Id,
    Guid LeadId,
    string ActionCode,
    string Title,
    string Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

public sealed record CreateKitSalesTaskRequest(
    string ActionCode,
    string? Title = null,
    DateTimeOffset? DueAt = null);

public sealed record KitSalesLeadDetailDto(
    KitSalesLeadDto Lead,
    IReadOnlyList<KitSalesInteractionDto> Interactions,
    IReadOnlyList<KitSalesTaskDto> Tasks);

public sealed record CreateKitSalesProspectRequest(
    string BusinessName,
    string ProductCode = "novixa",
    string BusinessType = "pharmacy",
    string? Province = null,
    string? Phone = null,
    string? Source = null,
    string? Notes = null,
    string? FacebookKind = null);

public sealed record ImportKitSalesProspectsRequest(
    string Text,
    string? Province = null,
    string? FacebookKind = null,
    string ProductCode = "novixa");

public sealed record KitSalesImportResultDto(
    int Created,
    int Skipped,
    IReadOnlyList<string> Errors,
    IReadOnlyList<KitSalesLeadDto> Leads);

public sealed record KitSalesPipelineBucketDto(string Status, int Count);

public sealed record KitSalesPipelineSummaryDto(
    int TotalLeads,
    IReadOnlyList<KitSalesPipelineBucketDto> ByStatus);

public sealed record KitSalesLeadFilterDto(
    IReadOnlyList<string> Provinces,
    IReadOnlyList<KitSalesPipelineBucketDto> ByFacebookKind);

public interface IKitSalesDeskService
{
    Task<KitSalesHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesProductDto>> ListProductsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesBusinessDto>> ListBusinessesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<KitSalesBusinessDto> CreateBusinessAsync(
        CreateKitSalesBusinessRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesLeadDto>> ListLeadsAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken = default,
        string? province = null,
        string? facebookKind = null);

    Task<KitSalesLeadFilterDto> GetLeadFiltersAsync(
        CancellationToken cancellationToken = default);

    Task<KitSalesLeadDto> CreateLeadAsync(
        CreateKitSalesLeadRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesLeadDto> CreateProspectAsync(
        CreateKitSalesProspectRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesImportResultDto> ImportProspectsAsync(
        ImportKitSalesProspectsRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesLeadDto> UpdateLeadAsync(
        Guid leadId,
        UpdateKitSalesLeadRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteLeadAsync(Guid leadId, CancellationToken cancellationToken = default);

    Task<KitSalesLeadDetailDto?> GetLeadDetailAsync(
        Guid leadId,
        CancellationToken cancellationToken = default);

    Task<KitSalesInteractionDto> CreateInteractionAsync(
        Guid leadId,
        CreateKitSalesInteractionRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesTaskDto> CreateTaskAsync(
        Guid leadId,
        CreateKitSalesTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesTaskDto> CompleteTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<KitSalesPipelineSummaryDto> GetPipelineSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<KitSalesInteractionDto> ReviewInteractionAsync(
        Guid interactionId,
        ReviewKitSalesInteractionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record KitSalesChatPreviewRequest(
    string Text,
    Guid? LeadId = null,
    bool Log = false,
    bool AutoReply = false);

public sealed record KitSalesChatPreviewDto(
    string Intent,
    string Reply,
    bool Escalate,
    IReadOnlyList<string> Citations,
    bool FacebookSendReady,
    string MeLink,
    string PhcUrl);

public sealed record KitSalesAssistDto(
    Guid LeadId,
    string BusinessName,
    string Headline,
    string Summary,
    string Goal,
    string Context,
    string? Classification,
    string ClassificationLabel,
    string NextHint,
    string NextActionCode,
    string NextActionLabel,
    DateTimeOffset SuggestedAt,
    string WindowLabel,
    string WhyNow,
    string? LastInboundContent = null,
    bool NeedsReply = false);

public sealed record KitSalesChatComposeDto(
    string Intent,
    string Reply,
    bool Escalate,
    IReadOnlyList<string> Citations,
    bool FacebookSendReady,
    string MeLink,
    string PhcUrl,
    Guid? DraftId,
    string ReviewStatus,
    string ClassificationLabel,
    string NextHint,
    KitSalesAssistDto? Assist,
    bool GeminiUsed = false,
    bool AutoReplied = false);

public sealed record SendKitSalesDraftRequest(string? Content = null);

public sealed record ReviewKitSalesInteractionRequest(
    string ReviewStatus,
    string? Content = null);

public sealed record KitSalesChatChannelDto(
    bool FacebookConfigured,
    bool FacebookSendReady,
    string MeLink,
    string PhcUrl,
    string PageUrl);

public sealed record KitSalesJourneyPainDto(
    string Code,
    string Label,
    string Source,
    string Category = "",
    string CategoryLabel = "");

public sealed record KitSalesJourneyPlanDto(
    Guid LeadId,
    string BusinessName,
    string Step,
    string PainCode,
    string PainLabel,
    string Draft,
    IReadOnlyList<string> Citations,
    DateTimeOffset SuggestedAt,
    string WindowLabel,
    string WhyNow,
    bool IsDue,
    bool FacebookSendReady,
    string? PainCategory = null,
    string? Hook = null,
    string? Outreach = null,
    string? Question = null,
    string? Cta = null,
    string? SolutionDirection = null,
    string? WhyThisPain = null,
    string DiscoveryMode = "explore",
    string? FacebookUrl = null,
    bool GeminiUsed = false);

public sealed record ScheduleKitSalesJourneyRequest(
    string? PainCode = null,
    string? Step = null,
    string? Content = null);

public sealed record KitSalesPainDto(
    string Code,
    string Category,
    string CategoryLabel,
    string Name,
    string Description,
    IReadOnlyList<string> Symptoms,
    IReadOnlyList<string> DetectionSignals,
    string Hook,
    string Outreach,
    string Question,
    string SolutionDirection,
    string SolutionMapping,
    string Cta,
    IReadOnlyList<string> Objections,
    string PhcMapping);

public sealed record KitSalesPainMarketBucketDto(
    string Category,
    string CategoryLabel,
    int Approached,
    int Replied,
    int Interested,
    decimal SignalRate,
    decimal ExploreShare);

public sealed record KitSalesPainMarketDto(
    string Mode,
    int TouchedLeads,
    IReadOnlyList<KitSalesPainMarketBucketDto> ByCategory);

public sealed record KitSalesLeadPainScoreDto(
    string PainCode,
    string Category,
    string Name,
    decimal Confidence,
    string? ResponseCode,
    int ApproachedCount,
    bool Confirmed);

public sealed record KitSalesLeadPainProfileDto(
    Guid LeadId,
    string RecommendedPainCode,
    string DiscoveryMode,
    string WhyThisPain,
    IReadOnlyList<KitSalesLeadPainScoreDto> Scores,
    IReadOnlyList<KitSalesPainMarketBucketDto> Categories);

public sealed record RecordKitSalesPainResponseRequest(
    string ResponseCode,
    string? Note = null);

public sealed record KitSalesFacebookSettingsDto(
    bool Enabled,
    bool HasPageToken,
    string? PageTokenLast4,
    bool HasVerifyToken,
    bool HasAppSecret,
    string? PageId,
    string MeLink,
    string PageUrl,
    string PhcUrl,
    string WebhookUrl,
    bool SendReady);

public sealed record SaveKitSalesFacebookSettingsRequest(
    bool Enabled,
    string? PageAccessToken = null,
    bool ClearPageToken = false,
    string? VerifyToken = null,
    bool ClearVerifyToken = false,
    string? AppSecret = null,
    bool ClearAppSecret = false,
    string? PageId = null,
    string? MeLink = null,
    string? PageUrl = null,
    string? PhcUrl = null);

public sealed record KitSalesFacebookTestDto(
    bool Ok,
    string? PageId,
    string? PageName,
    string? Error);

public sealed record KitSalesFacebookPsidDto(
    Guid LeadId,
    Guid BusinessId,
    string BusinessName,
    string Psid,
    string? FacebookUrl,
    DateTimeOffset CreatedAt);

public sealed record AttachKitSalesPsidRequest(string Psid);

public sealed record KitSalesGeminiSettingsDto(
    bool HasApiKey,
    string? ApiKeyLast4,
    string? TextModel,
    bool Ready,
    string Source);

public sealed record SaveKitSalesGeminiSettingsRequest(
    string? GeminiApiKey = null,
    bool ClearApiKey = false,
    string? TextModel = null);

public sealed record KitSalesGeminiTestDto(bool Ok, string? Model, string? Error);

public interface IKitSalesAiSettings
{
    Task<KitSalesGeminiSettingsDto> GetMaskedAsync(CancellationToken cancellationToken = default);

    Task<KitSalesGeminiSettingsDto> SaveAsync(
        SaveKitSalesGeminiSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesGeminiTestDto> TestAsync(CancellationToken cancellationToken = default);
}

public interface IKitSalesFacebookSettings
{
    Task<KitSalesFacebookSettingsDto> GetMaskedAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default);

    Task<KitSalesFacebookSettingsDto> SaveAsync(
        SaveKitSalesFacebookSettingsRequest request,
        string webhookUrl,
        CancellationToken cancellationToken = default);

    Task<KitSalesFacebookTestDto> TestAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesFacebookPsidDto>> ListPsidsAsync(
        CancellationToken cancellationToken = default);

    Task<KitSalesFacebookPsidDto?> GetPsidAsync(
        Guid leadId,
        CancellationToken cancellationToken = default);

    Task<KitSalesFacebookPsidDto> AttachPsidAsync(
        Guid leadId,
        AttachKitSalesPsidRequest request,
        CancellationToken cancellationToken = default);

    Task DetachPsidAsync(Guid leadId, CancellationToken cancellationToken = default);
}

public interface IKitSalesChatService
{
    Task<KitSalesChatChannelDto> GetChannelAsync(CancellationToken cancellationToken = default);

    Task<KitSalesChatPreviewDto> PreviewAsync(string text, CancellationToken cancellationToken = default);

    Task<KitSalesChatPreviewDto> ReplyAsync(
        KitSalesChatPreviewRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesAssistDto> GetAssistAsync(
        Guid leadId,
        CancellationToken cancellationToken = default);

    Task<KitSalesChatComposeDto> ComposeAsync(
        KitSalesChatPreviewRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesInteractionDto> SendDraftAsync(
        Guid draftId,
        SendKitSalesDraftRequest request,
        CancellationToken cancellationToken = default);

    Task HandleFacebookInboundAsync(
        string senderPsid,
        string text,
        CancellationToken cancellationToken = default);

    IReadOnlyList<KitSalesJourneyPainDto> ListPains();

    Task<KitSalesJourneyPlanDto> GetJourneyAsync(
        Guid leadId,
        string? painCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesJourneyPlanDto>> ListDueJourneysAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<KitSalesJourneyPlanDto> ScheduleJourneyAsync(
        Guid leadId,
        ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesJourneyPlanDto> SendJourneyAsync(
        Guid leadId,
        ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesJourneyPlanDto> RewriteJourneyAsync(
        Guid leadId,
        ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken = default);

    Task<int> ProcessDueJourneysAsync(CancellationToken cancellationToken = default);
}

public interface IKitSalesPainService
{
    IReadOnlyList<KitSalesPainDto> ListPains();

    Task EnsureCatalogAsync(CancellationToken cancellationToken = default);

    Task<KitSalesPainMarketDto> GetMarketAsync(CancellationToken cancellationToken = default);

    Task<KitSalesLeadPainProfileDto> GetLeadProfileAsync(
        Guid leadId,
        CancellationToken cancellationToken = default);

    Task RecordApproachAsync(
        Guid leadId,
        string painCode,
        CancellationToken cancellationToken = default);

    Task RecordResponseAsync(
        Guid leadId,
        string painCode,
        RecordKitSalesPainResponseRequest request,
        CancellationToken cancellationToken = default);
}

namespace KitPlatform.Packs.Survey;

public sealed class AssessmentSettings
{
    public const string SectionName = "Assessment";

    public string SessionCookieName { get; set; } = "assessment_session";

    public int SessionMaxAgeDays { get; set; } = 7;

    /// <summary>Platform events require tenant_id — use this tenant for anonymous public leads.</summary>
    public string? EventTenantCode { get; set; } = "NVX-CS08";

    /// <summary>Max public assessment API requests per IP per minute.</summary>
    public int PublicRequestsPerMinutePerIp { get; set; } = 30;

    /// <summary>Max lead capture attempts per phone number per day.</summary>
    public int CaptureLeadPerPhonePerDay { get; set; } = 5;

    /// <summary>Sync captured leads into pack_crm.crm_lead for EventTenantCode.</summary>
    public bool SyncLeadsToCrm { get; set; } = true;

    /// <summary>Public KAP survey base URL (referral links).</summary>
    public string KapPublicUrl { get; set; } = "https://survey.novixa.vn";

    /// <summary>Partner portal base URL (QR deep-link target optional).</summary>
    public string PartnerPortalPublicUrl { get; set; } = "https://partner.novixa.vn";

    /// <summary>
    /// KAP analysis pipeline level: 1 = Consulting Lite (deterministic),
    /// 2 = Hybrid (benchmark + trend), 3 = Full Intelligence (AI narrative).
    /// </summary>
    public int AnalysisPipelineLevel { get; set; } = 1;

    /// <summary>Benchmark cohort code for Phase 2+ comparisons.</summary>
    public string DefaultBenchmarkCohortCode { get; set; } = "PHARMACY_VN_BASELINE";

    public bool BenchmarkAggregateEnabled { get; set; } = true;

    public int BenchmarkAggregateIntervalHours { get; set; } = 24;

    /// <summary>
    /// Số bài hoàn thành tối thiểu trước khi tạo/cập nhật nhóm so sánh. Dưới ngưỡng này báo cáo
    /// không được nói "so với các nhà thuốc khác" (mẫu quá nhỏ để có ý nghĩa).
    /// </summary>
    public int BenchmarkAggregateMinSampleSize { get; set; } = 30;

    /// <summary>Phase 3 AI narrative (OpenAI-compatible).</summary>
    public KapAiNarrativeSettings AiNarrative { get; set; } = new();
}

public sealed class KapAiNarrativeSettings
{
    /// <summary>Enable LLM calls when AnalysisPipelineLevel >= 3.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>OpenAI-compatible base URL, e.g. https://api.openai.com/v1</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 45;

    public int MaxTokens { get; set; } = 2500;

    /// <summary>When LLM unavailable, use answer-level personalization fallback.</summary>
    public bool FallbackPersonalization { get; set; } = true;
}

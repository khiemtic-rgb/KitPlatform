namespace PharmaCore.Application.Integration;

public sealed class IntegrationOutboxOptions
{
    public const string SectionName = "IntegrationOutbox";

    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 20;

    /// <summary>POST JSON envelope khi có URL; nếu trống chỉ ghi log.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>HMAC-SHA256 (header X-Integration-Signature) khi gửi webhook.</summary>
    public string? WebhookSecret { get; set; }

    public int WebhookTimeoutSeconds { get; set; } = 15;

    public int MaxPublishAttempts { get; set; } = 8;
}

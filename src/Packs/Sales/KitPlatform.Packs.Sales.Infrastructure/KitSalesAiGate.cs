namespace KitPlatform.Packs.Sales.Infrastructure;

internal interface IKitSalesAiGate
{
    /// <summary>Reserves budget before any HTTP call. A denied ticket must not reach Gemini.</summary>
    Task<KitSalesAiTicket> AcquireAsync(string purpose, long reserveTokens, CancellationToken cancellationToken);

    /// <summary>Settles a granted ticket with reported usage, or keeps the reserve when usage is unknown.</summary>
    Task SettleAsync(KitSalesAiTicket ticket, KitSalesAiUsage usage, CancellationToken cancellationToken);
}

internal sealed record KitSalesAiTicket(
    Guid? RunId,
    string? ApiKey,
    string Model,
    string? DenyReason,
    long Reserve)
{
    public bool Granted => DenyReason is null && RunId is not null && !string.IsNullOrWhiteSpace(ApiKey);

    public static KitSalesAiTicket Denied(string reason, string model) => new(null, null, model, reason, 0);
}

internal sealed record KitSalesAiUsage(bool Ok, long? PromptTokens, long? OutputTokens, long? TotalTokens, string? Error);

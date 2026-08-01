namespace KitPlatform.Application.CustomerApp;

public sealed record CustomerRepurchaseSuggestionDto(
    Guid Id,
    Guid SalesOrderId,
    Guid? SalesOrderItemId,
    string OrderNumber,
    string OrderLabel,
    string Status,
    DateTimeOffset OrderDate,
    int? ReminderDaysSupply,
    DateOnly? SuggestedForDate,
    DateTimeOffset? SnoozedUntil,
    DateTimeOffset? DrinkRemindersCreatedAt,
    DateTimeOffset? ConvertedAt,
    Guid? ConvertedReservationId,
    Guid? ConvertedSalesOrderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerRepurchaseSuggestionListResult(IReadOnlyList<CustomerRepurchaseSuggestionDto> Items);

public sealed record SnoozeRepurchaseSuggestionRequest(DateTimeOffset SnoozedUntil);

public sealed record AcceptRepurchaseSuggestionRequest(
    Guid? FamilyMemberId = null,
    string? RemindTime = null);

/// <summary>Smart Refill: create pickup reservation from prior order lines and mark suggestion converted.</summary>
public sealed record ReorderRepurchaseSuggestionResult(
    CustomerRepurchaseSuggestionDto Suggestion,
    Guid ReservationId,
    string ReservationNumber);

public interface ICustomerRepurchaseService
{
    Task<CustomerRepurchaseSuggestionListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<CustomerRepurchaseSuggestionDto?> AcceptAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        AcceptRepurchaseSuggestionRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<ReorderRepurchaseSuggestionResult?> ReorderAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        CancellationToken cancellationToken = default);

    Task<CustomerRepurchaseSuggestionDto?> DismissAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        CancellationToken cancellationToken = default);

    Task<CustomerRepurchaseSuggestionDto?> SnoozeAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        DateTimeOffset snoozedUntil,
        CancellationToken cancellationToken = default);
}

using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Customers;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerRepurchaseService : ICustomerRepurchaseService
{
    private readonly CustomerRepurchaseRepository _repo;
    private readonly ICustomerReservationService _reservations;
    private readonly CustomerAppAuthRepository _authRepo;

    public CustomerRepurchaseService(
        CustomerRepurchaseRepository repo,
        ICustomerReservationService reservations,
        CustomerAppAuthRepository authRepo)
    {
        _repo = repo;
        _reservations = reservations;
        _authRepo = authRepo;
    }

    public async Task<CustomerRepurchaseSuggestionListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListAsync(tenantId, customerId, accountId, cancellationToken);
        return new CustomerRepurchaseSuggestionListResult(rows.Select(MapRow).ToList());
    }

    public async Task<CustomerRepurchaseSuggestionDto?> AcceptAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        AcceptRepurchaseSuggestionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var remindTime = ParseRemindTime(request?.RemindTime);
        await _repo.AcceptAsync(
            tenantId,
            customerId,
            accountId,
            suggestionId,
            request?.FamilyMemberId,
            remindTime,
            DateTimeOffset.UtcNow.AddHours(1),
            cancellationToken);

        var row = await _repo.GetAsync(tenantId, customerId, accountId, suggestionId, cancellationToken);
        return row is null ? null : MapRow(row);
    }

    public async Task<ReorderRepurchaseSuggestionResult?> ReorderAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        CancellationToken cancellationToken = default)
    {
        var relation = await _authRepo.GetPharmacyRelationAsync(tenantId, customerId, cancellationToken);
        if (!CustomerPharmacyRelations.IsMember(relation))
        {
            throw new InvalidOperationException(
                "Đặt lại thuộc dịch vụ nhà thuốc. Liên kết nhà thuốc (QR quầy / xác nhận nhân viên) để tiếp tục.");
        }

        var suggestion = await _repo.GetAsync(tenantId, customerId, accountId, suggestionId, cancellationToken);
        if (suggestion is null)
            return null;

        var status = suggestion.Status.Trim().ToLowerInvariant();
        if (status is "converted")
            throw new InvalidOperationException("Gợi ý này đã đặt lại rồi.");
        if (status is "dismissed" or "expired")
            throw new InvalidOperationException("Gợi ý không còn hiệu lực để đặt lại.");
        if (status is not ("pending" or "snoozed"))
            throw new InvalidOperationException("Không thể đặt lại gợi ý này.");

        var lines = await _repo.ListOrderLinesAsync(tenantId, suggestion.SalesOrderId, cancellationToken);
        if (lines.Count == 0)
            throw new InvalidOperationException("Đơn gốc không còn dòng thuốc để đặt lại.");

        var reservation = await _reservations.CreateForCustomerAsync(
            tenantId,
            customerId,
            new CreateCustomerReservationRequest(
                CustomerReservationFulfillmentTypes.Pickup,
                null,
                $"Tái mua từ đơn #{suggestion.OrderNumber}",
                lines
                    .Where(l => l.Quantity > 0)
                    .Select(l => new CustomerReservationLineRequest(l.ProductId, l.Quantity))
                    .ToList(),
                suggestionId),
            cancellationToken);

        var marked = await _repo.MarkConvertedAsync(
            tenantId,
            customerId,
            accountId,
            suggestionId,
            reservation.Id,
            cancellationToken);
        if (!marked)
        {
            throw new InvalidOperationException(
                "Không cập nhật được trạng thái tái mua (có thể vừa được xử lý). Kiểm tra đặt trước của bạn.");
        }

        var updated = await _repo.GetAsync(tenantId, customerId, accountId, suggestionId, cancellationToken)
            ?? throw new InvalidOperationException("Không tải được gợi ý sau khi đặt lại.");

        return new ReorderRepurchaseSuggestionResult(
            MapRow(updated),
            reservation.Id,
            reservation.ReservationNumber);
    }

    public async Task<CustomerRepurchaseSuggestionDto?> DismissAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        CancellationToken cancellationToken = default)
    {
        var ok = await _repo.UpdateStatusAsync(
            tenantId,
            customerId,
            accountId,
            suggestionId,
            "dismissed",
            null,
            setDismissedAt: true,
            cancellationToken);
        if (!ok)
            return null;

        var row = await _repo.GetAsync(tenantId, customerId, accountId, suggestionId, cancellationToken);
        return row is null ? null : MapRow(row);
    }

    public async Task<CustomerRepurchaseSuggestionDto?> SnoozeAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        DateTimeOffset snoozedUntil,
        CancellationToken cancellationToken = default)
    {
        if (snoozedUntil <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Thời gian snooze phải ở tương lai.");

        var ok = await _repo.UpdateStatusAsync(
            tenantId,
            customerId,
            accountId,
            suggestionId,
            "snoozed",
            snoozedUntil,
            setDismissedAt: false,
            cancellationToken);
        if (!ok)
            return null;

        var row = await _repo.GetAsync(tenantId, customerId, accountId, suggestionId, cancellationToken);
        return row is null ? null : MapRow(row);
    }

    private static TimeOnly ParseRemindTime(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && TimeOnly.TryParse(value, out var parsed))
            return parsed;
        return new TimeOnly(8, 0);
    }

    private static CustomerRepurchaseSuggestionDto MapRow(CustomerRepurchaseSuggestionRow row) =>
        new(
            row.Id,
            row.SalesOrderId,
            row.SalesOrderItemId,
            row.OrderNumber,
            row.OrderLabel,
            row.Status,
            new DateTimeOffset(DateTime.SpecifyKind(row.OrderDate, DateTimeKind.Utc)),
            row.ReminderDaysSupply,
            row.SuggestedForDate,
            row.SnoozedUntil.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(row.SnoozedUntil.Value, DateTimeKind.Utc))
                : null,
            row.DrinkRemindersCreatedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(row.DrinkRemindersCreatedAt.Value, DateTimeKind.Utc))
                : null,
            row.ConvertedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(row.ConvertedAt.Value, DateTimeKind.Utc))
                : null,
            row.ConvertedReservationId,
            row.ConvertedSalesOrderId,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc)));
}

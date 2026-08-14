using KitPlatform.Application.Abstractions;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Customers;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerChatService : ICustomerChatService
{
    private readonly CustomerChatRepository _repo;
    private readonly CustomerAppConsentRepository _consents;
    private readonly ICustomerPushService _push;
    private readonly IChatEventHub _events;
    private readonly IBranchAccessService _branchAccess;

    public CustomerChatService(
        CustomerChatRepository repo,
        CustomerAppConsentRepository consents,
        ICustomerPushService push,
        IChatEventHub events,
        IBranchAccessService branchAccess)
    {
        _repo = repo;
        _consents = consents;
        _push = push;
        _events = events;
        _branchAccess = branchAccess;
    }

    public async Task<CustomerChatThreadDto> GetThreadAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _repo.GetThreadAsync(tenantId, customerId, cancellationToken);
        if (thread is null)
        {
            return new CustomerChatThreadDto(Guid.Empty, 0, null, null);
        }

        return new CustomerChatThreadDto(
            thread.Id,
            thread.CustomerUnreadCount,
            thread.LastMessageAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(thread.LastMessageAt.Value, DateTimeKind.Utc))
                : null,
            thread.LastMessagePreview);
    }

    public async Task<CustomerChatMessageListResult> ListMessagesAsync(
        Guid tenantId,
        Guid customerId,
        Guid? beforeId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var thread = await EnsureThreadRowAsync(tenantId, customerId, cancellationToken);
        return await ListMessagesForThreadAsync(thread.Id, beforeId, limit, cancellationToken);
    }

    public async Task<CustomerChatMessageDto> SendCustomerMessageAsync(
        Guid tenantId,
        Guid customerId,
        SendCustomerChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = NormalizeBody(request.Body);
        if (!await _consents.HasGrantedConsentAsync(
                tenantId,
                customerId,
                CustomerConsentChannels.InApp,
                CustomerConsentPurposes.AiAssist,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Cần đồng ý chat dược sĩ trong app (mục Tài khoản) trước khi gửi tin nhắn.");
        }

        var thread = await EnsureThreadRowAsync(tenantId, customerId, cancellationToken);
        var customerName = await _repo.GetCustomerNameAsync(tenantId, customerId, cancellationToken);
        var row = await _repo.InsertMessageAsync(
            tenantId,
            thread.Id,
            CustomerChatSenderTypes.Customer,
            customerId,
            body,
            cancellationToken);

        _events.NotifyMessageSent(tenantId, customerId, row.Id, CustomerChatSenderTypes.Customer);
        return MapMessage(row with { SenderName = customerName });
    }

    public async Task MarkCustomerReadAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _repo.GetThreadAsync(tenantId, customerId, cancellationToken);
        if (thread is null) return;
        await _repo.MarkReadAsync(thread.Id, CustomerChatSenderTypes.Customer, cancellationToken);
        _events.NotifyRead(tenantId, customerId, CustomerChatSenderTypes.Customer);
    }

    public async Task<AdminChatThreadListResult> ListThreadsForStaffAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        Guid[]? allowed;
        try
        {
            (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // Staff chưa gán kho/chi nhánh: inbox rỗng thay vì 401 khiến app báo lỗi trùng.
            return new AdminChatThreadListResult([]);
        }

        var rows = await _repo.ListThreadsAsync(tenantId, allowed, cancellationToken);
        var items = rows.Select(row => new AdminChatThreadListItemDto(
            row.ThreadId,
            row.CustomerId,
            row.CustomerCode,
            row.CustomerName,
            row.CustomerPhone,
            row.StaffUnreadCount,
            row.LastMessageAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(row.LastMessageAt.Value, DateTimeKind.Utc))
                : null,
            row.LastMessagePreview)).ToList();
        return new AdminChatThreadListResult(items);
    }

    public async Task<AdminChatMessageListResult> ListMessagesForStaffAsync(
        Guid tenantId,
        Guid customerId,
        Guid? beforeId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var thread = await _repo.GetThreadAsync(tenantId, customerId, cancellationToken);
        if (thread is null)
            return new AdminChatMessageListResult([], false);

        await _branchAccess.EnsureWarehouseAccessAsync(thread.WarehouseId, cancellationToken);
        var result = await ListMessagesForThreadAsync(thread.Id, beforeId, limit, cancellationToken);
        return new AdminChatMessageListResult(result.Items, result.HasMore);
    }

    public async Task<CustomerChatMessageDto> SendStaffMessageAsync(
        Guid tenantId,
        Guid customerId,
        Guid staffUserId,
        string? staffName,
        SendStaffChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = NormalizeBody(request.Body);
        var thread = await EnsureThreadForStaffAsync(tenantId, customerId, cancellationToken);
        var row = await _repo.InsertMessageAsync(
            tenantId,
            thread.Id,
            CustomerChatSenderTypes.Staff,
            staffUserId,
            body,
            cancellationToken);

        var message = MapMessage(row with { SenderName = staffName });
        await _push.SendStaffChatReplyPushAsync(
            tenantId,
            customerId,
            staffName,
            body,
            cancellationToken);
        _events.NotifyMessageSent(tenantId, customerId, row.Id, CustomerChatSenderTypes.Staff);
        return message;
    }

    public async Task MarkStaffReadAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _repo.GetThreadAsync(tenantId, customerId, cancellationToken);
        if (thread is null) return;
        await _branchAccess.EnsureWarehouseAccessAsync(thread.WarehouseId, cancellationToken);
        await _repo.MarkReadAsync(thread.Id, CustomerChatSenderTypes.Staff, cancellationToken);
        _events.NotifyRead(tenantId, customerId, CustomerChatSenderTypes.Staff);
    }

    public Task SyncThreadWarehouseAsync(
        Guid tenantId,
        Guid customerId,
        Guid warehouseId,
        CancellationToken cancellationToken = default) =>
        _repo.SyncThreadWarehouseAsync(tenantId, customerId, warehouseId, cancellationToken);

    private async Task<ChatThreadRow> EnsureThreadForStaffAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var thread = await _repo.GetThreadAsync(tenantId, customerId, cancellationToken);
        if (thread is not null)
        {
            await _branchAccess.EnsureWarehouseAccessAsync(thread.WarehouseId, cancellationToken);
            return thread;
        }

        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);
        var warehouseId = await _repo.ResolveDefaultWarehouseIdAsync(tenantId, allowed, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy kho phù hợp với quyền chi nhánh.");
        var id = await _repo.EnsureThreadAsync(tenantId, customerId, warehouseId, cancellationToken);
        return new ChatThreadRow(id, warehouseId, 0, 0, null, null);
    }

    private async Task<ChatThreadRow> EnsureStaffThreadAccessAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var thread = await _repo.GetThreadAsync(tenantId, customerId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy hội thoại chat.");
        await _branchAccess.EnsureWarehouseAccessAsync(thread.WarehouseId, cancellationToken);
        return thread;
    }

    private async Task<ChatThreadRow> EnsureThreadRowAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var thread = await _repo.GetThreadAsync(tenantId, customerId, cancellationToken);
        if (thread is not null)
            return thread;

        var warehouseId = await _repo.ResolveDefaultWarehouseIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy kho mặc định.");
        var id = await _repo.EnsureThreadAsync(tenantId, customerId, warehouseId, cancellationToken);
        return new ChatThreadRow(id, warehouseId, 0, 0, null, null);
    }

    private async Task<CustomerChatMessageListResult> ListMessagesForThreadAsync(
        Guid threadId,
        Guid? beforeId,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await _repo.ListMessagesAsync(threadId, beforeId, limit, cancellationToken);
        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).ToList();
        page.Reverse();
        return new CustomerChatMessageListResult(page.Select(MapMessage).ToList(), hasMore);
    }

    private static CustomerChatMessageDto MapMessage(ChatMessageRow row) =>
        new(
            row.Id,
            row.SenderType,
            row.SenderName,
            row.Body,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
            row.ReadAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(row.ReadAt.Value, DateTimeKind.Utc))
                : null);

    private static string NormalizeBody(string? value)
    {
        var body = value?.Trim() ?? "";
        if (body.Length == 0)
            throw new InvalidOperationException("Nội dung tin nhắn không được để trống.");
        if (body.Length > 2000)
            throw new InvalidOperationException("Tin nhắn tối đa 2000 ký tự.");
        return body;
    }
}

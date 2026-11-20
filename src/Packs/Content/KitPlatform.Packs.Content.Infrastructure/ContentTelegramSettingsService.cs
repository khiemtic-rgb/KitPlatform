using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Đọc / lưu cấu hình Telegram cho admin và gửi tin thử. Bot token chỉ ghi: không bao giờ nằm trong DTO trả về,
/// không log.
/// </summary>
internal sealed class ContentTelegramSettingsService : IContentTelegramSettingsService
{
    private readonly ContentTelegramStore _store;
    private readonly ContentTelegramSender _sender;

    public ContentTelegramSettingsService(ContentTelegramStore store, ContentTelegramSender sender)
    {
        _store = store;
        _sender = sender;
    }

    public async Task<ContentTelegramSettingsDto> GetAsync(Guid brandId, CancellationToken ct = default)
    {
        await RequireBrandAsync(brandId, ct);
        return await MapAsync(brandId, ct);
    }

    public async Task<ContentTelegramSettingsDto> SaveAsync(Guid brandId, ContentTelegramSettingsRequest req, CancellationToken ct = default)
    {
        await RequireBrandAsync(brandId, ct);

        var chatId = string.IsNullOrWhiteSpace(req.ChatId) ? null : req.ChatId.Trim();
        if (chatId is not null && !ContentTelegramDefaults.IsValidChatId(chatId))
            throw new InvalidOperationException(ContentTelegramDefaults.ChatIdInvalid);

        string? token = null;
        if (req.BotToken is not null)
        {
            token = req.BotToken.Trim();
            if (token.Length > 0 && !ContentTelegramDefaults.IsValidToken(token))
                throw new InvalidOperationException(ContentTelegramDefaults.TokenInvalid);
        }

        string? secretRef = null;
        if (req.BotTokenSecretRef is not null)
        {
            secretRef = req.BotTokenSecretRef.Trim();
            if (secretRef.Length > 0 && !ContentTelegramDefaults.IsValidSecretRef(secretRef))
                throw new InvalidOperationException(ContentTelegramDefaults.SecretRefInvalid);
        }

        var row = new ContentTelegramBrandRow
        {
            BrandId = brandId,
            Enabled = req.Enabled,
            ChatId = chatId,
            NotifySuccess = req.NotifySuccess,
            NotifyLowStock = req.NotifyLowStock,
            NotifyFailure = req.NotifyFailure,
            NotifyProposal = req.NotifyProposal,
            LowStockRemaining = ContentTelegramDefaults.ClampRemaining(req.LowStockRemaining),
            LowStockEmptyDays = ContentTelegramDefaults.ClampEmptyDays(req.LowStockEmptyDays),
            QuietFromHour = ContentTelegramDefaults.ClampHour(req.QuietFromHour),
            QuietToHour = ContentTelegramDefaults.ClampHour(req.QuietToHour),
        };
        await _store.UpsertBrandAsync(row, ct);

        if (token is not null || secretRef is not null)
            await _store.SaveBotConfigAsync(token, secretRef, ct);

        return await MapAsync(brandId, ct);
    }

    public async Task<ContentTelegramTestResultDto> TestAsync(Guid brandId, CancellationToken ct = default)
    {
        var name = await RequireBrandAsync(brandId, ct);
        var row = await _store.GetBrandAsync(brandId, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.ChatId))
            throw new InvalidOperationException("Chưa lưu chat_id cho thương hiệu này.");

        var token = await _sender.ResolveTokenAsync(ct);
        if (token is null)
            throw new InvalidOperationException("Chưa có bot token.");

        var me = await _sender.GetMeAsync(token, ct);
        if (!me.Ok)
            return new ContentTelegramTestResultDto(false,
                me.StatusCode is null ? "Không gọi được Telegram. Thử lại sau." : "Token bot không dùng được.");

        var sent = await _sender.SendPlainAsync(token, row.ChatId!, ContentTelegramTemplates.TestPlain(name), ct);
        return new ContentTelegramTestResultDto(sent.Ok, ContentTelegramRetry.TestMessage(sent));
    }

    private async Task<string> RequireBrandAsync(Guid brandId, CancellationToken ct)
    {
        if (brandId == Guid.Empty) throw new InvalidOperationException("Thiếu thương hiệu.");
        return await _store.GetBrandNameAsync(brandId, ct)
               ?? throw new InvalidOperationException("Không tìm thấy thương hiệu.");
    }

    private async Task<ContentTelegramSettingsDto> MapAsync(Guid brandId, CancellationToken ct)
    {
        var bot = await _store.GetBotConfigAsync(ct);
        var configured = _sender.ResolveToken(bot) is not null;
        var r = await _store.GetBrandAsync(brandId, ct) ?? new ContentTelegramBrandRow { BrandId = brandId };
        return new ContentTelegramSettingsDto(
            configured,
            bot.SecretRef,
            brandId,
            r.Enabled,
            r.ChatId,
            r.NotifySuccess,
            r.NotifyLowStock,
            r.NotifyFailure,
            r.NotifyProposal,
            r.LowStockRemaining,
            r.LowStockEmptyDays,
            r.QuietFromHour,
            r.QuietToHour);
    }
}

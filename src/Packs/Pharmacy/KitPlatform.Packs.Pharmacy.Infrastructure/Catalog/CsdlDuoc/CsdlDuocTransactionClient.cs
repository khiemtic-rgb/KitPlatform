using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class CsdlDuocTransactionClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CsdlDuocTokenProvider _tokens;
    private readonly ILogger<CsdlDuocTransactionClient> _logger;

    public CsdlDuocTransactionClient(
        CsdlDuocTokenProvider tokens,
        ILogger<CsdlDuocTransactionClient> logger)
    {
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<(CsdlDuocTransactionCreateResponse? Body, int StatusCode, string Raw)> PostStockInAsync(
        CsdlDuocEffectiveCredentials credentials,
        CsdlDuocStockInRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        using var response = await SendAsync(credentials, HttpMethod.Post, "transactions/stock-in", json, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        CsdlDuocTransactionCreateResponse? body = null;
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(raw))
        {
            try { body = JsonSerializer.Deserialize<CsdlDuocTransactionCreateResponse>(raw, JsonOpts); }
            catch (Exception ex) { _logger.LogWarning(ex, "CSDL stock-in response parse failed"); }
        }
        else if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("CSDL stock-in HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(raw, 500));
        }

        return (body, (int)response.StatusCode, raw);
    }

    public async Task<(CsdlDuocTransactionStatusResponse? Body, int StatusCode, string Raw)> GetStockInStatusAsync(
        CsdlDuocEffectiveCredentials credentials,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var path = $"transactions/stock-in/{Uri.EscapeDataString(transactionId)}/status";
        using var response = await SendAsync(credentials, HttpMethod.Get, path, bodyJson: null, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        CsdlDuocTransactionStatusResponse? body = null;
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(raw))
        {
            try { body = JsonSerializer.Deserialize<CsdlDuocTransactionStatusResponse>(raw, JsonOpts); }
            catch { /* ignore */ }
        }

        return (body, (int)response.StatusCode, raw);
    }

    public async Task<(CsdlDuocTransactionCreateResponse? Body, int StatusCode, string Raw)> PostStockOutAsync(
        CsdlDuocEffectiveCredentials credentials,
        CsdlDuocStockOutRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        using var response = await SendAsync(credentials, HttpMethod.Post, "transactions/stock-out", json, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        CsdlDuocTransactionCreateResponse? body = null;
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(raw))
        {
            try { body = JsonSerializer.Deserialize<CsdlDuocTransactionCreateResponse>(raw, JsonOpts); }
            catch (Exception ex) { _logger.LogWarning(ex, "CSDL stock-out response parse failed"); }
        }
        else if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("CSDL stock-out HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(raw, 500));
        }

        return (body, (int)response.StatusCode, raw);
    }

    public async Task<(CsdlDuocTransactionStatusResponse? Body, int StatusCode, string Raw)> GetStockOutStatusAsync(
        CsdlDuocEffectiveCredentials credentials,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var path = $"transactions/stock-out/{Uri.EscapeDataString(transactionId)}/status";
        using var response = await SendAsync(credentials, HttpMethod.Get, path, bodyJson: null, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        CsdlDuocTransactionStatusResponse? body = null;
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(raw))
        {
            try { body = JsonSerializer.Deserialize<CsdlDuocTransactionStatusResponse>(raw, JsonOpts); }
            catch { /* ignore */ }
        }

        return (body, (int)response.StatusCode, raw);
    }

    public async Task<string?> ResolveUnitIdAsync(
        CsdlDuocEffectiveCredentials credentials,
        string drugId,
        string fallbackUnitId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(drugId)) return fallbackUnitId;
        var path = $"master/drugs/{Uri.EscapeDataString(drugId.Trim())}";
        using var response = await SendAsync(credentials, HttpMethod.Get, path, bodyJson: null, cancellationToken);
        if (!response.IsSuccessStatusCode) return fallbackUnitId;
        var drug = await response.Content.ReadFromJsonAsync<CsdlDuocDrugDto>(JsonOpts, cancellationToken);
        var unit = drug?.Packagings?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.UnitId))?.UnitId;
        return string.IsNullOrWhiteSpace(unit) ? fallbackUnitId : unit!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        CsdlDuocEffectiveCredentials credentials,
        HttpMethod method,
        string relativeUrl,
        string? bodyJson,
        CancellationToken cancellationToken)
    {
        async Task<HttpResponseMessage> OnceAsync()
        {
            var client = await _tokens.CreateAuthorizedClientAsync(credentials, cancellationToken);
            var request = new HttpRequestMessage(method, relativeUrl);
            if (bodyJson is not null)
                request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            return await client.SendAsync(request, cancellationToken);
        }

        var response = await OnceAsync();
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        _tokens.Invalidate(credentials.Username);
        return await OnceAsync();
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= max ? value
        : value[..max] + "…";
}

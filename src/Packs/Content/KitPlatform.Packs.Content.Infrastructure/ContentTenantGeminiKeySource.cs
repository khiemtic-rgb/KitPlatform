using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Khoá Gemini dự phòng cho phần viết bài: khi "Cấu hình AI viết bài" chưa có khoá, dùng khoá Gemini
/// đã lưu ở Cài đặt AI (production credential, mã hoá AES-GCM) của tenant.
/// Chạy được cả trong job nền (không có người đăng nhập): chỉ nhận khi đúng một tenant có khoá Gemini đang dùng.
/// Không ghi log khoá, chỉ ghi dấu vân tay ngắn.
/// </summary>
internal sealed class ContentTenantGeminiKeySource
{
    private readonly IDbConnectionFactory _db;
    private readonly IProductionCredentialKeyRing _keys;
    private readonly ILogger<ContentTenantGeminiKeySource> _log;

    public ContentTenantGeminiKeySource(
        IDbConnectionFactory db,
        IProductionCredentialKeyRing keys,
        ILogger<ContentTenantGeminiKeySource> log)
    {
        _db = db;
        _keys = keys;
        _log = log;
    }

    public async Task<(string Key, string Fingerprint)?> TryGetAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            var rows = (await conn.QueryAsync<Row>(new CommandDefinition("""
                SELECT v.tenant_id AS TenantId, v.credential_version_id AS VersionId,
                       EXISTS (SELECT 1 FROM pack_content.production_tenant_ai_config a
                               WHERE a.tenant_id = v.tenant_id AND a.credential_id = v.credential_id AND a.enabled) AS InUse
                FROM pack_content.production_credential_versions v
                JOIN pack_content.production_credentials c
                  ON c.credential_id = v.credential_id AND c.tenant_id = v.tenant_id
                WHERE c.status = 'ACTIVE' AND v.status = 'ACTIVE'
                  AND (lower(v.provider_id) LIKE '%gemini%' OR lower(v.provider_id) LIKE 'google%')
                ORDER BY InUse DESC, v.created_at DESC
                """, cancellationToken: ct))).ToList();
            if (rows.Count == 0) return null;

            var tenants = rows.Select(r => r.TenantId).Distinct().ToList();
            if (tenants.Count > 1)
            {
                // Nhiều tenant: chỉ nhận khi đúng một tenant đang dùng khoá trong Cài đặt AI.
                var inUse = rows.Where(r => r.InUse).Select(r => r.TenantId).Distinct().ToList();
                if (inUse.Count != 1)
                {
                    _log.LogWarning("Content Gemini fallback: {Count} tenants have Gemini keys; not choosing one.", tenants.Count);
                    return null;
                }
                rows = rows.Where(r => r.TenantId == inUse[0]).ToList();
            }

            var pick = rows[0];
            var secrets = new ProductionCredentialSecretRepository(_db, _keys, new FixedCaller(pick.TenantId));
            var key = (await secrets.OpenAsync(pick.VersionId, ct))?.Trim();
            if (string.IsNullOrEmpty(key)) return null;
            var fingerprint = "tenant:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..8].ToLowerInvariant();
            return (key, fingerprint);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning("Content Gemini fallback from AI settings failed: {Message}", ex.Message);
            return null;
        }
    }

    private sealed class FixedCaller(Guid tenant) : IProductionCallerTenant
    {
        public Guid RequireTenant() => tenant;
    }

    private sealed class Row
    {
        public Guid TenantId { get; set; }
        public Guid VersionId { get; set; }
        public bool InUse { get; set; }
    }
}

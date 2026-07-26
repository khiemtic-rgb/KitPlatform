using System.Data;
using KitPlatform.Application.Auth;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Auth;

internal sealed class KitAccountService : IKitAccountService
{
    private const short ActiveStatus = 1;

    private readonly KitAccountRepository _repository;
    private readonly IDbConnectionFactory _db;

    public KitAccountService(KitAccountRepository repository, IDbConnectionFactory db)
    {
        _repository = repository;
        _db = db;
    }

    public async Task AssertEmailPasswordCompatibleAsync(
        string email,
        string plaintextPassword,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        var existing = await _repository.FindByEmailAsync(normalized, null, null, cancellationToken);
        if (existing is null || existing.Status != ActiveStatus)
            return;

        if (!BCrypt.Net.BCrypt.Verify(plaintextPassword, existing.PasswordHash))
        {
            throw new InvalidOperationException(
                "Email đã có tài khoản KitPlatform. Dùng đúng mật khẩu hiện tại để thêm sản phẩm/workspace, hoặc đăng nhập bằng email.");
        }
    }

    public async Task<Guid> EnsureAccountForUserAsync(
        Guid userId,
        Guid tenantId,
        string email,
        string plaintextPassword,
        string passwordHash,
        string? displayName,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
            throw new InvalidOperationException("Email không hợp lệ.");

        if (connection is not null)
        {
            return await EnsureCoreAsync(
                userId, tenantId, normalized, plaintextPassword, passwordHash, displayName,
                connection, transaction, cancellationToken);
        }

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var accountId = await EnsureCoreAsync(
            userId, tenantId, normalized, plaintextPassword, passwordHash, displayName,
            conn, tx, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return accountId;
    }

    private async Task<Guid> EnsureCoreAsync(
        Guid userId,
        Guid tenantId,
        string email,
        string plaintextPassword,
        string passwordHash,
        string? displayName,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.FindByEmailAsync(email, connection, transaction, cancellationToken);
        Guid accountId;

        if (existing is null)
        {
            accountId = await _repository.InsertAccountAsync(
                email, passwordHash, displayName, connection, transaction, cancellationToken);
        }
        else
        {
            if (existing.Status != ActiveStatus)
                throw new InvalidOperationException("Tài khoản KitPlatform đã bị khóa.");

            if (!BCrypt.Net.BCrypt.Verify(plaintextPassword, existing.PasswordHash))
            {
                throw new InvalidOperationException(
                    "Email đã có tài khoản KitPlatform. Dùng đúng mật khẩu hiện tại để thêm sản phẩm/workspace.");
            }

            accountId = existing.Id;

            var existingUserInTenant = await _repository.FindMembershipUserIdAsync(
                accountId, tenantId, connection, transaction, cancellationToken);
            if (existingUserInTenant is Guid otherUserId && otherUserId != userId)
            {
                throw new InvalidOperationException(
                    "Tài khoản KitPlatform này đã thuộc workspace/đơn vị hiện tại. Đăng nhập bằng email thay vì tạo lại.");
            }
        }

        var vertical = await _repository.GetTenantVerticalAsync(tenantId, connection, transaction, cancellationToken);
        var productCode = KitAccountRepository.MapProductCode(vertical);
        var isDefault = existing is null;

        await _repository.UpsertMembershipAsync(
            accountId, tenantId, userId, productCode, isDefault, connection, transaction, cancellationToken);
        return accountId;
    }

    public async Task SyncPasswordForUserAsync(
        Guid userId,
        string plaintextPassword,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword) || string.IsNullOrWhiteSpace(passwordHash))
            return;

        var accountId = await _repository.FindAccountIdByUserIdAsync(userId, cancellationToken);
        if (accountId is null)
            return;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await _repository.UpdateAccountPasswordAsync(
            accountId.Value,
            passwordHash,
            conn,
            tx,
            cancellationToken);

        await _repository.SyncMembershipUserPasswordsAsync(
            accountId.Value,
            passwordHash,
            conn,
            tx,
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

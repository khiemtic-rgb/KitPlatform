using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure.Auth;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerAppAuthService : ICustomerAppAuthService
{
    private readonly CustomerAppAuthRepository _repo;
    private readonly CustomerAppJwtTokenService _tokens;
    private readonly CustomerAppAuthSettings _settings;
    private readonly ICustomerOtpSender _otpSender;
    private readonly IHostEnvironment _env;
    private readonly ILogger<CustomerAppAuthService> _logger;

    public CustomerAppAuthService(
        CustomerAppAuthRepository repo,
        CustomerAppJwtTokenService tokens,
        IOptions<CustomerAppAuthSettings> settings,
        ICustomerOtpSender otpSender,
        IHostEnvironment env,
        ILogger<CustomerAppAuthService> logger)
    {
        _repo = repo;
        _tokens = tokens;
        _settings = settings.Value;
        _otpSender = otpSender;
        _env = env;
        _logger = logger;
    }

    public async Task<CustomerOtpSentResponse> RequestOtpAsync(
        RequestCustomerOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantCode = ResolveTenantCode(request.TenantCode)
            ?? throw new InvalidOperationException("Mã nhà thuốc là bắt buộc.");
        var phone = CustomerAppAuthRepository.NormalizePhone(request.Phone);
        if (phone.Length < 9)
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");

        var tenant = await _repo.ResolveTenantAsync(tenantCode, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy nhà thuốc hoặc nhà thuốc đã ngừng hoạt động.");

        var account = await _repo.EnsureAccountForCustomerPhoneAsync(
            tenant.TenantId, tenant.TenantCode, phone, cancellationToken)
            ?? throw new InvalidOperationException(
                "Số điện thoại chưa được đăng ký tại nhà thuốc. Vui lòng liên hệ quầy.");

        var lastCreated = await _repo.GetLatestOtpCreatedAtAsync(tenant.TenantId, phone, cancellationToken);
        if (lastCreated.HasValue)
        {
            var elapsed = DateTime.UtcNow - lastCreated.Value.ToUniversalTime();
            if (elapsed.TotalSeconds < _settings.OtpCooldownSeconds)
            {
                var wait = _settings.OtpCooldownSeconds - (int)elapsed.TotalSeconds;
                throw new InvalidOperationException($"Vui lòng đợi {wait}s trước khi gửi lại mã OTP.");
            }
        }

        var code = CustomerAppAuthRepository.GenerateOtpCode();
        if (_env.IsDevelopment() && !string.IsNullOrWhiteSpace(_settings.DevBypassCode))
            code = _settings.DevBypassCode.Trim();

        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.OtpExpireMinutes);
        await _repo.InsertOtpChallengeAsync(
            tenant.TenantId,
            phone,
            CustomerAppAuthRepository.HashOtp(code),
            expiresAt,
            cancellationToken);

        await _otpSender.SendOtpAsync(
            phone,
            tenant.TenantCode,
            code,
            _settings.OtpExpireMinutes,
            cancellationToken);

        _logger.LogInformation(
            "Customer OTP requested for {Phone} (tenant {Tenant}, account {AccountId})",
            phone,
            tenant.TenantCode,
            account.AccountId);

        var message = _env.IsDevelopment()
            ? "Đã gửi mã OTP (dev: xem log API hoặc dùng mã bypass)."
            : "Đã gửi mã OTP qua SMS. Vui lòng kiểm tra tin nhắn.";

        return new CustomerOtpSentResponse(
            _settings.OtpExpireMinutes * 60,
            _settings.OtpCooldownSeconds,
            message);
    }

    public async Task<CustomerLoginResponse?> VerifyOtpAsync(
        VerifyCustomerOtpRequest request,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        var tenantCode = ResolveTenantCode(request.TenantCode);
        if (tenantCode is null)
            return null;

        var phone = CustomerAppAuthRepository.NormalizePhone(request.Phone);
        var code = request.Code.Trim();

        var tenant = await _repo.ResolveTenantAsync(tenantCode, cancellationToken);
        if (tenant is null)
            return null;

        var challenge = await _repo.GetActiveOtpChallengeAsync(tenant.TenantId, phone, cancellationToken);
        if (challenge is null)
            return null;

        if (challenge.AttemptCount >= _settings.MaxVerifyAttempts)
            return null;

        var bypass = _env.IsDevelopment()
            && !string.IsNullOrWhiteSpace(_settings.DevBypassCode)
            && code == _settings.DevBypassCode.Trim();

        var hash = CustomerAppAuthRepository.HashOtp(code);
        if (!bypass && !string.Equals(challenge.CodeHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            await _repo.IncrementOtpAttemptAsync(challenge.Id, cancellationToken);
            return null;
        }

        var account = await _repo.FindAccountByPhoneAsync(tenant.TenantId, phone, cancellationToken);
        if (account is null)
            return null;

        await _repo.ConsumeOtpChallengeAsync(challenge.Id, cancellationToken);
        await _repo.MarkAccountVerifiedAsync(account.AccountId, cancellationToken);

        return await IssueTokensAsync(account, cancellationToken);
    }

    public async Task<CustomerLoginResponse?> RefreshAsync(
        CustomerRefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return null;

        var hash = JwtTokenService.HashToken(request.RefreshToken);
        var accountId = await _repo.FindAccountIdByRefreshTokenHashAsync(hash, cancellationToken);
        if (accountId is null)
            return null;

        var account = await _repo.FindAccountByIdAsync(accountId.Value, cancellationToken);
        if (account is null)
            return null;

        await _repo.RevokeRefreshTokenAsync(hash, cancellationToken);
        return await IssueTokensAsync(account, cancellationToken);
    }

    public async Task<CustomerProfileDto?> GetProfileAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _repo.FindAccountByIdAsync(accountId, cancellationToken);
        return account is null ? null : ToProfile(account);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var hash = JwtTokenService.HashToken(refreshToken);
        await _repo.RevokeRefreshTokenAsync(hash, cancellationToken);
    }

    private async Task<CustomerLoginResponse> IssueTokensAsync(
        CustomerAccountRecord account,
        CancellationToken cancellationToken)
    {
        var (accessToken, expiresAt) = _tokens.CreateAccessToken(account);
        var refreshToken = JwtTokenService.GenerateRefreshToken();
        var refreshHash = JwtTokenService.HashToken(refreshToken);
        var refreshExpiry = _tokens.GetRefreshTokenExpiry();

        await _repo.StoreRefreshTokenAsync(account.AccountId, refreshHash, refreshExpiry, cancellationToken);

        return new CustomerLoginResponse(accessToken, refreshToken, expiresAt, ToProfile(account));
    }

    private static CustomerProfileDto ToProfile(CustomerAccountRecord account) =>
        new(account.AccountId, account.CustomerId, account.TenantId, account.TenantCode, account.FullName, account.Phone);

    private string? ResolveTenantCode(string? tenantCode)
    {
        if (!string.IsNullOrWhiteSpace(tenantCode))
            return tenantCode.Trim();

        return _env.IsDevelopment() ? "DEMO_PHARMACY" : null;
    }
}

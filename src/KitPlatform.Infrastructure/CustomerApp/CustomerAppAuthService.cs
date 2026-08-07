using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Infrastructure.Auth;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerAppAuthService : ICustomerAppAuthService
{
    private readonly CustomerAppAuthRepository _repo;
    private readonly CustomerAppJwtTokenService _tokens;
    private readonly CustomerAppAuthSettings _settings;
    private readonly ICustomerOtpSender _otpSender;
    private readonly ICustomerEngagementEventService _engagementEvents;
    private readonly IHostEnvironment _env;
    private readonly ILogger<CustomerAppAuthService> _logger;
    private readonly string? _configuredDefaultTenantCode;

    public CustomerAppAuthService(
        CustomerAppAuthRepository repo,
        CustomerAppJwtTokenService tokens,
        IOptions<CustomerAppAuthSettings> settings,
        ICustomerOtpSender otpSender,
        ICustomerEngagementEventService engagementEvents,
        IHostEnvironment env,
        IConfiguration configuration,
        ILogger<CustomerAppAuthService> logger)
    {
        _repo = repo;
        _tokens = tokens;
        _settings = settings.Value;
        _otpSender = otpSender;
        _engagementEvents = engagementEvents;
        _env = env;
        _logger = logger;
        _configuredDefaultTenantCode = configuration["Auth:DefaultTenantCode"]?.Trim();
        if (string.IsNullOrWhiteSpace(_configuredDefaultTenantCode))
            _configuredDefaultTenantCode = configuration["Assessment:EventTenantCode"]?.Trim();
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

        var channel = (request.Channel ?? CustomerAppOtpChannels.Remote).Trim().ToLowerInvariant();
        if (channel is not (CustomerAppOtpChannels.Counter or CustomerAppOtpChannels.Remote))
            throw new InvalidOperationException("Kênh đăng nhập không hợp lệ.");

        var authCfg = await _repo.GetTenantAppAuthAsync(tenant.TenantId, cancellationToken);

        if (channel == CustomerAppOtpChannels.Counter)
            return await RequestCounterOtpAsync(tenant, phone, request.CounterPin, authCfg, cancellationToken);

        return await RequestRemoteOtpAsync(tenant, phone, request.InviteCode, authCfg, cancellationToken);
    }

    private async Task<CustomerOtpSentResponse> RequestCounterOtpAsync(
        TenantPhoneRow tenant,
        string phone,
        string? counterPin,
        TenantCustomerAppAuthRow? authCfg,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authCfg?.CounterPinHash))
            throw new InvalidOperationException(
                "Nhà thuốc chưa cấu hình mã quầy. Vui lòng nhờ nhân viên đăng ký giúp.");

        var pin = (counterPin ?? string.Empty).Trim();
        if (pin.Length == 0)
            throw new InvalidOperationException("Vui lòng nhập mã quầy.");

        if (!string.Equals(
                CustomerAppAuthRepository.HashSecret(pin),
                authCfg.CounterPinHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Mã quầy không đúng.");
        }

        var account = await _repo.EnsureAccountForCustomerPhoneAsync(
            tenant.TenantId, tenant.TenantCode, phone, cancellationToken)
            ?? throw new InvalidOperationException(
                "Số điện thoại chưa có trên hệ thống nhà thuốc. Nhờ nhân viên tạo khách tại quầy trước.");

        await EnforceOtpCooldownAsync(tenant.TenantId, phone, cancellationToken);
        var (code, expiresAt, challengeId) = await CreateOtpChallengeAsync(
            tenant, phone, exposeOnCustomerApp: true, cancellationToken);

        await _repo.InsertLoginRequestAsync(
            tenant.TenantId,
            phone,
            account.CustomerId,
            CustomerAppOtpChannels.Counter,
            CustomerAppLoginRequestStatuses.Approved,
            referralCodeUsed: null,
            challengeId,
            cancellationToken);

        _logger.LogInformation(
            "Counter OTP issued for {Phone} (tenant {Tenant}, account {AccountId})",
            phone,
            tenant.TenantCode,
            account.AccountId);

        return new CustomerOtpSentResponse(
            _settings.OtpExpireMinutes * 60,
            _settings.OtpCooldownSeconds,
            "Mã đăng nhập hiển thị bên dưới. Không chia sẻ cho người khác.",
            code,
            CustomerAppOtpResponseStatuses.OtpSent);
    }

    private async Task<CustomerOtpSentResponse> RequestRemoteOtpAsync(
        TenantPhoneRow tenant,
        string phone,
        string? inviteCode,
        TenantCustomerAppAuthRow? authCfg,
        CancellationToken cancellationToken)
    {
        var pending = await _repo.FindPendingLoginRequestAsync(tenant.TenantId, phone, cancellationToken);
        if (pending is not null)
        {
            return new CustomerOtpSentResponse(
                0,
                _settings.OtpCooldownSeconds,
                "Yêu cầu đang chờ nhà thuốc xác nhận. Nhân viên sẽ gọi và gửi mã đăng nhập.",
                PilotCode: null,
                CustomerAppOtpResponseStatuses.PendingApproval);
        }

        var existing = await _repo.FindCustomerByPhoneAsync(tenant.TenantId, phone, cancellationToken);
        Guid customerId;
        string? referralUsed = null;

        if (existing is not null)
        {
            customerId = existing.CustomerId;
        }
        else
        {
            var normalizedInvite = CustomerAppAuthRepository.NormalizeInviteCode(inviteCode);
            if (string.IsNullOrWhiteSpace(authCfg?.InviteCodeHash) || string.IsNullOrWhiteSpace(normalizedInvite))
            {
                throw new InvalidOperationException(
                    "Số chưa có trên hệ thống. Nhập mã giới thiệu từ nhà thuốc hoặc đăng ký tại quầy.");
            }

            if (!string.Equals(
                    CustomerAppAuthRepository.HashSecret(normalizedInvite),
                    authCfg.InviteCodeHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Mã giới thiệu không đúng.");
            }

            var fullName = $"Khách app {phone[^4..]}";
            customerId = await _repo.CreateProspectCustomerAsync(
                tenant.TenantId, phone, fullName, cancellationToken);
            referralUsed = normalizedInvite;
        }

        await _repo.EnsureAccountForCustomerPhoneAsync(
            tenant.TenantId, tenant.TenantCode, phone, cancellationToken);

        await _repo.InsertLoginRequestAsync(
            tenant.TenantId,
            phone,
            customerId,
            CustomerAppOtpChannels.Remote,
            CustomerAppLoginRequestStatuses.Pending,
            referralUsed,
            otpChallengeId: null,
            cancellationToken);

        _logger.LogInformation(
            "Remote login request pending for {Phone} (tenant {Tenant}, customer {CustomerId})",
            phone,
            tenant.TenantCode,
            customerId);

        return new CustomerOtpSentResponse(
            0,
            _settings.OtpCooldownSeconds,
            "Đã gửi yêu cầu. Nhà thuốc sẽ gọi xác nhận và gửi mã đăng nhập (Zalo/tin nhắn).",
            PilotCode: null,
            CustomerAppOtpResponseStatuses.PendingApproval);
    }

    private async Task EnforceOtpCooldownAsync(Guid tenantId, string phone, CancellationToken cancellationToken)
    {
        var lastCreated = await _repo.GetLatestOtpCreatedAtAsync(tenantId, phone, cancellationToken);
        if (!lastCreated.HasValue)
            return;

        var elapsed = DateTime.UtcNow - lastCreated.Value.ToUniversalTime();
        if (elapsed.TotalSeconds < _settings.OtpCooldownSeconds)
        {
            var wait = _settings.OtpCooldownSeconds - (int)elapsed.TotalSeconds;
            throw new InvalidOperationException($"Vui lòng đợi {wait}s trước khi gửi lại mã OTP.");
        }
    }

    internal async Task<(string Code, DateTime ExpiresAt, Guid ChallengeId)> CreateOtpChallengeAsync(
        TenantPhoneRow tenant,
        string phone,
        bool exposeOnCustomerApp,
        CancellationToken cancellationToken)
    {
        var code = CustomerAppAuthRepository.GenerateOtpCode();
        if (_env.IsDevelopment() && !string.IsNullOrWhiteSpace(_settings.DevBypassCode))
            code = _settings.DevBypassCode.Trim();

        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.OtpExpireMinutes);
        var storePilot =
            _settings.ExposePilotOtpInAdmin
            || _settings.ExposePilotOtpOnCustomerApp
            || exposeOnCustomerApp;
        var pilotCode = storePilot ? code : null;
        var challengeId = await _repo.InsertOtpChallengeAsync(
            tenant.TenantId,
            phone,
            CustomerAppAuthRepository.HashOtp(code),
            expiresAt,
            pilotCode,
            cancellationToken);

        try
        {
            await _otpSender.SendOtpAsync(
                phone,
                tenant.TenantCode,
                code,
                _settings.OtpExpireMinutes,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Pilot without SMS gateway: still keep challenge for admin/counter display.
            _logger.LogWarning(ex, "OTP SMS send failed for {Phone}; continuing with pilot display", phone);
        }

        return (code, expiresAt, challengeId);
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
        await _engagementEvents.TryRecordDailyAppOpenAsync(
            tenant.TenantId,
            account.AccountId,
            account.CustomerId,
            cancellationToken);

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

    public async Task<CustomerProfileDto?> UpdatePreferredLocaleAsync(
        Guid accountId,
        string preferredLocale,
        CancellationToken cancellationToken = default)
    {
        var locale = preferredLocale?.Trim();
        if (string.IsNullOrWhiteSpace(locale))
            throw new InvalidOperationException("Ngôn ngữ không hợp lệ.");

        var updated = await _repo.UpdatePreferredLocaleAsync(accountId, locale, cancellationToken);
        if (!updated)
            throw new InvalidOperationException("Ngôn ngữ không được hỗ trợ hoặc tài khoản không tồn tại.");

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
        new(
            account.AccountId,
            account.CustomerId,
            account.TenantId,
            account.TenantCode,
            account.FullName,
            account.Phone,
            account.PreferredLocale);

    private string? ResolveTenantCode(string? tenantCode)
    {
        if (!string.IsNullOrWhiteSpace(tenantCode))
            return tenantCode.Trim();

        if (!string.IsNullOrWhiteSpace(_configuredDefaultTenantCode))
            return _configuredDefaultTenantCode;

        return _env.IsDevelopment() ? "DEMO_PHARMACY" : null;
    }
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerAppLoginAdminService : ICustomerAppLoginAdminService
{
    private readonly CustomerAppAuthRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly CustomerAppAuthSettings _settings;
    private readonly CustomerAppSmsSettings _smsSettings;
    private readonly ICustomerOtpSender _otpSender;
    private readonly IHostEnvironment _env;
    private readonly ILogger<CustomerAppLoginAdminService> _logger;

    public CustomerAppLoginAdminService(
        CustomerAppAuthRepository repo,
        ITenantContext tenant,
        IOptions<CustomerAppAuthSettings> settings,
        IOptions<CustomerAppSmsSettings> smsSettings,
        ICustomerOtpSender otpSender,
        IHostEnvironment env,
        ILogger<CustomerAppLoginAdminService> logger)
    {
        _repo = repo;
        _tenant = tenant;
        _settings = settings.Value;
        _smsSettings = smsSettings.Value;
        _otpSender = otpSender;
        _env = env;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomerAppLoginRequestDto>> ListAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListLoginRequestsAsync(_tenant.TenantId, status, cancellationToken);
        return rows.Select(r => new CustomerAppLoginRequestDto(
            r.Id,
            r.Phone,
            r.CustomerId,
            r.CustomerName,
            r.Channel,
            r.Status,
            r.ReferralCodeUsed,
            new DateTimeOffset(DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc)),
            r.ReviewedAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(r.ReviewedAt.Value, DateTimeKind.Utc)),
            r.RejectReason)).ToList();
    }

    public async Task<ApproveCustomerAppLoginResult> ApproveAsync(
        Guid requestId,
        Guid reviewedByUserId,
        CancellationToken cancellationToken = default)
    {
        var req = await _repo.GetLoginRequestAsync(_tenant.TenantId, requestId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy yêu cầu.");
        if (!string.Equals(req.Status, CustomerAppLoginRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yêu cầu không còn ở trạng thái chờ duyệt.");

        var tenant = await _repo.ResolveTenantByIdAsync(_tenant.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant không hợp lệ.");

        _ = await _repo.EnsureAccountForCustomerPhoneAsync(
            tenant.TenantId, tenant.TenantCode, req.Phone, cancellationToken)
            ?? throw new InvalidOperationException("Không tạo được tài khoản app cho số điện thoại này.");

        var (code, expiresAt, challengeId) = await IssueOtpAsync(tenant, req.Phone, cancellationToken);
        await _repo.MarkLoginRequestApprovedAsync(requestId, challengeId, reviewedByUserId, cancellationToken);

        _logger.LogInformation(
            "Approved remote app login {RequestId} for {Phone} by {UserId}",
            requestId,
            req.Phone,
            reviewedByUserId);

        return new ApproveCustomerAppLoginResult(
            requestId,
            req.CustomerId ?? Guid.Empty,
            req.Phone,
            _settings.ExposePilotOtpInAdmin ? code : null,
            new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
            "Đã duyệt. Gọi/Zalo gửi mã OTP cho khách.");
    }

    public async Task RejectAsync(
        Guid requestId,
        Guid reviewedByUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var req = await _repo.GetLoginRequestAsync(_tenant.TenantId, requestId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy yêu cầu.");
        if (!string.Equals(req.Status, CustomerAppLoginRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yêu cầu không còn ở trạng thái chờ duyệt.");

        await _repo.MarkLoginRequestRejectedAsync(requestId, reviewedByUserId, reason, cancellationToken);
    }

    public async Task<CustomerAppAuthSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetTenantAppAuthAsync(_tenant.TenantId, cancellationToken);
        return new CustomerAppAuthSettingsDto(
            !string.IsNullOrWhiteSpace(row?.CounterPinHash),
            !string.IsNullOrWhiteSpace(row?.InviteCodeHash),
            row?.InviteCodeHint);
    }

    public async Task<CustomerAppAuthSettingsDto> UpdateSettingsAsync(
        UpdateCustomerAppAuthSettingsRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        string? pinHash = null;
        string? inviteHash = null;
        string? inviteHint = null;

        if (!request.ClearCounterPin && !string.IsNullOrWhiteSpace(request.CounterPin))
        {
            var pin = request.CounterPin.Trim();
            if (pin.Length < 4 || pin.Length > 12)
                throw new InvalidOperationException("Mã quầy cần 4–12 ký tự.");
            pinHash = CustomerAppAuthRepository.HashSecret(pin);
        }

        if (!request.ClearInviteCode && !string.IsNullOrWhiteSpace(request.InviteCode))
        {
            var invite = CustomerAppAuthRepository.NormalizeInviteCode(request.InviteCode);
            if (invite.Length < 4 || invite.Length > 24)
                throw new InvalidOperationException("Mã mời cần 4–24 ký tự.");
            inviteHash = CustomerAppAuthRepository.HashSecret(invite);
            inviteHint = invite;
        }

        await _repo.UpsertTenantAppAuthAsync(
            _tenant.TenantId,
            pinHash,
            inviteHash,
            inviteHint,
            request.ClearCounterPin,
            request.ClearInviteCode,
            updatedByUserId,
            cancellationToken);

        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<IssueCounterPilotOtpResult> IssueCounterOtpAsync(
        IssueCounterPilotOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var phone = CustomerAppAuthRepository.NormalizePhone(request.Phone);
        if (phone.Length < 9)
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");

        var tenant = await _repo.ResolveTenantByIdAsync(_tenant.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant không hợp lệ.");

        var existing = await _repo.FindCustomerByPhoneAsync(tenant.TenantId, phone, cancellationToken);
        Guid customerId;
        if (existing is null)
        {
            var name = string.IsNullOrWhiteSpace(request.FullName)
                ? $"Khách quầy {phone[^4..]}"
                : request.FullName.Trim();
            // Counter-created customers are members (staff saw them).
            customerId = await _repo.CreateProspectCustomerAsync(tenant.TenantId, phone, name, cancellationToken);
            await _repo.MarkCustomerAsCounterMemberAsync(tenant.TenantId, customerId, cancellationToken);
        }
        else
        {
            customerId = existing.CustomerId;
            await _repo.MarkCustomerAsCounterMemberAsync(tenant.TenantId, customerId, cancellationToken);
        }

        await _repo.EnsureAccountForCustomerPhoneAsync(
            tenant.TenantId, tenant.TenantCode, phone, cancellationToken);

        var (code, expiresAt, challengeId) = await IssueOtpAsync(tenant, phone, cancellationToken);
        await _repo.InsertLoginRequestAsync(
            tenant.TenantId,
            phone,
            customerId,
            CustomerAppOtpChannels.Counter,
            CustomerAppLoginRequestStatuses.Approved,
            null,
            challengeId,
            cancellationToken);

        return new IssueCounterPilotOtpResult(
            customerId,
            phone,
            _settings.ExposePilotOtpInAdmin ? code : null,
            new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
            "Đã tạo mã OTP tại quầy. Đọc mã cho khách nhập trên app.");
    }

    private async Task<(string Code, DateTime ExpiresAt, Guid ChallengeId)> IssueOtpAsync(
        TenantPhoneRow tenant,
        string phone,
        CancellationToken cancellationToken)
    {
        var code = CustomerAppAuthRepository.GenerateOtpCode();
        if (_env.IsDevelopment() && !string.IsNullOrWhiteSpace(_settings.DevBypassCode))
            code = _settings.DevBypassCode.Trim();

        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.OtpExpireMinutes);
        var storePilot = _settings.ExposePilotOtpInAdmin || _settings.ExposePilotOtpOnCustomerApp;
        var challengeId = await _repo.InsertOtpChallengeAsync(
            tenant.TenantId,
            phone,
            CustomerAppAuthRepository.HashOtp(code),
            expiresAt,
            storePilot ? code : null,
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
            if (_smsSettings.Provider.Equals("Http", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(ex, "OTP SMS gateway failed for {Phone} during admin issue", phone);
                throw new InvalidOperationException(
                    "Không gửi được SMS OTP. Thử lại hoặc đọc mã trên Admin/POS nếu đã tạo.");
            }

            _logger.LogWarning(ex, "OTP SMS skipped/failed for {Phone} during admin issue (staff-read)", phone);
        }

        return (code, expiresAt, challengeId);
    }
}

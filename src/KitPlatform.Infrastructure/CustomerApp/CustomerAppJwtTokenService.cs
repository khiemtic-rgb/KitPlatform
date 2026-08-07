using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.CustomerApp;
using System.IdentityModel.Tokens.Jwt;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerAppJwtTokenService
{
    public const string TokenTypeClaim = CustomerAppAuthConstants.TokenTypeClaim;
    public const string TokenTypeValue = CustomerAppAuthConstants.TokenTypeValue;
    public const string CustomerIdClaim = CustomerAppAuthConstants.CustomerIdClaim;

    private readonly JwtSettings _jwt;
    private readonly CustomerAppAuthSettings _customer;

    public CustomerAppJwtTokenService(
        IOptions<JwtSettings> jwt,
        IOptions<CustomerAppAuthSettings> customer)
    {
        _jwt = jwt.Value;
        _customer = customer.Value;
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(CustomerAccountRecord account)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_customer.AccessTokenExpireMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.AccountId.ToString()),
            new(CustomerIdClaim, account.CustomerId.ToString()),
            new("tenant_id", account.TenantId.ToString()),
            new("tenant_code", account.TenantCode),
            new(TokenTypeClaim, TokenTypeValue),
            new(ClaimTypes.Name, account.FullName),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _customer.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public DateTimeOffset GetRefreshTokenExpiry() =>
        DateTimeOffset.UtcNow.AddDays(_customer.RefreshTokenExpireDays);
}

internal sealed record CustomerAccountRecord(
    Guid AccountId,
    Guid CustomerId,
    Guid TenantId,
    string TenantCode,
    string FullName,
    string Phone,
    string? PreferredLocale);

internal sealed record TenantPhoneRow(Guid TenantId, string TenantCode);

internal sealed record OtpChallengeRow(
    Guid Id,
    string CodeHash,
    DateTime ExpiresAt,
    int AttemptCount);

internal sealed record PilotOtpRow(
    string Code,
    DateTime ExpiresAt,
    DateTime CreatedAt);

internal sealed record TenantCustomerAppAuthRow(
    string? CounterPinHash,
    string? InviteCodeHash,
    string? InviteCodeHint);

internal sealed record CustomerPhoneLookupRow(Guid CustomerId, string FullName);

internal sealed record CustomerAppLoginRequestRow(
    Guid Id,
    Guid TenantId,
    string Phone,
    Guid? CustomerId,
    string Channel,
    string Status,
    string? ReferralCodeUsed,
    Guid? OtpChallengeId,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedByUserId,
    string? RejectReason);

internal sealed record CustomerAppLoginRequestListRow(
    Guid Id,
    string Phone,
    Guid? CustomerId,
    string? CustomerName,
    string Channel,
    string Status,
    string? ReferralCodeUsed,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    string? RejectReason);

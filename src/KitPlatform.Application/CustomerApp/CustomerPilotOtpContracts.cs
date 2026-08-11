namespace KitPlatform.Application.CustomerApp;

public sealed record CustomerPilotOtpStatusDto(
    bool Enabled,
    string? Code,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CreatedAt);

/// <summary>Active staff-read OTP row for the live counter panel.</summary>
public sealed record ActiveCounterOtpDto(
    string Phone,
    string Code,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    Guid? CustomerId,
    string? CustomerName);

public sealed record ActiveCounterOtpListDto(
    bool Enabled,
    IReadOnlyList<ActiveCounterOtpDto> Items);

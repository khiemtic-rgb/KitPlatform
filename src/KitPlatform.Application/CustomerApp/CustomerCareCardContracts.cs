namespace KitPlatform.Application.CustomerApp;

public static class CustomerCareCardGroups
{
    public const string Hypertension = "hypertension";
    public const string Joint = "joint";
    public const string Respiratory = "respiratory";
    public const string Diabetes = "diabetes";
}

public static class CustomerCareCardClimate
{
    public const string Cold = "cold";
    public const string Hot = "hot";
    public const string Humid = "humid";
}

public sealed record CustomerCareCardDto(
    string Code,
    string Group,
    string Title,
    string Body,
    string Disclaimer,
    string CtaLabel,
    string CtaPath,
    string? Climate,
    bool Elderly,
    string? MatchedHint);

public sealed record CustomerCareCardProductRow(
    string ProductName,
    string? GenericName,
    string? CategoryName,
    string? Ingredients);

public sealed record CustomerCareCardFacts(
    DateOnly Today,
    DateOnly? DateOfBirth,
    IReadOnlyList<CustomerCareCardProductRow> Products);

public interface ICustomerCareCardService
{
    Task<CustomerCareCardDto?> GetTodayAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);
}

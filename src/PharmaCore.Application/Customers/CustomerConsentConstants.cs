namespace PharmaCore.Application.Customers;

public static class CustomerConsentChannels
{
    public const short Sms = 1;
    public const short Zalo = 2;
    public const short Email = 3;
    public const short AppPush = 4;
}

public static class CustomerConsentPurposes
{
    public const short Marketing = 1;
    public const short CareReminder = 2;
    public const short Research = 3;
    public const short AiAssist = 4;
}

public static class CustomerConsentSources
{
    public const short Pos = 1;
    public const short Admin = 2;
    public const short App = 3;
    public const short Import = 4;
}

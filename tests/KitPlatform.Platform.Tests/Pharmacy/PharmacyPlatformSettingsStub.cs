using Moq;
using KitPlatform.Application.Configuration;

namespace KitPlatform.Platform.Tests.Pharmacy;

internal static class PharmacyPlatformSettingsStub
{
    public static ITenantPlatformSettings Create(string vertical = "pharmacy")
    {
        var settings = new TenantPlatformSettingsDto(
            1,
            vertical,
            [],
            [],
            new TenantPlatformI18nDto("vi-VN", ["vi-VN"], "vi-VN", "vi-VN", "vi-VN"),
            new Dictionary<string, bool>(),
            new Dictionary<string, string>());
        var mock = new Mock<ITenantPlatformSettings>();
        mock.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        return mock.Object;
    }
}

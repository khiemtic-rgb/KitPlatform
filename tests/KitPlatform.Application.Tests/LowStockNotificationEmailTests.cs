using KitPlatform.Packs.Pharmacy.Inventory;
using Xunit;

namespace KitPlatform.Tests;

public sealed class LowStockNotificationEmailTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_email_clears_the_setting(string? value)
    {
        Assert.Null(LowStockNotificationEmail.Normalize(value));
    }

    [Fact]
    public void Valid_email_is_trimmed()
    {
        Assert.Equal("kho@nhathuoc.vn", LowStockNotificationEmail.Normalize("  kho@nhathuoc.vn  "));
    }

    [Theory]
    [InlineData("kho")]
    [InlineData("kho@")]
    [InlineData("@nhathuoc.vn")]
    [InlineData("kho@@nhathuoc.vn")]
    [InlineData("kho@nhathuoc")]
    [InlineData("kho @nhathuoc.vn")]
    public void Invalid_email_is_rejected(string value)
    {
        Assert.Throws<InvalidOperationException>(() => LowStockNotificationEmail.Normalize(value));
    }

    [Fact]
    public void Email_longer_than_200_characters_is_rejected()
    {
        var value = new string('a', 190) + "@nhathuoc.vn";
        Assert.True(value.Length > LowStockNotificationEmail.MaxLength);
        Assert.Throws<InvalidOperationException>(() => LowStockNotificationEmail.Normalize(value));
    }
}

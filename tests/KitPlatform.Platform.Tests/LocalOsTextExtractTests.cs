using KitPlatform.Packs.LocalOs;
using Xunit;

namespace KitPlatform.Platform.Tests;

public class LocalOsTextExtractTests
{
    private const string VertChat =
        "Ae ơi quán Vert cần nv pt pha chế 18-22k/h đối diện 341 Phan Bội Châu lh chị Hoa 0766408636";

    [Fact]
    public void Extracts_phone_from_chat()
    {
        Assert.Equal("0766408636", LocalOsTextExtract.GuessPhone(VertChat));
        Assert.Contains("0766408636", LocalOsTextExtract.PhonesIn(VertChat));
    }

    [Fact]
    public void Extracts_contact_name()
    {
        Assert.Equal("chị Hoa", LocalOsTextExtract.GuessContactName(VertChat));
        Assert.Equal(
            "anh Minh",
            LocalOsTextExtract.GuessContactName("Liên hệ: anh Minh 0984660399"));
    }

    [Fact]
    public void Extracts_org_pay_and_type()
    {
        Assert.Contains("Vert", LocalOsTextExtract.GuessOrganizationName(VertChat) ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal("part_time", LocalOsTextExtract.GuessEmploymentType(VertChat));
        Assert.Contains("18", LocalOsTextExtract.GuessSalary(VertChat) ?? "");
    }

    [Fact]
    public void Structured_body_has_labels_and_contact()
    {
        var body = LocalOsTextExtract.StructuredBody(
            "job",
            "Tuyển barista — Vert",
            "Vert",
            "Đối diện 341 Phan Bội Châu",
            "0766408636",
            "chị Hoa",
            "18.000–22.000đ/giờ",
            null,
            null);
        Assert.Contains("Thu nhập:", body);
        Assert.Contains("Liên hệ: chị Hoa — 0766408636", body);
        Assert.DoesNotContain("Ae ơi", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Does_not_invent_name_when_only_phone()
    {
        Assert.Null(LocalOsTextExtract.GuessContactName("Liên hệ: 0328943419"));
    }
}

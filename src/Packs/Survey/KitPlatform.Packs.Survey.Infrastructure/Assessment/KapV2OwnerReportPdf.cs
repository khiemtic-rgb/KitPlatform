using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KitPlatform.Packs.Survey.Infrastructure;

public sealed record KapV2PdfAdmin(
    string? Name,
    string? Phone,
    string? Utm,
    bool CallConsent,
    IReadOnlyList<(string Code, string? Answer)> Branches);

public static class KapV2PdfFonts
{
    public const string Family = "Be Vietnam Pro";

    private static bool _ready;

    public static void EnsureRegistered()
    {
        if (_ready) return;
        QuestPDF.Settings.License = LicenseType.Community;
        Register("BeVietnamPro-Regular.ttf");
        Register("BeVietnamPro-Bold.ttf");
        _ready = true;
    }

    private static void Register(string logicalName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException("Thiếu font " + logicalName);
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        FontManager.RegisterFont(copy);
    }
}

public static class KapV2OwnerReportPdf
{
    public const string Hotline = "0984.660.399";

    public static byte[] Generate(
        KapV2OwnerReport report,
        string orgName,
        DateTimeOffset? completedAt,
        KapV2PdfAdmin? admin)
    {
        KapV2PdfFonts.EnsureRegistered();
        var score = ScoreOf(report.Headline);
        var label = KapV2OwnerReportBuilder.ScoreLabel(score);
        var date = completedAt?.ToLocalTime().ToString("dd/MM/yyyy") ?? "—";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontFamily(KapV2PdfFonts.Family).FontSize(10.5f).FontColor(Colors.Grey.Darken3).LineHeight(1.3f));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Trang ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    t.Span(" · Mẫu chữ: phân tích, ưu tiên, tiền, tiếp tục, chi tiết, Thực hành tốt nhà thuốc.").FontSize(8).FontColor(Colors.Grey.Medium);
                });
                page.Content().Column(col =>
                {
                    col.Item().Text(orgName).Bold().FontSize(18).FontColor(Colors.Teal.Darken2);
                    col.Item().PaddingTop(2).Text($"Ngày làm bài: {date}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).Text($"{score}/100  ·  {label}").Bold().FontSize(16).FontColor(Colors.Teal.Darken2);
                    col.Item().PaddingTop(4).Text(report.Headline);

                    if (report.Leaks.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Nhà thuốc đang mất tiền ở đâu").Bold().FontSize(13).FontColor(Colors.Teal.Darken2);
                        foreach (var leak in report.Leaks)
                        {
                            col.Item().PaddingTop(6).Text(leak.Title).Bold();
                            col.Item().Text(leak.Said);
                            col.Item().Text(leak.Body);
                        }
                    }

                    if (report.Concern is not null)
                    {
                        col.Item().PaddingTop(12).Text($"Điều anh/chị lo nhất: {report.Concern.Label.ToLowerInvariant()}").Bold().FontSize(13).FontColor(Colors.Teal.Darken2);
                        col.Item().PaddingTop(2).Text(report.Concern.Text);
                    }

                    col.Item().PaddingTop(12).Text(report.Actions.Count == 3 ? "3 việc nên làm trước" : "Việc nên làm trước").Bold().FontSize(13).FontColor(Colors.Teal.Darken2);
                    var n = 1;
                    foreach (var action in report.Actions)
                    {
                        col.Item().PaddingTop(8).Text($"{n}. {action.Title}").Bold();
                        col.Item().Text(action.Said);
                        col.Item().Text(action.Why);
                        col.Item().PaddingTop(2).Text("Cách làm").Bold().FontSize(9);
                        foreach (var step in action.Steps)
                            col.Item().Text("• " + step);
                        col.Item().PaddingTop(2).Text($"Ai làm: {action.Who}");
                        col.Item().Text($"Mất bao lâu: {action.Time}");
                        col.Item().Text($"Sau 30 ngày xem lại: {action.Check}");
                        if (!string.IsNullOrWhiteSpace(action.Tool))
                        {
                            var tool = report.HasSoftware ? "Với phần mềm đang dùng: " + action.Tool : action.Tool;
                            col.Item().PaddingTop(2).Text(tool).FontColor(Colors.Grey.Darken2);
                        }
                        n++;
                    }

                    if (report.Strengths.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Anh/chị đang làm tốt").Bold().FontSize(13).FontColor(Colors.Teal.Darken2);
                        foreach (var s in report.Strengths)
                            col.Item().Text("• " + s);
                    }

                    if (report.Watch.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Nên bắt đầu để ý").Bold().FontSize(13).FontColor(Colors.Teal.Darken2);
                        foreach (var w in report.Watch)
                            col.Item().Text("• " + w);
                    }

                    col.Item().PaddingTop(12).Text("Điểm từng phần").Bold().FontSize(13).FontColor(Colors.Teal.Darken2);
                    col.Item().Text("Thang 100: dưới 40 Cần cải thiện · 40–59 Cơ bản · 60–79 Khá tốt · từ 80 Xuất sắc.").FontSize(9).FontColor(Colors.Grey.Darken1);
                    foreach (var area in report.Areas)
                    {
                        var value = area.Pct is null
                            ? area.Note ?? ""
                            : $"{area.Pct}/100 {KapV2OwnerReportBuilder.ScoreLabel(area.Pct.Value)}";
                        col.Item().PaddingTop(2).Text($"{area.Name}: {value}");
                    }

                    col.Item().PaddingTop(12).Text("Muốn có người cùng làm?").Bold().FontSize(13).FontColor(Colors.Teal.Darken2);
                    col.Item().PaddingTop(2).Text("Novixa có thể gọi 10–15 phút để cùng anh/chị chọn việc làm trước và cách làm cho hợp với nhà thuốc. Không bắt buộc.");
                    col.Item().Text($"Gọi hoặc nhắn: {Hotline}").Bold();

                    if (admin is not null)
                    {
                        col.Item().PageBreak();
                        col.Item().Text("Dành cho người tư vấn").Bold().FontSize(16).FontColor(Colors.Teal.Darken2);
                        col.Item().PaddingTop(8).Text($"Tên: {Show(admin.Name)}");
                        col.Item().Text($"Số điện thoại: {Show(admin.Phone)}");
                        col.Item().Text($"Nguồn: {Show(admin.Utm)}");
                        col.Item().Text("Đồng ý gọi: " + (admin.CallConsent ? "Có" : "Không"));
                        col.Item().PaddingTop(8).Text("Câu trả lời rẽ nhánh").Bold();
                        foreach (var branch in admin.Branches)
                            col.Item().PaddingTop(2).Text($"{branch.Code}: {Show(branch.Answer)}");
                    }
                });
            });
        }).GeneratePdf();
    }

    private static int ScoreOf(string headline)
    {
        var marker = "đạt ";
        var at = headline.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return 0;
        var rest = headline[(at + marker.Length)..];
        var slash = rest.IndexOf('/');
        return slash > 0 && int.TryParse(rest[..slash], out var n) ? n : 0;
    }

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
}

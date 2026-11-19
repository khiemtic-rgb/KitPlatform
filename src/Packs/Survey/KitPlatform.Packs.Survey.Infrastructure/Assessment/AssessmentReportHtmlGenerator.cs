using System.Net;
using System.Text;
using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal static class AssessmentReportHtmlGenerator
{
    public static byte[] Generate(AssessmentFullReportDto report, string orgName)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8"/>
              <title>Báo cáo đánh giá năng lực nhà thuốc</title>
              <style>
                body { font-family: system-ui, sans-serif; margin: 2rem; color: #1a1a1a; }
                h1 { color: #0f766e; }
                .score-box { background: #f0fdfa; border: 1px solid #99f6e4; padding: 1rem 1.5rem; border-radius: 8px; margin: 1rem 0; }
                table { width: 100%; border-collapse: collapse; margin: 1rem 0; }
                th, td { border: 1px solid #e5e7eb; padding: 0.5rem 0.75rem; text-align: left; }
                th { background: #f9fafb; }
                .insight { margin: 0.75rem 0; padding: 0.75rem; background: #fffbeb; border-left: 4px solid #f59e0b; }
                .rec { margin: 0.75rem 0; padding: 0.75rem; background: #eff6ff; border-left: 4px solid #3b82f6; }
                @media print { body { margin: 1rem; } }
              </style>
            </head>
            <body>
            """);

        sb.Append($"<h1>Báo cáo đánh giá — {WebUtility.HtmlEncode(orgName)}</h1>");
        sb.Append($"<p>Template: {WebUtility.HtmlEncode(report.TemplateCode)} · Hoàn thành: {report.CompletedAt:dd/MM/yyyy HH:mm}</p>");

        sb.Append($"""
            <div class="score-box">
              <strong>Điểm tổng:</strong> {report.OverallScore:F2} / 4 ({report.OverallPct:F1}%)
            </div>
            """);

        sb.Append("<h2>Điểm theo nhóm</h2><table><thead><tr><th>Nhóm</th><th>Điểm</th><th>%</th></tr></thead><tbody>");
        foreach (var cat in report.CategoryScores)
        {
            if (cat.InsufficientData)
            {
                sb.Append($"<tr><td>{WebUtility.HtmlEncode(cat.Name)}</td><td colspan=\"2\">Chưa đủ dữ liệu</td></tr>");
                continue;
            }
            sb.Append($"<tr><td>{WebUtility.HtmlEncode(cat.Name)}</td><td>{cat.Score:F2}</td><td>{cat.ScorePct:F1}%</td></tr>");
        }
        sb.Append("</tbody></table>");

        if (report.DimensionScores.Count > 0)
        {
            sb.Append("<h2>Chi tiết dimension</h2><table><thead><tr><th>Dimension</th><th>Điểm</th><th>%</th></tr></thead><tbody>");
            foreach (var dim in report.DimensionScores)
            {
                sb.Append($"<tr><td>{WebUtility.HtmlEncode(dim.Name)}</td><td>{dim.Score:F2}</td><td>{dim.ScorePct:F1}%</td></tr>");
            }
            sb.Append("</tbody></table>");
        }

        if (report.Insights.Count > 0)
        {
            sb.Append("<h2>Nhận xét</h2>");
            foreach (var insight in report.Insights)
            {
                sb.Append($"<div class=\"insight\"><strong>{WebUtility.HtmlEncode(insight.Title)}</strong><p>{WebUtility.HtmlEncode(insight.Body)}</p></div>");
            }
        }

        if (report.Recommendations.Count > 0)
        {
            sb.Append("<h2>Đề xuất</h2>");
            foreach (var rec in report.Recommendations)
            {
                var hint = string.IsNullOrWhiteSpace(rec.EstimateHint) ? "" : $" · {rec.EstimateHint}";
                sb.Append($"<div class=\"rec\"><strong>{WebUtility.HtmlEncode(rec.Title)}</strong>{WebUtility.HtmlEncode(hint)}<p>{WebUtility.HtmlEncode(rec.Body)}</p></div>");
            }
        }

        sb.Append("<p style=\"margin-top:2rem;font-size:0.85rem;color:#6b7280;\">Powered by Novixa KIT Platform</p>");
        sb.Append("</body></html>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Lead briefing + next-best-action. Copy stays inside journey / knowledge intents.
/// </summary>
internal static class NovixaSalesAssist
{
    public static KitSalesAssistDto Build(
        KitSalesLeadDto lead,
        KitSalesJourneyPlanDto journey,
        KitSalesInteractionDto? lastInbound,
        KitSalesInteractionDto? lastOutbound)
    {
        var parsed = NovixaSalesJourney.Parse(lead.NextActionCode);
        var pain = NovixaSalesJourney.Pain(string.IsNullOrWhiteSpace(journey.PainCode) ? parsed.Pain : journey.PainCode);
        var classification = lastInbound?.Classification ?? lastInbound?.Outcome;
        var classLabel = ClassificationLabel(classification);
        var hasChat = lastInbound is not null || lastOutbound is { ReviewStatus: "sent" };
        var awaitingReply = lastOutbound is { ReviewStatus: "sent" }
            && (lastInbound is null || lastInbound.OccurredAt < lastOutbound.OccurredAt);
        var needsReply = lastInbound is not null
            && (lastOutbound is null
                || lastOutbound.ReviewStatus != "sent"
                || lastInbound.OccurredAt > lastOutbound.OccurredAt);

        var goal = journey.Step switch
        {
            "phc_ask" => "Mời khách thực hiện Pharmacy Health Check (20–30 phút, không thu phí).",
            "report_ask" => "Hỏi khách đã xem báo cáo Health Check chưa — chỗ nào thấy đúng.",
            "pilot_ask" => "Mời Pilot 30 ngày tại một cơ sở, 1–2 mục tiêu trên báo cáo.",
            _ => $"Mở đầu theo nỗi đau: {pain.Label.ToLowerInvariant()} — không chào phần mềm ngay.",
        };

        var context = string.Join(" · ", ContextBits(lead, journey, awaitingReply, classification));
        var hint = NextHint(journey.Step, classification, awaitingReply);
        var action = NextAction(journey.Step, classification);
        var when = lead.NextActionAt is { } due && due > DateTimeOffset.UtcNow.AddMinutes(-30)
            ? due
            : NovixaSalesJourney.NextQuietWindow(DateTimeOffset.UtcNow);

        return new KitSalesAssistDto(
            lead.Id,
            lead.BusinessName,
            $"{lead.BusinessName}{(string.IsNullOrWhiteSpace(lead.Province) ? "" : " · " + lead.Province)}",
            SummaryLine(lead, journey, hasChat, awaitingReply),
            goal,
            context,
            classification,
            classLabel,
            hint,
            action.Code,
            action.Label,
            when,
            NovixaSalesJourney.WindowLabel(when),
            NovixaSalesJourney.WhyNow,
            lastInbound?.Content,
            needsReply);
    }

    public static string ClassificationLabel(string? code) => code switch
    {
        "switch_software" => "Đang dùng phần mềm khác",
        "price" => "Đang hỏi giá",
        "phc" => "Quan tâm Health Check",
        "next_step" => "Muốn bước tiếp theo",
        "greeting" => "Mới chào hỏi",
        "is_sales" => "Nghi ngờ bị chào hàng",
        "app_optional" => "Ngại bắt buộc App",
        "no_revenue_promise" => "Đòi cam kết doanh thu",
        "no_unlimited_trial" => "Muốn dùng thử miễn phí",
        "not_in_phase1" => "Hỏi phần chưa có Phase 1",
        "what_is_novixa" => "Hỏi Novixa là gì",
        "unknown" => "Ngoài tài liệu — cần người",
        _ => string.IsNullOrWhiteSpace(code) ? "Chưa có tin khách" : "Đang cân nhắc",
    };

    private static string SummaryLine(
        KitSalesLeadDto lead,
        KitSalesJourneyPlanDto journey,
        bool hasChat,
        bool awaitingReply)
    {
        var stage = lead.LeadStatus switch
        {
            "discovered" => "Mới khám phá Novixa",
            "research" => "Đã tiếp cận Novixa",
            "contact" => "Đã liên hệ",
            "engage" => "Đang quan tâm Novixa",
            "phc" => "Đang làm / đã mời PHC",
            "qualify" => "Đang tư vấn",
            "demo" => "Đã / đang demo",
            "pilot" => "Đang Pilot",
            "paid" => "Đã trả phí",
            "lost" => "Đã mất",
            _ => lead.LeadStatus,
        };

        var phc = journey.Step is "hook" or "phc_ask" ? "PHC chưa hoàn thành" : "Đã qua bước mời PHC";
        var reply = !hasChat
            ? "chưa có tương tác"
            : awaitingReply
                ? "chưa có phản hồi sau lần liên hệ gần nhất"
                : "có tín hiệu gần đây";
        return $"{stage} · {phc} · {reply}";
    }

    private static IEnumerable<string> ContextBits(
        KitSalesLeadDto lead,
        KitSalesJourneyPlanDto journey,
        bool awaitingReply,
        string? classification)
    {
        yield return $"Giai đoạn {lead.LeadStatus}";
        yield return $"Nỗi đau: {journey.PainLabel}";
        if (awaitingReply)
            yield return "Đã gửi tin, chưa thấy trả lời";
        if (!string.IsNullOrWhiteSpace(classification))
            yield return ClassificationLabel(classification);
    }

    private static string NextHint(string step, string? classification, bool awaitingReply)
    {
        if (classification == "switch_software")
            return "Không bắt đầu từ “thay POS”. Gợi ý Pilot một cơ sở.";
        if (classification == "unknown")
            return "Ngoài SoT — người phụ trách trả lời, không để máy bịa.";
        if (awaitingReply && step == "phc_ask")
            return "Nhắc nhẹ Health Check khi quầy vắng — đã có thời gian xem chưa?";
        return step switch
        {
            "phc_ask" => "Giải thích thêm về PHC (20–30 phút, không thu phí).",
            "report_ask" => "Hỏi phần nào trên báo cáo thấy đúng nhất.",
            "pilot_ask" => "Mời Pilot 30 ngày, không chuyển toàn bộ ngay.",
            _ => "Giữ một nỗi đau — một câu mở, không dump tính năng.",
        };
    }

    private static (string Code, string Label) NextAction(string step, string? classification)
    {
        if (classification == "unknown")
            return ("follow_up", "Người phụ trách trả lời (ngoài SoT)");
        return step switch
        {
            "phc_ask" => ("phc", "Mời / nhắc Pharmacy Health Check"),
            "report_ask" => ("follow_up", "Hỏi đã xem báo cáo chưa"),
            "pilot_ask" => ("proposal", "Mời Pilot 30 ngày"),
            _ => ("follow_up", "Chat Facebook — câu mở theo nỗi đau"),
        };
    }
}

namespace KitPlatform.Packs.Sales;

public sealed record SalesTurnInput(
    string CustomerText,
    string Intent,
    string CatalogReply,
    string? ClaimId,
    bool CatalogEscalate,
    SalesAutonomyMode Mode,
    bool OptedOut,
    bool NewInboundAfterOptOut,
    bool LegalFlag,
    bool MasterEnabled,
    bool FirstMessage);

public sealed record SalesTurnDecision(
    string Reply,
    bool Draft,
    bool Send,
    bool Escalate,
    string Validation,
    SalesAutonomyMode Mode,
    bool OpenIncident);

public static class SalesMessengerTurn
{
    public static SalesTurnDecision Decide(SalesTurnInput input, SalesValidationResult validation)
    {
        var severe = validation.Decision == "block";
        if (input.OptedOut && !input.NewInboundAfterOptOut)
            return new SalesTurnDecision(input.CatalogReply, false, false, true, "opt_out", input.Mode, false);
        if (IsOptOutText(input.CustomerText))
            return new SalesTurnDecision(input.CatalogReply, false, false, true, "opt_out_message", input.Mode, true);

        var mode = input.Mode;
        if (!input.MasterEnabled || !input.LegalFlag)
            mode = mode == SalesAutonomyMode.Autonomous ? SalesAutonomyMode.Supervised : mode;
        if (severe)
            return new SalesTurnDecision(input.CatalogReply, false, false, true, validation.Decision, SalesAutonomy.Demote("validator_severe", mode), true);

        var usable = validation.Decision is "pass" or "fallback_catalog";
        var send = usable && mode == SalesAutonomyMode.Autonomous && input.MasterEnabled && input.LegalFlag && !input.CatalogEscalate;
        var draft = usable && !send && mode >= SalesAutonomyMode.Supervised;
        return new SalesTurnDecision(
            input.CatalogReply,
            draft,
            send,
            input.CatalogEscalate || validation.Decision == "escalate",
            validation.Decision,
            mode,
            false);
    }

    public static bool IsOptOutText(string text)
    {
        var folded = text.ToLowerInvariant();
        return folded.Contains("đừng nhắn") || folded.Contains("dung nhan") || folded.Contains("không liên hệ") || folded.Contains("khong lien he");
    }
}

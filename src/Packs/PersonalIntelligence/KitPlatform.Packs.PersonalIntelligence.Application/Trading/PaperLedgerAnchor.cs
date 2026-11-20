namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Explicit paper-ledger identity for one tenant. Account and exchange codes are not interchangeable across tenants.
/// </summary>
public sealed record PaperLedgerAnchor(string TenantCode, string ExchangeCode, string ExternalAccountId)
{
    public static readonly PaperLedgerAnchor KitMkt = new("KIT_MKT", "PAPER_LEDGER", "KIT_MKT-PAPER-LEDGER");

    public static readonly PaperLedgerAnchor SecondBrain = new("SECOND_BRAIN", "PAPER", "paper-fixture");

    public static readonly IReadOnlyList<PaperLedgerAnchor> All = [KitMkt, SecondBrain];

    public static PaperLedgerAnchor? ForTenant(string? tenantCode)
    {
        var code = tenantCode?.Trim() ?? "";
        foreach (var item in All)
        {
            if (string.Equals(item.TenantCode, code, StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    public bool Matches(string? tenantCode, string? exchangeCode, string? externalAccountId) =>
        string.Equals(TenantCode, tenantCode?.Trim(), StringComparison.Ordinal)
        && string.Equals(ExchangeCode, exchangeCode?.Trim(), StringComparison.Ordinal)
        && string.Equals(ExternalAccountId, externalAccountId?.Trim(), StringComparison.Ordinal);

    public static bool IsLedgerIdentity(string? exchangeCode, string? externalAccountId)
    {
        var exchange = exchangeCode?.Trim() ?? "";
        var account = externalAccountId?.Trim() ?? "";
        foreach (var item in All)
        {
            if (string.Equals(item.ExchangeCode, exchange, StringComparison.Ordinal)
                || string.Equals(item.ExternalAccountId, account, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

public sealed record PaperLedgerCandidate(
    string TenantCode,
    string ExchangeCode,
    string ExternalAccountId,
    Guid PersonId,
    Guid TradingAccountId);

public sealed record PaperLedgerChoice(Guid PersonId, Guid TradingAccountId, PaperLedgerAnchor Anchor);

public static class PaperLedgerSelection
{
    public const string MissingAnchorMessage = "Tenant này không có neo sổ paper nội bộ. Decision không được lưu.";

    public const string MismatchMessage = "Tenant, account và ledger không khớp. Decision không được lưu.";

    public static bool TrySelect(
        string? callerTenantCode,
        IReadOnlyList<PaperLedgerCandidate> rows,
        out PaperLedgerChoice choice,
        out string error)
    {
        choice = new PaperLedgerChoice(Guid.Empty, Guid.Empty, PaperLedgerAnchor.KitMkt);
        var anchor = PaperLedgerAnchor.ForTenant(callerTenantCode);
        if (anchor is null)
        {
            error = MissingAnchorMessage;
            return false;
        }

        if (rows.Any(row => !string.Equals(row.TenantCode?.Trim(), anchor.TenantCode, StringComparison.Ordinal)))
        {
            error = MismatchMessage;
            return false;
        }

        var ledgerRows = rows
            .Where(row => PaperLedgerAnchor.IsLedgerIdentity(row.ExchangeCode, row.ExternalAccountId))
            .ToArray();
        var matches = ledgerRows
            .Where(row => anchor.Matches(row.TenantCode, row.ExchangeCode, row.ExternalAccountId)
                && row.PersonId != Guid.Empty
                && row.TradingAccountId != Guid.Empty)
            .ToArray();
        if (matches.Length == 1 && ledgerRows.Length == 1)
        {
            var match = matches[0];
            choice = new PaperLedgerChoice(match.PersonId, match.TradingAccountId, anchor);
            error = "";
            return true;
        }

        error = ledgerRows.Length == 0 ? MissingAnchorMessage : MismatchMessage;
        return false;
    }
}

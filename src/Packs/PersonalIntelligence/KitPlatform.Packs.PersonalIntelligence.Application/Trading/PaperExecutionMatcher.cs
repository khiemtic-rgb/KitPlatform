namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Prices one paper fill. This is not <see cref="DemoPaperExecution"/>:
/// the fixture path still fills at the proposal price.
/// </summary>
/// <remarks>
/// Execution price, BUY:  tape * (1 + slippageBps / 10000), rounded to 8 decimal places, away from zero.
/// Execution price, SELL: tape * (1 - slippageBps / 10000), same rounding. A non-positive result is refused.
/// Notional: execution price * quantity.
/// Fee: max(minimumFee, round(notional * feeRate, 8, away from zero)). Fee currency is the tape quote currency.
/// Net cash, BUY:  -(notional + fee). The fee is added to the cost.
/// Net cash, SELL: notional - fee. The fee is subtracted from the proceeds.
/// Slippage quote: absolute value of (execution price - tape price) * quantity.
/// </remarks>
public static class PaperExecutionMatcher
{
    public static bool TryMatch(
        TradeProposal proposal,
        decimal slippageBps,
        decimal feeRate,
        decimal minimumFee,
        out PaperMatch match,
        out string refusal)
    {
        match = default!;
        refusal = "";
        if (proposal.Mode != ExecutionMode.Paper)
        {
            refusal = "The paper matcher does not price a live order.";
            return false;
        }

        if (slippageBps < 0m || feeRate < 0m || minimumFee < 0m)
        {
            refusal = "Slippage and fee settings must be non-negative.";
            return false;
        }

        var factor = slippageBps / 10_000m;
        var raw = proposal.Side == "BUY"
            ? proposal.Price * (1m + factor)
            : proposal.Price * (1m - factor);
        if (raw <= 0m)
        {
            refusal = "Slippage would make the execution price non-positive.";
            return false;
        }

        var execution = decimal.Round(raw, 8, MidpointRounding.AwayFromZero);
        var notional = execution * proposal.Quantity;
        var variable = decimal.Round(notional * feeRate, 8, MidpointRounding.AwayFromZero);
        var fee = variable > minimumFee ? variable : minimumFee;
        var slippageQuote = Math.Abs(execution - proposal.Price) * proposal.Quantity;
        var netCash = proposal.Side == "BUY" ? -(notional + fee) : notional - fee;
        match = new PaperMatch(proposal.Price, execution, proposal.Quantity, notional, fee, slippageQuote, netCash);
        return true;
    }
}

public sealed record PaperMatch(
    decimal TapePrice,
    decimal ExecutionPrice,
    decimal Quantity,
    decimal Notional,
    decimal Fee,
    decimal SlippageQuote,
    decimal NetCash);

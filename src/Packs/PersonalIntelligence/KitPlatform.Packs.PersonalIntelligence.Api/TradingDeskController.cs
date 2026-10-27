using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

[ApiController]
[Route("api/personal-intelligence/trading")]
[Authorize(Policy = PersonalIntelligencePolicies.Run)]
[RequirePersonalIntelligenceModule]
public sealed class TradingDeskController : ControllerBase
{
    private readonly ITradingDesk _desk;

    public TradingDeskController(ITradingDesk desk) => _desk = desk;

    [HttpGet("summary")]
    public async Task<ActionResult<TradingSummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await _desk.GetSummaryAsync(cancellationToken));

    [HttpGet("accounts")]
    public async Task<ActionResult<IReadOnlyList<TradingAccountDto>>> Accounts(CancellationToken cancellationToken) =>
        Ok(await _desk.ListAccountsAsync(cancellationToken));

    [HttpGet("trades")]
    public async Task<ActionResult<TradingPage<TradeRowDto>>> Trades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? tradingAccountId = null,
        [FromQuery] string? symbol = null,
        [FromQuery] string? side = null,
        [FromQuery] string? source = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? marketType = null,
        [FromQuery] string? productSubtype = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListTradesAsync(
            new TradeListQuery(page, pageSize, tradingAccountId, symbol, side, source, from, to, marketType, productSubtype),
            cancellationToken));

    [HttpGet("trades/{tradeId:guid}")]
    public async Task<ActionResult<TradeRowDto>> Trade(Guid tradeId, CancellationToken cancellationToken)
    {
        var row = await _desk.GetTradeAsync(tradeId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("decisions")]
    public async Task<ActionResult<TradingPage<DecisionRowDto>>> Decisions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListDecisionsAsync(page, pageSize, search, cancellationToken));

    [HttpGet("decisions/{decisionId:guid}")]
    public async Task<ActionResult<DecisionDetailDto>> Decision(Guid decisionId, CancellationToken cancellationToken)
    {
        var row = await _desk.GetDecisionAsync(decisionId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("decisions/{decisionId:guid}/paper")]
    [Authorize(Policy = PersonalIntelligencePolicies.Approve)]
    public async Task<ActionResult<PaperApprovalDto>> ApprovePaper(Guid decisionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.ApprovePaperAsync(decisionId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("decisions/{decisionId:guid}/reject")]
    [Authorize(Policy = PersonalIntelligencePolicies.Approve)]
    public async Task<ActionResult<PaperRejectionDto>> RejectPaper(Guid decisionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.RejectPaperAsync(decisionId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("operations")]
    public async Task<ActionResult<TradingPage<OperationRowDto>>> Operations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListOperationsAsync(page, pageSize, cancellationToken));

    [HttpGet("operations/{operationKey}")]
    public async Task<ActionResult<OperationRowDto>> Operation(string operationKey, CancellationToken cancellationToken)
    {
        var row = await _desk.GetOperationAsync(operationKey, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("paper-executions")]
    public async Task<ActionResult<TradingPage<PaperExecutionRowDto>>> Paper(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListPaperExecutionsAsync(page, pageSize, cancellationToken));

    [HttpPost("imports/preview")]
    public async Task<ActionResult<TradeImportPreviewDto>> Preview(
        [FromBody] TradeImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.PreviewImportAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("imports")]
    public async Task<ActionResult<TradeImportCommitDto>> Import(
        [FromBody] TradeImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CommitImportAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

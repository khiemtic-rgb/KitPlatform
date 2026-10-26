using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

[ApiController]
[Route("api/personal-intelligence/trading/exchanges")]
[RequirePersonalIntelligenceModule]
public sealed class TradingExchangeController : ControllerBase
{
    private readonly IExchangeConnectionService _connections;
    private readonly IExchangeExecutionSync _executions;
    private readonly IExchangeBackfill _backfill;

    public TradingExchangeController(IExchangeConnectionService connections, IExchangeExecutionSync executions, IExchangeBackfill backfill)
    {
        _connections = connections;
        _executions = executions;
        _backfill = backfill;
    }

    [HttpGet("catalog")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeRead)]
    public ActionResult Catalog() => Ok(ExchangeCatalog.Describe());

    [HttpGet("backfill")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeRead)]
    public Task<ActionResult> Backfill([FromQuery] Guid? connectionId, [FromQuery] string? marketType, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _backfill.ListAsync(connectionId, marketType, cancellationToken)));

    [HttpPost("{connectionId:guid}/backfill")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> RequestBackfill(Guid connectionId, [FromBody] ExchangeBackfillRequest request, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _backfill.RequestAsync(connectionId, request, cancellationToken)));

    [HttpPost("backfill/{jobId:guid}/pause")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> Pause(Guid jobId, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _backfill.PauseAsync(jobId, cancellationToken)));

    [HttpPost("backfill/{jobId:guid}/resume")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> Resume(Guid jobId, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _backfill.ResumeAsync(jobId, cancellationToken)));

    [HttpPost("backfill/{jobId:guid}/retry")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> Retry(Guid jobId, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _backfill.RetryAsync(jobId, cancellationToken)));

    [HttpGet]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeRead)]
    public Task<ActionResult> List(CancellationToken cancellationToken) =>
        Run(async () => Ok(await _connections.ListAsync(cancellationToken)));

    [HttpGet("{connectionId:guid}/account")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeRead)]
    public Task<ActionResult> Account(Guid connectionId, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _connections.GetAccountAsync(connectionId, cancellationToken)));

    [HttpPost("test")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> Test([FromBody] ExchangeCredentialRequest request, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _connections.TestAsync(request, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> Save([FromBody] ExchangeCredentialRequest request, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _connections.SaveAsync(request, cancellationToken)));

    [HttpPost("{connectionId:guid}/refresh")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> Refresh(Guid connectionId, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _connections.RefreshAsync(connectionId, cancellationToken)));

    [HttpGet("{connectionId:guid}/executions/latest")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeRead)]
    public Task<ActionResult> LatestExecutions(Guid connectionId, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _executions.LatestAsync(connectionId, cancellationToken)));

    [HttpPost("{connectionId:guid}/executions/preview")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> PreviewExecutions(Guid connectionId, [FromBody] ExchangeExecutionSyncRequest request, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _executions.PreviewAsync(connectionId, request, cancellationToken)));

    [HttpPost("{connectionId:guid}/executions")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> SyncExecutions(Guid connectionId, [FromBody] ExchangeExecutionSyncRequest request, CancellationToken cancellationToken) =>
        Run(async () => Ok(await _executions.CommitAsync(connectionId, request, cancellationToken)));

    [HttpDelete("{connectionId:guid}")]
    [Authorize(Policy = PersonalIntelligencePolicies.ExchangeConnect)]
    public Task<ActionResult> Disconnect(Guid connectionId, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            await _connections.DisconnectAsync(connectionId, cancellationToken);
            return Ok(new { status = "DISCONNECTED" });
        });

    private async Task<ActionResult> Run(Func<Task<ActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ExchangeConnectionException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code });
        }
    }
}

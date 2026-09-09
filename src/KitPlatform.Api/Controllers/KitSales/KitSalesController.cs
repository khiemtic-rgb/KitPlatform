using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Api.Controllers.KitSales;

[ApiController]
[Route("api/kit-sales")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.KitSales)]
public sealed class KitSalesController : ControllerBase
{
    private readonly IKitSalesDeskService _desk;
    private readonly IKitSalesChatService _chat;
    private readonly IKitSalesPainService _pains;
    private readonly IKitSalesFacebookSettings _facebook;
    private readonly IKitSalesAiSettings _ai;
    private readonly IConfiguration _config;

    public KitSalesController(
        IKitSalesDeskService desk,
        IKitSalesChatService chat,
        IKitSalesPainService pains,
        IKitSalesFacebookSettings facebook,
        IKitSalesAiSettings ai,
        IConfiguration config)
    {
        _desk = desk;
        _chat = chat;
        _pains = pains;
        _facebook = facebook;
        _ai = ai;
        _config = config;
    }

    [HttpGet("health")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesHealthDto>> Health(CancellationToken cancellationToken) =>
        Ok(await _desk.GetHealthAsync(cancellationToken));

    [HttpGet("products")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesProductDto>>> Products(
        CancellationToken cancellationToken) =>
        Ok(await _desk.ListProductsAsync(cancellationToken));

    [HttpGet("businesses")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesBusinessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesBusinessDto>>> Businesses(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListBusinessesAsync(limit, cancellationToken));

    [HttpPost("businesses")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesBusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesBusinessDto>> CreateBusiness(
        [FromBody] CreateKitSalesBusinessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateBusinessAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("leads")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesLeadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesLeadDto>>> Leads(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? province = null,
        [FromQuery] string? facebookKind = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListLeadsAsync(status, limit, cancellationToken, province, facebookKind));

    [HttpGet("leads/filters")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesLeadFilterDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesLeadFilterDto>> LeadFilters(
        CancellationToken cancellationToken) =>
        Ok(await _desk.GetLeadFiltersAsync(cancellationToken));

    [HttpPost("leads")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesLeadDto>> CreateLead(
        [FromBody] CreateKitSalesLeadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateLeadAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("prospects/import")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesImportResultDto>> ImportProspects(
        [FromBody] ImportKitSalesProspectsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.ImportProspectsAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("prospects")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesLeadDto>> CreateProspect(
        [FromBody] CreateKitSalesProspectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateProspectAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("leads/{leadId:guid}")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesLeadDto>> UpdateLead(
        Guid leadId,
        [FromBody] UpdateKitSalesLeadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.UpdateLeadAsync(leadId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("leads/{leadId:guid}")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteLead(Guid leadId, CancellationToken cancellationToken)
    {
        try
        {
            await _desk.DeleteLeadAsync(leadId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("leads/{leadId:guid}")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesLeadDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitSalesLeadDetailDto>> LeadDetail(
        Guid leadId,
        CancellationToken cancellationToken)
    {
        var detail = await _desk.GetLeadDetailAsync(leadId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("leads/{leadId:guid}/interactions")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesInteractionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesInteractionDto>> CreateInteraction(
        Guid leadId,
        [FromBody] CreateKitSalesInteractionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateInteractionAsync(leadId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("leads/{leadId:guid}/tasks")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesTaskDto>> CreateTask(
        Guid leadId,
        [FromBody] CreateKitSalesTaskRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateTaskAsync(leadId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("tasks/{taskId:guid}/complete")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesTaskDto>> CompleteTask(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CompleteTaskAsync(taskId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("pipeline/summary")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesPipelineSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesPipelineSummaryDto>> PipelineSummary(
        CancellationToken cancellationToken) =>
        Ok(await _desk.GetPipelineSummaryAsync(cancellationToken));

    [HttpGet("chat/channel")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesChatChannelDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesChatChannelDto>> ChatChannel(CancellationToken cancellationToken) =>
        Ok(await _chat.GetChannelAsync(cancellationToken));

    [HttpPost("chat/preview")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesChatPreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesChatPreviewDto>> ChatPreview(
        [FromBody] KitSalesChatPreviewRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _chat.PreviewAsync(request.Text ?? "", cancellationToken));

    [HttpGet("pains")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesPainDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesPainDto>>> Pains(CancellationToken cancellationToken)
    {
        await _pains.EnsureCatalogAsync(cancellationToken);
        return Ok(_pains.ListPains());
    }

    [HttpGet("pains/market")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesPainMarketDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesPainMarketDto>> PainMarket(CancellationToken cancellationToken) =>
        Ok(await _pains.GetMarketAsync(cancellationToken));

    [HttpGet("leads/{leadId:guid}/pains")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesLeadPainProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesLeadPainProfileDto>> LeadPains(
        Guid leadId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _pains.GetLeadProfileAsync(leadId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("leads/{leadId:guid}/pains/{painCode}/response")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LeadPainResponse(
        Guid leadId,
        string painCode,
        [FromBody] RecordKitSalesPainResponseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _pains.RecordResponseAsync(leadId, painCode, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("journey/pains")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesJourneyPainDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<KitSalesJourneyPainDto>> JourneyPains() =>
        Ok(_chat.ListPains());

    [HttpGet("journey/due")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesJourneyPlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesJourneyPlanDto>>> JourneyDue(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _chat.ListDueJourneysAsync(limit, cancellationToken));

    [HttpGet("leads/{leadId:guid}/journey")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesJourneyPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesJourneyPlanDto>> LeadJourney(
        Guid leadId,
        [FromQuery] string? pain = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _chat.GetJourneyAsync(leadId, pain, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("leads/{leadId:guid}/journey/schedule")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesJourneyPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesJourneyPlanDto>> ScheduleJourney(
        Guid leadId,
        [FromBody] ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _chat.ScheduleJourneyAsync(leadId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("leads/{leadId:guid}/journey/send")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesJourneyPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesJourneyPlanDto>> SendJourney(
        Guid leadId,
        [FromBody] ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _chat.SendJourneyAsync(leadId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("leads/{leadId:guid}/journey/rewrite")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesJourneyPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesJourneyPlanDto>> RewriteJourney(
        Guid leadId,
        [FromBody] ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _chat.RewriteJourneyAsync(leadId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("leads/{leadId:guid}/assist")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesAssistDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesAssistDto>> LeadAssist(
        Guid leadId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _chat.GetAssistAsync(leadId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("chat/compose")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesChatComposeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesChatComposeDto>> ChatCompose(
        [FromBody] KitSalesChatPreviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _chat.ComposeAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("chat/drafts/{draftId:guid}/send")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesInteractionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesInteractionDto>> SendChatDraft(
        Guid draftId,
        [FromBody] SendKitSalesDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _chat.SendDraftAsync(draftId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("chat/drafts/{draftId:guid}/approve")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesInteractionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesInteractionDto>> ApproveChatDraft(
        Guid draftId,
        [FromBody] SendKitSalesDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.ReviewInteractionAsync(
                draftId,
                new ReviewKitSalesInteractionRequest("approved", request.Content),
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("chat/reply")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesChatPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesChatPreviewDto>> ChatReply(
        [FromBody] KitSalesChatPreviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _chat.ReplyAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("settings/gemini")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesGeminiSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesGeminiSettingsDto>> GeminiSettings(
        CancellationToken cancellationToken) =>
        Ok(await _ai.GetMaskedAsync(cancellationToken));

    [HttpPut("settings/gemini")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesGeminiSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesGeminiSettingsDto>> SaveGeminiSettings(
        [FromBody] SaveKitSalesGeminiSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _ai.SaveAsync(request, cancellationToken));

    [HttpPost("settings/gemini/test")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesGeminiTestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesGeminiTestDto>> TestGeminiSettings(
        CancellationToken cancellationToken) =>
        Ok(await _ai.TestAsync(cancellationToken));

    [HttpGet("settings/facebook")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesFacebookSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesFacebookSettingsDto>> FacebookSettings(
        CancellationToken cancellationToken) =>
        Ok(await _facebook.GetMaskedAsync(WebhookUrl(), cancellationToken));

    [HttpPut("settings/facebook")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesFacebookSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesFacebookSettingsDto>> SaveFacebookSettings(
        [FromBody] SaveKitSalesFacebookSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _facebook.SaveAsync(request, WebhookUrl(), cancellationToken));

    [HttpPost("settings/facebook/test")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesFacebookTestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesFacebookTestDto>> TestFacebookSettings(
        CancellationToken cancellationToken) =>
        Ok(await _facebook.TestAsync(cancellationToken));

    [HttpGet("settings/facebook/psids")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesFacebookPsidDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesFacebookPsidDto>>> FacebookPsids(
        CancellationToken cancellationToken) =>
        Ok(await _facebook.ListPsidsAsync(cancellationToken));

    [HttpGet("leads/{leadId:guid}/psid")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesFacebookPsidDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<KitSalesFacebookPsidDto>> LeadPsid(
        Guid leadId,
        CancellationToken cancellationToken)
    {
        var row = await _facebook.GetPsidAsync(leadId, cancellationToken);
        return row is null ? NoContent() : Ok(row);
    }

    [HttpPost("leads/{leadId:guid}/psid")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesFacebookPsidDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesFacebookPsidDto>> AttachLeadPsid(
        Guid leadId,
        [FromBody] AttachKitSalesPsidRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _facebook.AttachPsidAsync(leadId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("leads/{leadId:guid}/psid")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DetachLeadPsid(Guid leadId, CancellationToken cancellationToken)
    {
        await _facebook.DetachPsidAsync(leadId, cancellationToken);
        return NoContent();
    }

    private string WebhookUrl()
    {
        var api = (_config["Platform:ApiUrl"] ?? "").TrimEnd('/');
        return string.IsNullOrWhiteSpace(api)
            ? "/api/kit-sales/facebook/webhook"
            : $"{api}/api/kit-sales/facebook/webhook";
    }
}

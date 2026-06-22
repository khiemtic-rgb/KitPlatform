using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using PharmaCore.Api.Authorization;

using PharmaCore.Application.Procurement;



namespace PharmaCore.Api.Controllers.Procurement;



[ApiController]

[Authorize]

[Route("api/procurement/supplier-payments")]

public sealed class SupplierPaymentsController : ControllerBase

{

    private readonly ISupplierPaymentService _payments;



    public SupplierPaymentsController(ISupplierPaymentService payments) => _payments = payments;



    [HttpGet]

    [Authorize(Policy = ProcurementPolicies.Read)]

    public async Task<ActionResult<IReadOnlyList<SupplierPaymentListItemDto>>> List(

        [FromQuery] string? search,

        [FromQuery] Guid? supplierId,

        [FromQuery] short? status,

        [FromQuery] DateOnly? dateFrom,

        [FromQuery] DateOnly? dateTo,

        CancellationToken cancellationToken = default) =>

        Ok(await _payments.GetAllAsync(

            new SupplierPaymentListFilter(search, supplierId, status, dateFrom, dateTo),

            cancellationToken));



    [HttpGet("{id:guid}")]

    [Authorize(Policy = ProcurementPolicies.Read)]

    public async Task<ActionResult<SupplierPaymentListItemDto>> Get(Guid id, CancellationToken cancellationToken)

    {

        var item = await _payments.GetAsync(id, cancellationToken);

        return item is null ? NotFound() : Ok(item);

    }



    [HttpPost]

    [Authorize(Policy = ProcurementPolicies.Write)]

    public async Task<ActionResult<SupplierPaymentListItemDto>> Create(

        [FromBody] CreateSupplierPaymentRequest request,

        CancellationToken cancellationToken)

    {

        var item = await _payments.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);

    }



    [HttpPut("{id:guid}")]

    [Authorize(Policy = ProcurementPolicies.Write)]

    public async Task<ActionResult<SupplierPaymentListItemDto>> Update(

        Guid id,

        [FromBody] UpdateSupplierPaymentRequest request,

        CancellationToken cancellationToken)

    {

        var item = await _payments.UpdateAsync(id, request, cancellationToken);

        return item is null ? NotFound() : Ok(item);

    }



    [HttpPost("{id:guid}/post")]

    [Authorize(Policy = ProcurementPolicies.Write)]

    public async Task<ActionResult<SupplierPaymentListItemDto>> Post(Guid id, CancellationToken cancellationToken)

    {

        var item = await _payments.PostAsync(id, cancellationToken);

        return item is null ? NotFound() : Ok(item);

    }



    [HttpPost("{id:guid}/cancel")]

    [Authorize(Policy = ProcurementPolicies.Write)]

    public async Task<ActionResult<SupplierPaymentListItemDto>> Cancel(Guid id, CancellationToken cancellationToken)

    {

        var item = await _payments.CancelAsync(id, cancellationToken);

        return item is null ? NotFound() : Ok(item);

    }

}


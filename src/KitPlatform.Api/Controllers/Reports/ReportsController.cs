using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Reports;

namespace KitPlatform.Api.Controllers.Reports;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportsService _reports;

    public ReportsController(IReportsService reports) => _reports = reports;

    [HttpGet("catalog")]
    [Authorize(Policy = ReportsPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<ReportCatalogItemDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ReportCatalogItemDto>> Catalog() =>
        Ok(_reports.GetCatalog());

    [HttpGet("sales/revenue-by-period")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesRevenueByPeriod(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string groupBy = ReportGroupBy.Day,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesRevenueByPeriodAsync(from, to, groupBy, warehouseId, cancellationToken);

    [HttpGet("sales/revenue-by-payment-method")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesRevenueByPaymentMethod(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesRevenueByPaymentMethodAsync(from, to, warehouseId, cancellationToken);

    [HttpGet("sales/shifts")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesShifts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesShiftsAsync(from, to, warehouseId, cancellationToken);

    [HttpGet("sales/revenue-by-category")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesRevenueByCategory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesRevenueByCategoryAsync(from, to, warehouseId, cancellationToken);

    [HttpGet("sales/revenue-by-clinic-doctor")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesRevenueByClinicDoctor(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesRevenueByClinicDoctorAsync(from, to, warehouseId, cancellationToken);

    [HttpGet("procurement/grn-value")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> ProcurementGrnValue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string groupBy = ReportGroupBy.Supplier,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunProcurementGrnValueAsync(from, to, groupBy, supplierId, warehouseId, cancellationToken);

    [HttpGet("procurement/payables-snapshot")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> ProcurementPayablesSnapshot(CancellationToken cancellationToken = default) =>
        _reports.RunProcurementPayablesSnapshotAsync(cancellationToken);

    [HttpGet("inventory/stock-snapshot")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> InventoryStockSnapshot(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunInventoryStockSnapshotAsync(warehouseId, search, cancellationToken);

    [HttpGet("inventory/near-expiry")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> InventoryNearExpiry(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] int expiryDays = 30,
        CancellationToken cancellationToken = default) =>
        _reports.RunInventoryNearExpiryAsync(warehouseId, expiryDays, cancellationToken);

    [HttpGet("inventory/movement-summary")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> InventoryMovementSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunInventoryMovementSummaryAsync(from, to, warehouseId, search, cancellationToken);
}

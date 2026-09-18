using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Reports;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Api.Controllers.Reports;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportsService _reports;
    private readonly ISalesService _sales;

    public ReportsController(IReportsService reports, ISalesService sales)
    {
        _reports = reports;
        _sales = sales;
    }

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

    [HttpGet("sales/shifts/{id:guid}")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public async Task<ActionResult<SalesShiftDetailDto>> SalesShiftDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var shift = await _sales.GetShiftAsync(id, cancellationToken);
        return shift is null ? NotFound() : Ok(shift);
    }

    [HttpGet("sales/shift-close-by-employee")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesShiftCloseByEmployee(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? branchId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesShiftCloseByEmployeeAsync(from, to, warehouseId, employeeId, branchId, cancellationToken);

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

    [HttpGet("sales/revenue-by-employee")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesRevenueByEmployee(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? employeeId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesRevenueByEmployeeAsync(from, to, warehouseId, employeeId, cancellationToken);

    [HttpGet("sales/revenue-by-employee-product")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesRevenueByEmployeeProduct(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesRevenueByEmployeeProductAsync(from, to, warehouseId, employeeId, search, cancellationToken);

    [HttpGet("sales/revenue-by-customer")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> SalesRevenueByCustomer(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunSalesRevenueByCustomerAsync(from, to, warehouseId, search, cancellationToken);

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

    [HttpGet("procurement/grn-documents")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> ProcurementGrnDocuments(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string groupBy = ReportGroupBy.Day,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunProcurementGrnDocumentsAsync(from, to, groupBy, supplierId, warehouseId, search, cancellationToken);

    [HttpGet("procurement/payables-snapshot")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> ProcurementPayablesSnapshot(CancellationToken cancellationToken = default) =>
        _reports.RunProcurementPayablesSnapshotAsync(cancellationToken);

    [HttpGet("inventory/stock-snapshot")]
    [Authorize(Policy = ReportsPolicies.Read)]
    public Task<ReportTableResultDto> InventoryStockSnapshot(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken cancellationToken = default) =>
        _reports.RunInventoryStockSnapshotAsync(warehouseId, search, categoryId, cancellationToken);

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

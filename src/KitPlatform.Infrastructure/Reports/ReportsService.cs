using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Procurement;
using KitPlatform.Application.Reports;

namespace KitPlatform.Infrastructure.Reports;

internal sealed class ReportsService : IReportsService
{
    private readonly ReportsRepository _repository;
    private readonly ISupplierPayablesService _payables;
    private readonly IBranchAccessService _branchAccess;

    public ReportsService(
        ReportsRepository repository,
        ISupplierPayablesService payables,
        IBranchAccessService branchAccess)
    {
        _repository = repository;
        _payables = payables;
        _branchAccess = branchAccess;
    }

    public IReadOnlyList<ReportCatalogItemDto> GetCatalog() =>
    [
        new(ReportCodes.SalesRevenueByPeriod, "Doanh thu theo kỳ", "sales",
            "Doanh thu theo ngày bán (kể cả nợ), khách trả, khách nợ, thu công nợ và hình thức thanh toán.", true, false, false),
        new(ReportCodes.SalesRevenueByPaymentMethod, "Thu tiền theo hình thức", "sales",
            "Tiền đã thu theo tiền mặt, thẻ, chuyển khoản, ví — tách thu tại quầy và thu công nợ.", true, false, false),
        new(ReportCodes.SalesShifts, "Ca bán hàng", "sales",
            "Danh sách ca và dòng Ngoài ca (đơn không gắn ca). Quỹ tiền mặt, doanh thu, nợ mới, thu công nợ.", true, false, false),
        new(ReportCodes.SalesRevenueByCategory, "Doanh thu theo nhóm sản phẩm", "sales",
            "Doanh thu ròng theo nhóm sản phẩm trong kỳ.", true, false, false),
        new(ReportCodes.SalesRevenueByClinicDoctor, "Đơn bán theo phòng khám / bác sĩ", "sales",
            "Tổng hợp đơn Connect (PK) theo phòng khám và bác sĩ kê đơn — số đơn, thu bán, hoàn, thu ròng.", true, false, false),
        new(ReportCodes.SalesRevenueByEmployee, "Doanh số theo nhân viên", "sales",
            "Thu bán, hoàn, thu ròng và số đơn theo người trên POS — không xếp hạng.", true, false, false),
        new(ReportCodes.SalesRevenueByEmployeeProduct, "Doanh số nhân viên theo sản phẩm", "sales",
            "Số lượng và thu ròng từng mặt theo người bán — dùng đối soát KPI / hoa hồng vượt mốc.", true, false, false),
        new(ReportCodes.SalesRevenueByCustomer, "Doanh số theo khách hàng", "sales",
            "Doanh thu và số đơn POS gắn hồ sơ khách theo ngày bán.", true, false, false),
        new(ReportCodes.SalesShiftCloseByEmployee, "Chốt ca theo nhân viên", "sales",
            "Một dòng mỗi người × ca: doanh thu, nợ mới, thu công nợ, tiền mặt, chuyển khoản, thu ròng.", true, false, false),
        new(ReportCodes.SalesReceivablesMovement, "Công nợ khách theo kỳ", "sales",
            "Nợ đầu, bán chịu, thu nợ, hoàn trừ nợ, nợ cuối và tuổi nợ còn lại.", true, false, false),
        new(ReportCodes.ProcurementGrnValue, "Giá trị nhập hàng", "procurement",
            "Tổng hợp phiếu nhập hoàn tất — số tiền trước thuế GTGT.", false, true, false),
        new(ReportCodes.ProcurementPayablesSnapshot, "Công nợ nhà cung cấp", "procurement",
            "Số còn phải trả và tuổi nợ tại thời điểm xem báo cáo.", false, true, false),
        new(ReportCodes.InventoryStockSnapshot, "Tồn kho hiện tại", "inventory",
            "Số lượng và giá trị tồn (tồn × giá vốn lô) theo sản phẩm và kho.", false, false, true),
        new(ReportCodes.InventoryNearExpiry, "Hàng cận hạn", "inventory",
            "Lô còn tồn có hạn dùng trong số ngày cảnh báo.", false, false, true),
        new(ReportCodes.InventoryMovementSummary, "Xuất — nhập — tồn", "inventory",
            "Tồn đầu kỳ, nhập/xuất trong kỳ và tồn cuối theo sản phẩm/kho.", false, false, true),
    ];

    public async Task<ReportTableResultDto> RunSalesRevenueByPeriodAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        groupBy = NormalizePeriodGroupBy(groupBy);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesRevenueByPeriodAsync(from, to, groupBy, scopedWarehouseId, allowed, cancellationToken);
        var columns = new List<ReportColumnDto>
        {
            Col("periodLabel", "Kỳ", ReportColumnFormats.Text, "left"),
            Col("orderCount", "Số đơn", ReportColumnFormats.Integer, "right"),
            Col("salesAmount", "Doanh thu", ReportColumnFormats.Money, "right"),
            Col("checkoutPaid", "Khách trả", ReportColumnFormats.Money, "right"),
            Col("newDebt", "Khách nợ", ReportColumnFormats.Money, "right"),
            Col("collectionAmount", "Thu công nợ", ReportColumnFormats.Money, "right"),
            Col("cashAmount", "Tiền mặt", ReportColumnFormats.Money, "right"),
            Col("transferAmount", "Chuyển khoản", ReportColumnFormats.Money, "right"),
            Col("cardAmount", "Thẻ", ReportColumnFormats.Money, "right"),
            Col("ewalletAmount", "Ví", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn", ReportColumnFormats.Money, "right"),
            Col("refundCash", "Hoàn tiền", ReportColumnFormats.Money, "right"),
            Col("refundDebt", "Hoàn trừ nợ", ReportColumnFormats.Money, "right"),
            Col("netAmount", "DT ròng", ReportColumnFormats.Money, "right"),
        };
        var filters = FilterLabels(from, to, groupBy, warehouseId);
        filters["Ghi chú"] =
            "Doanh thu = giá trị đơn theo ngày bán (kể cả nợ). Khách trả = thu tại quầy. Thu công nợ không tính vào doanh thu. Hoàn tiền = trả mặt; hoàn trừ nợ = giảm công nợ.";
        return BuildTable(
            ReportCodes.SalesRevenueByPeriod,
            "Doanh thu theo kỳ",
            filters,
            columns,
            rows,
            SumTotals(rows, "orderCount", "salesAmount", "checkoutPaid", "newDebt", "collectionAmount",
                "cashAmount", "transferAmount", "cardAmount", "ewalletAmount", "refundAmount",
                "refundCash", "refundDebt", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesRevenueByPaymentMethodAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesRevenueByPaymentMethodAsync(from, to, scopedWarehouseId, allowed, cancellationToken);
        var columns = new List<ReportColumnDto>
        {
            Col("paymentMethodLabel", "Hình thức", ReportColumnFormats.Text, "left"),
            Col("checkoutAmount", "Thu tại quầy", ReportColumnFormats.Money, "right"),
            Col("collectionAmount", "Thu công nợ", ReportColumnFormats.Money, "right"),
            Col("salesAmount", "Tổng thu", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn tiền", ReportColumnFormats.Money, "right"),
            Col("netAmount", "Thu ròng", ReportColumnFormats.Money, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        filters["Ghi chú"] = "Sổ quỹ theo ngày thu — không phải doanh thu. Đơn nợ không tạo dòng hình thức.";
        return BuildTable(
            ReportCodes.SalesRevenueByPaymentMethod,
            "Thu tiền theo hình thức",
            filters,
            columns,
            rows,
            SumTotals(rows, "checkoutAmount", "collectionAmount", "salesAmount", "refundAmount", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesShiftsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesShiftsAsync(from, to, scopedWarehouseId, allowed, cancellationToken);
        var columns = new List<ReportColumnDto>
        {
            Col("shiftNumber", "Mã ca", ReportColumnFormats.Text, "left"),
            Col("warehouseName", "Kho", ReportColumnFormats.Text, "left"),
            Col("openedAt", "Mở ca", ReportColumnFormats.Date, "left"),
            Col("closedAt", "Đóng ca", ReportColumnFormats.Date, "left"),
            Col("statusLabel", "Trạng thái", ReportColumnFormats.Text, "left"),
            Col("openingCash", "Quỹ đầu ca", ReportColumnFormats.Money, "right"),
            Col("closingCash", "Quỹ cuối ca", ReportColumnFormats.Money, "right"),
            Col("cashVariance", "Chênh lệch TM", ReportColumnFormats.Money, "right"),
            Col("revenueAmount", "Doanh thu ca", ReportColumnFormats.Money, "right"),
            Col("newDebt", "Nợ mới", ReportColumnFormats.Money, "right"),
            Col("collectionAmount", "Thu công nợ", ReportColumnFormats.Money, "right"),
            Col("netAmount", "Thu ròng ca", ReportColumnFormats.Money, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        filters["Ghi chú"] =
            "Dòng «Ngoài ca» gộp đơn bán không gắn ca trong kỳ (cùng kho). Thu công nợ trên dòng ca theo thời điểm thu.";
        return BuildTable(
            ReportCodes.SalesShifts,
            "Báo cáo ca làm việc",
            filters,
            columns,
            rows,
            SumTotals(rows, "revenueAmount", "newDebt", "collectionAmount", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesRevenueByCategoryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesRevenueByCategoryAsync(from, to, scopedWarehouseId, allowed, cancellationToken);
        rows = AppendSharePercent(rows);

        var columns = new List<ReportColumnDto>
        {
            Col("categoryLabel", "Danh mục", ReportColumnFormats.Text, "left"),
            Col("salesAmount", "Doanh thu", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "DT ròng", ReportColumnFormats.Money, "right"),
            Col("sharePercent", "Tỷ lệ %", ReportColumnFormats.Qty, "right"),
        };
        return BuildTable(
            ReportCodes.SalesRevenueByCategory,
            "Doanh thu theo danh mục",
            FilterLabels(from, to, null, warehouseId),
            columns,
            rows,
            SumTotals(rows, "salesAmount", "refundAmount", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesRevenueByClinicDoctorAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesRevenueByClinicDoctorAsync(from, to, scopedWarehouseId, allowed, cancellationToken);
        rows = AppendSharePercent(rows);

        var columns = new List<ReportColumnDto>
        {
            Col("clinicName", "Phòng khám", ReportColumnFormats.Text, "left"),
            Col("doctorName", "Bác sĩ", ReportColumnFormats.Text, "left"),
            Col("orderCount", "Số đơn", ReportColumnFormats.Integer, "right"),
            Col("salesAmount", "Doanh thu", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "DT ròng", ReportColumnFormats.Money, "right"),
            Col("sharePercent", "Tỷ lệ %", ReportColumnFormats.Qty, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        filters["Ghi chú"] =
            "Theo ngày bán, kể cả đơn nợ. Chỉ đơn POS gắn handoff Connect.";
        return BuildTable(
            ReportCodes.SalesRevenueByClinicDoctor,
            "Đơn bán theo phòng khám / bác sĩ",
            filters,
            columns,
            rows,
            SumTotals(rows, "orderCount", "salesAmount", "refundAmount", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesRevenueByEmployeeAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        Guid? employeeId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesRevenueByEmployeeAsync(
            from, to, scopedWarehouseId, allowed, employeeId, cancellationToken);
        rows = AppendSharePercent(rows);

        var columns = new List<ReportColumnDto>
        {
            Col("employeeName", "Nhân viên", ReportColumnFormats.Text, "left"),
            Col("orderCount", "Số đơn", ReportColumnFormats.Integer, "right"),
            Col("namedOrderCount", "Đơn gắn khách", ReportColumnFormats.Integer, "right"),
            Col("salesAmount", "Doanh thu", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "DT ròng", ReportColumnFormats.Money, "right"),
            Col("aov", "AOV", ReportColumnFormats.Money, "right"),
            Col("sharePercent", "Tỷ lệ %", ReportColumnFormats.Qty, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        if (employeeId.HasValue) filters["Nhân viên"] = employeeId.Value.ToString();
        filters["Ghi chú"] = "Theo ngày bán, kể cả đơn nợ. Hoàn về người của đơn gốc.";
        return BuildTable(
            ReportCodes.SalesRevenueByEmployee,
            "Doanh số theo nhân viên",
            filters,
            columns,
            rows,
            SumTotals(rows, "orderCount", "namedOrderCount", "salesAmount", "refundAmount", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesRevenueByEmployeeProductAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        Guid? employeeId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesRevenueByEmployeeProductAsync(
            from, to, scopedWarehouseId, allowed, employeeId, search, cancellationToken);

        var columns = new List<ReportColumnDto>
        {
            Col("employeeName", "Nhân viên", ReportColumnFormats.Text, "left"),
            Col("productCode", "Mã SP", ReportColumnFormats.Text, "left"),
            Col("productName", "Sản phẩm", ReportColumnFormats.Text, "left"),
            Col("orderCount", "Số đơn", ReportColumnFormats.Integer, "right"),
            Col("qty", "SL bán", ReportColumnFormats.Qty, "right"),
            Col("refundQty", "SL trả", ReportColumnFormats.Qty, "right"),
            Col("netQty", "SL ròng", ReportColumnFormats.Qty, "right"),
            Col("salesAmount", "Doanh thu", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "DT ròng", ReportColumnFormats.Money, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        if (employeeId.HasValue) filters["Nhân viên"] = employeeId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(search)) filters["Tìm kiếm"] = search.Trim();
        filters["Ghi chú"] = "Theo ngày bán, kể cả đơn nợ. SL ròng = bán − trả.";
        return BuildTable(
            ReportCodes.SalesRevenueByEmployeeProduct,
            "Doanh số nhân viên theo sản phẩm",
            filters,
            columns,
            rows,
            SumTotals(rows, "orderCount", "qty", "refundQty", "netQty", "salesAmount", "refundAmount", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesShiftCloseByEmployeeAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        Guid? employeeId,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        if (branchId.HasValue)
            await _branchAccess.EnsureBranchAccessAsync(branchId.Value, cancellationToken);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesShiftCloseByEmployeeAsync(
            from, to, scopedWarehouseId, allowed, employeeId, branchId, cancellationToken);
        rows = FilterRowsByGuid(rows, "employeeId", employeeId);
        rows = FilterRowsByGuid(rows, "branchId", branchId);
        if (scopedWarehouseId.HasValue)
            rows = FilterRowsByGuid(rows, "warehouseId", scopedWarehouseId);

        var columns = new List<ReportColumnDto>
        {
            Col("employeeName", "Nhân viên", ReportColumnFormats.Text, "left"),
            Col("branchName", "Chi nhánh", ReportColumnFormats.Text, "left"),
            Col("warehouseName", "Kho", ReportColumnFormats.Text, "left"),
            Col("shiftNumber", "Ca", ReportColumnFormats.Text, "left"),
            Col("openedAt", "Mở ca / ngày", ReportColumnFormats.Date, "left"),
            Col("statusLabel", "Trạng thái", ReportColumnFormats.Text, "left"),
            Col("orderCount", "Số đơn", ReportColumnFormats.Integer, "right"),
            Col("revenueAmount", "Doanh thu", ReportColumnFormats.Money, "right"),
            Col("checkoutPaid", "Khách trả", ReportColumnFormats.Money, "right"),
            Col("newDebt", "Nợ mới", ReportColumnFormats.Money, "right"),
            Col("collectionAmount", "Thu công nợ", ReportColumnFormats.Money, "right"),
            Col("salesAmount", "Tiền thu", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn tiền", ReportColumnFormats.Money, "right"),
            Col("cashNet", "Tiền mặt", ReportColumnFormats.Money, "right"),
            Col("transferNet", "Chuyển khoản", ReportColumnFormats.Money, "right"),
            Col("otherNet", "Khác", ReportColumnFormats.Money, "right"),
            Col("netAmount", "Thu ròng", ReportColumnFormats.Money, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        if (branchId.HasValue) filters["Chi nhánh"] = branchId.Value.ToString();
        if (employeeId.HasValue) filters["Nhân viên"] = employeeId.Value.ToString();
        filters["Ghi chú"] =
            "Doanh thu / nợ mới theo ngày bán. Tiền thu / TM / CK là sổ quỹ theo ngày thu. Bấm dòng có mã ca để mở tờ chốt.";
        return BuildTable(
            ReportCodes.SalesShiftCloseByEmployee,
            "Chốt ca theo nhân viên",
            filters,
            columns,
            rows,
            SumTotals(rows, "orderCount", "revenueAmount", "checkoutPaid", "newDebt", "collectionAmount", "salesAmount",
                "refundAmount", "cashNet", "transferNet", "otherNet", "netAmount"));
    }

    public async Task<ReportTableResultDto> RunSalesRevenueByCustomerAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesRevenueByCustomerAsync(
            from, to, scopedWarehouseId, allowed, search, cancellationToken);
        rows = AppendSharePercent(rows);
        var insight = await _repository.GetSalesCustomerInsightAsync(
            from, to, scopedWarehouseId, allowed, cancellationToken);

        var columns = new List<ReportColumnDto>
        {
            Col("customerCode", "Mã khách", ReportColumnFormats.Text, "left"),
            Col("customerName", "Tên khách", ReportColumnFormats.Text, "left"),
            Col("orderCount", "Số đơn", ReportColumnFormats.Integer, "right"),
            Col("netAmount", "DT ròng", ReportColumnFormats.Money, "right"),
            Col("lastOrderAt", "Đơn gần nhất", ReportColumnFormats.Date, "left"),
            Col("segmentLabel", "Nhóm", ReportColumnFormats.Text, "left"),
            Col("sharePercent", "Tỷ lệ %", ReportColumnFormats.Qty, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        if (!string.IsNullOrWhiteSpace(search)) filters["Tìm kiếm"] = search.Trim();
        filters["Ghi chú"] = "Theo ngày bán, kể cả đơn nợ. Chỉ đơn gắn hồ sơ khách.";

        var totals = SumTotals(rows, "orderCount", "salesAmount", "refundAmount", "netAmount")
            ?? new Dictionary<string, object?>();
        totals["customerName"] = "Tổng cộng";
        totals["walkInOrderCount"] = insight.WalkInOrderCount;
        totals["allOrderCount"] = insight.AllOrderCount;
        totals["weekdayOrderCount"] = insight.WeekdayOrderCount;
        totals["weekendOrderCount"] = insight.WeekendOrderCount;
        totals["peakHour"] = insight.PeakHour;
        totals["peakHourOrders"] = insight.PeakHourOrders;
        totals["firstTimeCount"] = rows.Count(r => r.TryGetValue("isReturning", out var v) && v is false);
        totals["returningCount"] = rows.Count(r => r.TryGetValue("isReturning", out var v) && v is true);

        return BuildTable(
            ReportCodes.SalesRevenueByCustomer,
            "Doanh số theo khách hàng",
            filters,
            columns,
            rows,
            totals);
    }

    public async Task<ReportTableResultDto> RunSalesReceivablesMovementAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetSalesReceivablesMovementAsync(
            from, to, scopedWarehouseId, allowed, cancellationToken);

        var columns = new List<ReportColumnDto>
        {
            Col("customerCode", "Mã khách", ReportColumnFormats.Text, "left"),
            Col("customerName", "Tên khách", ReportColumnFormats.Text, "left"),
            Col("opening", "Nợ đầu", ReportColumnFormats.Money, "right"),
            Col("creditSales", "Bán chịu", ReportColumnFormats.Money, "right"),
            Col("collections", "Thu nợ", ReportColumnFormats.Money, "right"),
            Col("returnAgainstDebt", "Hoàn trừ nợ", ReportColumnFormats.Money, "right"),
            Col("closing", "Nợ cuối", ReportColumnFormats.Money, "right"),
            Col("agingCurrent", "0–30 ngày", ReportColumnFormats.Money, "right"),
            Col("aging31To60", "31–60", ReportColumnFormats.Money, "right"),
            Col("aging61To90", "61–90", ReportColumnFormats.Money, "right"),
            Col("agingOver90", "> 90", ReportColumnFormats.Money, "right"),
            Col("openDocuments", "Đơn còn nợ", ReportColumnFormats.Integer, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        filters["Ghi chú"] =
            "Nợ đầu/cuối tái lập từ đơn bán − thu tại quầy − thu nợ − hoàn trừ nợ. Tuổi nợ theo số còn hiện tại.";
        return BuildTable(
            ReportCodes.SalesReceivablesMovement,
            "Công nợ khách theo kỳ",
            filters,
            columns,
            rows,
            SumTotals(rows, "opening", "creditSales", "collections", "returnAgainstDebt", "closing",
                "agingCurrent", "aging31To60", "aging61To90", "agingOver90", "openDocuments"));
    }

    public async Task<ReportTableResultDto> RunProcurementGrnValueAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy,
        Guid? supplierId,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        groupBy = groupBy is ReportGroupBy.Supplier or ReportGroupBy.Month or ReportGroupBy.Week or ReportGroupBy.Day
            ? groupBy
            : ReportGroupBy.Supplier;

        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetProcurementGrnValueAsync(from, to, groupBy, supplierId, scopedWarehouseId, allowed, cancellationToken);

        if (groupBy == ReportGroupBy.Supplier)
        {
            var columns = new List<ReportColumnDto>
            {
                Col("supplierCode", "Mã NCC", ReportColumnFormats.Text, "left"),
                Col("supplierName", "Nhà cung cấp", ReportColumnFormats.Text, "left"),
                Col("grnCount", "Số phiếu nhập", ReportColumnFormats.Integer, "right"),
                Col("totalQty", "Tổng số lượng", ReportColumnFormats.Qty, "right"),
                Col("preTaxAmount", "Tiền trước thuế", ReportColumnFormats.Money, "right"),
            };
            var filters = FilterLabels(from, to, null, warehouseId);
            if (supplierId.HasValue) filters["NCC"] = supplierId.Value.ToString();
            filters["Nhóm theo"] = "Nhà cung cấp";
            filters["Ghi chú"] = "Phiếu nhập hoàn tất — số tiền trước thuế GTGT";
            return BuildTable(
                ReportCodes.ProcurementGrnValue,
                "Giá trị nhập hàng theo nhà cung cấp",
                filters,
                columns,
                rows,
                SumTotals(rows, "grnCount", "totalQty", "preTaxAmount"));
        }

        var periodColumns = new List<ReportColumnDto>
        {
            Col("periodLabel", "Kỳ", ReportColumnFormats.Text, "left"),
            Col("grnCount", "Số phiếu nhập", ReportColumnFormats.Integer, "right"),
            Col("totalQty", "Tổng số lượng", ReportColumnFormats.Qty, "right"),
            Col("preTaxAmount", "Tiền trước thuế", ReportColumnFormats.Money, "right"),
        };
        var periodFilters = FilterLabels(from, to, groupBy, warehouseId);
        periodFilters["Ghi chú"] = "Phiếu nhập hoàn tất — số tiền trước thuế GTGT";
        return BuildTable(
            ReportCodes.ProcurementGrnValue,
            groupBy == ReportGroupBy.Month
                ? "Giá trị nhập hàng theo tháng"
                : groupBy == ReportGroupBy.Week
                    ? "Giá trị nhập hàng theo tuần"
                    : "Giá trị nhập hàng theo ngày",
            periodFilters,
            periodColumns,
            rows,
            SumTotals(rows, "grnCount", "totalQty", "preTaxAmount"));
    }

    public async Task<ReportTableResultDto> RunProcurementGrnDocumentsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy,
        Guid? supplierId,
        Guid? warehouseId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        groupBy = NormalizePeriodGroupBy(groupBy);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetProcurementGrnDocumentsAsync(
            from, to, groupBy, supplierId, scopedWarehouseId, allowed, search, cancellationToken);

        var columns = new List<ReportColumnDto>
        {
            Col("periodLabel", "Kỳ", ReportColumnFormats.Text, "left"),
            Col("grnNumber", "Số phiếu nhập", ReportColumnFormats.Text, "left"),
            Col("receiptDate", "Ngày nhập", ReportColumnFormats.Date, "left"),
            Col("supplierName", "Nhà cung cấp", ReportColumnFormats.Text, "left"),
            Col("warehouseName", "Kho", ReportColumnFormats.Text, "left"),
            Col("totalQty", "Số lượng", ReportColumnFormats.Qty, "right"),
            Col("preTaxAmount", "Tiền trước thuế", ReportColumnFormats.Money, "right"),
            Col("taxAmount", "Thuế GTGT", ReportColumnFormats.Money, "right"),
            Col("totalAmount", "Tổng tiền", ReportColumnFormats.Money, "right"),
            Col("statusLabel", "Trạng thái", ReportColumnFormats.Text, "left"),
        };
        var filters = FilterLabels(from, to, groupBy, warehouseId);
        if (supplierId.HasValue) filters["NCC"] = supplierId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(search)) filters["Tìm kiếm"] = search.Trim();
        filters["Ghi chú"] = "Phiếu nhập hoàn tất — tiền hàng, thuế GTGT và tổng thanh toán";

        return BuildTable(
            ReportCodes.ProcurementGrnValue,
            "Chi tiết nhập hàng",
            filters,
            columns,
            rows,
            SumTotals(rows, "totalQty", "preTaxAmount", "taxAmount", "totalAmount"));
    }

    public async Task<ReportTableResultDto> RunProcurementPayablesSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var payables = await _payables.GetSummaryAsync(null, cancellationToken);
        var rows = payables.Select(p => new Dictionary<string, object?>
        {
            ["supplierCode"] = p.SupplierCode,
            ["supplierName"] = p.SupplierName,
            ["totalPayable"] = p.TotalPayable,
            ["agingCurrent"] = p.Aging.Current,
            ["aging31To60"] = p.Aging.Days31To60,
            ["aging61To90"] = p.Aging.Days61To90,
            ["agingOver90"] = p.Aging.Over90,
            ["openDocuments"] = p.OpenDocumentCount,
        }).ToList();

        var columns = new List<ReportColumnDto>
        {
            Col("supplierCode", "Mã NCC", ReportColumnFormats.Text, "left"),
            Col("supplierName", "Nhà cung cấp", ReportColumnFormats.Text, "left"),
            Col("totalPayable", "Còn phải trả", ReportColumnFormats.Money, "right"),
            Col("agingCurrent", "0–30 ngày", ReportColumnFormats.Money, "right"),
            Col("aging31To60", "31–60", ReportColumnFormats.Money, "right"),
            Col("aging61To90", "61–90", ReportColumnFormats.Money, "right"),
            Col("agingOver90", "> 90", ReportColumnFormats.Money, "right"),
            Col("openDocuments", "Phiếu mở", ReportColumnFormats.Integer, "right"),
        };

        return BuildTable(
            ReportCodes.ProcurementPayablesSnapshot,
            "Công nợ nhà cung cấp",
            new Dictionary<string, string>
            {
                ["Thời điểm"] = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
                ["Ghi chú"] = "Theo phiếu nhập — số tiền trước thuế GTGT",
            },
            columns,
            rows,
            SumTotals(rows, "totalPayable", "agingCurrent", "aging31To60", "aging61To90", "agingOver90", "openDocuments"));
    }

    public async Task<ReportTableResultDto> RunInventoryStockSnapshotAsync(
        Guid? warehouseId,
        string? search,
        Guid? categoryId,
        CancellationToken cancellationToken = default)
    {
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetInventoryStockSnapshotAsync(scopedWarehouseId, allowed, search, categoryId, cancellationToken);
        var columns = new List<ReportColumnDto>
        {
            Col("productCode", "Mã SP", ReportColumnFormats.Text, "left"),
            Col("productName", "Tên SP", ReportColumnFormats.Text, "left"),
            Col("categoryLabel", "Nhóm sản phẩm", ReportColumnFormats.Text, "left"),
            Col("warehouseName", "Kho", ReportColumnFormats.Text, "left"),
            Col("totalQty", "Tồn", ReportColumnFormats.Qty, "right"),
            Col("unitName", "Đơn vị", ReportColumnFormats.Text, "left"),
            Col("stockValue", "Giá trị tồn", ReportColumnFormats.Money, "right"),
            Col("updatedAt", "Cập nhật", ReportColumnFormats.Date, "left"),
        };
        var filters = new Dictionary<string, string>
        {
            ["Thời điểm"] = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
        };
        if (warehouseId.HasValue) filters["Kho"] = warehouseId.Value.ToString();
        if (categoryId.HasValue) filters["Danh mục"] = categoryId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(search)) filters["Tìm kiếm"] = search.Trim();

        return BuildTable(
            ReportCodes.InventoryStockSnapshot,
            "Tồn kho và giá trị",
            filters,
            columns,
            rows,
            SumTotals(rows, "totalQty", "stockValue"));
    }

    public async Task<ReportTableResultDto> RunInventoryNearExpiryAsync(
        Guid? warehouseId,
        int expiryDays,
        CancellationToken cancellationToken = default)
    {
        if (expiryDays < 1) expiryDays = 30;
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(expiryDays);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetInventoryNearExpiryAsync(scopedWarehouseId, allowed, cutoff, cancellationToken);
        var columns = new List<ReportColumnDto>
        {
            Col("productCode", "Mã SP", ReportColumnFormats.Text, "left"),
            Col("productName", "Tên SP", ReportColumnFormats.Text, "left"),
            Col("warehouseName", "Kho", ReportColumnFormats.Text, "left"),
            Col("batchNumber", "Số lô", ReportColumnFormats.Text, "left"),
            Col("expiryDate", "HSD", ReportColumnFormats.Date, "left"),
            Col("totalQty", "Tồn", ReportColumnFormats.Qty, "right"),
            Col("stockValue", "Giá trị", ReportColumnFormats.Money, "right"),
        };
        var filters = new Dictionary<string, string>
        {
            ["HSD trong"] = $"{expiryDays} ngày tới",
            ["HSD trước"] = cutoff.ToString("dd/MM/yyyy"),
        };
        if (warehouseId.HasValue) filters["Kho"] = warehouseId.Value.ToString();

        return BuildTable(
            ReportCodes.InventoryNearExpiry,
            "Sắp hết hạn sử dụng",
            filters,
            columns,
            rows,
            SumTotals(rows, "totalQty", "stockValue"));
    }

    public async Task<ReportTableResultDto> RunInventoryMovementSummaryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetInventoryMovementSummaryAsync(from, to, scopedWarehouseId, allowed, search, cancellationToken);
        var columns = new List<ReportColumnDto>
        {
            Col("productCode", "Mã SP", ReportColumnFormats.Text, "left"),
            Col("productName", "Tên SP", ReportColumnFormats.Text, "left"),
            Col("warehouseName", "Kho", ReportColumnFormats.Text, "left"),
            Col("openingQty", "Tồn đầu", ReportColumnFormats.Qty, "right"),
            Col("inQty", "Nhập", ReportColumnFormats.Qty, "right"),
            Col("outQty", "Xuất", ReportColumnFormats.Qty, "right"),
            Col("closingQty", "Tồn cuối", ReportColumnFormats.Qty, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        filters["Ghi chú"] = "Tồn cuối = Tồn đầu + Nhập − Xuất (theo stock_movements)";
        if (!string.IsNullOrWhiteSpace(search)) filters["Tìm kiếm"] = search.Trim();

        return BuildTable(
            ReportCodes.InventoryMovementSummary,
            "Xuất — nhập — tồn",
            filters,
            columns,
            rows,
            SumTotals(rows, "openingQty", "inQty", "outQty", "closingQty"));
    }

    private static string NormalizePeriodGroupBy(string groupBy) =>
        groupBy switch
        {
            ReportGroupBy.Week => ReportGroupBy.Week,
            ReportGroupBy.Month => ReportGroupBy.Month,
            _ => ReportGroupBy.Day,
        };

    private static Dictionary<string, string> FilterLabels(
        DateTime fromUtc,
        DateTime toUtc,
        string? groupBy,
        Guid? warehouseId)
    {
        var labels = new Dictionary<string, string>
        {
            ["Kỳ"] = ReportsDateHelper.FormatVnDateRange(fromUtc, toUtc),
        };
        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            labels["Nhóm theo"] = groupBy switch
            {
                ReportGroupBy.Day => "Ngày",
                ReportGroupBy.Week => "Tuần",
                ReportGroupBy.Month => "Tháng",
                ReportGroupBy.Supplier => "Nhà cung cấp",
                _ => groupBy,
            };
        }
        if (warehouseId.HasValue)
            labels["Kho"] = warehouseId.Value.ToString();
        return labels;
    }

    private static ReportColumnDto Col(string key, string title, string format, string align) =>
        new(key, title, format, align);

    private static ReportTableResultDto BuildTable(
        string code,
        string title,
        IReadOnlyDictionary<string, string> filterLabels,
        IReadOnlyList<ReportColumnDto> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        IReadOnlyDictionary<string, object?>? totals) =>
        new(code, title, DateTime.UtcNow, filterLabels, columns, rows, totals);

    private static List<Dictionary<string, object?>> FilterRowsByGuid(
        IReadOnlyList<Dictionary<string, object?>> rows,
        string key,
        Guid? id)
    {
        if (!id.HasValue) return rows as List<Dictionary<string, object?>> ?? rows.ToList();
        return rows.Where(row =>
        {
            if (!row.TryGetValue(key, out var raw) || raw is null) return false;
            return Guid.TryParse(Convert.ToString(raw), out var parsed) && parsed == id.Value;
        }).ToList();
    }

    private static Dictionary<string, object?>? SumTotals(
        IReadOnlyList<Dictionary<string, object?>> rows,
        params string[] numericKeys)
    {
        if (rows.Count == 0) return null;
        var totals = new Dictionary<string, object?>
        {
            ["periodLabel"] = "Tổng cộng",
            ["supplierName"] = "Tổng cộng",
            ["productName"] = "Tổng cộng",
            ["paymentMethodLabel"] = "Tổng cộng",
            ["categoryLabel"] = "Tổng cộng",
            ["employeeName"] = "Tổng cộng",
            ["customerName"] = "Tổng cộng",
        };
        foreach (var key in numericKeys)
        {
            decimal sum = 0;
            foreach (var row in rows)
            {
                if (row.TryGetValue(key, out var val) && val != null)
                    sum += Convert.ToDecimal(val);
            }
            totals[key] = sum;
        }
        return totals;
    }

    private static List<Dictionary<string, object?>> AppendSharePercent(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        decimal totalNet = 0;
        foreach (var row in rows)
        {
            if (row.TryGetValue("netAmount", out var val) && val != null)
                totalNet += Convert.ToDecimal(val);
        }

        return rows.Select(row =>
        {
            var copy = new Dictionary<string, object?>(row);
            var net = row.TryGetValue("netAmount", out var val) && val != null ? Convert.ToDecimal(val) : 0m;
            copy["sharePercent"] = totalNet > 0 ? Math.Round(net / totalNet * 100m, 1) : 0m;
            return copy;
        }).ToList();
    }
}

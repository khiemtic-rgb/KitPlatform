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
            "Doanh thu bán, hoàn trả và thu ròng theo ngày/tuần/tháng (giờ Việt Nam).", true, false, false),
        new(ReportCodes.SalesRevenueByPaymentMethod, "Doanh thu theo hình thức thanh toán", "sales",
            "Thu ròng theo tiền mặt, thẻ, chuyển khoản, ví.", true, false, false),
        new(ReportCodes.SalesShifts, "Báo cáo ca làm việc", "sales",
            "Danh sách ca, quỹ tiền mặt và thu ròng trong ca.", true, false, false),
        new(ReportCodes.SalesRevenueByCategory, "Doanh thu theo danh mục", "sales",
            "Thu ròng theo nhóm sản phẩm (danh mục) trong kỳ.", true, false, false),
        new(ReportCodes.SalesRevenueByClinicDoctor, "Đơn bán theo phòng khám / bác sĩ", "sales",
            "Tổng hợp đơn Connect (PK) theo phòng khám và bác sĩ kê đơn — số đơn, thu bán, hoàn, thu ròng.", true, false, false),
        new(ReportCodes.ProcurementGrnValue, "Giá trị nhập hàng", "procurement",
            "Tổng hợp phiếu nhập hoàn tất — số tiền trước thuế GTGT.", false, true, false),
        new(ReportCodes.ProcurementPayablesSnapshot, "Công nợ nhà cung cấp", "procurement",
            "Số còn phải trả và tuổi nợ tại thời điểm xem báo cáo.", false, true, false),
        new(ReportCodes.InventoryStockSnapshot, "Tồn kho và giá trị", "inventory",
            "Số lượng và giá trị tồn (số lượng × giá vốn lô) theo sản phẩm/kho.", false, false, true),
        new(ReportCodes.InventoryNearExpiry, "Sắp hết hạn sử dụng", "inventory",
            "Lô tồn có hạn dùng trong số ngày cảnh báo.", false, false, true),
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
            Col("salesAmount", "Thu bán", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "Thu ròng", ReportColumnFormats.Money, "right"),
            Col("orderCount", "Số đơn", ReportColumnFormats.Integer, "right"),
        };
        return BuildTable(
            ReportCodes.SalesRevenueByPeriod,
            "Doanh thu theo kỳ",
            FilterLabels(from, to, groupBy, warehouseId),
            columns,
            rows,
            SumTotals(rows, "salesAmount", "refundAmount", "netAmount", "orderCount"));
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
            Col("salesAmount", "Thu bán", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "Thu ròng", ReportColumnFormats.Money, "right"),
        };
        return BuildTable(
            ReportCodes.SalesRevenueByPaymentMethod,
            "Doanh thu theo hình thức thanh toán",
            FilterLabels(from, to, null, warehouseId),
            columns,
            rows,
            SumTotals(rows, "salesAmount", "refundAmount", "netAmount"));
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
            Col("netAmount", "Thu ròng ca", ReportColumnFormats.Money, "right"),
        };
        return BuildTable(
            ReportCodes.SalesShifts,
            "Báo cáo ca làm việc",
            FilterLabels(from, to, null, warehouseId),
            columns,
            rows,
            SumTotals(rows, "netAmount"));
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
            Col("salesAmount", "Thu bán", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "Thu ròng", ReportColumnFormats.Money, "right"),
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
            Col("salesAmount", "Thu bán", ReportColumnFormats.Money, "right"),
            Col("refundAmount", "Hoàn trả", ReportColumnFormats.Money, "right"),
            Col("netAmount", "Thu ròng", ReportColumnFormats.Money, "right"),
            Col("sharePercent", "Tỷ lệ %", ReportColumnFormats.Qty, "right"),
        };
        var filters = FilterLabels(from, to, null, warehouseId);
        filters["Ghi chú"] =
            "Chỉ đơn POS gắn đơn PK Connect (mở quầy từ handoff). «Đã nhận tín hiệu» không tạo số liệu.";
        return BuildTable(
            ReportCodes.SalesRevenueByClinicDoctor,
            "Đơn bán theo phòng khám / bác sĩ",
            filters,
            columns,
            rows,
            SumTotals(rows, "orderCount", "salesAmount", "refundAmount", "netAmount"));
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
        groupBy = groupBy is ReportGroupBy.Supplier or ReportGroupBy.Month or ReportGroupBy.Day
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
            groupBy == ReportGroupBy.Month ? "Giá trị nhập hàng theo tháng" : "Giá trị nhập hàng theo ngày",
            periodFilters,
            periodColumns,
            rows,
            SumTotals(rows, "grnCount", "totalQty", "preTaxAmount"));
    }

    public async Task<ReportTableResultDto> RunProcurementPayablesSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var payables = await _payables.GetSummaryAsync(cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        var (scopedWarehouseId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var rows = await _repository.GetInventoryStockSnapshotAsync(scopedWarehouseId, allowed, search, cancellationToken);
        var columns = new List<ReportColumnDto>
        {
            Col("productCode", "Mã SP", ReportColumnFormats.Text, "left"),
            Col("productName", "Tên SP", ReportColumnFormats.Text, "left"),
            Col("warehouseName", "Kho", ReportColumnFormats.Text, "left"),
            Col("totalQty", "Tồn", ReportColumnFormats.Qty, "right"),
            Col("stockValue", "Giá trị tồn", ReportColumnFormats.Money, "right"),
        };
        var filters = new Dictionary<string, string>
        {
            ["Thời điểm"] = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
        };
        if (warehouseId.HasValue) filters["Kho"] = warehouseId.Value.ToString();
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

    private static Dictionary<string, object?>? SumTotals(
        IReadOnlyList<Dictionary<string, object?>> rows,
        params string[] numericKeys)
    {
        if (rows.Count == 0) return null;
        var totals = new Dictionary<string, object?> { ["periodLabel"] = "Tổng cộng", ["supplierName"] = "Tổng cộng", ["productName"] = "Tổng cộng", ["paymentMethodLabel"] = "Tổng cộng", ["categoryLabel"] = "Tổng cộng" };
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

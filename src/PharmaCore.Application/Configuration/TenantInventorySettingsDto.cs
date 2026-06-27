namespace PharmaCore.Application.Configuration;

public sealed record TenantDefaultMinStockDto(decimal? DefaultMinStockQty);

public sealed record UpdateTenantDefaultMinStockRequest(decimal? DefaultMinStockQty);

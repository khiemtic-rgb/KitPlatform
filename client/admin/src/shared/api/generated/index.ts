/**
 * Re-export types sinh từ OpenAPI — dùng dần thay cho khai báo tay trong *.types.ts.
 * Regenerate: npm run openapi:sync
 */
import type { components } from './api-schema';

export type { Req } from './dto-helpers';

export type ApiSchemas = components['schemas'];

// Sales
export type CompleteDraftSaleRequest = ApiSchemas['CompleteDraftSaleRequest'];
export type CreateSaleLineRequest = ApiSchemas['CreateSaleLineRequest'];
export type CreateSalePaymentRequest = ApiSchemas['CreateSalePaymentRequest'];
export type CreateSaleRequest = ApiSchemas['CreateSaleRequest'];
export type UpdateDraftSaleRequest = ApiSchemas['UpdateDraftSaleRequest'];
export type TenantBatchModeSettingsDto = ApiSchemas['TenantBatchModeSettingsDto'];
export type CustomerConsentDto = ApiSchemas['CustomerConsentDto'];
export type CustomerListItemDto = ApiSchemas['CustomerListItemDto'];
export type PosAllocationPreviewDto = ApiSchemas['PosAllocationPreviewDto'];
export type PosAllocationPreviewLineDto = ApiSchemas['PosAllocationPreviewLineDto'];
export type PosBatchAllocationPreviewDto = ApiSchemas['PosBatchAllocationPreviewDto'];
export type PosBatchHintDto = ApiSchemas['PosBatchHintDto'];
export type PosProductLookupDto = ApiSchemas['PosProductLookupDto'];
export type PosProductSearchItemDto = ApiSchemas['PosProductSearchItemDto'];
export type TenantReceiptSettingsDto = ApiSchemas['TenantReceiptSettingsDto'];
export type SalesOrderDetailDto = ApiSchemas['SalesOrderDetailDto'];
export type SalesOrderItemDto = ApiSchemas['SalesOrderItemDto'];
export type SalesOrderListItemDto = ApiSchemas['SalesOrderListItemDto'];
export type SalesPaymentDto = ApiSchemas['SalesPaymentDto'];
export type SalesRefundPaymentSummaryDto = ApiSchemas['SalesRefundPaymentSummaryDto'];
export type SalesReturnDetailDto = ApiSchemas['SalesReturnDetailDto'];
export type SalesReturnItemDto = ApiSchemas['SalesReturnItemDto'];
export type SalesReturnListItemDto = ApiSchemas['SalesReturnListItemDto'];
export type SalesShiftDetailDto = ApiSchemas['SalesShiftDetailDto'];
export type SalesShiftListItemDto = ApiSchemas['SalesShiftListItemDto'];
export type SalesShiftPaymentSummaryDto = ApiSchemas['SalesShiftPaymentSummaryDto'];
export type SalesShiftSummaryDto = ApiSchemas['SalesShiftSummaryDto'];
export type ShiftLotComplianceAlertDto = ApiSchemas['ShiftLotComplianceAlertDto'];

// Catalog
export type ActiveIngredientDto = ApiSchemas['ActiveIngredientDto'];
export type BrandDto = ApiSchemas['BrandDto'];
export type CategoryDto = ApiSchemas['CategoryDto'];
export type PagedProductListResult = ApiSchemas['PagedProductListResult'];
export type ProductBarcodeDto = ApiSchemas['ProductBarcodeDto'];
export type ProductDetailDto = ApiSchemas['ProductDetailDto'];
export type ProductImageDto = ApiSchemas['ProductImageDto'];
export type ProductIngredientDto = ApiSchemas['ProductIngredientDto'];
export type ProductListItemDto = ApiSchemas['ProductListItemDto'];
export type ProductPriceDto = ApiSchemas['ProductPriceDto'];
export type ProductUnitDto = ApiSchemas['ProductUnitDto'];
export type BarcodeCheckResultDto = ApiSchemas['BarcodeCheckResult'];
export type CreateProductRequest = ApiSchemas['CreateProductRequest'];
export type CreateBarcodeRequest = ApiSchemas['CreateBarcodeRequest'];
export type CreatePriceRequest = ApiSchemas['CreatePriceRequest'];
export type NextProductCodeDto = ApiSchemas['NextProductCodeDto'];
export type ProductImageItem = ApiSchemas['ProductImageItem'];
export type ProductIngredientSyncItem = ApiSchemas['ProductIngredientSyncItem'];
export type ProductUnitSyncItem = ApiSchemas['ProductUnitSyncItem'];
export type SimilarProductNameDto = ApiSchemas['SimilarProductNameDto'];
export type SimilarProductNamesResultDto = ApiSchemas['SimilarProductNamesResult'];
export type SyncProductCommercialRequest = ApiSchemas['SyncProductCommercialRequest'];
export type SyncProductIngredientsRequest = ApiSchemas['SyncProductIngredientsRequest'];
export type SyncProductUnitsRequest = ApiSchemas['SyncProductUnitsRequest'];
export type UpdateProductRequest = ApiSchemas['UpdateProductRequest'];

// Integration
export type IntegrationOutboxItemDto = ApiSchemas['IntegrationOutboxItemDto'];

// Inventory
export type AdjustmentDetailDto = ApiSchemas['AdjustmentDetailDto'];
export type AdjustmentItemDto = ApiSchemas['AdjustmentItemDto'];
export type AdjustmentListItemDto = ApiSchemas['AdjustmentListItemDto'];
export type BranchLookupDto = ApiSchemas['BranchLookupDto'];
export type OpeningBalanceBatchListItemDto = ApiSchemas['OpeningBalanceBatchListItemDto'];
export type OpeningBalanceLineRequest = ApiSchemas['OpeningBalanceLineRequest'];
export type OpeningBalanceResultDto = ApiSchemas['OpeningBalanceResultDto'];
export type PagedStockBatchesResult = ApiSchemas['PagedStockBatchesResult'];
export type PagedStockProductsResult = ApiSchemas['PagedStockProductsResult'];
export type StockBatchListItemDto = ApiSchemas['StockBatchListItemDto'];
export type StockProductSummaryDto = ApiSchemas['StockProductSummaryDto'];
export type TransferDetailDto = ApiSchemas['TransferDetailDto'];
export type TransferItemDto = ApiSchemas['TransferItemDto'];
export type TransferListItemDto = ApiSchemas['TransferListItemDto'];
export type WarehouseDto = ApiSchemas['WarehouseDto'];

// Procurement
export type GoodsReceiptDetailDto = ApiSchemas['GoodsReceiptDetailDto'];
export type GoodsReceiptItemDto = ApiSchemas['GoodsReceiptItemDto'];
export type GoodsReceiptListItemDto = ApiSchemas['GoodsReceiptListItemDto'];
export type LastPurchasePriceHintDto = ApiSchemas['LastPurchasePriceHintDto'];
export type PurchaseOrderDetailDto = ApiSchemas['PurchaseOrderDetailDto'];
export type PurchaseOrderItemDto = ApiSchemas['PurchaseOrderItemDto'];
export type PurchaseOrderListItemDto = ApiSchemas['PurchaseOrderListItemDto'];
export type SupplierDto = ApiSchemas['SupplierDto'];
export type SupplierPaymentListItemDto = ApiSchemas['SupplierPaymentListItemDto'];

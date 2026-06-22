import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthGuard, GuestGuard } from '@/shared/auth/AuthGuard';
import { AppLayout } from '@/shared/components/AppLayout';
import { LoginPage } from '@/modules/auth/LoginPage';
import { BrandListPage } from '@/modules/catalog/BrandListPage';
import { CatalogLayout } from '@/modules/catalog/CatalogLayout';
import { CategoryListPage } from '@/modules/catalog/CategoryListPage';
import { IngredientListPage } from '@/modules/catalog/IngredientListPage';
import { ProductListPage } from '@/modules/catalog/ProductListPage';
import { DashboardPage } from '@/modules/dashboard/DashboardPage';
import { InventoryLayout } from '@/modules/inventory/InventoryLayout';
import { WarehouseListPage } from '@/modules/inventory/WarehouseListPage';
import { StockListPage } from '@/modules/inventory/StockListPage';
import { OpeningBalancePage } from '@/modules/inventory/OpeningBalancePage';
import { TransferListPage } from '@/modules/inventory/TransferListPage';
import { AdjustmentListPage } from '@/modules/inventory/AdjustmentListPage';
import { ProcurementLayout } from '@/modules/procurement/ProcurementLayout';
import { SupplierListPage } from '@/modules/procurement/SupplierListPage';
import { PurchaseOrderListPage } from '@/modules/procurement/PurchaseOrderListPage';
import { GoodsReceiptListPage } from '@/modules/procurement/GoodsReceiptListPage';
import { SupplierPaymentListPage } from '@/modules/procurement/SupplierPaymentListPage';
import { SalesLayout } from '@/modules/sales/SalesLayout';
import { PosPage } from '@/modules/sales/PosPage';
import { SalesOrderListPage } from '@/modules/sales/SalesOrderListPage';
import { SalesReturnListPage } from '@/modules/sales/SalesReturnListPage';
import { SalesShiftReportPage } from '@/modules/sales/SalesShiftReportPage';
import { CustomerConsentPage } from '@/modules/sales/CustomerConsentPage';
import { ReceiptSettingsPage } from '@/modules/sales/ReceiptSettingsPage';

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<GuestGuard />}>
          <Route path="/login" element={<LoginPage />} />
        </Route>

        <Route element={<AuthGuard />}>
          <Route element={<AppLayout />}>
            <Route index element={<DashboardPage />} />
            <Route path="catalog" element={<CatalogLayout />}>
              <Route index element={<Navigate to="/catalog/products" replace />} />
              <Route path="products" element={<ProductListPage />} />
              <Route path="categories" element={<CategoryListPage />} />
              <Route path="brands" element={<BrandListPage />} />
              <Route path="ingredients" element={<IngredientListPage />} />
            </Route>
            <Route path="inventory" element={<InventoryLayout />}>
              <Route index element={<Navigate to="/inventory/stock" replace />} />
              <Route path="stock" element={<StockListPage />} />
              <Route path="warehouses" element={<WarehouseListPage />} />
              <Route path="opening-balance" element={<OpeningBalancePage />} />
              <Route path="transfers" element={<TransferListPage />} />
              <Route path="adjustments" element={<AdjustmentListPage />} />
            </Route>
            <Route path="procurement" element={<ProcurementLayout />}>
              <Route index element={<Navigate to="/procurement/purchase-orders" replace />} />
              <Route path="purchase-orders" element={<PurchaseOrderListPage />} />
              <Route path="goods-receipts" element={<GoodsReceiptListPage />} />
              <Route path="suppliers" element={<SupplierListPage />} />
              <Route path="supplier-payments" element={<SupplierPaymentListPage />} />
            </Route>
            <Route path="sales" element={<SalesLayout />}>
              <Route index element={<Navigate to="/sales/pos" replace />} />
              <Route path="pos" element={<PosPage />} />
              <Route path="orders" element={<SalesOrderListPage />} />
              <Route path="returns" element={<SalesReturnListPage />} />
              <Route path="shift" element={<SalesShiftReportPage />} />
              <Route path="customers" element={<CustomerConsentPage />} />
              <Route path="settings" element={<ReceiptSettingsPage />} />
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

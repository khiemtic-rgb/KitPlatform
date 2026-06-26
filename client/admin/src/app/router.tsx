import { lazy, Suspense, type ReactNode } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { AuthGuard, GuestGuard } from '@/shared/auth/AuthGuard';

const AppLayout = lazy(() =>
  import('@/shared/components/AppLayout').then((m) => ({ default: m.AppLayout })),
);

const LoginPage = lazy(() =>
  import('@/modules/auth/LoginPage').then((m) => ({ default: m.LoginPage })),
);
const DashboardPage = lazy(() =>
  import('@/modules/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })),
);
const CatalogLayout = lazy(() =>
  import('@/modules/catalog/CatalogLayout').then((m) => ({ default: m.CatalogLayout })),
);
const ProductListPage = lazy(() =>
  import('@/modules/catalog/ProductListPage').then((m) => ({ default: m.ProductListPage })),
);
const CategoryListPage = lazy(() =>
  import('@/modules/catalog/CategoryListPage').then((m) => ({ default: m.CategoryListPage })),
);
const BrandListPage = lazy(() =>
  import('@/modules/catalog/BrandListPage').then((m) => ({ default: m.BrandListPage })),
);
const IngredientListPage = lazy(() =>
  import('@/modules/catalog/IngredientListPage').then((m) => ({ default: m.IngredientListPage })),
);
const InventoryLayout = lazy(() =>
  import('@/modules/inventory/InventoryLayout').then((m) => ({ default: m.InventoryLayout })),
);
const StockListPage = lazy(() =>
  import('@/modules/inventory/StockListPage').then((m) => ({ default: m.StockListPage })),
);
const WarehouseListPage = lazy(() =>
  import('@/modules/inventory/WarehouseListPage').then((m) => ({ default: m.WarehouseListPage })),
);
const OpeningBalancePage = lazy(() =>
  import('@/modules/inventory/OpeningBalancePage').then((m) => ({ default: m.OpeningBalancePage })),
);
const TransferListPage = lazy(() =>
  import('@/modules/inventory/TransferListPage').then((m) => ({ default: m.TransferListPage })),
);
const AdjustmentListPage = lazy(() =>
  import('@/modules/inventory/AdjustmentListPage').then((m) => ({ default: m.AdjustmentListPage })),
);
const InventoryCountPage = lazy(() =>
  import('@/modules/inventory/InventoryCountPage').then((m) => ({ default: m.InventoryCountPage })),
);
const ProcurementLayout = lazy(() =>
  import('@/modules/procurement/ProcurementLayout').then((m) => ({ default: m.ProcurementLayout })),
);
const PurchaseOrderListPage = lazy(() =>
  import('@/modules/procurement/PurchaseOrderListPage').then((m) => ({
    default: m.PurchaseOrderListPage,
  })),
);
const GoodsReceiptListPage = lazy(() =>
  import('@/modules/procurement/GoodsReceiptListPage').then((m) => ({
    default: m.GoodsReceiptListPage,
  })),
);
const SupplierListPage = lazy(() =>
  import('@/modules/procurement/SupplierListPage').then((m) => ({ default: m.SupplierListPage })),
);
const SupplierPayablesPage = lazy(() =>
  import('@/modules/procurement/SupplierPayablesPage').then((m) => ({
    default: m.SupplierPayablesPage,
  })),
);
const SupplierPaymentListPage = lazy(() =>
  import('@/modules/procurement/SupplierPaymentListPage').then((m) => ({
    default: m.SupplierPaymentListPage,
  })),
);
const SalesLayout = lazy(() =>
  import('@/modules/sales/SalesLayout').then((m) => ({ default: m.SalesLayout })),
);
const PosPage = lazy(() => import('@/modules/sales/PosPage').then((m) => ({ default: m.PosPage })));
const SalesOrderListPage = lazy(() =>
  import('@/modules/sales/SalesOrderListPage').then((m) => ({ default: m.SalesOrderListPage })),
);
const CustomerDraftOrderListPage = lazy(() =>
  import('@/modules/sales/CustomerDraftOrderListPage').then((m) => ({
    default: m.CustomerDraftOrderListPage,
  })),
);
const CustomerReservationListPage = lazy(() =>
  import('@/modules/sales/CustomerReservationListPage').then((m) => ({
    default: m.CustomerReservationListPage,
  })),
);
const SalesReturnListPage = lazy(() =>
  import('@/modules/sales/SalesReturnListPage').then((m) => ({ default: m.SalesReturnListPage })),
);
const SalesShiftReportPage = lazy(() =>
  import('@/modules/sales/SalesShiftReportPage').then((m) => ({ default: m.SalesShiftReportPage })),
);
const CustomerChatPage = lazy(() =>
  import('@/modules/sales/CustomerChatPage').then((m) => ({ default: m.CustomerChatPage })),
);
const ReceiptSettingsPage = lazy(() =>
  import('@/modules/sales/ReceiptSettingsPage').then((m) => ({ default: m.ReceiptSettingsPage })),
);
const LoyaltySettingsPage = lazy(() =>
  import('@/modules/sales/LoyaltySettingsPage').then((m) => ({ default: m.LoyaltySettingsPage })),
);
const VoucherListPage = lazy(() =>
  import('@/modules/sales/VoucherListPage').then((m) => ({ default: m.VoucherListPage })),
);
const CustomerLayout = lazy(() =>
  import('@/modules/customer/CustomerLayout').then((m) => ({ default: m.CustomerLayout })),
);
const CustomerListPage = lazy(() =>
  import('@/modules/customer/CustomerListPage').then((m) => ({ default: m.CustomerListPage })),
);
const CustomerDetailPage = lazy(() =>
  import('@/modules/customer/CustomerDetailPage').then((m) => ({ default: m.CustomerDetailPage })),
);
const SystemLayout = lazy(() =>
  import('@/modules/system/SystemLayout').then((m) => ({ default: m.SystemLayout })),
);
const UserListPage = lazy(() =>
  import('@/modules/system/UserListPage').then((m) => ({ default: m.UserListPage })),
);
const RoleListPage = lazy(() =>
  import('@/modules/system/RoleListPage').then((m) => ({ default: m.RoleListPage })),
);
const BranchListPage = lazy(() =>
  import('@/modules/system/BranchListPage').then((m) => ({ default: m.BranchListPage })),
);

function RouteFallback() {
  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: '#f5f5f5',
      }}
    >
      <Spin size="large" tip="Đang tải...">
        <div style={{ minHeight: 120, minWidth: 120 }} />
      </Spin>
    </div>
  );
}

function SuspenseRoute({ children }: { children: ReactNode }) {
  return <Suspense fallback={<RouteFallback />}>{children}</Suspense>;
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <Suspense fallback={<RouteFallback />}>
        <Routes>
          <Route element={<GuestGuard />}>
            <Route
              path="/login"
              element={
                <SuspenseRoute>
                  <LoginPage />
                </SuspenseRoute>
              }
            />
          </Route>

          <Route element={<AuthGuard />}>
            <Route
              element={
                <SuspenseRoute>
                  <AppLayout />
                </SuspenseRoute>
              }
            >
              <Route
                index
                element={
                  <SuspenseRoute>
                    <DashboardPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="catalog"
                element={
                  <SuspenseRoute>
                    <CatalogLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/catalog/products" replace />} />
                <Route path="products" element={<ProductListPage />} />
                <Route path="categories" element={<CategoryListPage />} />
                <Route path="brands" element={<BrandListPage />} />
                <Route path="ingredients" element={<IngredientListPage />} />
              </Route>
              <Route
                path="inventory"
                element={
                  <SuspenseRoute>
                    <InventoryLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/inventory/stock" replace />} />
                <Route path="stock" element={<StockListPage />} />
                <Route path="warehouses" element={<WarehouseListPage />} />
                <Route path="opening-balance" element={<OpeningBalancePage />} />
                <Route path="transfers" element={<TransferListPage />} />
                <Route path="adjustments" element={<AdjustmentListPage />} />
                <Route path="adjustments/:id/count" element={<InventoryCountPage />} />
              </Route>
              <Route
                path="procurement"
                element={
                  <SuspenseRoute>
                    <ProcurementLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/procurement/purchase-orders" replace />} />
                <Route path="purchase-orders" element={<PurchaseOrderListPage />} />
                <Route path="goods-receipts" element={<GoodsReceiptListPage />} />
                <Route path="suppliers" element={<SupplierListPage />} />
                <Route path="supplier-payables" element={<SupplierPayablesPage />} />
                <Route path="supplier-payments" element={<SupplierPaymentListPage />} />
              </Route>
              <Route
                path="sales"
                element={
                  <SuspenseRoute>
                    <SalesLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/sales/pos" replace />} />
                <Route path="pos" element={<PosPage />} />
                <Route path="orders" element={<SalesOrderListPage />} />
                <Route path="customer-drafts" element={<CustomerDraftOrderListPage />} />
                <Route path="customer-reservations" element={<CustomerReservationListPage />} />
                <Route path="returns" element={<SalesReturnListPage />} />
                <Route path="shift" element={<SalesShiftReportPage />} />
                <Route path="customers" element={<Navigate to="/customer/list" replace />} />
                <Route path="chat" element={<CustomerChatPage />} />
                <Route path="loyalty" element={<LoyaltySettingsPage />} />
                <Route path="vouchers" element={<VoucherListPage />} />
                <Route path="settings" element={<ReceiptSettingsPage />} />
              </Route>
              <Route
                path="customer"
                element={
                  <SuspenseRoute>
                    <CustomerLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/customer/list" replace />} />
                <Route path="list" element={<CustomerListPage />} />
                <Route path=":customerId" element={<CustomerDetailPage />} />
              </Route>
              <Route
                path="system"
                element={
                  <SuspenseRoute>
                    <SystemLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/system/users" replace />} />
                <Route path="users" element={<UserListPage />} />
                <Route path="roles" element={<RoleListPage />} />
                <Route path="branches" element={<BranchListPage />} />
              </Route>
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}

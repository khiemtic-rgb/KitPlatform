import { lazy, Suspense, type ReactNode } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { useTranslation } from 'react-i18next';
import { AuthGuard, GuestGuard } from '@/shared/auth/AuthGuard';
import { RedirectPreserveSearch } from '@/shared/components/RedirectPreserveSearch';
import { TEMP_HIDDEN_MODULE_KEYS } from '@/modules/registry';

const rxModuleHidden = TEMP_HIDDEN_MODULE_KEYS.includes('rx');

const AppLayout = lazy(() =>
  import('@/shared/components/AppLayout').then((m) => ({ default: m.AppLayout })),
);

const SetupPage = lazy(() =>
  import('@/modules/platform/SetupPage').then((m) => ({ default: m.SetupPage })),
);
const PlatformOrganizationsPage = lazy(() =>
  import('@/modules/platform/PlatformOrganizationsPage').then((m) => ({
    default: m.PlatformOrganizationsPage,
  })),
);
const LoginPage = lazy(() =>
  import('@/modules/auth/LoginPage').then((m) => ({ default: m.LoginPage })),
);
const DashboardPage = lazy(() =>
  import('@/modules/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })),
);
const OwnerCockpitPage = lazy(() =>
  import('@/modules/success/OwnerCockpitPage').then((m) => ({ default: m.OwnerCockpitPage })),
);
const ShiftChecklistPage = lazy(() =>
  import('@/modules/success/ShiftChecklistPage').then((m) => ({ default: m.ShiftChecklistPage })),
);
const LossCashVariancePage = lazy(() =>
  import('@/modules/success/LossCashVariancePage').then((m) => ({ default: m.LossCashVariancePage })),
);
const LearningLayout = lazy(() =>
  import('@/modules/learning/LearningLayout').then((m) => ({ default: m.LearningLayout })),
);
const LearningWriteGuard = lazy(() =>
  import('@/modules/learning/LearningWriteGuard').then((m) => ({ default: m.LearningWriteGuard })),
);
const LearningProgramsPage = lazy(() =>
  import('@/modules/learning/LearningProgramsPage').then((m) => ({
    default: m.default ?? m.LearningProgramsPage,
  })),
);
const LearningProgramDetailPage = lazy(() =>
  import('@/modules/learning/LearningProgramDetailPage').then((m) => ({
    default: m.LearningProgramDetailPage,
  })),
);
const LearningEnrollmentsPage = lazy(() =>
  import('@/modules/learning/LearningEnrollmentsPage').then((m) => ({
    default: m.LearningEnrollmentsPage,
  })),
);
const LearningEvaluationsPage = lazy(() =>
  import('@/modules/learning/LearningEvaluationsPage').then((m) => ({
    default: m.LearningEvaluationsPage,
  })),
);
const LearningRecognizePage = lazy(() =>
  import('@/modules/learning/LearningRecognizePage').then((m) => ({
    default: m.LearningRecognizePage,
  })),
);
const LearningGrowPage = lazy(() =>
  import('@/modules/learning/LearningGrowPage').then((m) => ({
    default: m.LearningGrowPage,
  })),
);
const LearningTakePage = lazy(() =>
  import('@/modules/learning/LearningTakePage').then((m) => ({
    default: m.LearningTakePage,
  })),
);
const LearningTakeModulePage = lazy(() =>
  import('@/modules/learning/LearningTakeModulePage').then((m) => ({
    default: m.LearningTakeModulePage,
  })),
);
const LearningModulePreviewPage = lazy(() =>
  import('@/modules/learning/LearningModulePreviewPage').then((m) => ({
    default: m.LearningModulePreviewPage,
  })),
);
const LearningContentLevelsPage = lazy(() =>
  import('@/modules/learning/LearningContentLevelsPage').then((m) => ({
    default: m.LearningContentLevelsPage,
  })),
);
const LearningMailPage = lazy(() =>
  import('@/modules/learning/LearningMailPage').then((m) => ({
    default: m.LearningMailPage,
  })),
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
const NationalDrugLookupPage = lazy(() =>
  import('@/modules/catalog/NationalDrugLookupPage').then((m) => ({ default: m.NationalDrugLookupPage })),
);
const ProductImportPage = lazy(() =>
  import('@/modules/catalog/ProductImportPage').then((m) => ({ default: m.ProductImportPage })),
);
const ProductDuplicateMergePage = lazy(() =>
  import('@/modules/catalog/ProductDuplicateMergePage').then((m) => ({
    default: m.ProductDuplicateMergePage,
  })),
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
const LowStockPage = lazy(() =>
  import('@/modules/inventory/LowStockPage').then((m) => ({ default: m.LowStockPage })),
);
const GppOperationalChecklistPage = lazy(() =>
  import('@/modules/inventory/GppOperationalChecklistPage').then((m) => ({
    default: m.GppOperationalChecklistPage,
  })),
);
const Qd540ExportPage = lazy(() =>
  import('@/modules/inventory/Qd540ExportPage').then((m) => ({ default: m.Qd540ExportPage })),
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
const VatTreatmentListPage = lazy(() =>
  import('@/modules/procurement/VatTreatmentListPage').then((m) => ({
    default: m.VatTreatmentListPage,
  })),
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
const ReceivablesLayout = lazy(() =>
  import('@/modules/receivables/ReceivablesLayout').then((m) => ({ default: m.ReceivablesLayout })),
);
const SalesLayout = lazy(() =>
  import('@/modules/sales/SalesLayout').then((m) => ({ default: m.SalesLayout })),
);
const AppOrdersLayout = lazy(() =>
  import('@/modules/sales/AppOrdersLayout').then((m) => ({ default: m.AppOrdersLayout })),
);
const RxLayout = lazy(() =>
  import('@/modules/rx/RxLayout').then((m) => ({ default: m.RxLayout })),
);
const RxDashboardPage = lazy(() =>
  import('@/modules/rx/RxDashboardPage').then((m) => ({ default: m.RxDashboardPage })),
);
const ConnectLayout = lazy(() =>
  import('@/modules/connect/ConnectLayout').then((m) => ({ default: m.ConnectLayout })),
);
const ConnectOverviewPage = lazy(() =>
  import('@/modules/connect/ConnectOverviewPage').then((m) => ({ default: m.ConnectOverviewPage })),
);
const FamilyOsOverviewPage = lazy(() =>
  import('@/modules/family-os/FamilyOsOverviewPage').then((m) => ({
    default: m.FamilyOsOverviewPage,
  })),
);
const FamilyOsLayout = lazy(() =>
  import('@/modules/family-os/FamilyOsLayout').then((m) => ({ default: m.FamilyOsLayout })),
);
const FamilyOsDayFlowPage = lazy(() =>
  import('@/modules/family-os/FamilyOsDayFlowPage').then((m) => ({
    default: m.FamilyOsDayFlowPage,
  })),
);
const FamilyOsRoutinesPage = lazy(() =>
  import('@/modules/family-os/FamilyOsRoutinesPage').then((m) => ({
    default: m.FamilyOsRoutinesPage,
  })),
);
const FamilyOsAgreementsPage = lazy(() =>
  import('@/modules/family-os/FamilyOsAgreementsPage').then((m) => ({
    default: m.FamilyOsAgreementsPage,
  })),
);
const FamilyOsMembersPage = lazy(() =>
  import('@/modules/family-os/FamilyOsMembersPage').then((m) => ({
    default: m.FamilyOsMembersPage,
  })),
);
const ConnectNetworkPage = lazy(() =>
  import('@/modules/connect/ConnectNetworkPage').then((m) => ({ default: m.ConnectNetworkPage })),
);
const ConnectPartnersPage = lazy(() =>
  import('@/modules/connect/ConnectPartnersPage').then((m) => ({ default: m.ConnectPartnersPage })),
);
const ConnectTeamPage = lazy(() =>
  import('@/modules/connect/ConnectTeamPage').then((m) => ({ default: m.ConnectTeamPage })),
);
const ConnectReferralsPage = lazy(() =>
  import('@/modules/connect/ConnectReferralsPage').then((m) => ({ default: m.ConnectReferralsPage })),
);
const ConnectBookingsPage = lazy(() =>
  import('@/modules/connect/ConnectBookingsPage').then((m) => ({ default: m.ConnectBookingsPage })),
);
const ConnectStatusPage = lazy(() =>
  import('@/modules/connect/ConnectStatusPage').then((m) => ({ default: m.ConnectStatusPage })),
);
const ClinicLayout = lazy(() =>
  import('@/modules/clinic/ClinicLayout').then((m) => ({ default: m.ClinicLayout })),
);
const ClinicOverviewPage = lazy(() =>
  import('@/modules/clinic/ClinicOverviewPage').then((m) => ({ default: m.ClinicOverviewPage })),
);
const ClinicPatientsPage = lazy(() =>
  import('@/modules/clinic/ClinicPatientsPage').then((m) => ({ default: m.ClinicPatientsPage })),
);
const ClinicProvidersPage = lazy(() =>
  import('@/modules/clinic/ClinicProvidersPage').then((m) => ({ default: m.ClinicProvidersPage })),
);
const ClinicAppointmentsPage = lazy(() =>
  import('@/modules/clinic/ClinicAppointmentsPage').then((m) => ({ default: m.ClinicAppointmentsPage })),
);
const ClinicVisitsPage = lazy(() =>
  import('@/modules/clinic/ClinicVisitsPage').then((m) => ({ default: m.ClinicVisitsPage })),
);
const ClinicSettingsPage = lazy(() =>
  import('@/modules/clinic/ClinicSettingsPage').then((m) => ({ default: m.ClinicSettingsPage })),
);
const PosPage = lazy(() => import('@/modules/sales/PosPage').then((m) => ({ default: m.PosPage })));
const PrescriptionListPage = lazy(() =>
  import('@/modules/sales/PrescriptionListPage').then((m) => ({ default: m.PrescriptionListPage })),
);
const PrescriberListPage = lazy(() =>
  import('@/modules/sales/PrescriberListPage').then((m) => ({ default: m.PrescriberListPage })),
);
const PrescriberLinksPage = lazy(() =>
  import('@/modules/sales/PrescriberLinksPage').then((m) => ({ default: m.PrescriberLinksPage })),
);
const SalesOrderListPage = lazy(() =>
  import('@/modules/sales/SalesOrderListPage').then((m) => ({ default: m.SalesOrderListPage })),
);
const CustomerReceivablesPage = lazy(() =>
  import('@/modules/sales/CustomerReceivablesPage').then((m) => ({ default: m.CustomerReceivablesPage })),
);
const CustomerPaymentListPage = lazy(() =>
  import('@/modules/sales/CustomerPaymentListPage').then((m) => ({ default: m.CustomerPaymentListPage })),
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
const CustomerAppSettingsPage = lazy(() =>
  import('@/modules/sales/CustomerAppSettingsPage').then((m) => ({ default: m.CustomerAppSettingsPage })),
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
const CustomerGroupListPage = lazy(() =>
  import('@/modules/customer/CustomerGroupListPage').then((m) => ({ default: m.CustomerGroupListPage })),
);
const CustomerEngagementPage = lazy(() =>
  import('@/modules/customer/CustomerEngagementPage').then((m) => ({ default: m.CustomerEngagementPage })),
);
const CustomerDetailPage = lazy(() =>
  import('@/modules/customer/CustomerDetailPage').then((m) => ({ default: m.CustomerDetailPage })),
);
const PlatformPackSettingsPage = lazy(() =>
  import('@/modules/system/PlatformPackSettingsPage').then((m) => ({ default: m.PlatformPackSettingsPage })),
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
const AuditLogListPage = lazy(() =>
  import('@/modules/system/AuditLogListPage').then((m) => ({ default: m.AuditLogListPage })),
);
const KapLayout = lazy(() =>
  import('@/modules/kap/KapLayout').then((m) => ({ default: m.KapLayout })),
);
const KapLeadsPage = lazy(() =>
  import('@/modules/kap/KapLeadsPage').then((m) => ({ default: m.KapLeadsPage })),
);
const KapTemplatesPage = lazy(() =>
  import('@/modules/kap/KapTemplatesPage').then((m) => ({ default: m.KapTemplatesPage })),
);
const KapRulesPage = lazy(() =>
  import('@/modules/kap/KapRulesPage').then((m) => ({ default: m.KapRulesPage })),
);
const KapCampaignsPage = lazy(() =>
  import('@/modules/kap/KapCampaignsPage').then((m) => ({ default: m.KapCampaignsPage })),
);
const KapPartnersPage = lazy(() =>
  import('@/modules/kap/KapPartnersPage').then((m) => ({ default: m.KapPartnersPage })),
);
const ReportsLayout = lazy(() =>
  import('@/modules/reports/ReportsLayout').then((m) => ({ default: m.ReportsLayout })),
);
const ReportsHomePage = lazy(() =>
  import('@/modules/reports/ReportsHomePage').then((m) => ({ default: m.ReportsHomePage })),
);
const ReportViewPage = lazy(() =>
  import('@/modules/reports/ReportViewPage').then((m) => ({ default: m.ReportViewPage })),
);

function RouteFallback() {
  const { t } = useTranslation('common', { keyPrefix: 'routeLoading' });
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
      <Spin size="large" tip={t('tip')}>
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
          <Route
            path="/setup"
            element={
              <SuspenseRoute>
                <SetupPage />
              </SuspenseRoute>
            }
          />
          <Route
            path="/setup/organizations"
            element={
              <SuspenseRoute>
                <PlatformOrganizationsPage />
              </SuspenseRoute>
            }
          />

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
                path="success/cockpit"
                element={
                  <SuspenseRoute>
                    <OwnerCockpitPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="success/shift-checklist"
                element={
                  <SuspenseRoute>
                    <ShiftChecklistPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="success/loss"
                element={
                  <SuspenseRoute>
                    <LossCashVariancePage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="people"
                element={
                  <SuspenseRoute>
                    <LearningLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<LearningProgramsPage />} />
                <Route path="learn" element={<LearningTakePage />} />
                <Route path="learn/modules/:id" element={<LearningTakeModulePage />} />
                <Route path="dashboard" element={<Navigate to="/people" replace />} />
                <Route path="programs/:id" element={<LearningProgramDetailPage />} />
                <Route path="modules/:id" element={<LearningModulePreviewPage />} />
                <Route
                  path="content"
                  element={
                    <LearningWriteGuard>
                      <LearningContentLevelsPage />
                    </LearningWriteGuard>
                  }
                />
                <Route
                  path="enrollments"
                  element={
                    <LearningWriteGuard>
                      <LearningEnrollmentsPage />
                    </LearningWriteGuard>
                  }
                />
                <Route
                  path="evaluations"
                  element={
                    <LearningWriteGuard>
                      <LearningEvaluationsPage />
                    </LearningWriteGuard>
                  }
                />
                <Route
                  path="recognize"
                  element={
                    <LearningWriteGuard>
                      <LearningRecognizePage />
                    </LearningWriteGuard>
                  }
                />
                <Route
                  path="grow"
                  element={
                    <LearningWriteGuard>
                      <LearningGrowPage />
                    </LearningWriteGuard>
                  }
                />
                <Route
                  path="mail"
                  element={
                    <LearningWriteGuard>
                      <LearningMailPage />
                    </LearningWriteGuard>
                  }
                />
              </Route>
              <Route path="learning/*" element={<Navigate to="/people" replace />} />
              <Route
                path="catalog"
                element={
                  <SuspenseRoute>
                    <CatalogLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/catalog/products" replace />} />
                <Route
                  path="products/duplicates"
                  element={
                    <SuspenseRoute>
                      <ProductDuplicateMergePage />
                    </SuspenseRoute>
                  }
                />
                <Route path="products" element={<ProductListPage />} />
                <Route path="import" element={<ProductImportPage />} />
                <Route path="categories" element={<CategoryListPage />} />
                <Route path="brands" element={<BrandListPage />} />
                <Route path="ingredients" element={<IngredientListPage />} />
                <Route path="national-drugs" element={<NationalDrugLookupPage />} />
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
                <Route path="low-stock" element={<LowStockPage />} />
                <Route path="gpp-checklist" element={<GppOperationalChecklistPage />} />
                <Route path="warehouses" element={<WarehouseListPage />} />
                <Route path="opening-balance" element={<OpeningBalancePage />} />
                <Route path="transfers" element={<TransferListPage />} />
                <Route path="adjustments" element={<AdjustmentListPage />} />
                <Route path="adjustments/:id/count" element={<InventoryCountPage />} />
                <Route path="qd540-export" element={<Qd540ExportPage />} />
              </Route>
              <Route
                path="procurement"
                element={
                  <SuspenseRoute>
                    <ProcurementLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/procurement/suppliers" replace />} />
                <Route path="purchase-orders" element={<PurchaseOrderListPage />} />
                <Route path="goods-receipts" element={<GoodsReceiptListPage />} />
                <Route path="suppliers" element={<SupplierListPage />} />
                <Route path="vat-treatments" element={<VatTreatmentListPage />} />
                <Route
                  path="supplier-payables"
                  element={<RedirectPreserveSearch to="/receivables/suppliers" />}
                />
                <Route
                  path="supplier-payments"
                  element={<RedirectPreserveSearch to="/receivables/supplier-payments" />}
                />
              </Route>
              <Route
                path="receivables"
                element={
                  <SuspenseRoute>
                    <ReceivablesLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/receivables/customers" replace />} />
                <Route path="customers" element={<CustomerReceivablesPage />} />
                <Route path="customer-payments" element={<CustomerPaymentListPage />} />
                <Route path="suppliers" element={<SupplierPayablesPage />} />
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
                <Route
                  path="prescriptions"
                  element={
                    <RedirectPreserveSearch
                      to={rxModuleHidden ? '/connect/status' : '/rx/prescriptions'}
                    />
                  }
                />
                <Route
                  path="prescribers"
                  element={
                    <RedirectPreserveSearch
                      to={rxModuleHidden ? '/connect/team' : '/rx/prescribers'}
                    />
                  }
                />
                <Route
                  path="prescriber-links"
                  element={
                    <RedirectPreserveSearch
                      to={rxModuleHidden ? '/connect/team' : '/rx/prescriber-links'}
                    />
                  }
                />
                <Route path="orders" element={<SalesOrderListPage />} />
                <Route
                  path="customer-receivables"
                  element={<Navigate to="/receivables/customers" replace />}
                />
                <Route
                  path="customer-payments"
                  element={<RedirectPreserveSearch to="/receivables/customer-payments" />}
                />
                <Route
                  path="customer-drafts"
                  element={<RedirectPreserveSearch to="/sales/app-orders/drafts" />}
                />
                <Route
                  path="customer-reservations"
                  element={<RedirectPreserveSearch to="/sales/app-orders/reservations" />}
                />
                <Route
                  path="app-orders"
                  element={
                    <SuspenseRoute>
                      <AppOrdersLayout />
                    </SuspenseRoute>
                  }
                >
                  <Route index element={<Navigate to="/sales/app-orders/drafts" replace />} />
                  <Route path="drafts" element={<CustomerDraftOrderListPage />} />
                  <Route path="reservations" element={<CustomerReservationListPage />} />
                </Route>
                <Route path="returns" element={<SalesReturnListPage />} />
                <Route path="shift" element={<SalesShiftReportPage />} />
                <Route path="customers" element={<Navigate to="/customer/list" replace />} />
                <Route path="chat" element={<CustomerChatPage />} />
                <Route path="settings" element={<Navigate to="/system/pos-settings" replace />} />
                <Route path="loyalty" element={<Navigate to="/customer/loyalty" replace />} />
                <Route path="vouchers" element={<Navigate to="/customer/vouchers" replace />} />
              </Route>
              {rxModuleHidden ? (
                <Route path="rx/*" element={<Navigate to="/connect/status" replace />} />
              ) : (
                <Route
                  path="rx"
                  element={
                    <SuspenseRoute>
                      <RxLayout />
                    </SuspenseRoute>
                  }
                >
                  <Route index element={<Navigate to="/rx/overview" replace />} />
                  <Route path="overview" element={<RxDashboardPage />} />
                  <Route path="prescriptions" element={<PrescriptionListPage />} />
                  <Route path="prescribers" element={<PrescriberListPage />} />
                  <Route path="prescriber-links" element={<PrescriberLinksPage />} />
                </Route>
              )}
              <Route
                path="connect"
                element={
                  <SuspenseRoute>
                    <ConnectLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/connect/overview" replace />} />
                <Route path="overview" element={<ConnectOverviewPage />} />
                <Route path="network" element={<ConnectNetworkPage />} />
                <Route path="team" element={<ConnectTeamPage />} />
                <Route path="referrals" element={<ConnectReferralsPage />} />
                <Route path="bookings" element={<ConnectBookingsPage />} />
                <Route path="status" element={<ConnectStatusPage />} />
                <Route path="partners" element={<ConnectPartnersPage />} />
              </Route>
              <Route
                path="family-os"
                element={
                  <SuspenseRoute>
                    <FamilyOsLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/family-os/overview" replace />} />
                <Route path="overview" element={<FamilyOsOverviewPage />} />
                <Route path="members" element={<FamilyOsMembersPage />} />
                <Route path="day-flow" element={<FamilyOsDayFlowPage />} />
                <Route path="routines" element={<FamilyOsRoutinesPage />} />
                <Route path="agreements" element={<FamilyOsAgreementsPage />} />
              </Route>
              <Route
                path="clinic"
                element={
                  <SuspenseRoute>
                    <ClinicLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/clinic/overview" replace />} />
                <Route path="overview" element={<ClinicOverviewPage />} />
                <Route path="patients" element={<ClinicPatientsPage />} />
                <Route path="providers" element={<ClinicProvidersPage />} />
                <Route path="appointments" element={<ClinicAppointmentsPage />} />
                <Route path="visits" element={<ClinicVisitsPage />} />
                <Route path="settings" element={<ClinicSettingsPage />} />
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
                <Route path="groups" element={<CustomerGroupListPage />} />
                <Route path="engagement" element={<CustomerEngagementPage />} />
                <Route path="loyalty" element={<LoyaltySettingsPage />} />
                <Route path="vouchers" element={<VoucherListPage />} />
                <Route path=":customerId" element={<CustomerDetailPage />} />
              </Route>
              <Route
                path="kap"
                element={
                  <SuspenseRoute>
                    <KapLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/kap/leads" replace />} />
                <Route path="leads" element={<KapLeadsPage />} />
                <Route path="templates" element={<KapTemplatesPage />} />
                <Route path="rules" element={<KapRulesPage />} />
                <Route path="campaigns" element={<KapCampaignsPage />} />
                <Route path="partners" element={<KapPartnersPage />} />
              </Route>
              <Route
                path="reports"
                element={
                  <SuspenseRoute>
                    <ReportsLayout />
                  </SuspenseRoute>
                }
              >
                <Route
                  index
                  element={
                    <SuspenseRoute>
                      <ReportsHomePage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="sales/revenue-by-period"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="sales/revenue-by-payment-method"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="sales/shifts"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="sales/revenue-by-category"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="sales/revenue-by-clinic-doctor"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="procurement/grn-value"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="procurement/payables-snapshot"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="inventory/stock-snapshot"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="inventory/near-expiry"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="inventory/movement-summary"
                  element={
                    <SuspenseRoute>
                      <ReportViewPage />
                    </SuspenseRoute>
                  }
                />
              </Route>
              <Route
                path="system"
                element={
                  <SuspenseRoute>
                    <SystemLayout />
                  </SuspenseRoute>
                }
              >
                <Route index element={<Navigate to="/system/branches" replace />} />
                <Route path="branches" element={<BranchListPage />} />
                <Route path="users" element={<UserListPage />} />
                <Route path="roles" element={<RoleListPage />} />
                <Route path="platform-pack" element={<PlatformPackSettingsPage />} />
                <Route path="pos-settings" element={<ReceiptSettingsPage />} />
                <Route path="customer-app-settings" element={<CustomerAppSettingsPage />} />
                <Route path="audit-log" element={<AuditLogListPage />} />
                <Route
                  path="assessment-leads"
                  element={<RedirectPreserveSearch to="/kap/leads" />}
                />
              </Route>
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}

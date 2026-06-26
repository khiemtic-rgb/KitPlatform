export type DashboardSalesSnapshot = {
  todayNetTotal: number;
  weekNetTotal: number;
  todayOrderCount: number;
};

export type DashboardCatalogSnapshot = {
  productCount: number;
  customerCount: number;
};

export type DashboardInventorySnapshot = {
  activeBatchCount: number;
  nearExpiryBatchCount: number;
  lowStockBatchCount: number;
  expiryDays: number;
};

export type DashboardProcurementSnapshot = {
  pendingReceiptCount: number;
};

export type DashboardO2oSnapshot = {
  draftOrdersAwaitingCount: number;
  reservationsAwaitingCount: number;
  chatUnreadCount: number;
};

export type DashboardOverview = {
  sales: DashboardSalesSnapshot;
  catalog: DashboardCatalogSnapshot;
  inventory: DashboardInventorySnapshot;
  procurement: DashboardProcurementSnapshot;
  o2o: DashboardO2oSnapshot;
};

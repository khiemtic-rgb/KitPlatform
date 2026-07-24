import { useAuthStore } from '@/shared/auth/auth.store';

export function useHasPermission(permission: string): boolean {
  const user = useAuthStore((s) => s.user);
  if (!user) return false;
  if (user.roles.includes('ADMIN')) return true;
  return user.permissions?.includes(permission) ?? false;
}

export function useHasAnyPermission(...permissions: string[]): boolean {
  const user = useAuthStore((s) => s.user);
  if (!user) return false;
  if (user.roles.includes('ADMIN')) return true;
  const granted = user.permissions ?? [];
  return permissions.some((p) => granted.includes(p));
}

export function useIsAdmin(): boolean {
  const user = useAuthStore((s) => s.user);
  return user?.roles.includes('ADMIN') ?? false;
}

export function useSystemDeletePermanent(): boolean {
  return useHasPermission('system.delete_permanent');
}

export function useCanCatalogRead(): boolean {
  return useHasAnyPermission('catalog.read', 'catalog.write');
}

export function useCanCatalogWrite(): boolean {
  return useHasPermission('catalog.write');
}

export function useCanInventoryRead(): boolean {
  return useHasAnyPermission('inventory.read', 'inventory.write', 'inventory.approve');
}

export function useCanInventoryWrite(): boolean {
  return useHasPermission('inventory.write');
}

export function useCanProcurementRead(): boolean {
  return useHasAnyPermission(
    'procurement.read',
    'procurement.write',
    'procurement.suppliers',
    'procurement.po',
    'procurement.approve',
    'procurement.receive',
    'procurement.pay',
  );
}

export function useProcurementWrite(): boolean {
  return useHasAnyPermission(
    'procurement.write',
    'procurement.suppliers',
    'procurement.po',
    'procurement.approve',
    'procurement.receive',
    'procurement.pay',
  );
}

export function useCanSalesRead(): boolean {
  return useHasAnyPermission(
    'sales.read',
    'sales.write',
    'sales.pos',
    'sales.customers',
    'sales.settings',
    'sales.cancel',
  );
}

export function useCanSalesPos(): boolean {
  return useHasAnyPermission('sales.pos', 'sales.write');
}

export function useCanSalesCustomers(): boolean {
  return useHasAnyPermission('sales.customers', 'sales.write');
}

export function useCanSalesSettings(): boolean {
  return useHasAnyPermission('sales.settings', 'sales.write');
}

export function useCanSalesCancel(): boolean {
  return useHasPermission('sales.cancel');
}

export function useCanClinicRead(): boolean {
  return useHasAnyPermission('clinic.read', 'clinic.write');
}

export function useCanConnectRead(): boolean {
  return useHasAnyPermission('connect.read', 'connect.write');
}

/** Doanh thu / phân tích cửa hàng — chỉ reports hoặc ADMIN (không lấy theo success.*). */
export function useCanViewStoreAnalytics(): boolean {
  return useHasAnyPermission('reports.read', 'reports.write', 'reports.export') || useIsAdmin();
}

/** Menu Success: checklist NV hoặc Cockpit chủ. */
export function useCanAccessSuccessModule(): boolean {
  const checklistOrRead = useHasAnyPermission('success.read', 'success.checklist');
  const analytics = useCanViewStoreAnalytics();
  return checklistOrRead || analytics;
}

/** Cockpit chủ NT / loss — không cấp cho thu ngân chỉ có checklist/POS. */
export function useCanAccessOwnerCockpit(): boolean {
  return (
    useHasAnyPermission('success.read', 'reports.read', 'reports.write', 'reports.export') ||
    useIsAdmin()
  );
}

export function useCanReportsRead(): boolean {
  return (
    useHasAnyPermission('reports.read', 'reports.write', 'reports.export') || useIsAdmin()
  );
}

/** Xuất Excel/CSV — tách khỏi quyền xem/in báo cáo. */
export function useCanReportsExport(): boolean {
  return useHasPermission('reports.export') || useIsAdmin();
}

export function useCanSystemRead(): boolean {
  return useHasAnyPermission('system.read', 'system.write');
}

export function useCanRxRead(): boolean {
  return useHasAnyPermission(
    'rx.prescription.read',
    'rx.prescription.create',
    'rx.prescription.verify',
    'sales.write',
  );
}

export function useCanCustomerModule(): boolean {
  return useCanSalesCustomers() || useCanSalesRead();
}

export function useCanReceivables(): boolean {
  return useCanSalesCustomers() || useCanSalesRead();
}

/** NV có thể vào học; chủ/QL có learning.write hoặc ADMIN. */
export function useCanLearningRead(): boolean {
  return (
    useHasAnyPermission('learning.read', 'learning.write', 'sales.pos', 'sales.write') || useIsAdmin()
  );
}

export function useCanLearningWrite(): boolean {
  return useHasPermission('learning.write');
}

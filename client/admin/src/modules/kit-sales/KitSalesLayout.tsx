import { useEffect } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { Spin } from 'antd';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { ADMIN_MODULE_PLATFORM_CODES } from '@/shared/platform/platform-feature-map';

export function KitSalesLayout() {
  const navigate = useNavigate();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const isAdmin = roles.includes('ADMIN');
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const moduleCode = ADMIN_MODULE_PLATFORM_CODES.kitSales ?? 'kit_sales';
  const moduleOk = !platformLoaded || isModuleEnabled(moduleCode);

  useEffect(() => {
    if (!isAdmin || (platformLoaded && !moduleOk)) navigate('/', { replace: true });
  }, [isAdmin, moduleOk, navigate, platformLoaded]);

  if (!isAdmin || (platformLoaded && !moduleOk)) return null;

  return (
    <Spin spinning={false}>
      <Outlet />
    </Spin>
  );
}

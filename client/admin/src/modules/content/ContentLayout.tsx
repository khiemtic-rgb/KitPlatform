import { Suspense, useEffect, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Select, Spin } from 'antd';
import { useRegisterModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { ADMIN_MODULE_PLATFORM_CODES } from '@/shared/platform/platform-feature-map';
import { ContentBrandProvider, useContentBrand } from './content-brand-context';
import { fetchContentBrands, type ContentBrand } from '@/shared/api/content.api';
import { ContentWorkTabs, isContentWorkPath } from './ContentWorkEntry';
import { ContentSettingsTabs, isContentSettingsPath } from './ContentSettingsTabs';
function ContentBrandBar() {
  const workspace = useContentBrand();
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  useEffect(() => {
    void fetchContentBrands(true).then((rows) => setBrands(rows ?? [])).catch(() => setBrands([]));
  }, []);
  const value = workspace.scope.mode === 'one' ? workspace.brandId : workspace.scope.mode === 'all' ? 'all' : undefined;
  return (
    <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12 }}>
      <span>Thương hiệu</span>
      <Select
        style={{ minWidth: 240 }}
        placeholder="Chọn thương hiệu"
        value={value}
        onChange={(next: string) => workspace.setBrand(next)}
        options={[
          { value: 'all', label: 'Tất cả thương hiệu' },
          ...brands.map((b) => ({ value: b.id, label: b.name })),
        ]}
      />
      {!workspace.canWrite ? <span>Đang xem. Chọn một thương hiệu để tạo mới.</span> : null}
    </div>
  );
}

export function ContentLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const isAdmin = roles.includes('ADMIN');
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const contentModule = ADMIN_MODULE_PLATFORM_CODES.content ?? 'kit_content';
  const moduleOk = !platformLoaded || isModuleEnabled(contentModule);

  useRegisterModuleSubnav(null, '', () => undefined);

  useEffect(() => {
    if (!isAdmin) {
      navigate('/', { replace: true });
      return;
    }
    if (platformLoaded && !moduleOk) {
      if (isModuleEnabled('kit_sales')) navigate('/kit-sales', { replace: true });
      else navigate('/', { replace: true });
    }
  }, [isAdmin, isModuleEnabled, moduleOk, navigate, platformLoaded]);

  useEffect(() => {
    if (location.pathname === '/content' || location.pathname === '/content/') {
      navigate('/content/overview', { replace: true });
    }
  }, [location.pathname, navigate]);

  if (!isAdmin || (platformLoaded && !moduleOk)) return null;

  return (
    <Suspense
      fallback={
        <div style={{ display: 'flex', justifyContent: 'center', padding: 64 }}>
          <Spin tip="Đang mở…" />
        </div>
      }
    >
      <ContentBrandProvider>
        <ContentBrandBar />
        {isContentWorkPath(location.pathname) ? <ContentWorkTabs /> : null}
        {isContentSettingsPath(location.pathname) ? <ContentSettingsTabs /> : null}
        <Outlet />
      </ContentBrandProvider>
    </Suspense>
  );
}

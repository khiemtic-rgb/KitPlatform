import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useRegisterModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { ADMIN_MODULE_PLATFORM_CODES } from '@/shared/platform/platform-feature-map';

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
    if (!isAdmin || (platformLoaded && !moduleOk)) navigate('/', { replace: true });
  }, [isAdmin, moduleOk, navigate, platformLoaded]);

  useEffect(() => {
    if (location.pathname === '/content' || location.pathname === '/content/') {
      navigate('/content/ops', { replace: true });
    }
  }, [location.pathname, navigate]);

  if (!isAdmin || (platformLoaded && !moduleOk)) return null;

  return <Outlet />;
}

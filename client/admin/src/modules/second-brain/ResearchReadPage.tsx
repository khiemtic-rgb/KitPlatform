import { Alert } from 'antd';
import { Link, Navigate } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { ResearchPanel } from './ResearchPanel';

export function ResearchReadPage() {
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const permissions = useAuthStore((s) => s.user?.permissions ?? []);
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const moduleOn = useTenantPlatformStore((s) => s.isModuleEnabled('personal_intelligence'));
  const canRead = roles.includes('ADMIN')
    || permissions.includes('personal_intelligence.research.read')
    || permissions.includes('personal_intelligence.trading.run');
  const canOpenTrading = roles.includes('ADMIN')
    || permissions.includes('personal_intelligence.trading.run')
    || permissions.includes('personal_intelligence.exchange.connect')
    || permissions.includes('personal_intelligence.exchange.read');

  if (platformLoaded && !moduleOn) {
    return <Alert type="warning" showIcon message="Module personal_intelligence chưa được bật cho tenant này." />;
  }
  if (!canRead && canOpenTrading) {
    return <Navigate to="/second-brain/agent" replace />;
  }
  if (!canRead) {
    return <Alert type="error" showIcon message="Bạn chưa có quyền đọc Research trên tenant này." />;
  }

  return (
    <>
      {canOpenTrading ? <p><Link to="/second-brain/agent">Mở Trading Agent</Link></p> : null}
      <ResearchPanel />
    </>
  );
}

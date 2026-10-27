import { useEffect, useMemo } from 'react';
import { Alert } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  ApiOutlined,
  AuditOutlined,
  FormOutlined,
  SafetyCertificateOutlined,
  TableOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { secondBrainAccess } from './intent-workspace';

const workspaceTab = { key: 'workspace', label: 'Yêu cầu', path: '/second-brain/workspace', icon: <FormOutlined /> };

const tradingTabs = [
  { key: 'session', label: 'Phiên làm việc', path: '/second-brain/session', icon: <ThunderboltOutlined /> },
  { key: 'overview', label: 'Tình hình', path: '/second-brain/overview', icon: <TableOutlined /> },
  { key: 'ledger', label: 'Sổ giao dịch', path: '/second-brain/ledger', icon: <TableOutlined /> },
  { key: 'spot', label: 'Spot', path: '/second-brain/spot', icon: <TableOutlined /> },
  { key: 'futures', label: 'Futures', path: '/second-brain/futures', icon: <TableOutlined /> },
  { key: 'journal', label: 'Nhật ký', path: '/second-brain/journal', icon: <AuditOutlined /> },
  { key: 'risk', label: 'Rủi ro & Giả lập', path: '/second-brain/risk', icon: <SafetyCertificateOutlined /> },
  { key: 'exchange', label: 'Kết nối sàn', path: '/second-brain/exchange', icon: <ApiOutlined /> },
];

export function TradingLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const permissions = useAuthStore((s) => s.user?.permissions ?? []);
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const moduleOn = useTenantPlatformStore((s) => s.isModuleEnabled('personal_intelligence'));
  const access = secondBrainAccess(roles, permissions);
  const items = useMemo(() => [
    ...(access.canIntent ? [workspaceTab] : []),
    ...(access.canTrading ? tradingTabs : []),
  ], [access.canIntent, access.canTrading]);
  const activeKey = location.pathname.startsWith('/second-brain/workspace')
    ? 'workspace'
    : location.pathname.startsWith('/second-brain/account')
      ? 'exchange'
      : tradingTabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'workspace';
  useRegisterSimpleModuleSubnav(items, activeKey, (path) => navigate(path));

  useEffect(() => {
    if (!access.canIntent) return;
    const atRoot = location.pathname === '/second-brain' || location.pathname === '/second-brain/';
    if (atRoot) {
      navigate(access.canTrading ? '/second-brain/session' : '/second-brain/workspace', { replace: true });
      return;
    }
    if (!access.canTrading && !location.pathname.startsWith('/second-brain/workspace')) {
      navigate('/second-brain/workspace', { replace: true });
    }
  }, [access.canIntent, access.canTrading, location.pathname, navigate]);

  if (platformLoaded && !moduleOn) {
    return <Alert type="warning" showIcon message="Module personal_intelligence chưa được bật cho tenant này." />;
  }
  if (!access.canIntent) {
    return <Alert type="error" showIcon message="Bạn chưa có quyền dùng Second Brain trên tenant này." />;
  }
  return <Outlet />;
}

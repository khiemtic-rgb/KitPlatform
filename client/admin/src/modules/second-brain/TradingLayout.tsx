import { useEffect, useMemo } from 'react';
import { Alert } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  ApiOutlined,
  AuditOutlined,
  SafetyCertificateOutlined,
  TableOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';

const tabs = [
  { key: 'agent', label: 'Trading Agent', path: '/second-brain/agent', icon: <ThunderboltOutlined /> },
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
  const allowed = roles.includes('ADMIN')
    || permissions.includes('personal_intelligence.trading.run')
    || permissions.includes('personal_intelligence.exchange.connect')
    || permissions.includes('personal_intelligence.exchange.read');
  const activeKey = location.pathname.startsWith('/second-brain/account')
    ? 'exchange'
    : tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'session';
  const items = useMemo(() => tabs, []);
  useRegisterSimpleModuleSubnav(items, activeKey, (path) => navigate(path));

  useEffect(() => {
    if (location.pathname === '/second-brain' || location.pathname === '/second-brain/') {
      navigate('/second-brain/agent', { replace: true });
    }
  }, [location.pathname, navigate]);

  if (platformLoaded && !moduleOn) {
    return <Alert type="warning" showIcon message="Module personal_intelligence chưa được bật cho tenant này." />;
  }
  if (!allowed) {
    return <Alert type="error" showIcon message="Bạn chưa có quyền dùng Second Brain trên tenant này." />;
  }
  return <Outlet />;
}

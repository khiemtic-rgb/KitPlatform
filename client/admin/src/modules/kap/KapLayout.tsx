import { useEffect, useMemo } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  FormOutlined,
  ProfileOutlined,
  RocketOutlined,
  SolutionOutlined,
  TeamOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useKapAdminAccess } from '@/shared/hooks/useKapAdminAccess';

export function KapLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const { enabled, checked } = useKapAdminAccess();

  const tabs = useMemo(
    () => [
      { key: 'leads', label: 'Khách tiềm năng', path: '/kap/leads', icon: <SolutionOutlined /> },
      { key: 'partners', label: 'Đối tác', path: '/kap/partners', icon: <TeamOutlined /> },
      { key: 'templates', label: 'Biểu mẫu', path: '/kap/templates', icon: <ProfileOutlined /> },
      { key: 'rules', label: 'Quy tắc', path: '/kap/rules', icon: <ThunderboltOutlined /> },
      { key: 'campaigns', label: 'Chiến dịch', path: '/kap/campaigns', icon: <RocketOutlined /> },
    ],
    [],
  );

  useEffect(() => {
    if (checked && !enabled) navigate('/', { replace: true });
  }, [checked, enabled, navigate]);

  useEffect(() => {
    if (location.pathname === '/kap' || location.pathname === '/kap/') {
      navigate('/kap/leads', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'leads';
  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  if (!checked || !enabled) return null;

  return (
    <div>
      <div style={{ marginBottom: 12, padding: '8px 12px', background: '#f0fdfa', borderRadius: 8 }}>
        <FormOutlined style={{ color: '#0f766e', marginRight: 8 }} />
        <strong>KAP — Khảo sát</strong>
        <span style={{ marginLeft: 8, color: '#64748b', fontSize: 13 }}>
          Thu thập · Đánh giá · Gợi ý · Khách tiềm năng
        </span>
      </div>
      <Outlet />
    </div>
  );
}

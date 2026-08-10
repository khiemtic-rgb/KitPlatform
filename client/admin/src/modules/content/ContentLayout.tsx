import { useEffect, useMemo } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  FileTextOutlined,
  FundOutlined,
  ReadOutlined,
  SettingOutlined,
  TagsOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';

export function ContentLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const isAdmin = roles.includes('ADMIN');

  const tabs = useMemo(
    () => [
      { key: 'budget', label: 'Ngân sách', path: '/content/budget', icon: <FundOutlined /> },
      { key: 'settings', label: 'Cài đặt', path: '/content/settings', icon: <SettingOutlined /> },
      { key: 'brands', label: 'Brand / kênh', path: '/content/brands', icon: <TagsOutlined /> },
      { key: 'topics', label: 'Chủ đề', path: '/content/topics', icon: <FileTextOutlined /> },
    ],
    [],
  );

  useEffect(() => {
    if (!isAdmin) navigate('/', { replace: true });
  }, [isAdmin, navigate]);

  useEffect(() => {
    if (location.pathname === '/content' || location.pathname === '/content/') {
      navigate('/content/budget', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'budget';
  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  if (!isAdmin) return null;

  return (
    <div>
      <div style={{ marginBottom: 12, padding: '8px 12px', background: '#f8fafc', borderRadius: 8 }}>
        <ReadOutlined style={{ color: '#0f2747', marginRight: 8 }} />
        <strong>Content Park</strong>
        <span style={{ marginLeft: 8, color: '#64748b', fontSize: 13 }}>
          Độc lập · đa brand/web/page · cấu hình động · trần ngân sách
        </span>
      </div>
      <Outlet />
    </div>
  );
}

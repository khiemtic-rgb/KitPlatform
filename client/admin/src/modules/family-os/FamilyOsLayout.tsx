import { useEffect, useMemo } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { CalendarOutlined, DashboardOutlined, FileProtectOutlined, GiftOutlined, SettingOutlined, TeamOutlined, UnorderedListOutlined } from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';

export function FamilyOsLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  const tabs = useMemo(
    () => [
      {
        key: 'overview',
        label: 'Tổng quan',
        path: '/family-os/overview',
        icon: <DashboardOutlined />,
      },
      {
        key: 'members',
        label: 'Thành viên',
        path: '/family-os/members',
        icon: <TeamOutlined />,
      },
      {
        key: 'day-flow',
        label: 'Hôm nay',
        path: '/family-os/day-flow',
        icon: <CalendarOutlined />,
      },
      {
        key: 'routines',
        label: 'Nhịp sống',
        path: '/family-os/routines',
        icon: <UnorderedListOutlined />,
      },
      {
        key: 'agreements',
        label: 'Thỏa thuận nhà',
        path: '/family-os/agreements',
        icon: <FileProtectOutlined />,
      },
      {
        key: 'rewards',
        label: 'Kho thưởng',
        path: '/family-os/rewards',
        icon: <GiftOutlined />,
      },
      {
        key: 'star-settings',
        label: 'Cài đặt sao',
        path: '/family-os/star-settings',
        icon: <SettingOutlined />,
      },
    ],
    [],
  );

  useEffect(() => {
    if (location.pathname === '/family-os' || location.pathname === '/family-os/') {
      navigate('/family-os/overview', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey =
    tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'overview';

  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  return <Outlet />;
}

import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  CalendarOutlined,
  DashboardOutlined,
  IdcardOutlined,
  ProfileOutlined,
  SettingOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';

export function ClinicLayout() {
  const { t } = useTranslation('clinic', { keyPrefix: 'layout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const tabs = useMemo(
    () => [
      {
        key: 'overview',
        label: t('overview'),
        path: '/clinic/overview',
        icon: <DashboardOutlined />,
      },
      {
        key: 'patients',
        label: t('patients'),
        path: '/clinic/patients',
        icon: <UserOutlined />,
      },
      {
        key: 'providers',
        label: t('providers'),
        path: '/clinic/providers',
        icon: <IdcardOutlined />,
      },
      {
        key: 'appointments',
        label: t('appointments'),
        path: '/clinic/appointments',
        icon: <CalendarOutlined />,
      },
      {
        key: 'visits',
        label: t('visits'),
        path: '/clinic/visits',
        icon: <ProfileOutlined />,
      },
      {
        key: 'settings',
        label: t('settings'),
        path: '/clinic/settings',
        icon: <SettingOutlined />,
      },
    ],
    [t],
  );

  useEffect(() => {
    if (location.pathname === '/clinic' || location.pathname === '/clinic/') {
      navigate('/clinic/overview', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey =
    tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'overview';

  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  return <Outlet />;
}

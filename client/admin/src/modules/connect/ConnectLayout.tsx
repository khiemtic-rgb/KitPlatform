import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  CheckCircleOutlined,
  ClusterOutlined,
  DashboardOutlined,
  IdcardOutlined,
  ScheduleOutlined,
  SwapOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { fetchConnectOrgProfile } from '@/shared/api/connect.api';

export function ConnectLayout() {
  const { t } = useTranslation('connect', { keyPrefix: 'layout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();
  const [orgKind, setOrgKind] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void fetchConnectOrgProfile().then((profile) => {
      if (!cancelled) setOrgKind(profile?.orgKind ?? null);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const tabs = useMemo(() => {
    const all = [
      {
        key: 'overview',
        label: t('overview'),
        path: '/connect/overview',
        icon: <DashboardOutlined />,
      },
      {
        key: 'network',
        label: t('network'),
        path: '/connect/network',
        icon: <ClusterOutlined />,
      },
      {
        key: 'team',
        label: t('team'),
        path: '/connect/team',
        icon: <IdcardOutlined />,
        clinicOnly: true,
      },
      {
        key: 'referrals',
        label: t('referrals'),
        path: '/connect/referrals',
        icon: <SwapOutlined />,
      },
      {
        key: 'bookings',
        label: t('bookings'),
        path: '/connect/bookings',
        icon: <ScheduleOutlined />,
      },
      {
        key: 'status',
        label: t('status'),
        path: '/connect/status',
        icon: <CheckCircleOutlined />,
      },
      {
        key: 'partners',
        label: t('partners'),
        path: '/connect/partners',
        icon: <TeamOutlined />,
      },
    ];
    return all.filter((tab) => !tab.clinicOnly || orgKind === 'clinic');
  }, [t, orgKind]);

  useEffect(() => {
    if (location.pathname === '/connect' || location.pathname === '/connect/') {
      navigate('/connect/overview', { replace: true });
      return;
    }
    if (orgKind && orgKind !== 'clinic' && location.pathname.startsWith('/connect/team')) {
      navigate('/connect/overview', { replace: true });
    }
  }, [location.pathname, navigate, orgKind]);

  const activeKey =
    tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'overview';

  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  return <Outlet />;
}

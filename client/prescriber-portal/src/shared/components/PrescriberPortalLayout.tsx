import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Button, Typography } from 'antd';
import {
  HomeOutlined,
  MedicineBoxOutlined,
  SearchOutlined,
  MailOutlined,
  FileTextOutlined,
  LogoutOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/shared/auth/auth.store';

export function PrescriberPortalLayout() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const profile = useAuthStore((s) => s.profile);
  const clearSession = useAuthStore((s) => s.clearSession);

  const onLogout = () => {
    clearSession();
    navigate('/login', { replace: true });
  };

  return (
    <div className="prescriber-portal-shell">
      <header className="prescriber-portal-header">
        <div>
          <Typography.Text strong style={{ color: '#1d4ed8' }}>
            Novixa BS
          </Typography.Text>
          <div style={{ fontSize: 13, color: '#64748b' }}>{profile?.fullName}</div>
        </div>
        <Button type="text" icon={<LogoutOutlined />} onClick={onLogout} aria-label={t('common.logout')} />
      </header>

      <main className="prescriber-portal-content">
        <Outlet />
      </main>

      <nav className="prescriber-portal-bottom-nav" aria-label="Main">
        <NavLink to="/" end>
          <HomeOutlined />
          <span>{t('nav.home')}</span>
        </NavLink>
        <NavLink to="/prescriptions">
          <FileTextOutlined />
          <span>{t('nav.prescriptions')}</span>
        </NavLink>
        <NavLink to="/pharmacies">
          <MedicineBoxOutlined />
          <span>{t('nav.pharmacies')}</span>
        </NavLink>
        <NavLink to="/directory">
          <SearchOutlined />
          <span>{t('nav.directory')}</span>
        </NavLink>
        <NavLink to="/invites">
          <MailOutlined />
          <span>{t('nav.invites')}</span>
        </NavLink>
      </nav>
    </div>
  );
}

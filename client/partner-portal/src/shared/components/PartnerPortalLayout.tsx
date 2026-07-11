import { Layout, Menu, Typography, Button, Space } from 'antd';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/auth.store';

const { Header, Content } = Layout;

export function PartnerPortalLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const partner = useAuthStore((s) => s.partner);
  const clearSession = useAuthStore((s) => s.clearSession);

  const selected = location.pathname.startsWith('/leads')
    ? 'leads'
    : location.pathname.startsWith('/referral')
      ? 'referral'
      : 'home';

  return (
    <Layout style={{ minHeight: '100vh', background: '#f0fdfa' }}>
      <Header style={{ background: '#0f766e', display: 'flex', alignItems: 'center', gap: 24, paddingInline: 20 }}>
        <Typography.Text strong style={{ color: '#fff', whiteSpace: 'nowrap' }}>
          Novixa Partner
        </Typography.Text>
        <Menu
          theme="dark"
          mode="horizontal"
          selectedKeys={[selected]}
          style={{ flex: 1, background: 'transparent', minWidth: 0 }}
          items={[
            { key: 'home', label: <Link to="/">Tổng quan</Link> },
            { key: 'referral', label: <Link to="/referral">Link & QR</Link> },
            { key: 'leads', label: <Link to="/leads">Lead</Link> },
          ]}
        />
        <Space>
          <Typography.Text style={{ color: '#ccfbf1' }}>{partner?.name}</Typography.Text>
          <Button
            size="small"
            onClick={() => {
              clearSession();
              navigate('/login');
            }}
          >
            Đăng xuất
          </Button>
        </Space>
      </Header>
      <Content style={{ padding: 24, maxWidth: 960, margin: '0 auto', width: '100%' }}>
        <Outlet />
      </Content>
    </Layout>
  );
}

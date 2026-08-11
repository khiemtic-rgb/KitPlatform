import { useEffect, useMemo } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Button, Steps, Typography } from 'antd';
import {
  ApiOutlined,
  FileTextOutlined,
  FundOutlined,
  ReadOutlined,
  SettingOutlined,
  TagsOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';

const STEP_PATHS = [
  '/content/brands',
  '/content/topics',
  '/content/topics',
] as const;

export function ContentLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const isAdmin = roles.includes('ADMIN');

  const tabs = useMemo(
    () => [
      { key: 'topics', label: 'Làm bài', path: '/content/topics', icon: <FileTextOutlined /> },
      { key: 'brands', label: 'Thương hiệu & nơi đăng', path: '/content/brands', icon: <TagsOutlined /> },
      { key: 'budget', label: 'Giới hạn chi phí', path: '/content/budget', icon: <FundOutlined /> },
      { key: 'ai', label: 'Cấu hình AI', path: '/content/ai', icon: <ApiOutlined /> },
      { key: 'settings', label: 'Tuỳ chọn nâng cao', path: '/content/settings', icon: <SettingOutlined /> },
    ],
    [],
  );

  useEffect(() => {
    if (!isAdmin) navigate('/', { replace: true });
  }, [isAdmin, navigate]);

  useEffect(() => {
    if (location.pathname === '/content' || location.pathname === '/content/') {
      navigate('/content/topics', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'topics';
  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  const currentStep =
    activeKey === 'brands' ? 0 : activeKey === 'topics' ? 1 : activeKey === 'budget' || activeKey === 'ai' || activeKey === 'settings' ? 2 : 1;

  if (!isAdmin) return null;

  return (
    <div>
      <div
        style={{
          marginBottom: 16,
          padding: '14px 16px',
          background: 'linear-gradient(135deg, #f8fafc 0%, #eef6ff 100%)',
          border: '1px solid #e2e8f0',
          borderRadius: 10,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, marginBottom: 12 }}>
          <ReadOutlined style={{ color: '#0f2747', fontSize: 18, marginTop: 2 }} />
          <div>
            <Typography.Title level={5} style={{ margin: 0 }}>
              Nội dung — viết & đăng bài cho nhiều thương hiệu
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              Dùng như một toà soạn nhỏ: khai báo nơi đăng → nhờ AI viết → duyệt rồi xuất bản.
              Không liên quan bán hàng / tồn kho.
            </Typography.Text>
          </div>
        </div>

        <Steps
          size="small"
          current={currentStep}
          onChange={(i) => navigate(STEP_PATHS[i] ?? '/content/topics')}
          items={[
            {
              title: '1. Nơi đăng',
              description: 'Thương hiệu + web/fanpage',
            },
            {
              title: '2. Làm bài',
              description: 'Dán tiêu đề → Nhờ AI → chọn ảnh',
            },
            {
              title: '3. Duyệt & đăng',
              description: 'Xem / duyệt → Xuất bản',
            },
          ]}
          style={{ marginBottom: 8 }}
        />

        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          <Button size="small" type={activeKey === 'brands' ? 'primary' : 'default'} onClick={() => navigate('/content/brands')}>
            Bước 1 · Thương hiệu
          </Button>
          <Button size="small" type={activeKey === 'topics' ? 'primary' : 'default'} onClick={() => navigate('/content/topics')}>
            Bước 2–3 · Làm bài
          </Button>
          <Button size="small" type="link" onClick={() => navigate('/content/budget')}>
            Xem giới hạn chi phí AI
          </Button>
        </div>
      </div>
      <Outlet />
    </div>
  );
}

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
  UnorderedListOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { ADMIN_MODULE_PLATFORM_CODES } from '@/shared/platform/platform-feature-map';

const STEP_PATHS = [
  '/content/brands',
  '/content/packages',
  '/content/topics',
] as const;

export function ContentLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const isAdmin = roles.includes('ADMIN');
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const contentModule = ADMIN_MODULE_PLATFORM_CODES.content ?? 'kit_content';
  const moduleOk = !platformLoaded || isModuleEnabled(contentModule);

  const tabs = useMemo(
    () => [
      { key: 'packages', label: 'Ý tưởng', path: '/content/packages', icon: <FileTextOutlined /> },
      { key: 'videos', label: 'Videos', path: '/content/videos', icon: <VideoCameraOutlined /> },
      { key: 'topics', label: 'Bài viết', path: '/content/topics', icon: <UnorderedListOutlined /> },
      { key: 'brands', label: 'Thương hiệu & nơi đăng', path: '/content/brands', icon: <TagsOutlined /> },
      { key: 'budget', label: 'Giới hạn chi phí', path: '/content/budget', icon: <FundOutlined /> },
      { key: 'ai', label: 'Cấu hình AI', path: '/content/ai', icon: <ApiOutlined /> },
      { key: 'settings', label: 'Tuỳ chọn nâng cao', path: '/content/settings', icon: <SettingOutlined /> },
    ],
    [],
  );

  useEffect(() => {
    if (!isAdmin || (platformLoaded && !moduleOk)) navigate('/', { replace: true });
  }, [isAdmin, moduleOk, navigate, platformLoaded]);

  useEffect(() => {
    if (location.pathname === '/content' || location.pathname === '/content/') {
      navigate('/content/packages', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'packages';
  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  const currentStep =
    activeKey === 'brands'
      ? 0
      : activeKey === 'packages' || activeKey === 'videos'
        ? 1
        : activeKey === 'topics'
          ? 2
          : activeKey === 'budget' || activeKey === 'ai' || activeKey === 'settings'
            ? 2
            : 1;

  if (!isAdmin || (platformLoaded && !moduleOk)) return null;

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
              Marketing Park — viết & đăng cho nhiều thương hiệu
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              <strong>Ý tưởng</strong> (viết AI / sang brand khác) → <strong>Videos</strong> (tuỳ chọn) →{' '}
              <strong>Bài viết</strong> (ảnh + xuất bản web/FB). Không lẫn ERP.
            </Typography.Text>
          </div>
        </div>

        <Steps
          size="small"
          current={Math.min(currentStep, 2)}
          onChange={(i) => navigate(STEP_PATHS[i] ?? '/content/packages')}
          items={[
            {
              title: '1. Nơi đăng',
              description: 'Thương hiệu + web/fanpage',
            },
            {
              title: '2. Ý tưởng',
              description: 'Generate All · Sang brand khác',
            },
            {
              title: '3. Bài viết',
              description: 'Ảnh → duyệt → xuất bản',
            },
          ]}
          style={{ marginBottom: 8 }}
        />

        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          <Button size="small" type={activeKey === 'brands' ? 'primary' : 'default'} onClick={() => navigate('/content/brands')}>
            Bước 1 · Thương hiệu
          </Button>
          <Button
            size="small"
            type={activeKey === 'packages' ? 'primary' : 'default'}
            onClick={() => navigate('/content/packages')}
          >
            Bước 2 · Ý tưởng
          </Button>
          <Button
            size="small"
            type={activeKey === 'topics' ? 'primary' : 'default'}
            onClick={() => navigate('/content/topics')}
          >
            Bước 3 · Bài viết
          </Button>
          <Button size="small" type={activeKey === 'videos' ? 'primary' : 'default'} onClick={() => navigate('/content/videos')}>
            Videos
          </Button>
          <Button size="small" type="link" onClick={() => navigate('/content/budget')}>
            Giới hạn chi phí AI
          </Button>
        </div>
      </div>
      <Outlet />
    </div>
  );
}

import { useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import { ContentPageHeader } from './ContentPageHeader';

/** Các trang thuộc mục "Cấu hình". */
const SETTINGS_TABS: { to: string; label: string }[] = [
  { to: '/content/brands', label: 'Thương hiệu' },
  { to: '/content/settings/film-types', label: 'Loại phim' },
  { to: '/content/ai-text', label: 'AI viết bài' },
  { to: '/content/ai', label: 'AI dựng video' },
  { to: '/content/budget', label: 'Chi phí AI' },
];

function match(pathname: string, to: string) {
  return pathname === to || pathname.startsWith(`${to}/`);
}

export function isContentSettingsPath(pathname: string) {
  return pathname === '/content/settings' || SETTINGS_TABS.some((t) => match(pathname, t.to));
}

/** Thanh tab chung cho mục Cấu hình (thay trang chỉ có link). */
export function ContentSettingsTabs() {
  const location = useLocation();
  const navigate = useNavigate();
  const active = SETTINGS_TABS.find((t) => match(location.pathname, t.to))?.to;
  const brand = new URLSearchParams(location.search).get('brand');
  return (
    <>
      <ContentPageHeader title="Cấu hình" description="Thương hiệu, loại phim, AI và chi phí. Thường chỉ cài một lần." />
      <Tabs
        activeKey={active}
        onChange={(to) => navigate(brand ? `${to}?brand=${brand}` : to)}
        style={{ marginBottom: 4 }}
        items={SETTINGS_TABS.map((t) => ({ key: t.to, label: t.label }))}
      />
    </>
  );
}

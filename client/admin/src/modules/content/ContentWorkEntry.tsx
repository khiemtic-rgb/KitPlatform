import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Tabs, Tooltip } from 'antd';
import { ContentPageHeader } from './ContentPageHeader';

/** Các trang thuộc mục "Content" ở menu trái, theo thứ tự làm việc. */
export const CONTENT_WORK_TABS: { to: string; label: string; hint: string }[] = [
  { to: '/content/pool', label: 'Ý tưởng', hint: 'Kho ý tưởng thô, chấm độ hợp brand trước khi viết.' },
  { to: '/content/packages', label: 'Góc nhìn brand', hint: 'Một ý tưởng gốc → góc nhìn của brand → bản viết cho từng nơi đăng.' },
  { to: '/content/topics', label: 'Bài viết', hint: 'Bài theo từng nơi đăng: ảnh, duyệt, xuất bản. Có nút nhập bài có sẵn.' },
  { to: '/content/article-series', label: 'Series', hint: 'Chuỗi bài theo câu chuyện: series → tập → bài viết.' },
];

/** Trang con không có tab riêng nhưng thuộc tab nào. */
const OWNER: Record<string, string> = { '/content/import': '/content/topics', '/content/brand-view': '/content/article-series' };

const PREFIXES = [...CONTENT_WORK_TABS.map((t) => t.to), ...Object.keys(OWNER), '/content/work'];

export function isContentWorkPath(pathname: string) {
  return PREFIXES.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

/** Thanh tab chung cho mọi trang của mục Content (thay cho trang chỉ có 5 đường link). */
export function ContentWorkTabs() {
  const location = useLocation();
  const navigate = useNavigate();
  const own = Object.keys(OWNER).find((p) => location.pathname.startsWith(p));
  const active = own
    ? OWNER[own]
    : CONTENT_WORK_TABS.find((t) => location.pathname === t.to || location.pathname.startsWith(`${t.to}/`))?.to;
  const brand = new URLSearchParams(location.search).get('brand');
  // Trang danh sách có đầu trang "Bài viết"; trang chi tiết (một bài, một series) chỉ giữ thanh tab.
  const isRoot = CONTENT_WORK_TABS.some((t) => t.to === location.pathname);
  return (
    <>
      {isRoot ? (
        <ContentPageHeader title="Bài viết" description="Từ ý tưởng đến bài đăng: chọn ý tưởng, viết góc nhìn của thương hiệu, duyệt và xuất bản." />
      ) : null}
      <Tabs
        activeKey={active}
        onChange={(to) => navigate(brand ? `${to}?brand=${brand}` : to)}
        style={{ marginBottom: 4 }}
        items={CONTENT_WORK_TABS.map((t) => ({
          key: t.to,
          label: <Tooltip title={t.hint} mouseEnterDelay={0.4}>{t.label}</Tooltip>,
        }))}
      />
    </>
  );
}

/** /content/work: mở thẳng tab Nội dung, giữ thương hiệu đang chọn. */
export function ContentWorkEntry() {
  const location = useLocation();
  return <Navigate to={`/content/topics${location.search}`} replace />;
}

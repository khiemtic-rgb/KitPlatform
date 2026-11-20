import { Navigate, useLocation, useSearchParams } from 'react-router-dom';
import { Alert, Tabs } from 'antd';
import { RobotOutlined } from '@ant-design/icons';
import { ContentPageHeader } from './ContentPageHeader';
import { ContentCalendarPage } from './ContentCalendarPage';
import { ContentAgentPage } from './ContentAgentPage';
import { AgentScheduleSettingsCard } from './AgentScheduleSettingsCard';
import { ManualPostingTab } from './ManualPostingTab';
import { useContentBrand } from './content-brand-context';

const TABS = ['lich', 'duyet', 'dangtay', 'caidat'] as const;
type TabKey = (typeof TABS)[number];

/** Lịch đăng: gộp lịch, đề xuất/đã duyệt của Agent và cài đặt lịch của thương hiệu. */
export function ContentPublishingPage() {
  const [params, setParams] = useSearchParams();
  const workspace = useContentBrand();
  const raw = params.get('tab') as TabKey | null;
  const tab: TabKey = raw && TABS.includes(raw) ? raw : 'lich';

  const go = (next: string) => {
    const query = new URLSearchParams(params);
    if (next === 'lich') query.delete('tab');
    else query.set('tab', next);
    setParams(query, { replace: true });
  };

  return (
    <div>
      <ContentPageHeader
        title="Lịch đăng"
        description="Lịch các bài sẽ đăng. Agent đăng bài tự xếp giờ theo cài đặt và đưa đề xuất để duyệt; Fanpage chưa có API thì đăng tay theo giờ."
      />
      <Tabs
        activeKey={tab}
        onChange={go}
        destroyInactiveTabPane
        items={[
          { key: 'lich', label: 'Lịch', children: <ContentCalendarPage embedded /> },
          { key: 'duyet', label: <><RobotOutlined /> Agent đăng bài</>, children: <ContentAgentPage embedded /> },
          { key: 'dangtay', label: 'Đăng tay', children: <ManualPostingTab brandId={workspace.brandId} /> },
          {
            key: 'caidat',
            label: 'Cài đặt lịch Agent',
            children: workspace.brandId ? (
              <AgentScheduleSettingsCard brandId={workspace.brandId} />
            ) : (
              <Alert type="info" showIcon message="Chọn một thương hiệu ở đầu trang để xem và sửa cài đặt lịch đăng." />
            ),
          },
        ]}
      />
    </div>
  );
}

/** Đường dẫn cũ /content/agent → tab Agent đăng bài, giữ thương hiệu. */
export function ContentAgentRedirect() {
  const location = useLocation();
  const query = new URLSearchParams(location.search);
  query.set('tab', 'duyet');
  return <Navigate to={`/content/calendar?${query.toString()}`} replace />;
}

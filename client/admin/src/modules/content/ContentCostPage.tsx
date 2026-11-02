import { Space } from 'antd';
import { ContentBudgetPage } from './ContentBudgetPage';
import { ContentSettingsPage } from './ContentSettingsPage';

/** Cấu hình → Chi phí AI: mức đã dùng trong tháng và trần chi phí/chất lượng ảnh (trước đây là hai trang). */
export function ContentCostPage() {
  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <ContentBudgetPage />
      <ContentSettingsPage />
    </Space>
  );
}

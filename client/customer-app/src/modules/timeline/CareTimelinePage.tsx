import { Card, Typography } from 'antd';
import { useTranslation } from 'react-i18next';

/** Placeholder care timeline until dedicated screen ships. */
export function CareTimelinePage() {
  const { t } = useTranslation();
  return (
    <div style={{ padding: 16 }}>
      <Card>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {t('nav.timeline', { defaultValue: 'Hành trình chăm sóc' })}
        </Typography.Title>
        <Typography.Paragraph type="secondary">
          Tính năng đang hoàn thiện. Bạn vẫn dùng Nhắc thuốc và Hồ sơ sức khỏe bình thường.
        </Typography.Paragraph>
      </Card>
    </div>
  );
}

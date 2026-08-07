import { Card, Typography } from 'antd';
import { useTranslation } from 'react-i18next';

/** Placeholder Rx entry until dedicated e-Rx claim flow ships. */
export function RxEntryPage() {
  const { t } = useTranslation();
  return (
    <div style={{ padding: 16 }}>
      <Card>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {t('nav.rx', { defaultValue: 'Đơn thuốc' })}
        </Typography.Title>
        <Typography.Paragraph type="secondary">
          Tính năng nhận đơn đang hoàn thiện. Liên hệ nhà thuốc nếu cần hỗ trợ.
        </Typography.Paragraph>
      </Card>
    </div>
  );
}

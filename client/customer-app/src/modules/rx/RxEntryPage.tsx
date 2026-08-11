import { Card, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

/** Mode A: nhận đơn / liên kết tại quầy; e-Rx claim đầy đủ để phase sau. */
export function RxEntryPage() {
  const { t } = useTranslation();
  return (
    <div style={{ padding: 16 }}>
      <Card>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {t('nav.rx', { defaultValue: 'Đơn thuốc' })}
        </Typography.Title>
        <Typography.Paragraph type="secondary">
          {t('rx.modeAHint', {
            defaultValue:
              'Để dùng dịch vụ nhà thuốc, nhờ nhân viên quầy cấp mã đăng nhập (mã quầy + OTP). NV sẽ đọc mã OTP cho bạn nhập trên app.',
          })}
        </Typography.Paragraph>
        <Link to="/login">{t('auth.login', { defaultValue: 'Đăng nhập' })}</Link>
      </Card>
    </div>
  );
}

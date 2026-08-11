import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Input, QRCode, Space, Typography } from 'antd';
import { CopyOutlined, DownloadOutlined, LinkOutlined } from '@ant-design/icons';
import { fetchPlatformPublicConfig } from '@/shared/api/platform.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import { buildCustomerAppLoginUrl } from '@/shared/utils/customer-app-link';

interface CustomerAppLinkQrPanelProps {
  /** Smaller QR for modal / toolbar. */
  compact?: boolean;
}

/** Nội dung link + QR vào app khách (dùng trong Card hoặc Modal). */
export function CustomerAppLinkQrPanel({ compact = false }: CustomerAppLinkQrPanelProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'receiptSettings.customerAppLinkCard' });
  const { message } = App.useApp();
  const tenantCode = useAuthStore((s) => s.user?.tenantCode?.trim().toUpperCase() ?? '');
  const qrWrapRef = useRef<HTMLDivElement>(null);
  const [baseUrl, setBaseUrl] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const config = await fetchPlatformPublicConfig();
        setBaseUrl(config.customerAppUrl?.trim() ?? '');
      } catch (error) {
        message.error(apiErrorMessage(error, t('loadFailed')));
      } finally {
        setLoading(false);
      }
    })();
  }, [message, t]);

  const loginUrl = useMemo(() => {
    if (!baseUrl || !tenantCode) return '';
    return buildCustomerAppLoginUrl(baseUrl, tenantCode);
  }, [baseUrl, tenantCode]);

  const copyLink = async () => {
    if (!loginUrl) return;
    try {
      await navigator.clipboard.writeText(loginUrl);
      message.success(t('copySuccess'));
    } catch {
      message.error(t('copyFailed'));
    }
  };

  const downloadQr = () => {
    const canvas = qrWrapRef.current?.querySelector('canvas');
    if (!canvas || !loginUrl) {
      message.error(t('downloadFailed'));
      return;
    }
    const link = document.createElement('a');
    link.download = `customer-app-${tenantCode || 'qr'}.png`;
    link.href = canvas.toDataURL('image/png');
    link.click();
    message.success(t('downloadSuccess'));
  };

  if (loading) {
    return <Typography.Text type="secondary">{t('loading')}</Typography.Text>;
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%', maxWidth: compact ? 480 : 520 }}>
      <Typography.Text type="secondary" style={{ fontSize: 13 }}>
        {t('intro')}
      </Typography.Text>

      {!tenantCode ? <Typography.Text type="warning">{t('missingTenant')}</Typography.Text> : null}

      {!baseUrl ? <Typography.Text type="warning">{t('missingAppUrl')}</Typography.Text> : null}

      {loginUrl ? (
        <>
          <Input
            readOnly
            value={loginUrl}
            addonBefore={<LinkOutlined />}
            addonAfter={
              <Button type="link" size="small" icon={<CopyOutlined />} onClick={() => void copyLink()}>
                {t('copy')}
              </Button>
            }
          />

          <Space align="start" wrap size="large">
            <div ref={qrWrapRef}>
              <QRCode value={loginUrl} size={compact ? 168 : 200} bordered />
            </div>
            <Space direction="vertical" size="small">
              <Typography.Text strong>{t('qrCaption', { tenant: tenantCode })}</Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('qrHint')}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('installHint')}
              </Typography.Text>
              <Button icon={<DownloadOutlined />} onClick={downloadQr}>
                {t('downloadQr')}
              </Button>
            </Space>
          </Space>
        </>
      ) : null}
    </Space>
  );
}

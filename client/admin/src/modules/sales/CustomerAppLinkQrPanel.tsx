import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Collapse, Input, QRCode, Space, Typography } from 'antd';
import { CopyOutlined, DownloadOutlined, LinkOutlined } from '@ant-design/icons';
import { fetchPlatformPublicConfig } from '@/shared/api/platform.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import { buildCustomerAppLoginUrl } from '@/shared/utils/customer-app-link';

interface CustomerAppLinkQrPanelProps {
  /** Smaller QR for modal / toolbar. */
  compact?: boolean;
}

/** Nội dung link + QR Novixa Health + kịch bản giới thiệu tại quầy. */
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
    link.download = `novixa-health-${tenantCode || 'qr'}.png`;
    link.href = canvas.toDataURL('image/png');
    link.click();
    message.success(t('downloadSuccess'));
  };

  if (loading) {
    return <Typography.Text type="secondary">{t('loading')}</Typography.Text>;
  }

  const scriptBlock = (
    <div
      style={{
        padding: '10px 12px',
        borderRadius: 8,
        background: 'rgba(37, 99, 235, 0.06)',
        border: '1px solid rgba(37, 99, 235, 0.18)',
      }}
    >
      <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
        {t('scriptLabel')}
      </Typography.Text>
      <Typography.Paragraph strong style={{ margin: 0, fontSize: compact ? 14 : 15, lineHeight: 1.45 }}>
        {t('scriptLine')}
      </Typography.Paragraph>
      <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 8 }}>
        {t('flowLine')}
      </Typography.Text>
    </div>
  );

  const faqItems = [
    {
      key: 'install',
      label: t('faq.installQ'),
      children: <Typography.Paragraph style={{ margin: 0 }}>{t('faq.installA')}</Typography.Paragraph>,
    },
    {
      key: 'privacy',
      label: t('faq.privacyQ'),
      children: <Typography.Paragraph style={{ margin: 0 }}>{t('faq.privacyA')}</Typography.Paragraph>,
    },
    {
      key: 'whenInstall',
      label: t('faq.whenInstallQ'),
      children: <Typography.Paragraph style={{ margin: 0 }}>{t('faq.whenInstallA')}</Typography.Paragraph>,
    },
  ];

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%', maxWidth: compact ? 520 : 560 }}>
      <Typography.Text type="secondary" style={{ fontSize: 13 }}>
        {t('intro')}
      </Typography.Text>

      {scriptBlock}

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
            <Space direction="vertical" size="small" style={{ maxWidth: 280 }}>
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

      <Collapse
        size="small"
        ghost={!compact}
        items={[
          {
            key: 'guide',
            label: t('faq.title'),
            children: <Collapse size="small" ghost items={faqItems} />,
          },
        ]}
        defaultActiveKey={compact ? [] : ['guide']}
      />

      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {t('dontPush')}
      </Typography.Text>
    </Space>
  );
}

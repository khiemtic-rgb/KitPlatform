import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Popover, Space, Spin, Typography, message } from 'antd';
import { MobileOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  fetchCustomer,
  fetchCustomerPilotOtp,
  issueCounterPilotOtp,
} from '@/shared/api/customer-admin.api';
import type { CustomerPilotOtpStatus } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';

const POLL_MS = 4000;

type Props = {
  customerId: string | null | undefined;
  canWrite: boolean;
};

export function PosCounterOtpButton({ customerId, canWrite }: Props) {
  const { t } = useTranslation('sales', { keyPrefix: 'pos.counterOtp' });
  const [open, setOpen] = useState(false);
  const [status, setStatus] = useState<CustomerPilotOtpStatus | null>(null);
  const [loading, setLoading] = useState(false);
  const [issuing, setIssuing] = useState(false);
  const [issuedCode, setIssuedCode] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!customerId) {
      setStatus(null);
      return;
    }
    setLoading(true);
    try {
      setStatus(await fetchCustomerPilotOtp(customerId));
    } catch {
      setStatus({ enabled: false, code: null, expiresAt: null, createdAt: null });
    } finally {
      setLoading(false);
    }
  }, [customerId]);

  useEffect(() => {
    if (!open || !customerId) return;
    void load();
    const timer = window.setInterval(() => void load(), POLL_MS);
    return () => window.clearInterval(timer);
  }, [open, customerId, load]);

  const onIssue = async () => {
    if (!customerId) return;
    setIssuing(true);
    try {
      const detail = await fetchCustomer(customerId);
      const res = await issueCounterPilotOtp({
        phone: detail.phone,
        fullName: detail.fullName,
      });
      setIssuedCode(res.pilotCode ?? null);
      message.success(res.message || t('issued'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('issueFailed')));
    } finally {
      setIssuing(false);
    }
  };

  if (!customerId || !canWrite) {
    return null;
  }

  const displayCode = issuedCode || status?.code;
  const expiresLabel =
    status?.expiresAt != null ? dayjs(status.expiresAt).format('HH:mm:ss') : null;

  const content = (
    <div style={{ maxWidth: 280 }}>
      {loading && !status ? (
        <Spin size="small" tip={t('loading')} />
      ) : !status?.enabled ? (
        <Alert type="warning" showIcon message={t('disabled')} />
      ) : displayCode ? (
        <Space direction="vertical" size="small" style={{ width: '100%' }}>
          <Alert
            type="success"
            showIcon
            message={t('activeMessage')}
            description={
              <Space direction="vertical" size={4}>
                <Typography.Title
                  level={3}
                  style={{ margin: 0, letterSpacing: 6, fontFamily: 'monospace' }}
                >
                  {displayCode}
                </Typography.Title>
                {expiresLabel ? (
                  <Typography.Text type="secondary">{t('expiresAt', { time: expiresLabel })}</Typography.Text>
                ) : null}
              </Space>
            }
          />
          <Typography.Text type="secondary">{t('hint')}</Typography.Text>
          <Button size="small" loading={issuing} onClick={() => void onIssue()}>
            {t('issueAgain')}
          </Button>
        </Space>
      ) : (
        <Space direction="vertical" size="small" style={{ width: '100%' }}>
          <Alert type="info" showIcon message={t('waiting')} description={t('waitingHint')} />
          <Button type="primary" size="small" loading={issuing} onClick={() => void onIssue()}>
            {t('issue')}
          </Button>
        </Space>
      )}
    </div>
  );

  return (
    <Popover
      title={t('title')}
      trigger="click"
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) setIssuedCode(null);
      }}
      content={content}
    >
      <Button icon={<MobileOutlined />}>{t('button')}</Button>
    </Popover>
  );
}

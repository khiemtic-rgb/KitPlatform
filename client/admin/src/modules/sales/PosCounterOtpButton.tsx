import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, App, Button, Input, Popover, Space, Spin, Typography } from 'antd';
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
  /** Compact next to OTP strip (POS). */
  compact?: boolean;
};

export function PosCounterOtpButton({ customerId, canWrite, compact = false }: Props) {
  const { t } = useTranslation('sales', { keyPrefix: 'pos.counterOtp' });
  const { message } = App.useApp();
  const [open, setOpen] = useState(false);
  const [status, setStatus] = useState<CustomerPilotOtpStatus | null>(null);
  const [loading, setLoading] = useState(false);
  const [issuing, setIssuing] = useState(false);
  const [issuedCode, setIssuedCode] = useState<string | null>(null);
  const [phoneInput, setPhoneInput] = useState('');
  const [resolvedPhone, setResolvedPhone] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!customerId) {
      setStatus({ enabled: true, code: null, expiresAt: null, createdAt: null });
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
    if (!open) return;
    void load();
    if (!customerId) return;
    const timer = window.setInterval(() => void load(), POLL_MS);
    return () => window.clearInterval(timer);
  }, [open, customerId, load]);

  useEffect(() => {
    if (!open || !customerId) {
      if (!customerId) setResolvedPhone(null);
      return;
    }
    let cancelled = false;
    void fetchCustomer(customerId)
      .then((detail) => {
        if (cancelled) return;
        setResolvedPhone(detail.phone);
        setPhoneInput(detail.phone.replace(/\D/g, ''));
      })
      .catch(() => {
        if (!cancelled) setResolvedPhone(null);
      });
    return () => {
      cancelled = true;
    };
  }, [open, customerId]);

  const onIssue = async () => {
    const phone = (customerId ? resolvedPhone : phoneInput)?.replace(/\D/g, '') ?? '';
    if (phone.length < 9) {
      message.warning(t('phoneRequired'));
      return;
    }
    setIssuing(true);
    try {
      let fullName: string | undefined;
      if (customerId) {
        const detail = await fetchCustomer(customerId);
        fullName = detail.fullName;
      }
      const res = await issueCounterPilotOtp({
        phone,
        fullName,
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

  if (!canWrite) {
    return null;
  }

  const displayCode = issuedCode || status?.code;
  const expiresLabel =
    status?.expiresAt != null ? dayjs(status.expiresAt).format('HH:mm:ss') : null;

  const content = (
    <div style={{ maxWidth: 300 }}>
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
          {!customerId ? (
            <Input
              inputMode="tel"
              placeholder={t('phonePlaceholder')}
              value={phoneInput}
              onChange={(e) => setPhoneInput(e.target.value.replace(/[^\d]/g, '').slice(0, 11))}
              maxLength={11}
            />
          ) : (
            <Typography.Text type="secondary">
              {t('phoneLabel')}: {resolvedPhone || '…'}
            </Typography.Text>
          )}
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
        if (!next) {
          setIssuedCode(null);
          if (!customerId) setPhoneInput('');
        }
      }}
      content={content}
    >
      <Button type={compact ? 'primary' : 'default'} size={compact ? 'small' : 'middle'} icon={<MobileOutlined />}>
        {t('button')}
      </Button>
    </Popover>
  );
}

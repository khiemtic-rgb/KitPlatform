import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Card, Space, Typography } from 'antd';
import { MobileOutlined, ReloadOutlined, SoundOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { fetchActiveCounterOtps } from '@/shared/api/customer-admin.api';
import type { ActiveCounterOtpItem } from '@/shared/api/customer-admin.types';

const POLL_MS = 3000;

function formatPhoneDisplay(phone: string): string {
  const d = phone.replace(/\D/g, '');
  if (d.length === 10) return `${d.slice(0, 4)} ${d.slice(4, 7)} ${d.slice(7)}`;
  return phone;
}

function playChime() {
  try {
    const Ctx = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    const ctx = new Ctx();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.value = 880;
    gain.gain.value = 0.04;
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start();
    gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.25);
    osc.stop(ctx.currentTime + 0.28);
    window.setTimeout(() => void ctx.close(), 400);
  } catch {
    /* ignore autoplay / unsupported */
  }
}

type Props = {
  /** Compact strip for POS toolbar area. */
  compact?: boolean;
  /** Extra actions next to reload (e.g. «Mã app» on POS). */
  actions?: ReactNode;
};

export function ActiveCounterOtpPanel({ compact = false, actions }: Props) {
  const { t } = useTranslation('customer', { keyPrefix: 'activeCounterOtp' });
  const [enabled, setEnabled] = useState(true);
  const [items, setItems] = useState<ActiveCounterOtpItem[]>([]);
  const [loading, setLoading] = useState(true);
  const prevKeysRef = useRef<string>('');

  const load = useCallback(async () => {
    try {
      const result = await fetchActiveCounterOtps();
      setEnabled(result.enabled);
      const next = result.items;
      const key = next.map((i) => `${i.phone}:${i.code}`).join('|');
      if (key && key !== prevKeysRef.current && prevKeysRef.current !== '') {
        playChime();
      }
      prevKeysRef.current = key;
      setItems(next);
    } catch {
      setEnabled(false);
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), POLL_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  if (!enabled) return null;

  const latest = items[0] ?? null;

  if (compact) {
    const compactActions = (
      <Space size={4}>
        {actions}
        <Button size="small" icon={<ReloadOutlined />} onClick={() => void load()} loading={loading} />
      </Space>
    );
    if (!latest) {
      return (
        <Alert
          type="info"
          showIcon
          icon={<MobileOutlined />}
          className="active-counter-otp--compact"
          message={t('waitingCompact')}
          description={t('waitingCompactHint')}
          action={compactActions}
        />
      );
    }
    return (
      <Alert
        type="success"
        showIcon
        icon={<SoundOutlined />}
        className="active-counter-otp--compact"
        message={
          <Space wrap size={12} align="center">
            <Typography.Text strong>
              {latest.customerName || t('unknownName')} · {formatPhoneDisplay(latest.phone)}
            </Typography.Text>
            <Typography.Text
              copyable={{ text: latest.code }}
              style={{ fontSize: 28, fontFamily: 'ui-monospace, monospace', letterSpacing: 6, fontWeight: 700 }}
            >
              {latest.code}
            </Typography.Text>
            <Typography.Text type="secondary">
              {t('expiresAt', { time: dayjs(latest.expiresAt).format('HH:mm:ss') })}
            </Typography.Text>
            {items.length > 1 ? (
              <Typography.Text type="secondary">{t('moreCount', { count: items.length - 1 })}</Typography.Text>
            ) : null}
          </Space>
        }
        action={compactActions}
      />
    );
  }

  return (
    <Card
      size="small"
      title={t('title')}
      extra={
        <Button size="small" icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('reload')}
        </Button>
      }
      style={{ marginBottom: 16 }}
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('intro')}
      </Typography.Paragraph>

      {!latest ? (
        <Alert type="info" showIcon message={t('waiting')} description={t('waitingHint')} />
      ) : (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          {items.map((item) => (
            <Alert
              key={`${item.phone}-${item.code}-${item.createdAt}`}
              type="success"
              showIcon
              message={
                <Space wrap>
                  <Typography.Text strong>{item.customerName || t('unknownName')}</Typography.Text>
                  <Typography.Text>{formatPhoneDisplay(item.phone)}</Typography.Text>
                </Space>
              }
              description={
                <Space direction="vertical" size={2}>
                  <Typography.Text
                    copyable={{ text: item.code }}
                    style={{
                      fontSize: 36,
                      lineHeight: 1.2,
                      fontFamily: 'ui-monospace, monospace',
                      letterSpacing: 8,
                      fontWeight: 700,
                    }}
                  >
                    {item.code}
                  </Typography.Text>
                  <Typography.Text type="secondary">
                    {t('expiresAt', { time: dayjs(item.expiresAt).format('HH:mm:ss') })} ·{' '}
                    {t('readAloud')}
                  </Typography.Text>
                </Space>
              }
            />
          ))}
        </Space>
      )}
    </Card>
  );
}

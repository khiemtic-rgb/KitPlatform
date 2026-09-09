import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Progress, Space, Tag, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKitSalesLeadPains,
  recordKitSalesPainResponse,
  type KitSalesLeadPainProfile,
} from '@/shared/api/kit-sales.api';

const RESPONSES = [
  'no_response',
  'seen_no_reply',
  'replied_not_interested',
  'objection',
  'interested',
  'phc_clicked',
] as const;

interface KitSalesPainCardProps {
  leadId: string;
  painCode?: string;
  onChanged?: () => void;
}

export function KitSalesPainCard({ leadId, painCode, onChanged }: KitSalesPainCardProps) {
  const { t } = useTranslation('kitSales');
  const [profile, setProfile] = useState<KitSalesLeadPainProfile | null>(null);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    try {
      setProfile(await fetchKitSalesLeadPains(leadId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('pain.failed')));
    }
  };

  useEffect(() => {
    void load();
  }, [leadId]);

  const activeCode = painCode ?? profile?.recommendedPainCode;
  const max = useMemo(
    () => Math.max(1, ...(profile?.scores.map((s) => Math.max(s.confidence, 0)) ?? [1])),
    [profile],
  );

  const onResponse = async (code: string) => {
    if (!activeCode) return;
    setSaving(true);
    try {
      await recordKitSalesPainResponse(leadId, activeCode, code);
      message.success(t('pain.logged'));
      await load();
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('pain.failed')));
    } finally {
      setSaving(false);
    }
  };

  if (!profile) return null;

  return (
    <Card size="small" title={t('pain.profileTitle')}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {profile.whyThisPain}
      </Typography.Paragraph>
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={profile.discoveryMode === 'exploit' ? 'green' : 'blue'}>
          {t(`pain.mode.${profile.discoveryMode}`, { defaultValue: profile.discoveryMode })}
        </Tag>
        <Tag>{t('pain.recommended')}: {activeCode}</Tag>
      </Space>
      {profile.scores.map((row) => (
        <div key={row.category} style={{ marginBottom: 8 }}>
          <Space>
            <Typography.Text>{row.name}</Typography.Text>
            <Typography.Text type="secondary">{Math.round(row.confidence)}</Typography.Text>
            {row.confirmed && <Tag color="green">{t('pain.confirmed')}</Tag>}
          </Space>
          <Progress
            percent={Math.round((Math.max(row.confidence, 0) / max) * 100)}
            showInfo={false}
            size="small"
          />
        </div>
      ))}
      <Typography.Text type="secondary">{t('pain.logResponse')}</Typography.Text>
      <Space wrap style={{ marginTop: 8 }}>
        {RESPONSES.map((code) => (
          <Button key={code} size="small" loading={saving} onClick={() => void onResponse(code)}>
            {t(`pain.response.${code}`)}
          </Button>
        ))}
      </Space>
    </Card>
  );
}

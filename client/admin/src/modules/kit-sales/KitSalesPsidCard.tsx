import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Input, Space, Tag, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  attachKitSalesLeadPsid,
  detachKitSalesLeadPsid,
  fetchKitSalesLeadPsid,
  type KitSalesFacebookPsid,
} from '@/shared/api/kit-sales.api';

interface KitSalesPsidCardProps {
  leadId: string;
}

export function KitSalesPsidCard({ leadId }: KitSalesPsidCardProps) {
  const { t } = useTranslation('kitSales');
  const [row, setRow] = useState<KitSalesFacebookPsid | null>(null);
  const [psid, setPsid] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      setRow(await fetchKitSalesLeadPsid(leadId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.psidFailed')));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [leadId]);

  const onAttach = async () => {
    setSaving(true);
    try {
      const next = await attachKitSalesLeadPsid(leadId, psid.trim());
      setRow(next);
      setPsid('');
      message.success(t('settings.psidSaved'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.psidFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onDetach = async () => {
    setSaving(true);
    try {
      await detachKitSalesLeadPsid(leadId);
      setRow(null);
      message.success(t('settings.psidRemoved'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.psidFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card size="small" title={t('settings.psidTitle')} loading={loading}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('settings.psidHint')}
      </Typography.Paragraph>
      {row ? (
        <Space wrap>
          <Tag color="green">{row.psid}</Tag>
          <Button size="small" danger onClick={() => void onDetach()} loading={saving}>
            {t('settings.psidDetach')}
          </Button>
        </Space>
      ) : (
        <Space.Compact style={{ width: '100%' }}>
          <Input
            value={psid}
            onChange={(event) => setPsid(event.target.value)}
            placeholder={t('settings.psidPlaceholder')}
          />
          <Button type="primary" onClick={() => void onAttach()} loading={saving} disabled={!psid.trim()}>
            {t('settings.psidAttach')}
          </Button>
        </Space.Compact>
      )}
    </Card>
  );
}

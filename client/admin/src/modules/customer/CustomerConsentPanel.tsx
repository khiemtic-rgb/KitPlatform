import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Space, Switch, Table, message } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import {
  fetchCustomerConsents,
  upsertCustomerConsents,
  type CustomerConsent,
} from '@/shared/api/customer.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { formatDisplayDate } from '@/shared/utils/date';

const DEFAULT_MATRIX: { channel: number; purpose: number }[] = [
  { channel: 1, purpose: 1 },
  { channel: 2, purpose: 1 },
  { channel: 3, purpose: 1 },
  { channel: 1, purpose: 2 },
  { channel: 4, purpose: 2 },
  { channel: 3, purpose: 2 },
  { channel: 5, purpose: 4 },
];

type ConsentRow = {
  key: string;
  channel: number;
  purpose: number;
  granted: boolean;
  grantedAt?: string;
  revokedAt?: string;
};

function mergeMatrix(consents: CustomerConsent[]): ConsentRow[] {
  const byKey = new Map(consents.map((c) => [`${c.channel}:${c.purpose}`, c]));
  return DEFAULT_MATRIX.map(({ channel, purpose }) => {
    const existing = byKey.get(`${channel}:${purpose}`);
    return {
      key: `${channel}:${purpose}`,
      channel,
      purpose,
      granted: existing?.granted ?? false,
      grantedAt: existing?.grantedAt,
      revokedAt: existing?.revokedAt,
    };
  });
}

interface CustomerConsentPanelProps {
  customerId: string;
}

export function CustomerConsentPanel({ customerId }: CustomerConsentPanelProps) {
  const { t } = useTranslation('customer', { keyPrefix: 'consentPanel' });
  const { consentChannelLabel, consentPurposeLabel } = useSalesEnums();
  const canWrite = useHasPermission('sales.write');
  const [rows, setRows] = useState<ConsentRow[]>(mergeMatrix([]));
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const loadConsents = useCallback(async () => {
    setLoading(true);
    try {
      const consents = await fetchCustomerConsents(customerId);
      setRows(mergeMatrix(consents));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [customerId, t]);

  useEffect(() => {
    void loadConsents();
  }, [loadConsents]);

  const columns: ColumnsType<ConsentRow> = useMemo(
    () => [
      {
        title: t('columns.channel'),
        dataIndex: 'channel',
        width: 100,
        render: (v: number) => consentChannelLabel(v),
      },
      {
        title: t('columns.purpose'),
        dataIndex: 'purpose',
        render: (v: number) => consentPurposeLabel(v),
      },
      {
        title: t('columns.granted'),
        width: 100,
        render: (_, row) => (
          <Switch
            checked={row.granted}
            disabled={!canWrite}
            onChange={(granted) =>
              setRows((prev) => prev.map((r) => (r.key === row.key ? { ...r, granted } : r)))
            }
          />
        ),
      },
      {
        title: t('columns.updatedAt'),
        width: 160,
        render: (_, row) => {
          const stamp = row.granted ? row.grantedAt : row.revokedAt;
          return stamp ? formatDisplayDate(stamp) : '—';
        },
      },
    ],
    [canWrite, consentChannelLabel, consentPurposeLabel, t],
  );

  const save = async () => {
    setSaving(true);
    try {
      const saved = await upsertCustomerConsents(
        customerId,
        rows.map((r) => ({
          channel: r.channel,
          purpose: r.purpose,
          granted: r.granted,
          source: 2,
        })),
      );
      setRows(mergeMatrix(saved));
      message.success(t('messages.saveSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        message={t('alertTitle')}
        description={t('alertDescription')}
      />
      {canWrite ? (
        <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void save()}>
          {t('saveButton')}
        </Button>
      ) : null}
      <Table rowKey="key" size="small" loading={loading} pagination={false} columns={columns} dataSource={rows} />
    </Space>
  );
}

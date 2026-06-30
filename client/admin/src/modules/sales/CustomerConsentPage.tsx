import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Card, Select, Space, Switch, Table, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { searchCustomers } from '@/shared/api/sales.api';
import type { CustomerListItem } from '@/shared/api/sales.types';
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

export function CustomerConsentPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'customerConsent' });
  const { consentChannelLabel, consentPurposeLabel } = useSalesEnums();
  const canWrite = useHasPermission('sales.write');
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [customerId, setCustomerId] = useState<string>();
  const [rows, setRows] = useState<ConsentRow[]>(mergeMatrix([]));
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    void searchCustomers().then(setCustomers).catch(() => setCustomers([]));
  }, []);

  const selectedCustomer = useMemo(
    () => customers.find((c) => c.id === customerId),
    [customerId, customers],
  );

  const loadConsents = useCallback(async (id: string) => {
    setLoading(true);
    try {
      const consents = await fetchCustomerConsents(id);
      setRows(mergeMatrix(consents));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!customerId) {
      setRows(mergeMatrix([]));
      return;
    }
    void loadConsents(customerId);
  }, [customerId, loadConsents]);

  const columns: ColumnsType<ConsentRow> = [
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
          disabled={!canWrite || !customerId}
          onChange={(granted) =>
            setRows((prev) =>
              prev.map((r) => (r.key === row.key ? { ...r, granted } : r)),
            )
          }
        />
      ),
    },
    {
      title: t('columns.updated'),
      width: 160,
      render: (_, row) => {
        const stamp = row.granted ? row.grantedAt : row.revokedAt;
        return stamp ? formatDisplayDate(stamp) : '—';
      },
    },
  ];

  const save = async () => {
    if (!customerId) {
      message.warning(t('messages.selectCustomer'));
      return;
    }
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
      const msg = apiErrorMessage(error, t('messages.saveFailed'));
      if (msg.includes('404')) {
        message.error(t('messages.apiNotFound'));
      } else if (msg.includes('500')) {
        message.error(t('messages.migrationMissing', { message: msg }));
      } else {
        message.error(msg);
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card title={t('title')}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('alert.message')}
        description={t('alert.description')}
      />
      <Space wrap style={{ marginBottom: 16 }}>
        <Select
          showSearch
          optionFilterProp="label"
          style={{ width: 360 }}
          placeholder={t('selectCustomer')}
          value={customerId}
          onChange={setCustomerId}
          options={customers.map((c) => ({
            value: c.id,
            label: `${c.customerCode} — ${c.fullName} (${c.phone})`,
          }))}
        />
        <Button type="primary" disabled={!canWrite || !customerId} loading={saving} onClick={() => void save()}>
          {t('save')}
        </Button>
      </Space>
      {selectedCustomer && (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {selectedCustomer.fullName} · {selectedCustomer.phone}
        </Typography.Paragraph>
      )}
      <Table rowKey="key" size="small" loading={loading} pagination={false} columns={columns} dataSource={rows} />
    </Card>
  );
}

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Select, Space, Switch, Table, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { searchCustomers } from '@/shared/api/sales.api';
import type { CustomerListItem } from '@/shared/api/sales.types';
import {
  CONSENT_CHANNEL_LABELS,
  CONSENT_PURPOSE_LABELS,
  fetchCustomerConsents,
  upsertCustomerConsents,
  type CustomerConsent,
} from '@/shared/api/customer.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { formatDisplayDate } from '@/shared/utils/date';

const DEFAULT_MATRIX: { channel: number; purpose: number }[] = [
  { channel: 1, purpose: 1 },
  { channel: 2, purpose: 1 },
  { channel: 3, purpose: 1 },
  { channel: 1, purpose: 2 },
  { channel: 3, purpose: 2 },
  { channel: 3, purpose: 4 },
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
      message.error(apiErrorMessage(error, 'Không tải được đồng ý khách hàng'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!customerId) {
      setRows(mergeMatrix([]));
      return;
    }
    void loadConsents(customerId);
  }, [customerId, loadConsents]);

  const columns: ColumnsType<ConsentRow> = [
    {
      title: 'Kênh',
      dataIndex: 'channel',
      width: 100,
      render: (v: number) => CONSENT_CHANNEL_LABELS[v] ?? v,
    },
    {
      title: 'Mục đích',
      dataIndex: 'purpose',
      render: (v: number) => CONSENT_PURPOSE_LABELS[v] ?? v,
    },
    {
      title: 'Đồng ý',
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
      title: 'Cập nhật',
      width: 160,
      render: (_, row) => {
        const stamp = row.granted ? row.grantedAt : row.revokedAt;
        return stamp ? formatDisplayDate(stamp) : '—';
      },
    },
  ];

  const save = async () => {
    if (!customerId) {
      message.warning('Chọn khách hàng');
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
      message.success('Đã lưu đồng ý — sự kiện CDP đã ghi outbox');
    } catch (error) {
      const msg = apiErrorMessage(error, 'Không lưu được đồng ý');
      if (msg.includes('404')) {
        message.error(
          'API chưa có endpoint đồng ý KH — chạy .\\scripts\\restart-api.ps1 rồi thử lại.',
        );
      } else if (msg.includes('500')) {
        message.error(
          `${msg} — có thể thiếu migration 016: chạy setup-and-migrate hoặc psql -f migrations/016_customer_consents_and_outbox.sql`,
        );
      } else {
        message.error(msg);
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card title="Đồng ý khách hàng (CDP)">
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Opt-in / opt-out theo kênh & mục đích"
        description="Mỗi lần lưu ghi sự kiện customer.consent.updated vào integration_outbox để worker CDP xử lý sau."
      />
      <Space wrap style={{ marginBottom: 16 }}>
        <Select
          showSearch
          optionFilterProp="label"
          style={{ width: 360 }}
          placeholder="Chọn khách hàng"
          value={customerId}
          onChange={setCustomerId}
          options={customers.map((c) => ({
            value: c.id,
            label: `${c.customerCode} — ${c.fullName} (${c.phone})`,
          }))}
        />
        <Button type="primary" disabled={!canWrite || !customerId} loading={saving} onClick={() => void save()}>
          Lưu đồng ý
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

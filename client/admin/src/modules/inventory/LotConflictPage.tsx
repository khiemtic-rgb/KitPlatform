import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, App, Button, Card, Input, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { fetchLotConflicts, unifyLotDates } from '@/shared/api/inventory.api';
import type { InventoryLotConflict, InventoryLotConflictCard } from '@/shared/api/inventory.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanInventoryWrite } from '@/shared/auth/usePermission';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';

type Draft = {
  manufactureDate?: string;
  expiryDate?: string;
};

function toDateKey(value?: string): string {
  if (!value) return '';
  return value.length >= 10 ? value.slice(0, 10) : value;
}

function dateOptions(values: string[]) {
  return values.map((value) => {
    const key = toDateKey(value);
    return { value: key, label: formatDisplayDate(key) };
  });
}

export function LotConflictPage() {
  const { t } = useTranslation('inventory', { keyPrefix: 'lotConflicts' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const { message } = App.useApp();
  const canWrite = useCanInventoryWrite();
  const [items, setItems] = useState<InventoryLotConflict[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [savingKey, setSavingKey] = useState<string>();

  const rowKey = (row: InventoryLotConflict) => `${row.productId}:${row.batchNumber}`;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const rows = await fetchLotConflicts(search || undefined);
      setItems(rows);
      setDrafts((prev) => {
        const next: Record<string, Draft> = {};
        for (const row of rows) {
          const key = `${row.productId}:${row.batchNumber}`;
          next[key] = {
            manufactureDate:
              prev[key]?.manufactureDate ??
              (row.manufactureDates.length === 1 ? toDateKey(row.manufactureDates[0]) : undefined),
            expiryDate:
              prev[key]?.expiryDate ??
              (row.expiryDates.length === 1 ? toDateKey(row.expiryDates[0]) : undefined),
          };
        }
        return next;
      });
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [search, message, t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 350);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const apply = async (row: InventoryLotConflict) => {
    const key = rowKey(row);
    const draft = drafts[key];
    if (!draft?.expiryDate) {
      message.warning(t('messages.expiryRequired'));
      return;
    }
    setSavingKey(key);
    try {
      const result = await unifyLotDates({
        productId: row.productId,
        batchNumber: row.batchNumber,
        manufactureDate: draft.manufactureDate || undefined,
        expiryDate: draft.expiryDate,
      });
      message.success(t('messages.unifySuccess', { count: result.cardsUpdated, lot: result.batchNumber }));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.unifyFailed')));
    } finally {
      setSavingKey(undefined);
    }
  };

  const cardColumns: ColumnsType<InventoryLotConflictCard> = [
    { title: ts('warehouse'), dataIndex: 'warehouseName' },
    { title: t('storedLot'), dataIndex: 'batchNumber', width: 140 },
    {
      title: ts('manufactureAbbr'),
      dataIndex: 'manufactureDate',
      width: 120,
      render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
    },
    {
      title: ts('expiryAbbr'),
      dataIndex: 'expiryDate',
      width: 120,
      render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
    },
    {
      title: ts('stockQty'),
      dataIndex: 'quantityAvailable',
      width: 90,
      align: 'right',
      render: (v: number) => formatDisplayQuantity(v),
    },
  ];

  const columns: ColumnsType<InventoryLotConflict> = useMemo(
    () => [
      {
        title: ts('productName'),
        key: 'product',
        render: (_, row) => (
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
              {ts('productCodeLabel', { code: row.productCode })}
            </Typography.Text>
            <span>{row.productName}</span>
          </div>
        ),
      },
      { title: t('lot'), dataIndex: 'batchNumber', width: 140 },
      {
        title: t('manufactureDates'),
        dataIndex: 'manufactureDates',
        width: 200,
        render: (values: string[]) =>
          values.length === 0 ? (
            '—'
          ) : (
            <Space size={[4, 4]} wrap>
              {values.map((value) => (
                <Tag key={value} color={values.length > 1 ? 'orange' : undefined}>
                  {formatDisplayDate(value)}
                </Tag>
              ))}
            </Space>
          ),
      },
      {
        title: t('expiryDates'),
        dataIndex: 'expiryDates',
        width: 200,
        render: (values: string[]) => (
          <Space size={[4, 4]} wrap>
            {values.map((value) => (
              <Tag key={value} color={values.length > 1 ? 'red' : undefined}>
                {formatDisplayDate(value)}
              </Tag>
            ))}
          </Space>
        ),
      },
      {
        title: t('warehouses'),
        dataIndex: 'warehouseCount',
        width: 80,
        align: 'right',
      },
      {
        title: ts('stockQty'),
        dataIndex: 'quantityAvailable',
        width: 90,
        align: 'right',
        render: (v: number) => formatDisplayQuantity(v),
      },
      {
        title: t('pickCorrect'),
        key: 'fix',
        width: 360,
        render: (_, row) => {
          const key = rowKey(row);
          const draft = drafts[key] ?? {};
          return (
            <Space wrap>
              <Select
                allowClear
                placeholder={ts('manufactureAbbr')}
                style={{ width: 130 }}
                value={draft.manufactureDate}
                options={dateOptions(row.manufactureDates)}
                onChange={(value) =>
                  setDrafts((prev) => ({ ...prev, [key]: { ...prev[key], manufactureDate: value } }))
                }
                disabled={!canWrite || !row.canUnify}
              />
              <Select
                placeholder={ts('expiryAbbr')}
                style={{ width: 130 }}
                value={draft.expiryDate}
                options={dateOptions(row.expiryDates)}
                onChange={(value) =>
                  setDrafts((prev) => ({ ...prev, [key]: { ...prev[key], expiryDate: value } }))
                }
                disabled={!canWrite || !row.canUnify}
              />
              <Button
                type="primary"
                size="small"
                loading={savingKey === key}
                disabled={!canWrite || !row.canUnify || !draft.expiryDate}
                onClick={() => void apply(row)}
              >
                {t('apply')}
              </Button>
            </Space>
          );
        },
      },
    ],
    [canWrite, drafts, savingKey, t, ts],
  );

  return (
    <Card
      title={t('title')}
      extra={
        <Space>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('searchPlaceholder')}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            style={{ width: 260 }}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            {tc('actions.reload')}
          </Button>
        </Space>
      }
    >
      <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Alert type="warning" showIcon message={t('hint')} />
        <Typography.Text type="secondary">{t('count', { count: items.length })}</Typography.Text>
        <Table
          rowKey={rowKey}
          loading={loading}
          columns={columns}
          dataSource={items}
          pagination={false}
          expandable={{
            expandedRowRender: (row) => (
              <Table
                size="small"
                rowKey="batchId"
                pagination={false}
                columns={cardColumns}
                dataSource={row.cards}
              />
            ),
          }}
          locale={{ emptyText: t('empty') }}
        />
      </Space>
    </Card>
  );
}

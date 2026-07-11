import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, DatePicker, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import { ReloadOutlined } from '@ant-design/icons';
import { fetchAuditLogs, type AuditLogItem } from '@/shared/api/audit.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayDateTime } from '@/shared/utils/date';
import { useSystemEnums } from '@/shared/i18n/use-system-enums';

const ENTITY_TYPES = [
  'sales_order',
  'goods_receipt',
  'purchase_order',
  'supplier_payment',
  'opening_balance',
  'inventory_adjustment',
] as const;

const ACTION_TYPES = ['create', 'complete', 'approve', 'cancel'] as const;

export function AuditLogListPage() {
  const { t } = useTranslation('system', { keyPrefix: 'auditLog' });
  const { t: tc } = useTranslation('common');
  const { auditActionLabel, auditEntityLabel } = useSystemEnums();
  const [items, setItems] = useState<AuditLogItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [entityType, setEntityType] = useState<string>();
  const [action, setAction] = useState<string>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [appliedEntityType, setAppliedEntityType] = useState<string>();
  const [appliedAction, setAppliedAction] = useState<string>();
  const [appliedRange, setAppliedRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [loading, setLoading] = useState(false);

  const entityOptions = useMemo(
    () => ENTITY_TYPES.map((value) => ({ value, label: auditEntityLabel(value) })),
    [auditEntityLabel],
  );

  const actionOptions = useMemo(
    () => ACTION_TYPES.map((value) => ({ value, label: auditActionLabel(value) })),
    [auditActionLabel],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const from = appliedRange?.[0]?.startOf('day').toISOString();
      const to = appliedRange?.[1]?.endOf('day').toISOString();
      const result = await fetchAuditLogs({
        entityType: appliedEntityType,
        action: appliedAction,
        from,
        to,
        page,
        pageSize,
      });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      setItems([]);
      setTotal(0);
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [appliedAction, appliedEntityType, appliedRange, page, pageSize, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const applyFilters = () => {
    setPage(1);
    setAppliedEntityType(entityType);
    setAppliedAction(action);
    setAppliedRange(range);
  };

  const columns: ColumnsType<AuditLogItem> = useMemo(
    () => [
      {
        title: t('columns.time'),
        dataIndex: 'createdAt',
        width: 160,
        render: (v: string) => formatDisplayDateTime(v),
      },
      {
        title: t('columns.user'),
        dataIndex: 'username',
        width: 120,
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('columns.entityType'),
        dataIndex: 'entityType',
        width: 140,
        render: (v: string) => <Tag>{auditEntityLabel(v)}</Tag>,
      },
      {
        title: t('columns.action'),
        dataIndex: 'action',
        width: 110,
        render: (v: string) => auditActionLabel(v),
      },
      {
        title: t('columns.detail'),
        dataIndex: 'payloadJson',
        ellipsis: true,
        render: (v?: string) => v ?? '—',
      },
    ],
    [auditActionLabel, auditEntityLabel, t],
  );

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          {t('title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('description')}
        </Typography.Paragraph>
      </div>

      <Card size="small">
        <Space wrap>
          <Select
            allowClear
            placeholder={t('filters.entityType')}
            style={{ minWidth: 180 }}
            value={entityType}
            onChange={setEntityType}
            options={entityOptions}
          />
          <Select
            allowClear
            placeholder={t('filters.action')}
            style={{ minWidth: 140 }}
            value={action}
            onChange={setAction}
            options={actionOptions}
          />
          <DatePicker.RangePicker value={range} onChange={setRange} format="DD/MM/YYYY" />
          <Button type="primary" icon={<ReloadOutlined />} onClick={applyFilters} loading={loading}>
            {tc('actions.filter')}
          </Button>
        </Space>
      </Card>

      <Table
        rowKey="id"
        size="small"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          onChange: (p, ps) => {
            setPage(p);
            setPageSize(ps);
          },
        }}
      />
    </Space>
  );
}

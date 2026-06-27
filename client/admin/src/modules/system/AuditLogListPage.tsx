import { useCallback, useEffect, useState } from 'react';
import { Button, Card, DatePicker, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import { ReloadOutlined } from '@ant-design/icons';
import {
  AUDIT_ACTION_LABELS,
  AUDIT_ENTITY_LABELS,
  fetchAuditLogs,
  type AuditLogItem,
} from '@/shared/api/audit.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayDateTime } from '@/shared/utils/date';

const ENTITY_OPTIONS = Object.entries(AUDIT_ENTITY_LABELS).map(([value, label]) => ({ value, label }));

export function AuditLogListPage() {
  const [items, setItems] = useState<AuditLogItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [entityType, setEntityType] = useState<string>();
  const [action, setAction] = useState<string>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const from = range?.[0]?.startOf('day').toISOString();
      const to = range?.[1]?.endOf('day').toISOString();
      const result = await fetchAuditLogs({ entityType, action, from, to, page, pageSize });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được nhật ký'));
    } finally {
      setLoading(false);
    }
  }, [action, entityType, page, pageSize, range]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: ColumnsType<AuditLogItem> = [
    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      width: 160,
      render: (v: string) => formatDisplayDateTime(v),
    },
    {
      title: 'Người dùng',
      dataIndex: 'username',
      width: 120,
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'Loại',
      dataIndex: 'entityType',
      width: 140,
      render: (v: string) => <Tag>{AUDIT_ENTITY_LABELS[v] ?? v}</Tag>,
    },
    {
      title: 'Hành động',
      dataIndex: 'action',
      width: 110,
      render: (v: string) => AUDIT_ACTION_LABELS[v] ?? v,
    },
    {
      title: 'Chi tiết',
      dataIndex: 'payloadJson',
      ellipsis: true,
      render: (v?: string) => v ?? '—',
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          Nhật ký hệ thống
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Ghi nhận mua hàng, bán hàng, nhập tồn đầu và duyệt kiểm kê.
        </Typography.Paragraph>
      </div>

      <Card size="small">
        <Space wrap>
          <Select
            allowClear
            placeholder="Loại nghiệp vụ"
            style={{ minWidth: 180 }}
            value={entityType}
            onChange={setEntityType}
            options={ENTITY_OPTIONS}
          />
          <Select
            allowClear
            placeholder="Hành động"
            style={{ minWidth: 140 }}
            value={action}
            onChange={setAction}
            options={[
              { value: 'create', label: 'Tạo mới' },
              { value: 'complete', label: 'Hoàn tất' },
              { value: 'approve', label: 'Duyệt' },
              { value: 'cancel', label: 'Hủy' },
            ]}
          />
          <DatePicker.RangePicker value={range} onChange={setRange} format="DD/MM/YYYY" />
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Lọc
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

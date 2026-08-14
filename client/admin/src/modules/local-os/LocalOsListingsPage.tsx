import { useCallback, useEffect, useState } from 'react';
import { Button, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchLocalOsListings,
  setLocalOsListingStatus,
  type LocalListing,
} from '@/shared/api/local-os.api';

const STATUS_COLOR: Record<string, string> = {
  NEEDS_REVIEW: 'gold',
  ACTIVE: 'green',
  HIDDEN: 'default',
  EXPIRED: 'red',
};

const KIND_LABEL: Record<string, string> = {
  job: 'Việc',
  event: 'Sự kiện',
  room: 'Trọ',
};

export function LocalOsListingsPage() {
  const [items, setItems] = useState<LocalListing[]>([]);
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState<string>('NEEDS_REVIEW');
  const [kind, setKind] = useState<string | undefined>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchLocalOsListings({ status: status || undefined, kind }));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được hàng chờ tin.'));
    } finally {
      setLoading(false);
    }
  }, [status, kind]);

  useEffect(() => {
    void load();
  }, [load]);

  const setStatusOf = async (id: string, next: string) => {
    try {
      await setLocalOsListingStatus(id, next);
      message.success(next === 'ACTIVE' ? 'Đã đăng lên site.' : 'Đã cập nhật trạng thái.');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đổi được trạng thái.'));
    }
  };

  const columns: ColumnsType<LocalListing> = [
    {
      title: 'Loại',
      dataIndex: 'kind',
      width: 90,
      render: (v: string) => KIND_LABEL[v] ?? v,
    },
    {
      title: 'Tin',
      dataIndex: 'title',
      render: (_, row) => (
        <div>
          <div style={{ fontWeight: 600 }}>{row.title}</div>
          <Typography.Text type="secondary">
            {[row.placeText, row.contactName, row.contactPhone].filter(Boolean).join(' · ')}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 140,
      render: (v: string) => <Tag color={STATUS_COLOR[v] ?? 'default'}>{v}</Tag>,
    },
    {
      title: '',
      width: 220,
      render: (_, row) => (
        <Space>
          {row.status !== 'ACTIVE' ? (
            <Button size="small" type="primary" onClick={() => void setStatusOf(row.id, 'ACTIVE')}>
              Đăng
            </Button>
          ) : null}
          {row.status !== 'HIDDEN' ? (
            <Button size="small" onClick={() => void setStatusOf(row.id, 'HIDDEN')}>
              Ẩn
            </Button>
          ) : null}
          {row.status === 'HIDDEN' ? (
            <Button size="small" onClick={() => void setStatusOf(row.id, 'NEEDS_REVIEW')}>
              Về chờ duyệt
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  return (
    <div style={{ padding: 16 }}>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Hàng chờ tin — Thái Nguyên Life
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Tin gửi từ site mặc định chờ duyệt. Site công khai chỉ hiện tin ACTIVE, không gắn cờ an toàn,
        còn hạn.
      </Typography.Paragraph>
      <Space style={{ marginBottom: 12 }} wrap>
        <Select
          style={{ width: 180 }}
          value={status}
          onChange={setStatus}
          options={[
            { value: 'NEEDS_REVIEW', label: 'Chờ duyệt' },
            { value: 'ACTIVE', label: 'Đang đăng' },
            { value: 'HIDDEN', label: 'Ẩn' },
            { value: 'EXPIRED', label: 'Hết hạn' },
            { value: '', label: 'Tất cả' },
          ]}
        />
        <Select
          allowClear
          placeholder="Loại tin"
          style={{ width: 140 }}
          value={kind}
          onChange={setKind}
          options={[
            { value: 'job', label: 'Việc' },
            { value: 'event', label: 'Sự kiện' },
            { value: 'room', label: 'Trọ' },
          ]}
        />
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          Tải lại
        </Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 20 }}
      />
    </div>
  );
}

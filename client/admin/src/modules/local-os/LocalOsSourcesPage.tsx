import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Form, Input, Modal, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createLocalOsSource,
  fetchLocalOsSources,
  fetchLocalOsWatchRuns,
  runLocalOsWatch,
  setLocalOsSourceStatus,
  updateLocalOsSource,
  type LocalSource,
  type LocalWatchRun,
} from '@/shared/api/local-os.api';

const KIND_LABEL: Record<string, string> = {
  facebook_group: 'Facebook group',
  facebook_page: 'Facebook page',
  official_web: 'Website chính thức',
  rss: 'RSS',
  partner: 'Đối tác',
  user: 'Người dùng',
};

export function LocalOsSourcesPage() {
  const [items, setItems] = useState<LocalSource[]>([]);
  const [runs, setRuns] = useState<LocalWatchRun[]>([]);
  const [loading, setLoading] = useState(false);
  const [watching, setWatching] = useState(false);
  const [editing, setEditing] = useState<LocalSource | null | 'new'>(null);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [src, watchRuns] = await Promise.all([fetchLocalOsSources(), fetchLocalOsWatchRuns()]);
      setItems(src);
      setRuns(watchRuns);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được sổ nguồn.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    try {
      const values = await form.validateFields();
      if (editing === 'new') {
        await createLocalOsSource(values);
        message.success('Đã thêm nguồn. Hệ thống không quét Facebook group.');
      } else if (editing) {
        await updateLocalOsSource(editing.id, values);
        message.success('Đã cập nhật nguồn.');
      }
      setEditing(null);
      await load();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(apiErrorMessage(error, 'Không lưu được nguồn.'));
    }
  };

  const columns: ColumnsType<LocalSource> = [
    {
      title: 'Loại',
      dataIndex: 'sourceKind',
      width: 160,
      render: (v: string) => KIND_LABEL[v] ?? v,
    },
    {
      title: 'Nguồn',
      dataIndex: 'name',
      render: (_, row) => (
        <div>
          <div style={{ fontWeight: 600 }}>{row.name}</div>
          {row.url ? (
            <Typography.Link href={row.url} target="_blank" rel="noopener">
              {row.url}
            </Typography.Link>
          ) : null}
        </div>
      ),
    },
    {
      title: 'Canh',
      dataIndex: 'watchEnabled',
      width: 90,
      render: (v: boolean) => (v ? <Tag color="blue">Mục lục</Tag> : <Tag>Tắt</Tag>),
    },
    { title: 'Nhóm tin', dataIndex: 'category', width: 100 },
    { title: 'Đối tượng', dataIndex: 'audience', width: 110 },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (v: string) => (
        <Tag color={v === 'active' ? 'green' : 'default'}>{v === 'active' ? 'Đang dùng' : 'Tạm dừng'}</Tag>
      ),
    },
    {
      title: '',
      width: 220,
      render: (_, row) => (
        <Space wrap>
          <Button
            size="small"
            onClick={() => {
              setEditing(row);
              form.setFieldsValue(row);
            }}
          >
            Sửa
          </Button>
          <Button
            size="small"
            onClick={() =>
              void setLocalOsSourceStatus(row.id, row.status === 'active' ? 'paused' : 'active')
                .then(() => load())
                .catch((error) => message.error(apiErrorMessage(error, 'Không đổi được trạng thái.')))
            }
          >
            {row.status === 'active' ? 'Tạm dừng' : 'Bật lại'}
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div style={{ padding: 16 }}>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Sổ nguồn — Thái Nguyên Life
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Canh mục lục website chính thức lúc <strong>8:00 sáng</strong> (giờ Việt Nam). Tin từ nguồn tin cậy
        <strong> tự đăng</strong> lên site. Form công khai và dán tay vẫn chờ duyệt. Không quét Facebook.
      </Typography.Paragraph>
      <Space style={{ marginBottom: 12 }} wrap>
        <Button
          type="primary"
          onClick={() => {
            setEditing('new');
            form.resetFields();
            form.setFieldsValue({ sourceKind: 'official_web', status: 'active' });
          }}
        >
          Thêm nguồn
        </Button>
        <Button
          loading={watching}
          onClick={() => {
            setWatching(true);
            void runLocalOsWatch()
              .then((r) => {
                message.success(
                  `Canh xong: +${r.createdCount} nháp, bỏ trùng ${r.skippedExisting}, lọc ${r.skippedFilter}.`,
                );
                return load();
              })
              .catch((error) => message.error(apiErrorMessage(error, 'Không canh được nguồn.')))
              .finally(() => setWatching(false));
          }}
        >
          Canh nguồn ngay
        </Button>
        <Link to="/local-os/listings">Hàng chờ tin</Link>
        {' · '}
        <Link to="/local-os/stats">Thống kê truy cập</Link>
      </Space>
      {runs[0] ? (
        <Typography.Paragraph type="secondary">
          Lần canh gần nhất: {new Date(runs[0].startedAt).toLocaleString('vi-VN')}
          {runs[0].finishedAt ? '' : ' (chưa xong)'} · +{runs[0].createdCount} tin ·{' '}
          {runs[0].trigger === 'scheduled' ? 'lịch 8h sáng' : 'bấm tay'}
          {runs[0].note ? ` — ${runs[0].note}` : ''}
        </Typography.Paragraph>
      ) : null}
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />

      <Modal
        title={editing === 'new' ? 'Thêm nguồn' : 'Sửa nguồn'}
        open={!!editing}
        onCancel={() => setEditing(null)}
        onOk={() => void save()}
        okText="Lưu"
      >
        <Form form={form} layout="vertical">
          <Form.Item name="sourceKind" label="Loại" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'facebook_group', label: 'Facebook group (chỉ đăng ký, không quét)' },
                { value: 'facebook_page', label: 'Facebook page' },
                { value: 'official_web', label: 'Website chính thức' },
                { value: 'rss', label: 'RSS' },
                { value: 'partner', label: 'Đối tác' },
                { value: 'user', label: 'Người dùng' },
              ]}
            />
          </Form.Item>
          <Form.Item name="name" label="Tên" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="url" label="URL">
            <Input placeholder="https://… group home hoặc website" />
          </Form.Item>
          <Form.Item name="category" label="Nhóm tin">
            <Select
              allowClear
              options={[
                { value: 'job', label: 'Việc' },
                { value: 'event', label: 'Sự kiện' },
                { value: 'room', label: 'Trọ' },
                { value: 'mixed', label: 'Hỗn hợp' },
              ]}
            />
          </Form.Item>
          <Form.Item name="audience" label="Đối tượng">
            <Select
              allowClear
              options={[
                { value: 'student', label: 'Sinh viên' },
                { value: 'worker', label: 'Người đi làm' },
                { value: 'mixed', label: 'Hỗn hợp' },
              ]}
            />
          </Form.Item>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Form, Input, Modal, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchLocalOsListings,
  fetchLocalOsSources,
  ingestLocalOsSource,
  runLocalOsWatch,
  setLocalOsListingStatus,
  updateLocalOsListing,
  type LocalListing,
  type LocalSource,
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
  const [ingesting, setIngesting] = useState(false);
  const [watching, setWatching] = useState(false);
  const [status, setStatus] = useState<string>('NEEDS_REVIEW');
  const [kind, setKind] = useState<string | undefined>();
  const [editing, setEditing] = useState<LocalListing | null>(null);
  const [sources, setSources] = useState<LocalSource[]>([]);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();

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

  useEffect(() => {
    void fetchLocalOsSources()
      .then(setSources)
      .catch(() => undefined);
  }, []);

  const ingest = async () => {
    try {
      const values = await form.validateFields();
      setIngesting(true);
      const result = await ingestLocalOsSource({
        sourceUrl: values.sourceUrl,
        pastedText: values.pastedText,
        kind: values.kind,
        sourceId: values.sourceId,
      });
      message.success(result.note);
      form.resetFields();
      setStatus('NEEDS_REVIEW');
      await load();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(apiErrorMessage(error, 'Không đưa được tin vào hàng chờ.'));
    } finally {
      setIngesting(false);
    }
  };

  const setStatusOf = async (id: string, next: string) => {
    try {
      await setLocalOsListingStatus(id, next);
      message.success(next === 'ACTIVE' ? 'Đã đăng lên site.' : 'Đã cập nhật trạng thái.');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đổi được trạng thái.'));
    }
  };

  const saveEdit = async () => {
    if (!editing) return;
    try {
      const values = await editForm.validateFields();
      await updateLocalOsListing(editing.id, {
        kind: values.kind,
        title: values.title,
        summary: values.summary,
        placeText: values.placeText,
        salaryText: values.salaryText,
        contactPhone: values.contactPhone,
        sourceKind: editing.sourceKind ?? 'url_paste',
        sourceUrl: editing.sourceUrl,
        trust: editing.trust,
        safetyFlag: editing.safetyFlag,
        status: editing.status,
      });
      message.success('Đã sửa. Vẫn chờ duyệt trừ khi bạn bấm Đăng.');
      setEditing(null);
      await load();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(apiErrorMessage(error, 'Không lưu được.'));
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
            {[row.placeText, row.salaryText, row.contactPhone].filter(Boolean).join(' · ')}
          </Typography.Text>
          {row.sourceName || row.sourceUrl ? (
            <div>
              {row.sourceName ? (
                <Typography.Text type="secondary">{row.sourceName} · </Typography.Text>
              ) : null}
              {row.sourceUrl ? (
                <Typography.Link href={row.sourceUrl} target="_blank" rel="noopener">
                  Link bài
                </Typography.Link>
              ) : null}
            </div>
          ) : null}
        </div>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 140,
      render: (v: string, row) => (
        <Space direction="vertical" size={0}>
          <Tag color={STATUS_COLOR[v] ?? 'default'}>{v}</Tag>
          {row.safetyFlag ? <Tag color="red">Cờ an toàn</Tag> : null}
        </Space>
      ),
    },
    {
      title: '',
      width: 280,
      render: (_, row) => (
        <Space wrap>
          <Button
            size="small"
            onClick={() => {
              setEditing(row);
              editForm.setFieldsValue({
                kind: row.kind,
                title: row.title,
                summary: row.summary,
                placeText: row.placeText,
                salaryText: row.salaryText,
                contactPhone: row.contactPhone,
              });
            }}
          >
            Sửa
          </Button>
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
        Dán <strong>link một bài</strong> hoặc bấm <strong>Canh nguồn</strong> (website chính thức → nháp). Facebook:
        dán thêm nội dung bài — máy không đọc group. Tin vào <strong>chờ duyệt</strong>; site chỉ hiện sau khi bấm
        Đăng. Không tự đăng group. <Link to="/local-os/sources">Sổ nguồn</Link>
      </Typography.Paragraph>
      <Space style={{ marginBottom: 12 }}>
        <Button
          loading={watching}
          onClick={() => {
            setWatching(true);
            void runLocalOsWatch()
              .then((r) => {
                message.success(`Canh xong: +${r.createdCount} nháp chờ duyệt.`);
                setStatus('NEEDS_REVIEW');
                return load();
              })
              .catch((error) => message.error(apiErrorMessage(error, 'Không canh được nguồn.')))
              .finally(() => setWatching(false));
          }}
        >
          Canh nguồn ngay
        </Button>
      </Space>

      <Form form={form} layout="vertical" style={{ maxWidth: 720, marginBottom: 20 }}>
        <Form.Item name="sourceId" label="Nguồn đã đăng ký (nếu biết)">
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="Tự khớp theo group / website — hoặc chọn"
            options={sources
              .filter((s) => s.status === 'active')
              .map((s) => ({ value: s.id, label: `${s.name} (${s.sourceKind})` }))}
          />
        </Form.Item>
        <Form.Item
          name="sourceUrl"
          label="Link bài"
          rules={[{ required: true, message: 'Dán URL bài viết.' }]}
        >
          <Input placeholder="https://www.facebook.com/groups/…/posts/… hoặc trang web bài viết" />
        </Form.Item>
        <Form.Item name="kind" label="Loại (nếu biết)">
          <Select
            allowClear
            placeholder="Tự đoán"
            options={[
              { value: 'job', label: 'Việc' },
              { value: 'event', label: 'Sự kiện' },
              { value: 'room', label: 'Trọ' },
            ]}
          />
        </Form.Item>
        <Form.Item
          name="pastedText"
          label="Nội dung bài (bắt buộc với Facebook)"
        >
          <Input.TextArea rows={5} placeholder="Copy chữ trên bài Facebook / hoặc để trống nếu là trang web công khai." />
        </Form.Item>
        <Button type="primary" loading={ingesting} onClick={() => void ingest()}>
          Đưa vào chờ duyệt
        </Button>
      </Form>

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

      <Modal
        title="Sửa tin (vẫn chờ duyệt cho đến khi Đăng)"
        open={!!editing}
        onCancel={() => setEditing(null)}
        onOk={() => void saveEdit()}
        okText="Lưu"
      >
        <Form form={editForm} layout="vertical">
          <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'job', label: 'Việc' },
                { value: 'event', label: 'Sự kiện' },
                { value: 'room', label: 'Trọ' },
              ]}
            />
          </Form.Item>
          <Form.Item name="title" label="Tiêu đề" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="placeText" label="Địa điểm">
            <Input />
          </Form.Item>
          <Form.Item name="salaryText" label="Lương / giá">
            <Input />
          </Form.Item>
          <Form.Item name="contactPhone" label="SĐT">
            <Input />
          </Form.Item>
          <Form.Item name="summary" label="Nội dung">
            <Input.TextArea rows={6} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

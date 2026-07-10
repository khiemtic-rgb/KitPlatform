import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Form,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  approvePrescriberLink,
  fetchPendingPrescriberLinks,
  fetchPrescriberLinks,
  invitePrescriberLink,
  rejectPrescriberLink,
  revokePrescriberLink,
  type RxPrescriberLink,
} from '@/shared/api/rx.api';
import { useHasPermission } from '@/shared/auth/usePermission';
import { apiErrorMessage } from '@/shared/api/api-error';

const linkStatusColor: Record<string, string> = {
  active: 'green',
  pending_nt_invite: 'blue',
  pending_nt_approval: 'gold',
  rejected: 'default',
  revoked: 'red',
};

type InviteFormValues = {
  phone: string;
  fullName: string;
  licenseNumber?: string;
  specialty?: string;
  notes?: string;
};

export function PrescriberLinksPage() {
  const canManage = useHasPermission('rx.prescriber.link.manage') || useHasPermission('rx.prescriber.manage');
  const [items, setItems] = useState<RxPrescriberLink[]>([]);
  const [pending, setPending] = useState<RxPrescriberLink[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string | undefined>();
  const [inviteOpen, setInviteOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<InviteFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [links, queue] = await Promise.all([
        fetchPrescriberLinks(statusFilter),
        fetchPendingPrescriberLinks(),
      ]);
      setItems(links);
      setPending(queue);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được liên kết bác sĩ'));
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  const onInvite = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await invitePrescriberLink(values);
      message.success('Đã gửi lời mời bác sĩ');
      setInviteOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được lời mời'));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<RxPrescriberLink> = useMemo(
    () => [
      {
        title: 'Bác sĩ',
        render: (_, row) => (
          <div>
            <div>{row.prescriberName ?? '—'}</div>
            <div style={{ color: '#64748b', fontSize: 12 }}>{row.prescriberPhone ?? '—'}</div>
          </div>
        ),
      },
      { title: 'CCHN', dataIndex: 'prescriberLicenseNumber', render: (v) => v || '—' },
      {
        title: 'Trạng thái',
        dataIndex: 'linkStatus',
        render: (value: string) => <Tag color={linkStatusColor[value] ?? 'default'}>{value}</Tag>,
      },
      { title: 'Khởi tạo', dataIndex: 'initiatedBy' },
      {
        title: 'Thao tác',
        render: (_, row) => (
          <Space>
            {canManage && row.linkStatus === 'pending_nt_approval' ? (
              <>
                <Button type="link" onClick={() => void approve(row.id)}>
                  Duyệt
                </Button>
                <Button type="link" danger onClick={() => void reject(row.id)}>
                  Từ chối
                </Button>
              </>
            ) : null}
            {canManage && row.linkStatus === 'active' ? (
              <Popconfirm title="Thu hồi liên kết?" onConfirm={() => void revoke(row.id)}>
                <Button type="link" danger>
                  Thu hồi
                </Button>
              </Popconfirm>
            ) : null}
          </Space>
        ),
      },
    ],
    [canManage],
  );

  async function approve(id: string) {
    try {
      await approvePrescriberLink(id);
      message.success('Đã duyệt liên kết');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không duyệt được'));
    }
  }

  async function reject(id: string) {
    try {
      await rejectPrescriberLink(id);
      message.success('Đã từ chối yêu cầu');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không từ chối được'));
    }
  }

  async function revoke(id: string) {
    try {
      await revokePrescriberLink(id);
      message.success('Đã thu hồi liên kết');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thu hồi được'));
    }
  }

  return (
    <div style={{ padding: 16 }}>
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        <Card
          title="Hàng chờ duyệt liên kết"
          extra={
            <Button icon={<ReloadOutlined />} onClick={() => void load()}>
              Tải lại
            </Button>
          }
        >
          <Table
            rowKey="id"
            loading={loading}
            dataSource={pending}
            pagination={false}
            columns={columns}
            locale={{ emptyText: 'Không có yêu cầu chờ duyệt' }}
          />
        </Card>

        <Card
          title="Mạng bác sĩ liên kết"
          extra={
            canManage ? (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setInviteOpen(true)}>
                Mời bác sĩ
              </Button>
            ) : null
          }
        >
          <Space style={{ marginBottom: 16 }}>
            <Select
              allowClear
              placeholder="Lọc trạng thái"
              style={{ width: 220 }}
              value={statusFilter}
              onChange={setStatusFilter}
              options={[
                { value: 'active', label: 'Active' },
                { value: 'pending_nt_invite', label: 'Chờ BS chấp nhận' },
                { value: 'pending_nt_approval', label: 'Chờ NT duyệt' },
                { value: 'revoked', label: 'Revoked' },
              ]}
            />
          </Space>
          <Table rowKey="id" loading={loading} dataSource={items} columns={columns} />
        </Card>
      </Space>

      <Modal
        title="Mời bác sĩ liên kết"
        open={inviteOpen}
        onCancel={() => setInviteOpen(false)}
        onOk={() => void onInvite()}
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item name="fullName" label="Họ tên BS" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="phone" label="Số điện thoại (OTP portal)" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item
            name="licenseNumber"
            label="Số CCHN"
            rules={[{ required: true, message: 'Nhập số chứng chỉ hành nghề (CCHN)' }]}
            extra="Bắt buộc — không kê đơn / duyệt liên kết nếu thiếu CCHN"
          >
            <Input placeholder="Số CCHN" />
          </Form.Item>
          <Form.Item name="specialty" label="Chuyên khoa">
            <Input />
          </Form.Item>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

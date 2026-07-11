import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import {
  createKapPartner,
  fetchKapPartners,
  updateKapPartner,
  type KapPartnerListItem,
} from '@/shared/api/kap-admin.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayDateTime } from '@/shared/utils/date';
import {
  KAP_PARTNER_STATUS_LABELS,
  KAP_PARTNER_TYPE_LABELS,
  kapLabel,
} from '@/modules/kap/kap-labels';

const TYPE_CODE_PREFIX: Record<string, string> = {
  ctv: 'CTV',
  consultant: 'CSL',
  tdv: 'TDV',
  agency: 'DLY',
};

function normalizeVnPhone(phone?: string): string | null {
  let digits = (phone ?? '').replace(/\D/g, '');
  if (!digits) return null;
  if (digits.startsWith('84') && digits.length >= 11) {
    digits = `0${digits.slice(2)}`;
  }
  if (digits.length === 9 && !digits.startsWith('0')) {
    digits = `0${digits}`;
  }
  if (digits.length < 9 || digits.length > 11) return null;
  return digits;
}

function suggestPartnerCode(partnerType: string, phone?: string): string {
  const prefix = TYPE_CODE_PREFIX[partnerType] ?? 'CTV';
  const phoneNorm = normalizeVnPhone(phone);
  if (phoneNorm) return `${prefix}${phoneNorm}`;
  return `${prefix}${Date.now().toString().slice(-8)}`;
}

const TYPE_OPTIONS = Object.entries(KAP_PARTNER_TYPE_LABELS).map(([value, label]) => ({
  value,
  label,
}));

const STATUS_OPTIONS = Object.entries(KAP_PARTNER_STATUS_LABELS).map(([value, label]) => ({
  value,
  label,
}));

export function KapPartnersPage() {
  const { message } = App.useApp();
  const [items, setItems] = useState<KapPartnerListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<KapPartnerListItem | null>(null);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchKapPartners());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được danh sách đối tác'));
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);

      let code = String(values.code ?? '').trim().toUpperCase();
      if (!editing && !code) {
        code = suggestPartnerCode(values.partnerType, values.phone);
      }

      if (editing) {
        await updateKapPartner(editing.id, {
          name: values.name,
          partnerType: values.partnerType,
          phone: values.phone,
          email: values.email,
          status: values.status,
          commissionRatePct: values.commissionRatePct,
          notes: values.notes,
          newPassword: values.password || undefined,
        });
      } else {
        await createKapPartner({
          code,
          name: values.name,
          partnerType: values.partnerType,
          password: values.password,
          phone: values.phone,
          email: values.email,
          commissionRatePct: values.commissionRatePct,
          notes: values.notes,
        });
      }
      message.success(editing ? 'Đã cập nhật đối tác' : `Đã tạo đối tác ${code}`);
      setOpen(false);
      setEditing(null);
      form.resetFields();
      await load();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        message.warning('Vui lòng điền đủ các trường bắt buộc');
        throw error;
      }
      message.error(apiErrorMessage(error, 'Không lưu được đối tác'));
      throw error;
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Đối tác kênh
          </Typography.Title>
          <Typography.Text type="secondary">
            Cấp tài khoản cổng đối tác — link/QR khảo sát gắn mã giới thiệu
          </Typography.Text>
        </div>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => {
            setEditing(null);
            form.resetFields();
            form.setFieldsValue({ partnerType: 'ctv', status: 'active' });
            setOpen(true);
          }}
        >
          Thêm đối tác
        </Button>
      </Space>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={items}
        scroll={{ x: 960 }}
        columns={[
          { title: 'Mã', dataIndex: 'code', width: 140 },
          { title: 'Tên', dataIndex: 'name', ellipsis: true },
          {
            title: 'Loại',
            dataIndex: 'partnerType',
            width: 160,
            render: (v: string) => kapLabel(KAP_PARTNER_TYPE_LABELS, v),
          },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            width: 120,
            render: (v: string) => (
              <Tag color={v === 'active' ? 'green' : v === 'suspended' ? 'orange' : 'default'}>
                {kapLabel(KAP_PARTNER_STATUS_LABELS, v)}
              </Tag>
            ),
          },
          { title: 'Khảo sát', dataIndex: 'submissionCount', width: 90, align: 'right' },
          { title: 'Khách tiềm năng', dataIndex: 'leadCount', width: 120, align: 'right' },
          {
            title: 'Đăng nhập gần nhất',
            dataIndex: 'lastLoginAt',
            width: 160,
            render: (v) => (v ? formatDisplayDateTime(v) : '—'),
          },
          {
            title: 'Thao tác',
            width: 100,
            fixed: 'right',
            render: (_, row) => (
              <Button
                size="small"
                type="link"
                icon={<EditOutlined />}
                onClick={() => {
                  setEditing(row);
                  form.setFieldsValue({
                    code: row.code,
                    name: row.name,
                    partnerType: row.partnerType,
                    phone: row.phone,
                    email: row.email,
                    status: row.status,
                    commissionRatePct: row.commissionRatePct,
                  });
                  setOpen(true);
                }}
              >
                Sửa
              </Button>
            ),
          },
        ]}
      />

      <Modal
        title={editing ? `Sửa đối tác ${editing.code}` : 'Thêm đối tác'}
        open={open}
        onCancel={() => {
          if (saving) return;
          setOpen(false);
        }}
        onOk={() => handleSave()}
        confirmLoading={saving}
        destroyOnHidden
        okText="Lưu"
        cancelButtonProps={{ disabled: saving }}
        maskClosable={!saving}
      >
        <Form form={form} layout="vertical" scrollToFirstError preserve={false}>
          <Form.Item name="name" label="Họ tên / Tổ chức" rules={[{ required: true, message: 'Nhập họ tên' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="partnerType" label="Loại" rules={[{ required: true, message: 'Chọn loại' }]}>
            <Select
              options={TYPE_OPTIONS}
              onChange={() => {
                if (editing) return;
                const phone = form.getFieldValue('phone') as string | undefined;
                const type = form.getFieldValue('partnerType') as string;
                if (normalizeVnPhone(phone)) {
                  form.setFieldValue('code', suggestPartnerCode(type, phone));
                }
              }}
            />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
              <Select options={STATUS_OPTIONS} />
            </Form.Item>
          )}
          <Form.Item
            name="phone"
            label="SĐT"
            rules={
              editing
                ? []
                : [
                    { required: true, message: 'Nhập SĐT để tạo mã đối tác' },
                    {
                      validator: async (_, value) => {
                        if (!normalizeVnPhone(value)) {
                          throw new Error('SĐT không hợp lệ (vd: 0984660399)');
                        }
                      },
                    },
                  ]
            }
          >
            <Input
              placeholder="0984660399"
              onChange={(e) => {
                if (editing) return;
                const phone = e.target.value;
                const type = (form.getFieldValue('partnerType') as string) || 'ctv';
                if (normalizeVnPhone(phone)) {
                  form.setFieldValue('code', suggestPartnerCode(type, phone));
                }
              }}
            />
          </Form.Item>
          {!editing && (
            <Form.Item
              name="code"
              label="Mã đối tác"
              extra="Quy ước: CTV/TDV/CSL/DLY + SĐT — vd CTV0984660399 (để trống cũng tự sinh)"
              rules={[
                {
                  validator: async (_, value) => {
                    const v = String(value ?? '').trim();
                    if (!v) return;
                    if (v.length < 2) throw new Error('Mã tối thiểu 2 ký tự');
                  },
                },
              ]}
            >
              <Input placeholder="Tự sinh từ loại + SĐT" style={{ textTransform: 'uppercase' }} />
            </Form.Item>
          )}
          <Form.Item name="email" label="Email">
            <Input />
          </Form.Item>
          <Form.Item name="commissionRatePct" label="% hoa hồng (tham chiếu)">
            <InputNumber min={0} max={100} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            name="password"
            label={editing ? 'Mật khẩu mới (để trống nếu giữ nguyên)' : 'Mật khẩu'}
            rules={
              editing
                ? []
                : [
                    { required: true, message: 'Nhập mật khẩu' },
                    { type: 'string', min: 6, message: 'Tối thiểu 6 ký tự' },
                  ]
            }
          >
            <Input.Password />
          </Form.Item>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

import { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Card,
  Empty,
  Form,
  Input,
  Modal,
  Popconfirm,
  Space,
  Spin,
  Switch,
  Tag,
  Typography,
  message,
} from 'antd';
import { HomeOutlined, PlusOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import {
  createAddress,
  deleteAddress,
  fetchAddresses,
  getApiErrorMessage,
  updateAddress,
} from '@/shared/api/customer-app.api';
import type { CustomerAddress } from '@/shared/api/customer-app.types';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { BackToHomeButton } from '@/shared/components/BackToHomeButton';
import { useAuthStore } from '@/shared/auth/auth.store';

type AddressFormValues = {
  label: string;
  recipientName?: string;
  phone?: string;
  addressLine: string;
  ward?: string;
  district?: string;
  province?: string;
  isDefault?: boolean;
};

function formatAddressLine(address: CustomerAddress) {
  return [address.addressLine, address.ward, address.district, address.province].filter(Boolean).join(', ');
}

export function AddressesPage() {
  const profile = useAuthStore((s) => s.profile);
  const { online } = useApiHealth();
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<CustomerAddress[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<CustomerAddress | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<AddressFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setItems(await fetchAddresses());
    } catch (error) {
      setItems([]);
      setLoadError(getApiErrorMessage(error, 'Không tải được địa chỉ'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useRetryWhenApiOnline(() => load());

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      label: 'Nhà',
      recipientName: profile?.fullName ?? '',
      phone: profile?.phone ?? '',
      addressLine: '',
      ward: '',
      district: '',
      province: '',
      isDefault: items.length === 0,
    });
    setModalOpen(true);
  };

  const openEdit = (address: CustomerAddress) => {
    setEditing(address);
    form.setFieldsValue({
      label: address.label,
      recipientName: address.recipientName ?? '',
      phone: address.phone ?? '',
      addressLine: address.addressLine,
      ward: address.ward ?? '',
      district: address.district ?? '',
      province: address.province ?? '',
      isDefault: address.isDefault,
    });
    setModalOpen(true);
  };

  const onSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const payload = {
        label: values.label.trim(),
        recipientName: values.recipientName?.trim() || undefined,
        phone: values.phone?.trim() || undefined,
        addressLine: values.addressLine.trim(),
        ward: values.ward?.trim() || undefined,
        district: values.district?.trim() || undefined,
        province: values.province?.trim() || undefined,
        isDefault: values.isDefault ?? false,
      };
      if (editing) {
        await updateAddress(editing.id, payload);
        message.success('Đã cập nhật địa chỉ');
      } else {
        await createAddress(payload);
        message.success('Đã thêm địa chỉ');
      }
      setModalOpen(false);
      await load();
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không lưu được địa chỉ'));
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async (id: string) => {
    try {
      await deleteAddress(id);
      message.success('Đã xóa địa chỉ');
      await load();
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không xóa được địa chỉ'));
    }
  };

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <BackToHomeButton />
      <Typography.Title level={5} style={{ margin: 0 }}>
        Địa chỉ giao hàng
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
        Lưu địa chỉ nhận thuốc — dùng khi nhà thuốc giao hàng tận nơi.
      </Typography.Paragraph>

      {loadError && !shouldHidePageErrorForOfflineApi(loadError, online) ? (
        <Card size="small" style={{ borderRadius: 12 }}>
          <Typography.Text type="danger">{loadError}</Typography.Text>
          <div style={{ marginTop: 8 }}>
            <Button size="small" onClick={() => void load()}>
              Thử lại
            </Button>
          </div>
        </Card>
      ) : null}

      <Button type="primary" block size="large" icon={<PlusOutlined />} onClick={openCreate}>
        Thêm địa chỉ
      </Button>

      {items.length === 0 ? (
        <Card size="small" style={{ borderRadius: 12 }}>
          <Empty description="Chưa có địa chỉ" image={Empty.PRESENTED_IMAGE_SIMPLE} />
        </Card>
      ) : (
        items.map((address) => (
          <Card
            key={address.id}
            size="small"
            style={{ borderRadius: 12, borderColor: address.isDefault ? '#5eead4' : undefined }}
          >
            <Space direction="vertical" size={6} style={{ width: '100%' }}>
              <Space wrap>
                <HomeOutlined style={{ color: '#0f766e' }} />
                <Typography.Text strong>{address.label}</Typography.Text>
                {address.isDefault ? <Tag color="success">Mặc định</Tag> : null}
              </Space>
              {address.recipientName ? (
                <Typography.Text>{address.recipientName}</Typography.Text>
              ) : null}
              {address.phone ? (
                <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                  {address.phone}
                </Typography.Text>
              ) : null}
              <Typography.Text style={{ fontSize: 14 }}>{formatAddressLine(address)}</Typography.Text>
              <Space wrap>
                <Button size="small" onClick={() => openEdit(address)}>
                  Sửa
                </Button>
                <Popconfirm title="Xóa địa chỉ này?" okText="Xóa" cancelText="Huỷ" onConfirm={() => void onDelete(address.id)}>
                  <Button size="small" danger>
                    Xóa
                  </Button>
                </Popconfirm>
              </Space>
            </Space>
          </Card>
        ))
      )}

      <Link to="/profile">← Về tài khoản</Link>

      <Modal
        title={editing ? 'Sửa địa chỉ' : 'Thêm địa chỉ'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void onSave()}
        confirmLoading={saving}
        okText="Lưu"
        cancelText="Huỷ"
        destroyOnClose
      >
        <Form form={form} layout="vertical" style={{ marginTop: 8 }}>
          <Form.Item name="label" label="Nhãn" rules={[{ required: true, message: 'Nhập nhãn' }]}>
            <Input placeholder="Nhà, Cơ quan…" />
          </Form.Item>
          <Form.Item name="recipientName" label="Người nhận">
            <Input />
          </Form.Item>
          <Form.Item name="phone" label="Số điện thoại">
            <Input inputMode="tel" />
          </Form.Item>
          <Form.Item name="addressLine" label="Địa chỉ" rules={[{ required: true, message: 'Nhập địa chỉ' }]}>
            <Input.TextArea rows={2} placeholder="Số nhà, tên đường" />
          </Form.Item>
          <Form.Item name="ward" label="Phường / xã">
            <Input />
          </Form.Item>
          <Form.Item name="district" label="Quận / huyện">
            <Input />
          </Form.Item>
          <Form.Item name="province" label="Tỉnh / thành phố">
            <Input />
          </Form.Item>
          <Form.Item name="isDefault" label="Đặt làm mặc định" valuePropName="checked">
            <Switch checkedChildren="Có" unCheckedChildren="Không" />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}

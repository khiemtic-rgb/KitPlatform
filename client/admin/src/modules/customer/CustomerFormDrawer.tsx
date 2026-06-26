import { useEffect, useState } from 'react';
import { Button, Drawer, Form, Input, Select, Space, message } from 'antd';
import { CloseOutlined, SaveOutlined } from '@ant-design/icons';
import {
  createCustomer,
  fetchNextCustomerCode,
  updateCustomer,
} from '@/shared/api/customer-admin.api';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import {
  CUSTOMER_GENDER_OPTIONS,
  CUSTOMER_STATUS_OPTIONS,
} from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';

interface CustomerFormValues {
  customerCode?: string;
  fullName: string;
  phone: string;
  email?: string;
  dateOfBirth?: string;
  gender?: number;
  status?: number;
}

interface CustomerFormDrawerProps {
  open: boolean;
  editing: CustomerDetail | null;
  onClose: () => void;
  onSaved: (customer: CustomerDetail) => void;
  /** POS: chỉ họ tên + SĐT, mã tự sinh */
  variant?: 'full' | 'quick';
}

function normalizeCustomerCodeInput(value: string | undefined): string | undefined {
  if (value == null) return value;
  const trimmed = value.trim();
  return trimmed ? trimmed.toUpperCase() : undefined;
}

export function CustomerFormDrawer({
  open,
  editing,
  onClose,
  onSaved,
  variant = 'full',
}: CustomerFormDrawerProps) {
  const isQuick = variant === 'quick' && !editing;
  const [form] = Form.useForm<CustomerFormValues>();
  const [saving, setSaving] = useState(false);
  const [loadingCode, setLoadingCode] = useState(false);

  useEffect(() => {
    if (!open) return;

    if (editing) {
      form.setFieldsValue({
        customerCode: editing.customerCode.toUpperCase(),
        fullName: editing.fullName,
        phone: editing.phone,
        email: editing.email,
        dateOfBirth: editing.dateOfBirth,
        gender: editing.gender,
        status: editing.status,
      });
      return;
    }

    form.resetFields();
    form.setFieldsValue({ status: 1 });
    if (isQuick) return;

    setLoadingCode(true);
    void fetchNextCustomerCode()
      .then((code) => form.setFieldsValue({ customerCode: code.toUpperCase() }))
      .catch(() => {
        /* gợi ý mã tùy chọn */
      })
      .finally(() => setLoadingCode(false));
  }, [open, editing, form, isQuick]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields(isQuick ? ['fullName', 'phone'] : undefined);
      const customerCode = isQuick ? undefined : normalizeCustomerCodeInput(values.customerCode);
      setSaving(true);
      const saved = editing
        ? await updateCustomer(editing.id, {
            customerCode: customerCode!,
            fullName: values.fullName.trim(),
            phone: values.phone.trim(),
            email: values.email?.trim() || undefined,
            dateOfBirth: values.dateOfBirth || undefined,
            gender: values.gender,
            status: values.status ?? 1,
          })
        : await createCustomer({
            fullName: values.fullName.trim(),
            phone: values.phone.trim(),
            customerCode: customerCode || undefined,
            email: values.email?.trim() || undefined,
            dateOfBirth: values.dateOfBirth || undefined,
            gender: values.gender,
          });
      if (!isQuick) {
        message.success(editing ? 'Đã cập nhật khách hàng' : 'Đã thêm khách hàng');
      }
      onSaved(saved);
      onClose();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(apiErrorMessage(error, 'Không lưu được khách hàng'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={editing ? 'Sửa khách hàng' : isQuick ? 'Thêm khách nhanh' : 'Thêm khách hàng'}
      width={isQuick ? 400 : 420}
      open={open}
      onClose={onClose}
      destroyOnClose
      extra={
        <Space>
          <Button icon={<CloseOutlined />} onClick={onClose}>
            Hủy
          </Button>
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void handleSave()}>
            Lưu
          </Button>
        </Space>
      }
    >
      <Form form={form} layout="vertical" requiredMark="optional">
        {!isQuick ? (
          <Form.Item
            name="customerCode"
            label="Mã KH"
            extra={
              editing
                ? undefined
                : 'Gợi ý mã tiếp theo — xóa trắng để hệ thống tự sinh khi lưu'
            }
            normalize={(value) => normalizeCustomerCodeInput(value) ?? ''}
            rules={editing ? [{ required: true, message: 'Nhập mã khách hàng' }] : undefined}
          >
            <Input
              placeholder="KH009"
              style={{ textTransform: 'uppercase' }}
              disabled={loadingCode && !editing}
            />
          </Form.Item>
        ) : null}
        <Form.Item
          name="fullName"
          label="Họ tên"
          rules={[{ required: true, message: 'Nhập họ tên' }]}
        >
          <Input placeholder="Nguyễn Văn A" autoFocus={isQuick} />
        </Form.Item>
        <Form.Item
          name="phone"
          label="Số điện thoại"
          rules={[{ required: true, message: 'Nhập SĐT' }]}
        >
          <Input placeholder="0909123456" />
        </Form.Item>
        {!isQuick ? (
          <>
            <Form.Item name="email" label="Email">
              <Input placeholder="email@example.com" />
            </Form.Item>
            <Form.Item name="dateOfBirth" label="Ngày sinh">
              <PharmaDatePicker placeholder="dd/mm/yyyy" style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="gender" label="Giới tính">
              <Select allowClear placeholder="Chọn" options={CUSTOMER_GENDER_OPTIONS} />
            </Form.Item>
          </>
        ) : null}
        {editing ? (
          <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
            <Select options={CUSTOMER_STATUS_OPTIONS} />
          </Form.Item>
        ) : null}
      </Form>
    </Drawer>
  );
}

import { useEffect, useState } from 'react';
import { Button, Drawer, Form, Input, Select, Space, message } from 'antd';
import { CloseOutlined, SaveOutlined } from '@ant-design/icons';
import {
  createUser,
  fetchEmployees,
  fetchRoles,
  updateUser,
} from '@/shared/api/identity-admin.api';
import type { UserDetail } from '@/shared/api/identity-admin.types';
import { USER_STATUS_OPTIONS } from '@/shared/api/identity-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';

interface UserFormValues {
  username?: string;
  email: string;
  password?: string;
  newPassword?: string;
  status: number;
  roleIds: string[];
  employeeId?: string;
}

interface UserFormDrawerProps {
  open: boolean;
  editing: UserDetail | null;
  onClose: () => void;
  onSaved: () => void;
}

export function UserFormDrawer({ open, editing, onClose, onSaved }: UserFormDrawerProps) {
  const [form] = Form.useForm<UserFormValues>();
  const [saving, setSaving] = useState(false);
  const [roleOptions, setRoleOptions] = useState<{ value: string; label: string }[]>([]);
  const [employeeOptions, setEmployeeOptions] = useState<{ value: string; label: string }[]>([]);

  useEffect(() => {
    if (!open) return;
    void Promise.all([fetchRoles(), fetchEmployees()])
      .then(([roles, employees]) => {
        setRoleOptions(roles.map((r) => ({ value: r.id, label: `${r.roleCode} — ${r.roleName}` })));
        setEmployeeOptions(
          employees.map((e) => ({
            value: e.id,
            label: `${e.employeeCode} — ${e.fullName}${e.hasUserAccount && e.id !== editing?.employeeId ? ' (đã có TK)' : ''}`,
            disabled: e.hasUserAccount && e.id !== editing?.employeeId,
          })),
        );
      })
      .catch(() => {
        setRoleOptions([]);
        setEmployeeOptions([]);
      });
  }, [open, editing?.employeeId]);

  useEffect(() => {
    if (!open) return;
    if (editing) {
      form.setFieldsValue({
        username: editing.username,
        email: editing.email,
        status: editing.status,
        roleIds: editing.roleIds,
        employeeId: editing.employeeId,
      });
      return;
    }
    form.resetFields();
    form.setFieldsValue({ status: 1, roleIds: [] });
  }, [open, editing, form]);

  const handleSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      if (editing) {
        await updateUser(editing.id, {
          email: values.email.trim(),
          status: values.status,
          roleIds: values.roleIds,
          employeeId: values.employeeId,
          newPassword: values.newPassword?.trim() || undefined,
        });
        message.success('Đã cập nhật tài khoản');
      } else {
        await createUser({
          username: values.username!.trim(),
          email: values.email.trim(),
          password: values.password!,
          status: values.status,
          roleIds: values.roleIds,
          employeeId: values.employeeId,
        });
        message.success('Đã tạo tài khoản');
      }
      onSaved();
      onClose();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được tài khoản'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={editing ? 'Sửa tài khoản' : 'Thêm tài khoản'}
      open={open}
      onClose={onClose}
      width={440}
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
      <Form form={form} layout="vertical">
        {!editing ? (
          <Form.Item
            name="username"
            label="Tên đăng nhập"
            rules={[{ required: true, message: 'Nhập tên đăng nhập' }]}
          >
            <Input autoComplete="off" />
          </Form.Item>
        ) : (
          <Form.Item label="Tên đăng nhập">
            <Input value={editing.username} disabled />
          </Form.Item>
        )}
        <Form.Item
          name="email"
          label="Email"
          rules={[
            { required: true, message: 'Nhập email' },
            { type: 'email', message: 'Email không hợp lệ' },
          ]}
        >
          <Input />
        </Form.Item>
        {!editing ? (
          <Form.Item
            name="password"
            label="Mật khẩu"
            rules={[
              { required: true, message: 'Nhập mật khẩu' },
              { min: 8, message: 'Tối thiểu 8 ký tự' },
            ]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
        ) : (
          <Form.Item
            name="newPassword"
            label="Mật khẩu mới"
            extra="Để trống nếu không đổi"
            rules={[{ min: 8, message: 'Tối thiểu 8 ký tự' }]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
        )}
        <Form.Item
          name="roleIds"
          label="Vai trò"
          rules={[{ required: true, message: 'Chọn ít nhất một vai trò' }]}
        >
          <Select mode="multiple" options={roleOptions} placeholder="Chọn vai trò" />
        </Form.Item>
        <Form.Item name="employeeId" label="Nhân viên liên kết">
          <Select allowClear options={employeeOptions} placeholder="Tuỳ chọn" />
        </Form.Item>
        <Form.Item name="status" label="Trạng thái">
          <Select options={USER_STATUS_OPTIONS} />
        </Form.Item>
      </Form>
    </Drawer>
  );
}

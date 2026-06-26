import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Drawer, Form, Input, Select, Space, Switch, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  createBranch,
  fetchBranches,
  updateBranch,
} from '@/shared/api/identity-admin.api';
import type { BranchListItem } from '@/shared/api/identity-admin.types';
import { BRANCH_STATUS_LABELS, BRANCH_STATUS_OPTIONS } from '@/shared/api/identity-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';

interface BranchFormValues {
  branchCode: string;
  branchName: string;
  address?: string;
  phone?: string;
  isHeadOffice?: boolean;
  status?: number;
}

export function BranchListPage() {
  const canWrite = useHasPermission('system.write');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<BranchListItem[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<BranchListItem | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<BranchFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchBranches());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi nhánh'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ status: 1, isHeadOffice: false });
    setDrawerOpen(true);
  };

  const openEdit = (row: BranchListItem) => {
    setEditing(row);
    form.setFieldsValue({
      branchCode: row.branchCode,
      branchName: row.branchName,
      address: row.address,
      phone: row.phone,
      isHeadOffice: row.isHeadOffice,
      status: row.status,
    });
    setDrawerOpen(true);
  };

  const handleSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const code = values.branchCode.trim().toUpperCase();
      if (editing) {
        await updateBranch(editing.id, {
          branchCode: code,
          branchName: values.branchName.trim(),
          address: values.address?.trim(),
          phone: values.phone?.trim(),
          isHeadOffice: values.isHeadOffice ?? false,
          status: values.status ?? 1,
        });
        message.success('Đã cập nhật chi nhánh');
      } else {
        await createBranch({
          branchCode: code,
          branchName: values.branchName.trim(),
          address: values.address?.trim(),
          phone: values.phone?.trim(),
          isHeadOffice: values.isHeadOffice ?? false,
          status: values.status ?? 1,
        });
        message.success('Đã thêm chi nhánh');
      }
      setDrawerOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được chi nhánh'));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<BranchListItem> = [
    { title: 'Mã CN', dataIndex: 'branchCode', width: 100 },
    { title: 'Tên chi nhánh', dataIndex: 'branchName' },
    { title: 'Điện thoại', dataIndex: 'phone', width: 130, render: (v?: string) => v ?? '—' },
    {
      title: 'Trụ sở',
      dataIndex: 'isHeadOffice',
      width: 90,
      render: (v: boolean) => (v ? <Tag color="blue">Chính</Tag> : '—'),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{BRANCH_STATUS_LABELS[v] ?? v}</Tag>
      ),
    },
    {
      title: '',
      width: 80,
      render: (_, row) =>
        canWrite ? (
          <Button type="link" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            Sửa
          </Button>
        ) : null,
    },
  ];

  return (
    <Card
      title="Chi nhánh"
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
          {canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Thêm chi nhánh
            </Button>
          ) : null}
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />

      <Drawer
        title={editing ? 'Sửa chi nhánh' : 'Thêm chi nhánh'}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        width={420}
        extra={
          <Space>
            <Button onClick={() => setDrawerOpen(false)}>Hủy</Button>
            <Button type="primary" loading={saving} onClick={() => void handleSave()}>
              Lưu
            </Button>
          </Space>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="branchCode"
            label="Mã chi nhánh"
            rules={[{ required: true, message: 'Nhập mã chi nhánh' }]}
          >
            <Input placeholder="HN01" style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Form.Item
            name="branchName"
            label="Tên chi nhánh"
            rules={[{ required: true, message: 'Nhập tên chi nhánh' }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="phone" label="Điện thoại">
            <Input />
          </Form.Item>
          <Form.Item name="address" label="Địa chỉ">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="isHeadOffice" label="Trụ sở chính" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="status" label="Trạng thái">
            <Select options={BRANCH_STATUS_OPTIONS} />
          </Form.Item>
        </Form>
      </Drawer>
    </Card>
  );
}

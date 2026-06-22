import { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Card,
  Drawer,
  Form,
  Input,
  message,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { PlusOutlined, ReloadOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import {
  createWarehouse,
  deleteWarehouse,
  fetchBranchLookups,
  fetchWarehouses,
  updateWarehouse,
} from '@/shared/api/inventory.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { Warehouse } from '@/shared/api/inventory.types';
import { STATUS_LABELS, WAREHOUSE_TYPE_LABELS } from '@/shared/api/inventory.types';

export function WarehouseListPage() {
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<Warehouse[]>([]);
  const [branches, setBranches] = useState<{ id: string; branchName: string }[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<Warehouse | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [warehouses, branchList] = await Promise.all([fetchWarehouses(), fetchBranchLookups()]);
      setItems(warehouses);
      setBranches(branchList);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được danh sách kho'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ warehouseType: 1, isDefault: false, status: 1 });
    setDrawerOpen(true);
  };

  const openEdit = (row: Warehouse) => {
    setEditing(row);
    form.setFieldsValue({
      branchId: row.branchId,
      warehouseCode: row.warehouseCode,
      warehouseName: row.warehouseName,
      warehouseType: row.warehouseType,
      isDefault: row.isDefault,
      address: row.address,
      status: row.status,
    });
    setDrawerOpen(true);
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      if (editing) {
        await updateWarehouse(editing.id, {
          warehouseName: values.warehouseName,
          warehouseType: values.warehouseType,
          isDefault: values.isDefault ?? false,
          address: values.address,
          status: values.status ?? 1,
        });
        message.success('Đã cập nhật kho');
      } else {
        await createWarehouse({
          branchId: values.branchId,
          warehouseCode: values.warehouseCode,
          warehouseName: values.warehouseName,
          warehouseType: values.warehouseType,
          isDefault: values.isDefault ?? false,
          address: values.address,
        });
        message.success('Đã tạo kho');
      }
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không lưu được kho'));
      }
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<Warehouse> = [
    { title: 'Mã kho', dataIndex: 'warehouseCode', width: 120 },
    { title: 'Tên kho', dataIndex: 'warehouseName' },
    { title: 'Chi nhánh', dataIndex: 'branchName', width: 160 },
    {
      title: 'Loại',
      dataIndex: 'warehouseType',
      width: 130,
      render: (v: number) => WAREHOUSE_TYPE_LABELS[v] ?? v,
    },
    {
      title: 'Mặc định',
      dataIndex: 'isDefault',
      width: 90,
      render: (v: boolean) => (v ? <Tag color="blue">Có</Tag> : '—'),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 100,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{STATUS_LABELS[v] ?? v}</Tag>
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 120,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            Sửa
          </Button>
          <Popconfirm
            title="Xóa kho này?"
            onConfirm={async () => {
              try {
                await deleteWarehouse(row.id);
                message.success('Đã xóa');
                load();
              } catch (error) {
                message.error(apiErrorMessage(error, 'Không xóa được kho'));
              }
            }}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              Xóa
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Card
      title="Danh sách kho"
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
            Tải lại
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Thêm kho
          </Button>
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />

      <Drawer
        title={editing ? 'Sửa kho' : 'Thêm kho'}
        width={480}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Space>
            <Button onClick={() => setDrawerOpen(false)}>Hủy</Button>
            <Button type="primary" loading={saving} onClick={handleSave}>
              Lưu
            </Button>
          </Space>
        }
      >
        <Form form={form} layout="vertical">
          {!editing && (
            <>
              <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true }]}>
                <Select
                  options={branches.map((b) => ({ value: b.id, label: b.branchName }))}
                  placeholder="Chọn chi nhánh"
                />
              </Form.Item>
              <Form.Item name="warehouseCode" label="Mã kho" rules={[{ required: true }]}>
                <Input placeholder="VD: WH_CH01" />
              </Form.Item>
            </>
          )}
          <Form.Item name="warehouseName" label="Tên kho" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="warehouseType" label="Loại kho" rules={[{ required: true }]}>
            <Select
              options={Object.entries(WAREHOUSE_TYPE_LABELS).map(([value, label]) => ({
                value: Number(value),
                label,
              }))}
            />
          </Form.Item>
          <Form.Item name="isDefault" label="Kho mặc định">
            <Select
              options={[
                { value: true, label: 'Có' },
                { value: false, label: 'Không' },
              ]}
            />
          </Form.Item>
          <Form.Item name="address" label="Địa chỉ">
            <Input.TextArea rows={2} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label="Trạng thái">
              <Select
                options={Object.entries(STATUS_LABELS).map(([value, label]) => ({
                  value: Number(value),
                  label,
                }))}
              />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </Card>
  );
}

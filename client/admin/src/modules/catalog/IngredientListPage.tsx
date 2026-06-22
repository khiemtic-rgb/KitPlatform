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
import { PlusOutlined, ReloadOutlined, EditOutlined, StopOutlined } from '@ant-design/icons';
import {
  createActiveIngredient,
  deleteActiveIngredient,
  fetchActiveIngredients,
  updateActiveIngredient,
} from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { ActiveIngredient } from '@/shared/api/catalog.types';
import { STATUS_LABELS } from '@/shared/api/catalog.types';

export function IngredientListPage() {
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<ActiveIngredient[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<ActiveIngredient | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchActiveIngredients());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được hoạt chất'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    setDrawerOpen(true);
  };

  const openEdit = (row: ActiveIngredient) => {
    setEditing(row);
    setDrawerOpen(true);
  };

  useEffect(() => {
    if (!drawerOpen) return;
    if (editing) {
      form.setFieldsValue({
        ingredientCode: editing.ingredientCode,
        ingredientName: editing.ingredientName,
        description: editing.description,
        status: editing.status,
      });
    } else {
      form.resetFields();
    }
  }, [drawerOpen, editing, form]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      if (editing) {
        await updateActiveIngredient(editing.id, {
          ingredientName: values.ingredientName,
          description: values.description,
          status: values.status ?? 1,
        });
        message.success('Đã cập nhật hoạt chất');
      } else {
        await createActiveIngredient({
          ingredientCode: values.ingredientCode,
          ingredientName: values.ingredientName,
          description: values.description,
        });
        message.success('Đã tạo hoạt chất');
      }
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không lưu được hoạt chất'));
      }
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<ActiveIngredient> = [
    { title: 'Mã', dataIndex: 'ingredientCode', width: 140 },
    { title: 'Tên hoạt chất', dataIndex: 'ingredientName' },
    {
      title: 'Mô tả',
      dataIndex: 'description',
      ellipsis: true,
      render: (v) => v ?? '—',
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 100,
      render: (status: number) => (
        <Tag color={status === 1 ? 'green' : 'default'}>{STATUS_LABELS[status] ?? status}</Tag>
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 140,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            Sửa
          </Button>
          {row.status === 1 && (
            <Popconfirm title="Ngừng hoạt chất này?" onConfirm={() => void handleDelete(row.id)}>
              <Button type="link" size="small" danger icon={<StopOutlined />}>
                Ngừng
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  const handleDelete = async (id: string) => {
    try {
      await deleteActiveIngredient(id);
      message.success('Đã ngừng hoạt chất');
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ngừng được hoạt chất'));
    }
  };

  return (
    <Card
      title="Hoạt chất"
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Thêm hoạt chất
          </Button>
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={{ pageSize: 20 }} />

      <Drawer
        title={editing ? 'Sửa hoạt chất' : 'Thêm hoạt chất'}
        width={420}
        open={drawerOpen}
        destroyOnClose
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="ingredientCode"
            label="Mã hoạt chất"
            rules={[{ required: true, message: 'Nhập mã' }]}
          >
            <Input disabled={!!editing} placeholder="VD: PARACETAMOL" />
          </Form.Item>
          <Form.Item
            name="ingredientName"
            label="Tên hoạt chất"
            rules={[{ required: true, message: 'Nhập tên' }]}
          >
            <Input placeholder="VD: Paracetamol" />
          </Form.Item>
          <Form.Item name="description" label="Mô tả">
            <Input.TextArea rows={3} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label="Trạng thái" initialValue={1}>
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

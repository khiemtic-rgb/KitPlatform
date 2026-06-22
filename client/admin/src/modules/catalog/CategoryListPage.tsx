import { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Card,
  Drawer,
  Form,
  Input,
  InputNumber,
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
  createCategory,
  deleteCategory,
  fetchCategories,
  updateCategory,
} from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { Category } from '@/shared/api/catalog.types';
import { STATUS_LABELS } from '@/shared/api/catalog.types';

export function CategoryListPage() {
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<Category[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<Category | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchCategories());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được danh mục'));
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
    form.setFieldsValue({ sortOrder: 0 });
    setDrawerOpen(true);
  };

  const openEdit = (row: Category) => {
    setEditing(row);
    form.setFieldsValue({
      categoryCode: row.categoryCode,
      categoryName: row.categoryName,
      description: row.description,
      parentId: row.parentId,
      sortOrder: row.sortOrder,
      status: row.status,
    });
    setDrawerOpen(true);
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      if (editing) {
        await updateCategory(editing.id, {
          categoryName: values.categoryName,
          description: values.description,
          parentId: values.parentId,
          sortOrder: values.sortOrder ?? 0,
          status: values.status ?? 1,
        });
        message.success('Đã cập nhật danh mục');
      } else {
        await createCategory({
          categoryCode: values.categoryCode,
          categoryName: values.categoryName,
          description: values.description,
          parentId: values.parentId,
          sortOrder: values.sortOrder ?? 0,
        });
        message.success('Đã tạo danh mục');
      }
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không lưu được danh mục'));
      }
    } finally {
      setSaving(false);
    }
  };

  const parentOptions = items
    .filter((c) => c.status === 1 && c.id !== editing?.id)
    .map((c) => ({ value: c.id, label: c.categoryName }));

  const columns: ColumnsType<Category> = [
    { title: 'Mã', dataIndex: 'categoryCode', width: 120 },
    { title: 'Tên danh mục', dataIndex: 'categoryName' },
    { title: 'Danh mục cha', dataIndex: 'parentName', render: (v) => v ?? '—' },
    { title: 'Thứ tự', dataIndex: 'sortOrder', width: 80, align: 'center' },
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
            title="Xóa danh mục này?"
            onConfirm={async () => {
              try {
                await deleteCategory(row.id);
                message.success('Đã xóa');
                load();
              } catch (error) {
                message.error(apiErrorMessage(error, 'Không xóa được'));
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
      title="Danh mục sản phẩm"
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load} />
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Thêm danh mục
          </Button>
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />

      <Drawer
        title={editing ? `Sửa: ${editing.categoryCode}` : 'Thêm danh mục'}
        width={480}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" loading={saving} onClick={handleSave}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          {!editing && (
            <Form.Item name="categoryCode" label="Mã danh mục" rules={[{ required: true, message: 'Nhập mã' }]}>
              <Input placeholder="VD: GIAM_DAU" />
            </Form.Item>
          )}
          <Form.Item name="categoryName" label="Tên danh mục" rules={[{ required: true, message: 'Nhập tên' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="parentId" label="Danh mục cha">
            <Select allowClear placeholder="Không có (gốc)" options={parentOptions} />
          </Form.Item>
          <Form.Item name="sortOrder" label="Thứ tự hiển thị">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="description" label="Mô tả">
            <Input.TextArea rows={2} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
              <Select
                options={Object.entries(STATUS_LABELS).map(([k, v]) => ({
                  value: Number(k),
                  label: v,
                }))}
              />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </Card>
  );
}

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
import { createBrand, deleteBrand, fetchBrands, updateBrand } from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { Brand } from '@/shared/api/catalog.types';
import { STATUS_LABELS } from '@/shared/api/catalog.types';

export function BrandListPage() {
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<Brand[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<Brand | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchBrands());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được thương hiệu'));
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
    setDrawerOpen(true);
  };

  const openEdit = (row: Brand) => {
    setEditing(row);
    form.setFieldsValue({
      brandCode: row.brandCode,
      brandName: row.brandName,
      countryCode: row.countryCode,
      status: row.status,
    });
    setDrawerOpen(true);
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      if (editing) {
        await updateBrand(editing.id, {
          brandName: values.brandName,
          countryCode: values.countryCode,
          status: values.status ?? 1,
        });
        message.success('Đã cập nhật thương hiệu');
      } else {
        await createBrand({
          brandCode: values.brandCode,
          brandName: values.brandName,
          countryCode: values.countryCode,
        });
        message.success('Đã tạo thương hiệu');
      }
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không lưu được thương hiệu'));
      }
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<Brand> = [
    { title: 'Mã', dataIndex: 'brandCode', width: 120 },
    { title: 'Tên thương hiệu', dataIndex: 'brandName' },
    { title: 'Quốc gia', dataIndex: 'countryCode', width: 90, render: (v) => v ?? '—' },
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
            title="Xóa thương hiệu này?"
            onConfirm={async () => {
              try {
                await deleteBrand(row.id);
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
      title="Thương hiệu"
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load} />
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Thêm thương hiệu
          </Button>
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />

      <Drawer
        title={editing ? `Sửa: ${editing.brandCode}` : 'Thêm thương hiệu'}
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
            <Form.Item name="brandCode" label="Mã thương hiệu" rules={[{ required: true, message: 'Nhập mã' }]}>
              <Input placeholder="VD: DHG" />
            </Form.Item>
          )}
          <Form.Item name="brandName" label="Tên thương hiệu" rules={[{ required: true, message: 'Nhập tên' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="countryCode" label="Mã quốc gia (ISO)">
            <Input maxLength={2} placeholder="VD: VN" style={{ width: 120 }} />
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

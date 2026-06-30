import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { useCatalogEnums } from '@/shared/i18n/use-catalog-enums';

export function CategoryListPage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'categories' });
  const { t: ts } = useTranslation('catalog', { keyPrefix: 'shared' });
  const { productStatusLabel, productStatusOptions } = useCatalogEnums();
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
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
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
      minStockQty: row.minStockQty,
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
          minStockQty: values.minStockQty,
        });
        message.success(t('messages.updateSuccess'));
      } else {
        await createCategory({
          categoryCode: values.categoryCode,
          categoryName: values.categoryName,
          description: values.description,
          parentId: values.parentId,
          sortOrder: values.sortOrder ?? 0,
          minStockQty: values.minStockQty,
        });
        message.success(t('messages.createSuccess'));
      }
      setDrawerOpen(false);
      void load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.saveFailed')));
      }
    } finally {
      setSaving(false);
    }
  };

  const parentOptions = items
    .filter((c) => c.status === 1 && c.id !== editing?.id)
    .map((c) => ({ value: c.id, label: c.categoryName }));

  const columns: ColumnsType<Category> = [
    { title: t('columns.code'), dataIndex: 'categoryCode', width: 120 },
    { title: t('columns.name'), dataIndex: 'categoryName' },
    { title: t('columns.parent'), dataIndex: 'parentName', render: (v) => v ?? '—' },
    { title: t('columns.sortOrder'), dataIndex: 'sortOrder', width: 80, align: 'center' },
    {
      title: t('columns.minStockQty'),
      dataIndex: 'minStockQty',
      width: 80,
      align: 'right',
      render: (v?: number) => (v != null ? v.toLocaleString('vi-VN') : '—'),
    },
    {
      title: ts('status'),
      dataIndex: 'status',
      width: 100,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{productStatusLabel(v)}</Tag>
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 120,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            {ts('edit')}
          </Button>
          <Popconfirm
            title={t('deleteConfirm')}
            onConfirm={async () => {
              try {
                await deleteCategory(row.id);
                message.success(ts('deleted'));
                void load();
              } catch (error) {
                message.error(apiErrorMessage(error, ts('deleteFailed')));
              }
            }}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              {ts('delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={t('title')}
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} />
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('add')}
          </Button>
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />

      <Drawer
        title={editing ? t('drawer.editTitle', { code: editing.categoryCode }) : t('drawer.createTitle')}
        width={480}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            {ts('save')}
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          {!editing && (
            <Form.Item
              name="categoryCode"
              label={t('fields.categoryCode')}
              rules={[{ required: true, message: ts('enterCode') }]}
            >
              <Input placeholder={t('placeholders.categoryCode')} />
            </Form.Item>
          )}
          <Form.Item
            name="categoryName"
            label={t('fields.categoryName')}
            rules={[{ required: true, message: ts('enterName') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="parentId" label={t('fields.parentId')}>
            <Select allowClear placeholder={t('placeholders.parentId')} options={parentOptions} />
          </Form.Item>
          <Form.Item name="sortOrder" label={t('fields.sortOrder')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="minStockQty" label={t('fields.minStockQty')}>
            <InputNumber
              min={0}
              precision={3}
              style={{ width: '100%' }}
              placeholder={t('placeholders.minStockQty')}
            />
          </Form.Item>
          <Form.Item name="description" label={ts('description')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label={ts('status')} rules={[{ required: true }]}>
              <Select options={productStatusOptions} />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </Card>
  );
}

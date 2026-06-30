import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { useCatalogEnums } from '@/shared/i18n/use-catalog-enums';

export function IngredientListPage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'ingredients' });
  const { t: ts } = useTranslation('catalog', { keyPrefix: 'shared' });
  const { productStatusLabel, productStatusOptions } = useCatalogEnums();
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
        message.success(t('messages.updateSuccess'));
      } else {
        await createActiveIngredient({
          ingredientCode: values.ingredientCode,
          ingredientName: values.ingredientName,
          description: values.description,
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

  const handleDelete = async (id: string) => {
    try {
      await deleteActiveIngredient(id);
      message.success(t('messages.deactivateSuccess'));
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.deactivateFailed')));
    }
  };

  const columns: ColumnsType<ActiveIngredient> = [
    { title: t('columns.code'), dataIndex: 'ingredientCode', width: 140 },
    { title: t('columns.name'), dataIndex: 'ingredientName' },
    {
      title: ts('description'),
      dataIndex: 'description',
      ellipsis: true,
      render: (v) => v ?? '—',
    },
    {
      title: ts('status'),
      dataIndex: 'status',
      width: 100,
      render: (status: number) => (
        <Tag color={status === 1 ? 'green' : 'default'}>{productStatusLabel(status)}</Tag>
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 140,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            {ts('edit')}
          </Button>
          {row.status === 1 && (
            <Popconfirm title={t('deactivateConfirm')} onConfirm={() => void handleDelete(row.id)}>
              <Button type="link" size="small" danger icon={<StopOutlined />}>
                {t('deactivate')}
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={t('title')}
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            {ts('reload')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('add')}
          </Button>
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={{ pageSize: 20 }} />

      <Drawer
        title={editing ? t('drawer.editTitle') : t('drawer.createTitle')}
        width={420}
        open={drawerOpen}
        destroyOnClose
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            {ts('save')}
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="ingredientCode"
            label={t('fields.ingredientCode')}
            rules={[{ required: true, message: ts('enterCode') }]}
          >
            <Input disabled={!!editing} placeholder={t('placeholders.ingredientCode')} />
          </Form.Item>
          <Form.Item
            name="ingredientName"
            label={t('fields.ingredientName')}
            rules={[{ required: true, message: ts('enterName') }]}
          >
            <Input placeholder={t('placeholders.ingredientName')} />
          </Form.Item>
          <Form.Item name="description" label={ts('description')}>
            <Input.TextArea rows={3} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label={ts('status')} initialValue={1}>
              <Select options={productStatusOptions} />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </Card>
  );
}

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
import { PlusOutlined, ReloadOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { createBrand, deleteBrand, fetchBrands, updateBrand } from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { Brand } from '@/shared/api/catalog.types';
import { useCatalogEnums } from '@/shared/i18n/use-catalog-enums';

export function BrandListPage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'brands' });
  const { t: ts } = useTranslation('catalog', { keyPrefix: 'shared' });
  const { productStatusLabel, productStatusOptions } = useCatalogEnums();
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
        message.success(t('messages.updateSuccess'));
      } else {
        await createBrand({
          brandCode: values.brandCode,
          brandName: values.brandName,
          countryCode: values.countryCode,
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

  const columns: ColumnsType<Brand> = [
    { title: t('columns.code'), dataIndex: 'brandCode', width: 120 },
    { title: t('columns.name'), dataIndex: 'brandName' },
    { title: t('columns.country'), dataIndex: 'countryCode', width: 90, render: (v) => v ?? '—' },
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
                await deleteBrand(row.id);
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
        title={editing ? t('drawer.editTitle', { code: editing.brandCode }) : t('drawer.createTitle')}
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
              name="brandCode"
              label={t('fields.brandCode')}
              rules={[{ required: true, message: ts('enterCode') }]}
            >
              <Input placeholder={t('placeholders.brandCode')} />
            </Form.Item>
          )}
          <Form.Item
            name="brandName"
            label={t('fields.brandName')}
            rules={[{ required: true, message: ts('enterName') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="countryCode" label={t('fields.countryCode')}>
            <Input maxLength={2} placeholder={t('placeholders.countryCode')} style={{ width: 120 }} />
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

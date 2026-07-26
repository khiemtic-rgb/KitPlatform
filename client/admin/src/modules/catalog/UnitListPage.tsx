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
  createMeasureUnit,
  deleteMeasureUnit,
  fetchMeasureUnits,
  updateMeasureUnit,
} from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { MeasureUnit } from '@/shared/api/catalog.types';
import { useCatalogEnums } from '@/shared/i18n/use-catalog-enums';

export function UnitListPage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'units' });
  const { t: ts } = useTranslation('catalog', { keyPrefix: 'shared' });
  const { productStatusLabel, productStatusOptions } = useCatalogEnums();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<MeasureUnit[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<MeasureUnit | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchMeasureUnits());
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

  const openEdit = (row: MeasureUnit) => {
    setEditing(row);
    form.setFieldsValue({
      unitName: row.unitName,
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
        await updateMeasureUnit(editing.id, {
          unitName: values.unitName,
          sortOrder: values.sortOrder ?? 0,
          status: values.status ?? 1,
        });
        message.success(t('messages.updateSuccess'));
      } else {
        await createMeasureUnit({
          unitName: values.unitName,
          sortOrder: values.sortOrder ?? 0,
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

  const columns: ColumnsType<MeasureUnit> = [
    { title: t('columns.name'), dataIndex: 'unitName' },
    { title: t('columns.sortOrder'), dataIndex: 'sortOrder', width: 140 },
    {
      title: ts('status'),
      dataIndex: 'status',
      width: 120,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{productStatusLabel(v)}</Tag>
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
          <Popconfirm
            title={t('deleteConfirm')}
            onConfirm={async () => {
              try {
                await deleteMeasureUnit(row.id);
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
        title={editing ? t('drawer.editTitle', { name: editing.unitName }) : t('drawer.createTitle')}
        width={420}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            {ts('save')}
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="unitName"
            label={t('fields.unitName')}
            rules={[{ required: true, message: ts('enterName') }, { max: 50 }]}
          >
            <Input placeholder={t('placeholders.unitName')} />
          </Form.Item>
          <Form.Item name="sortOrder" label={t('fields.sortOrder')}>
            <InputNumber min={0} style={{ width: 140 }} />
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

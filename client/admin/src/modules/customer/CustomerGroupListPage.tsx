import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Drawer,
  Form,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  createCustomerGroup,
  deleteCustomerGroup,
  fetchCustomerGroups,
  updateCustomerGroup,
  type CustomerGroup,
} from '@/shared/api/customer-groups.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCustomerEnums } from '@/shared/i18n/use-customer-enums';

export function CustomerGroupListPage() {
  const { t } = useTranslation('customer', { keyPrefix: 'groups' });
  const { t: tc } = useTranslation('common');
  const { customerStatusLabel, customerStatusOptions } = useCustomerEnums();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<CustomerGroup[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<CustomerGroup | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchCustomerGroups());
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
    form.setFieldsValue({ discountPercent: 0, status: 1 });
    setDrawerOpen(true);
  };

  const openEdit = (row: CustomerGroup) => {
    setEditing(row);
    form.setFieldsValue({
      groupCode: row.groupCode,
      groupName: row.groupName,
      discountPercent: row.discountPercent,
      status: row.status,
    });
    setDrawerOpen(true);
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      if (editing) {
        await updateCustomerGroup(editing.id, {
          groupName: String(values.groupName).trim(),
          discountPercent: Number(values.discountPercent ?? 0),
          status: Number(values.status ?? 1),
        });
        message.success(t('messages.updateSuccess'));
      } else {
        await createCustomerGroup({
          groupCode: String(values.groupCode).trim(),
          groupName: String(values.groupName).trim(),
          discountPercent: Number(values.discountPercent ?? 0),
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

  const handleDelete = async (row: CustomerGroup) => {
    try {
      await deleteCustomerGroup(row.id);
      message.success(t('messages.deleteSuccess'));
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.deleteFailed')));
    }
  };

  const columns: ColumnsType<CustomerGroup> = [
    { title: t('columns.code'), dataIndex: 'groupCode', width: 120 },
    { title: t('columns.name'), dataIndex: 'groupName' },
    {
      title: t('columns.discount'),
      dataIndex: 'discountPercent',
      width: 120,
      render: (v: number) => `${Number(v).toLocaleString()}%`,
    },
    {
      title: t('columns.status'),
      dataIndex: 'status',
      width: 110,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{customerStatusLabel(v)}</Tag>
      ),
    },
    {
      title: tc('fields.actions'),
      key: 'actions',
      width: 120,
      render: (_, row) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={() => openEdit(row)} />
          <Popconfirm title={t('messages.deleteConfirm')} onConfirm={() => void handleDelete(row)}>
            <Button type="text" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('create')}
        </Button>
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          {tc('actions.reload')}
        </Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 20, showSizeChanger: true }}
      />
      <Drawer
        title={editing ? t('editTitle') : t('createTitle')}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        width={420}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            {tc('actions.save')}
          </Button>
        }
      >
        <Form form={form} layout="vertical" requiredMark="optional">
          <Form.Item
            name="groupCode"
            label={t('fields.code')}
            rules={[{ required: true, message: t('validation.codeRequired') }]}
          >
            <Input disabled={!!editing} style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Form.Item
            name="groupName"
            label={t('fields.name')}
            rules={[{ required: true, message: t('validation.nameRequired') }]}
          >
            <Input placeholder={t('placeholders.name')} />
          </Form.Item>
          <Form.Item
            name="discountPercent"
            label={t('fields.discount')}
            extra={t('hints.discount')}
            rules={[{ required: true, message: t('validation.discountRequired') }]}
          >
            <InputNumber min={0} max={100} step={0.5} style={{ width: '100%' }} addonAfter="%" />
          </Form.Item>
          {editing ? (
            <Form.Item name="status" label={t('fields.status')} rules={[{ required: true }]}>
              <Select options={customerStatusOptions} />
            </Form.Item>
          ) : null}
        </Form>
      </Drawer>
    </>
  );
}

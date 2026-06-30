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
import {
  createWarehouse,
  deleteWarehouse,
  fetchBranchLookups,
  fetchWarehouses,
  updateWarehouse,
} from '@/shared/api/inventory.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { Warehouse } from '@/shared/api/inventory.types';
import { useInventoryEnums } from '@/shared/i18n/use-inventory-enums';

export function WarehouseListPage() {
  const { t } = useTranslation('inventory', { keyPrefix: 'warehouseList' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const { warehouseTypeLabel, warehouseTypeOptions, statusLabel, statusOptions } = useInventoryEnums();
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
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

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
        message.success(t('messages.updateSuccess'));
      } else {
        await createWarehouse({
          branchId: values.branchId,
          warehouseCode: values.warehouseCode,
          warehouseName: values.warehouseName,
          warehouseType: values.warehouseType,
          isDefault: values.isDefault ?? false,
          address: values.address,
        });
        message.success(t('messages.createSuccess'));
      }
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.saveFailed')));
      }
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<Warehouse> = [
    { title: t('columns.warehouseCode'), dataIndex: 'warehouseCode', width: 120 },
    { title: t('columns.warehouseName'), dataIndex: 'warehouseName' },
    { title: t('columns.branch'), dataIndex: 'branchName', width: 160 },
    {
      title: t('columns.type'),
      dataIndex: 'warehouseType',
      width: 130,
      render: (v: number) => warehouseTypeLabel(v),
    },
    {
      title: t('columns.isDefault'),
      dataIndex: 'isDefault',
      width: 90,
      render: (v: boolean) => (v ? <Tag color="blue">{tc('actions.yes')}</Tag> : '—'),
    },
    {
      title: t('columns.status'),
      dataIndex: 'status',
      width: 100,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{statusLabel(v)}</Tag>
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 120,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            {tc('actions.edit')}
          </Button>
          <Popconfirm
            title={t('deleteConfirm')}
            onConfirm={async () => {
              try {
                await deleteWarehouse(row.id);
                message.success(tc('messages.deleted'));
                load();
              } catch (error) {
                message.error(apiErrorMessage(error, t('messages.deleteFailed')));
              }
            }}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              {tc('actions.delete')}
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
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
            {tc('actions.reload')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('addWarehouse')}
          </Button>
        </Space>
      }
    >
      <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />

      <Drawer
        title={editing ? t('editWarehouse') : t('addWarehouse')}
        width={480}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Space>
            <Button onClick={() => setDrawerOpen(false)}>{tc('actions.cancel')}</Button>
            <Button type="primary" loading={saving} onClick={handleSave}>
              {tc('actions.save')}
            </Button>
          </Space>
        }
      >
        <Form form={form} layout="vertical">
          {!editing && (
            <>
              <Form.Item name="branchId" label={t('columns.branch')} rules={[{ required: true }]}>
                <Select
                  options={branches.map((b) => ({ value: b.id, label: b.branchName }))}
                  placeholder={ts('selectBranch')}
                />
              </Form.Item>
              <Form.Item name="warehouseCode" label={t('columns.warehouseCode')} rules={[{ required: true }]}>
                <Input placeholder={ts('warehouseCodePlaceholder')} />
              </Form.Item>
            </>
          )}
          <Form.Item name="warehouseName" label={t('columns.warehouseName')} rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="warehouseType" label={t('columns.type')} rules={[{ required: true }]}>
            <Select options={warehouseTypeOptions} />
          </Form.Item>
          <Form.Item name="isDefault" label={t('columns.isDefault')}>
            <Select
              options={[
                { value: true, label: tc('actions.yes') },
                { value: false, label: tc('actions.no') },
              ]}
            />
          </Form.Item>
          <Form.Item name="address" label={ts('address')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label={t('columns.status')}>
              <Select options={statusOptions} />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </Card>
  );
}

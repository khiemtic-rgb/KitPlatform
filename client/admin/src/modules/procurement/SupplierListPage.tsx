import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AutoComplete,
  Button,
  Card,
  Col,
  Drawer,
  Form,
  Input,
  InputNumber,
  Row,
  Select,
  Space,
  Table,
  Tag,
  message,
  Popconfirm,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { DownloadOutlined, PlusOutlined, ReloadOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import {
  createSupplier,
  deleteSupplier,
  fetchSuppliers,
  updateSupplier,
} from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { Supplier } from '@/shared/api/procurement.types';
import { filterSuppliersById } from '@/modules/procurement/procurement-list-filters';
import { SupplierImportCard } from '@/modules/procurement/SupplierImportCard';
import { downloadCsv } from '@/shared/utils/download-csv';
import { useProcurementEnums } from '@/shared/i18n/use-procurement-enums';

import { useProcurementWrite } from '@/shared/auth/usePermission';

export function SupplierListPage() {
  const { t } = useTranslation('procurement', { keyPrefix: 'suppliers' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const { supplierStatusLabel, supplierStatusOptions } = useProcurementEnums();
  const canWrite = useProcurementWrite();
  const [loading, setLoading] = useState(false);
  const [allItems, setAllItems] = useState<Supplier[]>([]);
  const [supplierFilterId, setSupplierFilterId] = useState<string | undefined>();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<Supplier | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setAllItems(await fetchSuppliers());
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const items = useMemo(
    () => filterSuppliersById(allItems, supplierFilterId),
    [allItems, supplierFilterId],
  );

  const nameSuggestions = useMemo(
    () =>
      allItems.map((s) => ({
        value: s.supplierName,
        label: `${s.supplierCode} — ${s.supplierName}`,
      })),
    [allItems],
  );

  const resetSearch = () => setSupplierFilterId(undefined);

  const exportSuppliers = () => {
    if (items.length === 0) {
      message.info(tShared('messages.noExportData'));
      return;
    }
    downloadCsv(
      `nha-cung-cap-${new Date().toISOString().slice(0, 10)}.csv`,
      [
        t('exportColumns.code'),
        t('exportColumns.name'),
        t('exportColumns.phone'),
        t('exportColumns.taxCode'),
        t('exportColumns.paymentTerms'),
        t('exportColumns.status'),
      ],
      items.map((row) => [
        row.supplierCode,
        row.supplierName,
        row.phone ?? '',
        row.taxCode ?? '',
        String(row.paymentTerms),
        supplierStatusLabel(row.status),
      ]),
    );
  };

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ paymentTerms: 30, status: 1 });
    setDrawerOpen(true);
  };

  const openEdit = (row: Supplier) => {
    setEditing(row);
    form.setFieldsValue(row);
    setDrawerOpen(true);
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      if (editing) {
        await updateSupplier(editing.id, {
          supplierName: values.supplierName,
          taxCode: values.taxCode,
          contactName: values.contactName,
          phone: values.phone,
          email: values.email,
          address: values.address,
          paymentTerms: values.paymentTerms ?? 30,
          status: values.status ?? 1,
        });
        message.success(t('messages.updated'));
      } else {
        await createSupplier({
          supplierCode: values.supplierCode,
          supplierName: values.supplierName,
          taxCode: values.taxCode,
          contactName: values.contactName,
          phone: values.phone,
          email: values.email,
          address: values.address,
          paymentTerms: values.paymentTerms ?? 30,
        });
        message.success(t('messages.created'));
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

  const columns: ColumnsType<Supplier> = [
    { title: tShared('columns.code'), dataIndex: 'supplierCode', width: 110 },
    { title: tShared('columns.supplierName'), dataIndex: 'supplierName' },
    {
      title: tShared('columns.phone'),
      dataIndex: 'phone',
      width: 120,
      render: (v) => v ?? tShared('emDash'),
    },
    { title: tShared('columns.paymentTermsDays'), dataIndex: 'paymentTerms', width: 120 },
    {
      title: tShared('columns.status'),
      dataIndex: 'status',
      width: 110,
      render: (s: number) => (
        <Tag color={s === 1 ? 'green' : 'default'}>{supplierStatusLabel(s)}</Tag>
      ),
    },
    {
      title: '',
      width: 160,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            {tCommon('edit')}
          </Button>
          <Popconfirm
            title={t('deleteConfirm')}
            onConfirm={async () => {
              try {
                await deleteSupplier(row.id);
                message.success(t('messages.deleted'));
                void load();
              } catch (error) {
                message.error(apiErrorMessage(error, t('messages.deleteFailed')));
              }
            }}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              {tCommon('delete')}
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
            {tCommon('reload')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!canWrite}>
            {t('add')}
          </Button>
        </Space>
      }
    >
      <div style={{ marginBottom: 16 }}>
        <Row gutter={[12, 12]}>
          <Col xs={24} md={12} lg={8}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={tShared('filters.supplierSelect')}
              style={{ width: '100%' }}
              value={supplierFilterId}
              onChange={(value) => setSupplierFilterId(value)}
              options={allItems.map((s) => ({
                value: s.id,
                label: `${s.supplierCode} — ${s.supplierName}`,
              }))}
            />
          </Col>
          <Col xs={24}>
            <Space wrap>
              <Button onClick={resetSearch}>{tShared('clearFilters')}</Button>
              <Button icon={<DownloadOutlined />} onClick={exportSuppliers} disabled={loading}>
                {tShared('exportExcel')}
              </Button>
            </Space>
          </Col>
        </Row>
      </div>

      {canWrite && (
        <Card size="small" title={t('importTitle')} style={{ marginBottom: 16 }}>
          <SupplierImportCard onImported={load} />
        </Card>
      )}

      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{
          pageSize: 20,
          showTotal: (total) => tShared('pagination.suppliers', { count: total }),
        }}
      />

      <Drawer
        title={editing ? t('editDrawer') : t('createDrawer')}
        width={480}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" onClick={handleSave} loading={saving}>
            {tCommon('save')}
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          {!editing && (
            <Form.Item
              name="supplierCode"
              label={tShared('columns.supplierCode')}
              rules={[{ required: true, message: tVal('enterSupplierCode') }]}
              normalize={(value) => (typeof value === 'string' ? value.toUpperCase() : value)}
            >
              <Input placeholder={t('codePlaceholder')} style={{ textTransform: 'uppercase' }} />
            </Form.Item>
          )}
          <Form.Item
            name="supplierName"
            label={tShared('columns.supplierName')}
            rules={[{ required: true, message: tVal('enterSupplierName') }]}
          >
            <AutoComplete
              options={nameSuggestions}
              filterOption={(input, option) =>
                String(option?.label ?? '')
                  .toLowerCase()
                  .includes(input.toLowerCase())
              }
              placeholder={t('namePlaceholder')}
            />
          </Form.Item>
          <Form.Item name="taxCode" label={tShared('columns.taxCode')}>
            <Input />
          </Form.Item>
          <Form.Item name="contactName" label={tShared('columns.contactName')}>
            <Input />
          </Form.Item>
          <Form.Item name="phone" label={tShared('columns.phone')}>
            <Input />
          </Form.Item>
          <Form.Item name="email" label={tShared('columns.email')}>
            <Input />
          </Form.Item>
          <Form.Item name="address" label={tShared('columns.address')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="paymentTerms" label={tShared('columns.paymentTermsFull')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label={tShared('columns.status')}>
              <Select options={supplierStatusOptions} />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </Card>
  );
}

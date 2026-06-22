import { useCallback, useEffect, useMemo, useState } from 'react';
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
import { SUPPLIER_STATUS_LABELS } from '@/shared/api/procurement.types';
import { filterSuppliersById } from '@/modules/procurement/procurement-list-filters';
import { downloadCsv } from '@/shared/utils/download-csv';

import { useProcurementWrite } from '@/shared/auth/usePermission';

export function SupplierListPage() {
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
      message.error(apiErrorMessage(error, 'Không tải được nhà cung cấp'));
    } finally {
      setLoading(false);
    }
  }, []);

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
      message.info('Không có dữ liệu để xuất');
      return;
    }
    downloadCsv(
      `nha-cung-cap-${new Date().toISOString().slice(0, 10)}.csv`,
      ['Mã NCC', 'Tên NCC', 'Điện thoại', 'MST', 'Hạn TT (ngày)', 'Trạng thái'],
      items.map((row) => [
        row.supplierCode,
        row.supplierName,
        row.phone ?? '',
        row.taxCode ?? '',
        String(row.paymentTerms),
        SUPPLIER_STATUS_LABELS[row.status] ?? String(row.status),
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
        message.success('Đã cập nhật NCC');
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
        message.success('Đã tạo NCC');
      }
      setDrawerOpen(false);
      void load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không lưu được NCC'));
      }
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<Supplier> = [
    { title: 'Mã', dataIndex: 'supplierCode', width: 110 },
    { title: 'Tên NCC', dataIndex: 'supplierName' },
    { title: 'Điện thoại', dataIndex: 'phone', width: 120, render: (v) => v ?? '—' },
    { title: 'Hạn TT (ngày)', dataIndex: 'paymentTerms', width: 120 },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (s: number) => (
        <Tag color={s === 1 ? 'green' : 'default'}>{SUPPLIER_STATUS_LABELS[s] ?? s}</Tag>
      ),
    },
    {
      title: '',
      width: 160,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
            Sửa
          </Button>
          <Popconfirm
            title="Xóa NCC này?"
            onConfirm={async () => {
              try {
                await deleteSupplier(row.id);
                message.success('Đã xóa NCC');
                void load();
              } catch (error) {
                message.error(apiErrorMessage(error, 'Không xóa được NCC'));
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
      title="Nhà cung cấp"
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
            Tải lại
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!canWrite}>
            Thêm NCC
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
              placeholder="Chọn NCC (mã, tên)"
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
              <Button onClick={resetSearch}>Xóa lọc</Button>
              <Button icon={<DownloadOutlined />} onClick={exportSuppliers} disabled={loading}>
                Xuất Excel
              </Button>
            </Space>
          </Col>
        </Row>
      </div>

      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 20, showTotal: (total) => `${total} NCC` }}
      />

      <Drawer
        title={editing ? 'Sửa NCC' : 'Thêm NCC'}
        width={480}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" onClick={handleSave} loading={saving}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          {!editing && (
            <Form.Item
              name="supplierCode"
              label="Mã NCC"
              rules={[{ required: true, message: 'Nhập mã NCC' }]}
              normalize={(value) => (typeof value === 'string' ? value.toUpperCase() : value)}
            >
              <Input placeholder="VD: NCC001" style={{ textTransform: 'uppercase' }} />
            </Form.Item>
          )}
          <Form.Item name="supplierName" label="Tên NCC" rules={[{ required: true, message: 'Nhập tên NCC' }]}>
            <AutoComplete
              options={nameSuggestions}
              filterOption={(input, option) =>
                String(option?.label ?? '')
                  .toLowerCase()
                  .includes(input.toLowerCase())
              }
              placeholder="Gõ để xem gợi ý NCC có sẵn"
            />
          </Form.Item>
          <Form.Item name="taxCode" label="Mã số thuế">
            <Input />
          </Form.Item>
          <Form.Item name="contactName" label="Người liên hệ">
            <Input />
          </Form.Item>
          <Form.Item name="phone" label="Điện thoại">
            <Input />
          </Form.Item>
          <Form.Item name="email" label="Email">
            <Input />
          </Form.Item>
          <Form.Item name="address" label="Địa chỉ">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="paymentTerms" label="Hạn thanh toán (ngày)">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label="Trạng thái">
              <Select
                options={Object.entries(SUPPLIER_STATUS_LABELS).map(([value, label]) => ({
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

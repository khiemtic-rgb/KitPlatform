import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Descriptions,
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
import { PlusOutlined, EyeOutlined } from '@ant-design/icons';
import { SupplierPaymentFilterBar } from '@/modules/procurement/SupplierPaymentFilterBar';
import {
  cancelSupplierPayment,
  createSupplierPayment,
  fetchGoodsReceipts,
  fetchPurchaseOrders,
  fetchSupplierPayments,
  fetchSupplierPayment,
  fetchSuppliers,
  postSupplierPayment,
  updateSupplierPayment,
} from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  GoodsReceiptListItem,
  PurchaseOrderListItem,
  Supplier,
  SupplierPaymentListFilters,
  SupplierPaymentListItem,
} from '@/shared/api/procurement.types';
import {
  PAYMENT_METHOD_LABELS,
  SUPPLIER_PAYMENT_STATUS_LABELS,
} from '@/shared/api/procurement.types';
import { useProcurementWrite } from '@/shared/auth/usePermission';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';
import { formatDisplayDate } from '@/shared/utils/date';
import { downloadCsv } from '@/shared/utils/download-csv';
import { formatDisplayMoney, moneyInputNumberProps, moneyInputNumberStyle } from '@/shared/utils/money';

const PAYMENT_STATUS_TAG: Record<number, string> = {
  1: 'default',
  2: 'success',
  3: 'error',
};

const emptyFilters: SupplierPaymentListFilters = {};

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

function toFormPaymentDate(value?: string): string {
  if (!value) return todayIsoDate();
  return value.length >= 10 ? value.slice(0, 10) : value;
}

export function SupplierPaymentListPage() {
  const canWrite = useProcurementWrite();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<SupplierPaymentListItem[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrderListItem[]>([]);
  const [goodsReceipts, setGoodsReceipts] = useState<GoodsReceiptListItem[]>([]);
  const [filters, setFilters] = useState<SupplierPaymentListFilters>(emptyFilters);
  const [searchInput, setSearchInput] = useState('');
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingRow, setEditingRow] = useState<SupplierPaymentListItem | null>(null);
  const [detail, setDetail] = useState<SupplierPaymentListItem | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const supplierId = Form.useWatch('supplierId', form);

  const loadReferenceData = useCallback(async () => {
    const [sup, pos, grns] = await Promise.all([
      fetchSuppliers(true),
      fetchPurchaseOrders(),
      fetchGoodsReceipts({ status: 2 }),
    ]);
    setSuppliers(sup);
    setPurchaseOrders(pos);
    setGoodsReceipts(grns);
  }, []);

  const loadPayments = useCallback(async (nextFilters: SupplierPaymentListFilters, search: string) => {
    setFilters(nextFilters);
    setSearchInput(search);
    setLoading(true);
    try {
      const payments = await fetchSupplierPayments({
        ...nextFilters,
        search: search.trim() || undefined,
      });
      setItems(payments);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được thanh toán NCC'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadReferenceData();
    void loadPayments(emptyFilters, '');
  }, [loadReferenceData, loadPayments]);

  useEffect(() => {
    if (!drawerOpen) return;
    if (editingRow) {
      form.setFieldsValue({
        supplierId: editingRow.supplierId,
        purchaseOrderId: editingRow.purchaseOrderId,
        goodsReceiptId: editingRow.goodsReceiptId,
        amount: editingRow.amount,
        paymentMethod: editingRow.paymentMethod,
        paymentDate: toFormPaymentDate(editingRow.paymentDate),
        notes: editingRow.notes,
      });
    } else {
      form.resetFields();
      form.setFieldsValue({ paymentMethod: 2, paymentDate: todayIsoDate() });
    }
  }, [drawerOpen, editingRow, form]);

  const resetFilters = () => {
    void loadPayments(emptyFilters, '');
  };

  const exportPayments = () => {
    if (items.length === 0) {
      message.info('Không có dữ liệu để xuất');
      return;
    }
    downloadCsv(
      `thanh-toan-ncc-${new Date().toISOString().slice(0, 10)}.csv`,
      ['Số phiếu TT', 'NCC', 'Số tiền', 'Hình thức', 'Trạng thái', 'Ngày TT', 'PO', 'GRN'],
      items.map((row) => [
        row.paymentNumber,
        row.supplierName,
        formatDisplayMoney(row.amount),
        PAYMENT_METHOD_LABELS[row.paymentMethod] ?? String(row.paymentMethod),
        SUPPLIER_PAYMENT_STATUS_LABELS[row.status] ?? String(row.status),
        formatDisplayDate(row.paymentDate),
        row.poNumber ?? '—',
        row.grnNumber ?? '—',
      ]),
    );
  };

  const searchSuggestions = useMemo(() => {
    const q = searchInput.trim().toLowerCase();
    const seen = new Set<string>();
    const options: { value: string; label: string }[] = [];

    const add = (value: string, label: string) => {
      const trimmed = value.trim();
      if (!trimmed || seen.has(trimmed)) return;
      if (q && !trimmed.toLowerCase().includes(q) && !label.toLowerCase().includes(q)) return;
      seen.add(trimmed);
      options.push({ value: trimmed, label });
    };

    for (const payment of items) {
      add(payment.paymentNumber, `TT ${payment.paymentNumber} — ${payment.supplierName}`);
      if (payment.poNumber) add(payment.poNumber, `PO ${payment.poNumber}`);
      if (payment.grnNumber) add(payment.grnNumber, `GRN ${payment.grnNumber}`);
    }
    for (const supplier of suppliers) {
      add(supplier.supplierCode, `${supplier.supplierCode} — ${supplier.supplierName}`);
    }

    return options.slice(0, 20);
  }, [items, suppliers, searchInput]);

  const openCreate = () => {
    setEditingId(null);
    setEditingRow(null);
    setDrawerOpen(true);
  };

  const openEdit = async (row: SupplierPaymentListItem) => {
    setEditingId(row.id);
    setEditingRow(row);
    setDetailOpen(false);
    setDrawerOpen(true);
    try {
      const full = await fetchSupplierPayment(row.id);
      setEditingRow(full);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi tiết phiếu — dùng dữ liệu danh sách'));
    }
  };

  const closeFormDrawer = () => {
    setDrawerOpen(false);
    setEditingId(null);
    setEditingRow(null);
  };

  const openDetail = (row: SupplierPaymentListItem) => {
    setDetail(row);
    setDetailOpen(true);
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      const payload = {
        supplierId: values.supplierId as string,
        purchaseOrderId: values.purchaseOrderId as string | undefined,
        goodsReceiptId: values.goodsReceiptId as string | undefined,
        amount: Number(values.amount),
        paymentMethod: Number(values.paymentMethod),
        paymentDate: values.paymentDate as string | undefined,
        notes: values.notes as string | undefined,
      };
      if (Number.isNaN(payload.amount) || payload.amount <= 0) {
        message.error('Số tiền không hợp lệ');
        return;
      }
      if (editingId) {
        await updateSupplierPayment(editingId, payload);
        message.success('Đã cập nhật phiếu thanh toán');
      } else {
        await createSupplierPayment(payload);
        message.success('Đã tạo phiếu thanh toán (Nháp)');
      }
      setDrawerOpen(false);
      setEditingId(null);
      setEditingRow(null);
      void loadPayments(filters, searchInput);
    } catch (error) {
      if (isAxiosError(error)) {
        const status = error.response?.status;
        if (status === 404 && editingId) {
          message.error('API chưa hỗ trợ sửa phiếu. Restart PharmaCore.Api sau khi build.');
        } else {
          message.error(apiErrorMessage(error, 'Không lưu được phiếu thanh toán'));
        }
      } else {
        message.error('Kiểm tra lại thông tin trên form');
      }
    } finally {
      setSaving(false);
    }
  };

  const handlePost = async (id: string) => {
    try {
      setSaving(true);
      const updated = await postSupplierPayment(id);
      message.success('Đã ghi sổ thanh toán');
      setDetail(updated);
      void loadPayments(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ghi sổ được'));
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = async (id: string) => {
    try {
      setSaving(true);
      await cancelSupplierPayment(id);
      message.success('Đã hủy phiếu thanh toán');
      setDetailOpen(false);
      void loadPayments(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiếu'));
    } finally {
      setSaving(false);
    }
  };

  const filteredPos = purchaseOrders.filter((po) => !supplierId || po.supplierId === supplierId);
  const filteredGrns = goodsReceipts.filter((grn) => !supplierId || grn.supplierId === supplierId);

  const columns: ColumnsType<SupplierPaymentListItem> = [
    { title: 'Số phiếu TT', dataIndex: 'paymentNumber', width: 130 },
    { title: 'NCC', dataIndex: 'supplierName' },
    {
      title: 'Số tiền',
      dataIndex: 'amount',
      width: 120,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: 'Hình thức',
      dataIndex: 'paymentMethod',
      width: 120,
      render: (m: number) => PAYMENT_METHOD_LABELS[m] ?? m,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (s: number) => (
        <Tag color={PAYMENT_STATUS_TAG[s] ?? 'default'}>{SUPPLIER_PAYMENT_STATUS_LABELS[s] ?? s}</Tag>
      ),
    },
    {
      title: 'Ngày TT',
      dataIndex: 'paymentDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    { title: 'Đơn đặt hàng', dataIndex: 'poNumber', width: 120, render: (v) => v ?? '—' },
    { title: 'Phiếu nhập', dataIndex: 'grnNumber', width: 120, render: (v) => v ?? '—' },
    {
      title: '',
      width: 90,
      render: (_, row) => (
        <Button
          type="link"
          size="small"
          icon={<EyeOutlined />}
          onClick={(e) => {
            e.stopPropagation();
            openDetail(row);
          }}
        >
          Xem
        </Button>
      ),
    },
  ];

  return (
    <Card
      title="Thanh toán NCC"
      extra={
        canWrite ? (
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Ghi nhận TT
          </Button>
        ) : undefined
      }
    >
      <SupplierPaymentFilterBar
        filters={filters}
        searchInput={searchInput}
        searchSuggestions={searchSuggestions}
        suppliers={suppliers}
        loading={loading}
        onSearchInputChange={setSearchInput}
        onFiltersChange={setFilters}
        onApply={(nextFilters, search) => void loadPayments(nextFilters, search)}
        onReset={resetFilters}
        onExport={exportPayments}
      />

      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 20, showTotal: (total) => `${total} phiếu` }}
        scroll={{ x: 1100 }}
        onRow={(record) => ({
          onClick: () => openDetail(record),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={editingId ? 'Sửa phiếu thanh toán' : 'Ghi nhận thanh toán NCC'}
        width={480}
        open={drawerOpen}
        destroyOnClose
        onClose={closeFormDrawer}
        extra={
          <Button type="primary" onClick={() => void handleSave()} loading={saving}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="supplierId" label="Nhà cung cấp" rules={[{ required: true, message: 'Chọn NCC' }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={suppliers.map((s) => ({
                value: s.id,
                label: `${s.supplierCode} — ${s.supplierName}`,
              }))}
              onChange={() => {
                form.setFieldsValue({ purchaseOrderId: undefined, goodsReceiptId: undefined });
              }}
            />
          </Form.Item>
          <Form.Item name="purchaseOrderId" label="Liên kết đơn đặt hàng (tùy chọn)">
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              disabled={!supplierId}
              placeholder={supplierId ? 'Chọn đơn đặt hàng' : 'Chọn NCC trước'}
              options={filteredPos.map((po) => ({
                value: po.id,
                label: `${po.poNumber} — ${po.supplierName}`,
              }))}
            />
          </Form.Item>
          <Form.Item name="goodsReceiptId" label="Liên kết phiếu nhập (tùy chọn)">
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              disabled={!supplierId}
              placeholder={supplierId ? 'Chọn phiếu nhập' : 'Chọn NCC trước'}
              options={filteredGrns.map((grn) => ({
                value: grn.id,
                label: `${grn.grnNumber} — ${grn.supplierName}`,
              }))}
            />
          </Form.Item>
          <Form.Item
            name="paymentDate"
            label="Ngày thanh toán"
            rules={[{ required: true, message: 'Chọn ngày thanh toán' }]}
          >
            <PharmaDatePicker placeholder="dd/mm/yyyy" />
          </Form.Item>
          <Form.Item
            name="amount"
            label="Số tiền"
            rules={[
              { required: true, message: 'Nhập số tiền' },
              { type: 'number', min: 1, message: 'Số tiền phải lớn hơn 0' },
            ]}
          >
            <InputNumber {...moneyInputNumberProps} style={moneyInputNumberStyle} placeholder="0" />
          </Form.Item>
          <Form.Item name="paymentMethod" label="Hình thức" rules={[{ required: true, message: 'Chọn hình thức' }]}>
            <Select
              options={Object.entries(PAYMENT_METHOD_LABELS).map(([value, label]) => ({
                value: Number(value),
                label,
              }))}
            />
          </Form.Item>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} maxLength={500} showCount />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={detail ? `Xem ${detail.paymentNumber}` : 'Xem phiếu thanh toán'}
        width={520}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail &&
          canWrite &&
          detail.status === 1 && (
            <Space>
              <Button type="primary" onClick={() => void handlePost(detail.id)} loading={saving}>
                Ghi sổ
              </Button>
              <Button onClick={() => void openEdit(detail)}>Sửa</Button>
              <Popconfirm title="Hủy phiếu thanh toán nháp?" onConfirm={() => void handleCancel(detail.id)}>
                <Button danger>Hủy</Button>
              </Popconfirm>
            </Space>
          )
        }
      >
        {detail && (
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="NCC">{detail.supplierName}</Descriptions.Item>
            <Descriptions.Item label="Số tiền">{formatDisplayMoney(detail.amount)}</Descriptions.Item>
            <Descriptions.Item label="Hình thức">
              {PAYMENT_METHOD_LABELS[detail.paymentMethod] ?? detail.paymentMethod}
            </Descriptions.Item>
            <Descriptions.Item label="Trạng thái">
              <Tag color={PAYMENT_STATUS_TAG[detail.status] ?? 'default'}>
                {SUPPLIER_PAYMENT_STATUS_LABELS[detail.status] ?? detail.status}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Ngày TT">{formatDisplayDate(detail.paymentDate)}</Descriptions.Item>
            {detail.postedAt && (
              <Descriptions.Item label="Ngày ghi sổ">{formatDisplayDate(detail.postedAt)}</Descriptions.Item>
            )}
            <Descriptions.Item label="Đơn đặt hàng">{detail.poNumber ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Phiếu nhập">{detail.grnNumber ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Ghi chú">{detail.notes ?? '—'}</Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>
    </Card>
  );
}

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { PlusOutlined, EyeOutlined, EditOutlined, CheckOutlined, CloseCircleOutlined, SaveOutlined } from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { SupplierPaymentFilterBar } from '@/modules/procurement/SupplierPaymentFilterBar';
import {
  parseSupplierPaymentPrefill,
  type SupplierPaymentPrefill,
} from '@/modules/procurement/supplier-payment-nav';
import { SupplierPaymentAmountHint } from '@/modules/procurement/SupplierPaymentAmountHint';
import {
  loadSupplierPaymentAmountHints,
  type SupplierPaymentAmountHints,
} from '@/modules/procurement/supplier-payment-amount-hints';
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
import { SUPPLIER_PAYMENT_STATUS_TAG } from '@/shared/api/procurement.types';
import { useProcurementWrite } from '@/shared/auth/usePermission';
import { useProcurementEnums } from '@/shared/i18n/use-procurement-enums';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';
import { formatDisplayDate } from '@/shared/utils/date';
import { downloadCsv } from '@/shared/utils/download-csv';
import { formatDisplayMoney, moneyInputNumberProps, moneyInputNumberStyle } from '@/shared/utils/money';

const emptyFilters: SupplierPaymentListFilters = {};

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

function toFormPaymentDate(value?: string): string {
  if (!value) return todayIsoDate();
  return value.length >= 10 ? value.slice(0, 10) : value;
}

export function SupplierPaymentListPage() {
  const { t } = useTranslation('procurement', { keyPrefix: 'supplierPayments' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const { paymentMethodLabel, paymentMethodOptions, supplierPaymentStatusLabel } = useProcurementEnums();
  const canWrite = useProcurementWrite();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const prefillHandled = useRef(false);
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
  const [referenceReady, setReferenceReady] = useState(false);
  const [amountHints, setAmountHints] = useState<SupplierPaymentAmountHints | null>(null);
  const [amountHintsLoading, setAmountHintsLoading] = useState(false);
  const supplierId = Form.useWatch('supplierId', form);
  const goodsReceiptId = Form.useWatch('goodsReceiptId', form);
  const purchaseOrderId = Form.useWatch('purchaseOrderId', form);
  const emDash = tShared('emDash');

  const openCreate = useCallback((prefill?: SupplierPaymentPrefill) => {
    setEditingId(null);
    setEditingRow(null);
    if (prefill) {
      form.setFieldsValue({
        supplierId: prefill.supplierId,
        purchaseOrderId: prefill.purchaseOrderId,
        goodsReceiptId: prefill.goodsReceiptId,
        amount: prefill.amount,
        paymentMethod: 2,
        paymentDate: todayIsoDate(),
        notes: undefined,
      });
    } else {
      form.resetFields();
      form.setFieldsValue({ paymentMethod: 2, paymentDate: todayIsoDate() });
    }
    setDrawerOpen(true);
  }, [form]);

  const loadReferenceData = useCallback(async () => {
    const [sup, pos, grns] = await Promise.all([
      fetchSuppliers(true),
      fetchPurchaseOrders({ page: 1, pageSize: 500 }),
      fetchGoodsReceipts({ status: 2, page: 1, pageSize: 500 }),
    ]);
    setSuppliers(sup);
    setPurchaseOrders(pos.items);
    setGoodsReceipts(grns.items);
    setReferenceReady(true);
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
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadReferenceData();
    void loadPayments(emptyFilters, '');
  }, [loadReferenceData, loadPayments]);

  useEffect(() => {
    if (!drawerOpen || !editingRow) return;
    form.setFieldsValue({
      supplierId: editingRow.supplierId,
      purchaseOrderId: editingRow.purchaseOrderId,
      goodsReceiptId: editingRow.goodsReceiptId,
      amount: editingRow.amount,
      paymentMethod: editingRow.paymentMethod,
      paymentDate: toFormPaymentDate(editingRow.paymentDate),
      notes: editingRow.notes,
    });
  }, [drawerOpen, editingRow, form]);

  useEffect(() => {
    if (prefillHandled.current || !referenceReady) return;
    const prefill = parseSupplierPaymentPrefill(searchParams);
    if (!prefill) return;

    prefillHandled.current = true;
    if (!canWrite) {
      message.warning(t('messages.noWritePermission'));
      navigate('/procurement/supplier-payments', { replace: true });
      return;
    }

    const linkedGrn = prefill.goodsReceiptId
      ? goodsReceipts.find((grn) => grn.id === prefill.goodsReceiptId)
      : undefined;
    openCreate({
      ...prefill,
      purchaseOrderId: prefill.purchaseOrderId ?? linkedGrn?.purchaseOrderId,
    });
    navigate('/procurement/supplier-payments', { replace: true });
  }, [searchParams, referenceReady, goodsReceipts, canWrite, navigate, openCreate, t]);

  useEffect(() => {
    if (!drawerOpen) {
      setAmountHints(null);
      return;
    }
    if (!supplierId && !goodsReceiptId && !purchaseOrderId) {
      setAmountHints(null);
      return;
    }

    let cancelled = false;
    setAmountHintsLoading(true);
    void loadSupplierPaymentAmountHints({
      supplierId,
      goodsReceiptId,
      purchaseOrderId,
      purchaseOrders,
    })
      .then((hints) => {
        if (!cancelled) setAmountHints(hints);
      })
      .finally(() => {
        if (!cancelled) setAmountHintsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [drawerOpen, supplierId, goodsReceiptId, purchaseOrderId, purchaseOrders]);

  const resetFilters = () => {
    void loadPayments(emptyFilters, '');
  };

  const exportPayments = () => {
    if (items.length === 0) {
      message.info(tShared('messages.noExportData'));
      return;
    }
    downloadCsv(
      `thanh-toan-ncc-${new Date().toISOString().slice(0, 10)}.csv`,
      [
        t('exportColumns.paymentNumber'),
        t('exportColumns.supplier'),
        t('exportColumns.amount'),
        t('exportColumns.method'),
        t('exportColumns.status'),
        t('exportColumns.paymentDate'),
        t('exportColumns.poNumber'),
        t('exportColumns.grnNumber'),
      ],
      items.map((row) => [
        row.paymentNumber,
        row.supplierName,
        formatDisplayMoney(row.amount),
        paymentMethodLabel(row.paymentMethod),
        supplierPaymentStatusLabel(row.status),
        formatDisplayDate(row.paymentDate),
        row.poNumber ?? emDash,
        row.grnNumber ?? emDash,
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

  const openEdit = async (row: SupplierPaymentListItem) => {
    setEditingId(row.id);
    setEditingRow(row);
    setDetailOpen(false);
    setDrawerOpen(true);
    try {
      const full = await fetchSupplierPayment(row.id);
      setEditingRow(full);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
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
        message.error(tVal('invalidAmount'));
        return;
      }
      if (editingId) {
        await updateSupplierPayment(editingId, payload);
        message.success(t('messages.updated'));
      } else {
        await createSupplierPayment(payload);
        message.success(t('messages.created'));
      }
      setDrawerOpen(false);
      setEditingId(null);
      setEditingRow(null);
      void loadPayments(filters, searchInput);
    } catch (error) {
      if (isAxiosError(error)) {
        const status = error.response?.status;
        if (status === 404 && editingId) {
          message.error(t('messages.apiEditUnsupported'));
        } else {
          message.error(apiErrorMessage(error, t('messages.saveFailed')));
        }
      } else {
        message.error(tShared('messages.checkForm'));
      }
    } finally {
      setSaving(false);
    }
  };

  const handlePost = async (id: string) => {
    try {
      setSaving(true);
      const updated = await postSupplierPayment(id);
      message.success(t('messages.posted'));
      setDetail(updated);
      void loadPayments(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.postFailed')));
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = async (id: string) => {
    try {
      setSaving(true);
      await cancelSupplierPayment(id);
      message.success(t('messages.cancelled'));
      setDetailOpen(false);
      void loadPayments(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    } finally {
      setSaving(false);
    }
  };

  const filteredPos = purchaseOrders.filter((po) => !supplierId || po.supplierId === supplierId);
  const filteredGrns = goodsReceipts.filter((grn) => !supplierId || grn.supplierId === supplierId);

  const columns: ColumnsType<SupplierPaymentListItem> = useMemo(
    () => [
      { title: tShared('columns.paymentNumber'), dataIndex: 'paymentNumber', width: 130 },
      { title: tShared('columns.supplierShort'), dataIndex: 'supplierName' },
      {
        title: tShared('columns.amount'),
        dataIndex: 'amount',
        width: 120,
        align: 'right',
        render: (v: number) => (
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
        ),
      },
      {
        title: tShared('columns.paymentMethod'),
        dataIndex: 'paymentMethod',
        width: 120,
        render: (m: number) => paymentMethodLabel(m),
      },
      {
        title: tShared('columns.status'),
        dataIndex: 'status',
        width: 110,
        render: (s: number) => (
          <Tag color={SUPPLIER_PAYMENT_STATUS_TAG[s] ?? 'default'}>{supplierPaymentStatusLabel(s)}</Tag>
        ),
      },
      {
        title: tShared('columns.paymentDate'),
        dataIndex: 'paymentDate',
        width: 110,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: tShared('columns.purchaseOrder'),
        dataIndex: 'poNumber',
        width: 120,
        render: (v) => v ?? emDash,
      },
      {
        title: tShared('columns.grnNumber'),
        dataIndex: 'grnNumber',
        width: 120,
        render: (v) => v ?? emDash,
      },
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
            {tCommon('view')}
          </Button>
        ),
      },
    ],
    [tShared, paymentMethodLabel, supplierPaymentStatusLabel, emDash, tCommon],
  );

  return (
    <Card
      title={t('title')}
      extra={
        canWrite ? (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openCreate()}>
            {t('record')}
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
        pagination={{ pageSize: 20, showTotal: (total) => tShared('pagination.payments', { count: total }) }}
        scroll={{ x: 1100 }}
        onRow={(record) => ({
          onClick: () => openDetail(record),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={editingId ? t('editDrawer') : t('createDrawer')}
        width={480}
        open={drawerOpen}
        destroyOnClose
        onClose={closeFormDrawer}
        extra={
          <Button type="primary" icon={<SaveOutlined />} onClick={() => void handleSave()} loading={saving}>
            {tCommon('save')}
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="supplierId"
            label={tShared('columns.supplierName')}
            rules={[{ required: true, message: tVal('selectSupplier') }]}
          >
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
          <Form.Item name="purchaseOrderId" label={t('linkPoOptional')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              disabled={!supplierId}
              placeholder={supplierId ? t('selectPo') : t('selectSupplierFirst')}
              options={filteredPos.map((po) => ({
                value: po.id,
                label: `${po.poNumber} — ${po.supplierName}`,
              }))}
            />
          </Form.Item>
          <Form.Item name="goodsReceiptId" label={t('linkGrnOptional')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              disabled={!supplierId}
              placeholder={supplierId ? t('selectGrn') : t('selectSupplierFirst')}
              options={filteredGrns.map((grn) => ({
                value: grn.id,
                label: `${grn.grnNumber} — ${grn.supplierName}`,
              }))}
              onChange={(grnId) => {
                if (!grnId) return;
                const grn = filteredGrns.find((g) => g.id === grnId);
                if (grn?.purchaseOrderId) {
                  form.setFieldValue('purchaseOrderId', grn.purchaseOrderId);
                }
              }}
            />
          </Form.Item>
          <SupplierPaymentAmountHint
            hints={amountHints}
            loading={amountHintsLoading}
            onFillAmount={(amount) => form.setFieldValue('amount', amount)}
          />
          <Form.Item
            name="paymentDate"
            label={t('paymentDate')}
            rules={[{ required: true, message: tVal('selectPaymentDate') }]}
          >
            <PharmaDatePicker placeholder={tShared('datePlaceholder')} />
          </Form.Item>
          <Form.Item
            name="amount"
            label={tShared('columns.amount')}
            rules={[
              { required: true, message: tVal('enterAmount') },
              { type: 'number', min: 1, message: tVal('amountPositive') },
            ]}
          >
            <InputNumber {...moneyInputNumberProps} style={moneyInputNumberStyle} placeholder={tShared('moneyPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="paymentMethod"
            label={tShared('columns.paymentMethod')}
            rules={[{ required: true, message: tVal('selectPaymentMethod') }]}
          >
            <Select options={paymentMethodOptions} />
          </Form.Item>
          <Form.Item name="notes" label={tShared('columns.notes')}>
            <Input.TextArea rows={2} maxLength={500} showCount />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={detail ? t('viewDrawerWithNumber', { paymentNumber: detail.paymentNumber }) : t('viewDrawer')}
        width={520}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail &&
          canWrite &&
          detail.status === 1 && (
            <Space>
              <Button type="primary" icon={<CheckOutlined />} onClick={() => void handlePost(detail.id)} loading={saving}>
                {t('post')}
              </Button>
              <Button icon={<EditOutlined />} onClick={() => void openEdit(detail)}>
                {tCommon('edit')}
              </Button>
              <Popconfirm title={t('cancelConfirm')} onConfirm={() => void handleCancel(detail.id)}>
                <Button danger icon={<CloseCircleOutlined />}>
                  {tCommon('cancel')}
                </Button>
              </Popconfirm>
            </Space>
          )
        }
      >
        {detail && (
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={tShared('columns.supplierShort')}>{detail.supplierName}</Descriptions.Item>
            <Descriptions.Item label={tShared('columns.amount')}>{formatDisplayMoney(detail.amount)}</Descriptions.Item>
            <Descriptions.Item label={tShared('columns.paymentMethod')}>
              {paymentMethodLabel(detail.paymentMethod)}
            </Descriptions.Item>
            <Descriptions.Item label={tShared('columns.status')}>
              <Tag color={SUPPLIER_PAYMENT_STATUS_TAG[detail.status] ?? 'default'}>
                {supplierPaymentStatusLabel(detail.status)}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label={tShared('columns.paymentDate')}>{formatDisplayDate(detail.paymentDate)}</Descriptions.Item>
            {detail.postedAt && (
              <Descriptions.Item label={tShared('columns.postedDate')}>{formatDisplayDate(detail.postedAt)}</Descriptions.Item>
            )}
            <Descriptions.Item label={tShared('columns.purchaseOrder')}>{detail.poNumber ?? emDash}</Descriptions.Item>
            <Descriptions.Item label={tShared('columns.grnNumber')}>{detail.grnNumber ?? emDash}</Descriptions.Item>
            <Descriptions.Item label={tShared('columns.notes')}>{detail.notes ?? emDash}</Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>
    </Card>
  );
}

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Drawer,
  Empty,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import {
  CheckCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  ExportOutlined,
  EyeOutlined,
  ImportOutlined,
  PlusOutlined,
  ReloadOutlined,
  SaveOutlined,
  StopOutlined,
  SwapOutlined,
} from '@ant-design/icons';
import {
  cancelTransfer,
  completeTransfer,
  createTransfer,
  fetchStockBatches,
  fetchStockProducts,
  fetchTransfer,
  fetchTransfers,
  fetchWarehouses,
} from '@/shared/api/inventory.api';
import { fetchProduct } from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { TransferDetail, TransferListItem, Warehouse } from '@/shared/api/inventory.types';
import type { ProductUnit } from '@/shared/api/catalog.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';
import { inventoryT } from '@/shared/i18n';
import { useInventoryEnums } from '@/shared/i18n/use-inventory-enums';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { formatUnitLabel } from '@/modules/procurement/product-unit.helpers';

const TRANSFER_DRAWER_WIDTH = 'calc(100vw - 248px)';

interface TransferDraftLine {
  key: string;
  productId: string;
  productLabel: string;
  batchId: string;
  batchLabel: string;
  productUnitId: string;
  unitName: string;
  conversionFactor: number;
  quantity: number;
  quantityAvailable: number;
}

interface LineEditorState {
  productId?: string;
  productLabel?: string;
  batchId?: string;
  batchLabel?: string;
  productUnitId?: string;
  unitName?: string;
  conversionFactor: number;
  quantity: number;
  quantityAvailable: number;
}

const emptyEditor = (): LineEditorState => ({
  conversionFactor: 1,
  quantity: 1,
  quantityAvailable: 0,
});

function stockProductLabel(code: string, name: string, unitName?: string | null, totalQty?: number) {
  const t = inventoryT();
  const unit = unitName ? t('shared.stockProductUnit', { unit: unitName }) : '';
  const stock =
    totalQty != null ? t('shared.stockProductStock', { qty: formatDisplayQuantity(totalQty) }) : '';
  return t('shared.stockProductOption', { code, name, unit, stock });
}

function batchOptionLabel(batchNumber: string, expiryDate: string | undefined, quantityAvailable: number) {
  const t = inventoryT();
  const expiry = expiryDate
    ? t('shared.expirySuffix', { date: formatDisplayDate(expiryDate) })
    : '';
  return t('shared.batchWithExpiry', {
    number: batchNumber,
    expiry,
    qty: formatDisplayQuantity(quantityAvailable),
  });
}

function toBaseQuantity(quantity: number, conversionFactor: number) {
  return Math.round(quantity * conversionFactor * 1000) / 1000;
}

function newLineKey() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function TransferLineEditor({
  fromWarehouseId,
  value,
  onChange,
  onSubmit,
  onCancel,
  submitLabel,
  cancelLabel,
}: {
  fromWarehouseId?: string;
  value: LineEditorState;
  onChange: (next: LineEditorState) => void;
  onSubmit: () => void;
  onCancel?: () => void;
  submitLabel: string;
  cancelLabel?: string;
}) {
  const { t } = useTranslation('inventory', { keyPrefix: 'transferList' });
  const [productOptions, setProductOptions] = useState<{ value: string; label: string }[]>([]);
  const [batchOptions, setBatchOptions] = useState<
    { value: string; label: string; quantityAvailable: number; batchNumber: string }[]
  >([]);
  const [productLoading, setProductLoading] = useState(false);
  const [batchLoading, setBatchLoading] = useState(false);
  const [units, setUnits] = useState<ProductUnit[]>([]);
  const productSearchTimer = useRef<number | undefined>(undefined);
  const valueRef = useRef(value);
  valueRef.current = value;

  const patchValue = useCallback(
    (patch: Partial<LineEditorState>) => {
      onChange({ ...valueRef.current, ...patch });
    },
    [onChange],
  );

  const searchProducts = useCallback(
    (query: string) => {
      if (!fromWarehouseId) {
        setProductOptions([]);
        return;
      }
      window.clearTimeout(productSearchTimer.current);
      productSearchTimer.current = window.setTimeout(() => {
        void (async () => {
          setProductLoading(true);
          try {
            const result = await fetchStockProducts({
              warehouseId: fromWarehouseId,
              search: query.trim() || undefined,
              page: 1,
              pageSize: 20,
            });
            setProductOptions(
              result.items.map((p) => ({
                value: p.productId,
                label: stockProductLabel(p.productCode, p.productName, p.saleUnitName, p.totalQuantity),
              })),
            );
          } catch {
            setProductOptions([]);
          } finally {
            setProductLoading(false);
          }
        })();
      }, 300);
    },
    [fromWarehouseId],
  );

  const loadBatches = useCallback(
    async (productId: string, preferredBatchId?: string) => {
      if (!fromWarehouseId) {
        setBatchOptions([]);
        return;
      }
      setBatchLoading(true);
      try {
        const result = await fetchStockBatches({
          warehouseId: fromWarehouseId,
          productId,
          page: 1,
          pageSize: 50,
        });
        const options = result.items
          .filter((b) => b.quantityAvailable > 0)
          .map((b) => ({
            value: b.id,
            label: batchOptionLabel(b.batchNumber, b.expiryDate, b.quantityAvailable),
            quantityAvailable: b.quantityAvailable,
            batchNumber: b.batchNumber,
          }));
        setBatchOptions(options);
        const preferred = preferredBatchId
          ? options.find((o) => o.value === preferredBatchId)
          : undefined;
        const picked = preferred ?? options[0];
        if (picked) {
          patchValue({
            batchId: picked.value,
            batchLabel: picked.label,
            quantityAvailable: picked.quantityAvailable,
          });
        } else {
          patchValue({
            batchId: undefined,
            batchLabel: undefined,
            quantityAvailable: 0,
          });
        }
      } catch {
        setBatchOptions([]);
      } finally {
        setBatchLoading(false);
      }
    },
    [fromWarehouseId, patchValue],
  );

  useEffect(() => {
    searchProducts('');
    return () => window.clearTimeout(productSearchTimer.current);
  }, [fromWarehouseId, searchProducts]);

  useEffect(() => {
    if (!value.productId || !fromWarehouseId) {
      setUnits([]);
      setBatchOptions([]);
      return;
    }
    void loadBatches(value.productId, value.batchId);
    let cancelled = false;
    void fetchProduct(value.productId)
      .then((product) => {
        if (cancelled) return;
        setUnits(product.units);
        const current = valueRef.current;
        if (current.productUnitId) {
          const existing = product.units.find((u) => u.id === current.productUnitId);
          if (existing) {
            patchValue({
              unitName: formatUnitLabel(existing),
              conversionFactor: existing.conversionFactor,
            });
            return;
          }
        }
        const defaultId =
          product.units.find((u) => u.isBaseUnit)?.id ??
          product.units.find((u) => u.isSaleUnit)?.id ??
          product.units[0]?.id;
        const unit = product.units.find((u) => u.id === defaultId);
        if (unit) {
          patchValue({
            productUnitId: unit.id,
            unitName: formatUnitLabel(unit),
            conversionFactor: unit.conversionFactor,
          });
        }
      })
      .catch(() => {
        if (!cancelled) setUnits([]);
      });
    return () => {
      cancelled = true;
    };
    // Intentionally only when product / warehouse changes
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value.productId, fromWarehouseId]);

  const baseQty = toBaseQuantity(value.quantity || 0, value.conversionFactor || 1);
  const exceedStock = Boolean(value.batchId && baseQty > value.quantityAvailable);

  return (
    <div
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: 8,
        alignItems: 'flex-end',
        width: '100%',
        padding: '10px 12px',
        background: '#f7fafb',
        border: '1px solid #d9e2e6',
        borderRadius: 6,
        marginBottom: 12,
      }}
    >
      <div style={{ flex: '1 1 280px', minWidth: 240 }}>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('productPlaceholder')}
        </Typography.Text>
        <Select
          showSearch
          filterOption={false}
          style={{ width: '100%' }}
          placeholder={t('productPlaceholder')}
          disabled={!fromWarehouseId}
          loading={productLoading}
          options={productOptions}
          value={value.productId}
          onSearch={searchProducts}
          onDropdownVisibleChange={(open) => {
            if (open) searchProducts('');
          }}
          popupMatchSelectWidth={false}
          dropdownStyle={{ minWidth: 420 }}
          notFoundContent={
            fromWarehouseId ? t('notFound.noStockProducts') : t('notFound.selectFromWarehouseFirst')
          }
          onChange={(productId, option) => {
            const label = (option as { label?: string })?.label ?? productId;
            onChange({
              ...emptyEditor(),
              productId,
              productLabel: label,
              quantity: 1,
            });
          }}
        />
      </div>

      <div style={{ flex: '1 1 240px', minWidth: 200 }}>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('batchPlaceholder')}
        </Typography.Text>
        <Select
          style={{ width: '100%' }}
          placeholder={t('batchPlaceholder')}
          disabled={!value.productId || batchOptions.length === 0}
          loading={batchLoading}
          options={batchOptions}
          value={value.batchId}
          popupMatchSelectWidth={false}
          dropdownStyle={{ minWidth: 360 }}
          notFoundContent={
            value.productId ? t('notFound.noStockBatches') : t('notFound.selectProductFirst')
          }
          onChange={(batchId) => {
            const picked = batchOptions.find((o) => o.value === batchId);
            patchValue({
              batchId,
              batchLabel: picked?.label ?? batchId,
              quantityAvailable: picked?.quantityAvailable ?? 0,
            });
          }}
        />
      </div>

      <div style={{ flex: '0 0 130px' }}>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('unit')}
        </Typography.Text>
        <div>
          <ProductUnitSelect
            productId={value.productId}
            value={value.productUnitId}
            width={130}
            onChange={(unitId) => {
              const unit = units.find((u) => u.id === unitId);
              patchValue({
                productUnitId: unitId,
                unitName: unit ? formatUnitLabel(unit) : unitId,
                conversionFactor: unit?.conversionFactor ?? 1,
              });
            }}
          />
        </div>
      </div>

      <div style={{ flex: '0 0 100px' }}>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('qty')}
        </Typography.Text>
        <InputNumber
          min={0.001}
          style={{ width: '100%' }}
          value={value.quantity}
          onChange={(qty) => patchValue({ quantity: Number(qty ?? 0) })}
        />
      </div>

      <Space wrap>
        <Button
          type="primary"
          icon={onCancel ? <SaveOutlined /> : <PlusOutlined />}
          onClick={onSubmit}
        >
          {submitLabel}
        </Button>
        {onCancel && cancelLabel ? (
          <Button onClick={onCancel}>{cancelLabel}</Button>
        ) : null}
      </Space>

      {value.batchId ? (
        <Typography.Text
          type={exceedStock ? 'danger' : 'secondary'}
          style={{ width: '100%', fontSize: 12 }}
        >
          {t('convertedQtyHint', {
            qty: formatDisplayQuantity(baseQty),
            available: formatDisplayQuantity(value.quantityAvailable),
          })}
        </Typography.Text>
      ) : null}
    </div>
  );
}

export function TransferListPage() {
  const { t } = useTranslation('inventory', { keyPrefix: 'transferList' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const { transferStatusLabel } = useInventoryEnums();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<TransferListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<TransferDetail | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const fromWarehouseId = Form.useWatch('fromWarehouseId', form) as string | undefined;
  const [lines, setLines] = useState<TransferDraftLine[]>([]);
  const [composer, setComposer] = useState<LineEditorState>(emptyEditor);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<LineEditorState>(emptyEditor);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [transfers, wh] = await Promise.all([fetchTransfers(), fetchWarehouses()]);
      setItems(transfers);
      setWarehouses(wh);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    if (!drawerOpen) return;
    setLines([]);
    setComposer(emptyEditor());
    setEditingKey(null);
    setEditDraft(emptyEditor());
  }, [fromWarehouseId, drawerOpen]);

  const openCreate = () => {
    form.resetFields();
    setLines([]);
    setComposer(emptyEditor());
    setEditingKey(null);
    setDrawerOpen(true);
  };

  const openDetail = async (id: string) => {
    try {
      setDetail(await fetchTransfer(id));
      setDetailOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    }
  };

  const validateEditor = (draft: LineEditorState): string | null => {
    if (!draft.productId) return t('validation.selectProduct');
    if (!draft.batchId) return t('validation.selectBatch');
    if (!draft.productUnitId) return t('validation.selectUnit');
    if (!draft.quantity || draft.quantity <= 0) return t('validation.quantity');
    const baseQty = toBaseQuantity(draft.quantity, draft.conversionFactor || 1);
    if (baseQty > draft.quantityAvailable) {
      return t('validation.exceedStock', {
        qty: formatDisplayQuantity(baseQty),
        available: formatDisplayQuantity(draft.quantityAvailable),
      });
    }
    return null;
  };

  const editorToLine = (draft: LineEditorState, key: string): TransferDraftLine => ({
    key,
    productId: draft.productId!,
    productLabel: draft.productLabel || draft.productId!,
    batchId: draft.batchId!,
    batchLabel: draft.batchLabel || draft.batchId!,
    productUnitId: draft.productUnitId!,
    unitName: draft.unitName || '—',
    conversionFactor: draft.conversionFactor || 1,
    quantity: draft.quantity,
    quantityAvailable: draft.quantityAvailable,
  });

  const handleAddLine = () => {
    const err = validateEditor(composer);
    if (err) {
      message.warning(err);
      return;
    }
    setLines((prev) => [editorToLine(composer, newLineKey()), ...prev]);
    setComposer(emptyEditor());
    setEditingKey(null);
  };

  const startEditLine = (line: TransferDraftLine) => {
    setEditingKey(line.key);
    setEditDraft({
      productId: line.productId,
      productLabel: line.productLabel,
      batchId: line.batchId,
      batchLabel: line.batchLabel,
      productUnitId: line.productUnitId,
      unitName: line.unitName,
      conversionFactor: line.conversionFactor,
      quantity: line.quantity,
      quantityAvailable: line.quantityAvailable,
    });
  };

  const saveEditLine = () => {
    if (!editingKey) return;
    const err = validateEditor(editDraft);
    if (err) {
      message.warning(err);
      return;
    }
    setLines((prev) =>
      prev.map((line) => (line.key === editingKey ? editorToLine(editDraft, editingKey) : line)),
    );
    setEditingKey(null);
    setEditDraft(emptyEditor());
  };

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      if (values.fromWarehouseId === values.toWarehouseId) {
        message.warning(t('validation.sameWarehouse'));
        return;
      }
      if (lines.length === 0) {
        message.warning(t('validation.emptyLines'));
        return;
      }
      setSaving(true);
      const created = await createTransfer({
        fromWarehouseId: values.fromWarehouseId,
        toWarehouseId: values.toWarehouseId,
        notes: values.notes,
        items: lines.map((line) => ({
          batchId: line.batchId,
          quantity: toBaseQuantity(line.quantity, line.conversionFactor),
        })),
      });
      message.success(t('messages.createSuccess', { number: created.transferNumber }));
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.createFailed')));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleComplete = async (id: string) => {
    try {
      await completeTransfer(id);
      message.success(t('messages.completeSuccess'));
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.completeFailed')));
    }
  };

  const handleCancel = async (id: string) => {
    try {
      await cancelTransfer(id);
      message.success(t('messages.cancelSuccess'));
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    }
  };

  const lineColumns: ColumnsType<TransferDraftLine> = useMemo(
    () => [
      {
        title: ts('productName'),
        dataIndex: 'productLabel',
        ellipsis: true,
      },
      {
        title: ts('batchAbbr'),
        dataIndex: 'batchLabel',
        width: 200,
        ellipsis: true,
      },
      {
        title: t('unit'),
        dataIndex: 'unitName',
        width: 110,
      },
      {
        title: t('qty'),
        dataIndex: 'quantity',
        width: 90,
        align: 'right',
        render: (v: number) => formatDisplayQuantity(v),
      },
      {
        title: t('qtyBase'),
        key: 'base',
        width: 100,
        align: 'right',
        render: (_, row) => formatDisplayQuantity(toBaseQuantity(row.quantity, row.conversionFactor)),
      },
      {
        title: tc('fields.actions'),
        key: 'actions',
        width: 120,
        render: (_, row) => (
          <Space size={4} onClick={(e) => e.stopPropagation()}>
            <Button
              type="link"
              size="small"
              icon={<EditOutlined />}
              onClick={() => startEditLine(row)}
            >
              {tc('actions.edit')}
            </Button>
            <Button
              type="link"
              size="small"
              danger
              icon={<DeleteOutlined />}
              onClick={() => {
                setLines((prev) => prev.filter((l) => l.key !== row.key));
                if (editingKey === row.key) {
                  setEditingKey(null);
                  setEditDraft(emptyEditor());
                }
              }}
            />
          </Space>
        ),
      },
    ],
    [t, ts, tc, editingKey],
  );

  const columns: ColumnsType<TransferListItem> = [
    { title: ts('documentNumber'), dataIndex: 'transferNumber', width: 130 },
    { title: ts('fromWarehouse'), dataIndex: 'fromWarehouseName' },
    { title: ts('toWarehouse'), dataIndex: 'toWarehouseName' },
    {
      title: tc('fields.status'),
      dataIndex: 'status',
      width: 110,
      render: (v: number) => (
        <Tag color={v === 3 ? 'green' : v === 1 ? 'gold' : 'blue'}>{transferStatusLabel(v)}</Tag>
      ),
    },
    {
      title: ts('date'),
      dataIndex: 'transferDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    { title: ts('lineCount'), dataIndex: 'itemCount', width: 70, align: 'right' },
    {
      title: tc('fields.actions'),
      key: 'actions',
      width: 160,
      render: (_, row) => (
        <Space size={4} onClick={(e) => e.stopPropagation()}>
          <Tag
            color="blue"
            icon={<EyeOutlined />}
            style={{ cursor: 'pointer', margin: 0 }}
            onClick={() => openDetail(row.id)}
          >
            {ts('detail')}
          </Tag>
          {row.status !== 3 && row.status !== 4 && (
            <>
              <Tag
                color="green"
                icon={<CheckCircleOutlined />}
                style={{ cursor: 'pointer', margin: 0 }}
                onClick={() => handleComplete(row.id)}
              >
                {ts('complete')}
              </Tag>
              <Tag
                color="red"
                icon={<StopOutlined />}
                style={{ cursor: 'pointer', margin: 0 }}
                onClick={() => handleCancel(row.id)}
              >
                {ts('cancel')}
              </Tag>
            </>
          )}
        </Space>
      ),
    },
  ];

  const warehouseOptions = warehouses.map((w) => ({ value: w.id, label: w.warehouseName }));

  return (
    <>
      <Card
        title={
          <span>
            <SwapOutlined style={{ marginRight: 8 }} />
            {t('title')}
          </span>
        }
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
              {tc('actions.reload')}
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('createDocument')}
            </Button>
          </Space>
        }
      >
        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={items}
          pagination={false}
          onRow={(row) => ({
            onClick: () => void openDetail(row.id),
            style: { cursor: 'pointer' },
          })}
        />
      </Card>

      <Drawer
        title={
          <span>
            <SwapOutlined style={{ marginRight: 8 }} />
            {t('createTitle')}
          </span>
        }
        width={TRANSFER_DRAWER_WIDTH}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        styles={{
          body: {
            paddingTop: 8,
            paddingBottom: 8,
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          },
        }}
        extra={
          <Space>
            <Button onClick={() => setDrawerOpen(false)}>{tc('actions.cancel')}</Button>
            <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={handleCreate}>
              {tc('actions.save')}
            </Button>
          </Space>
        }
      >
        <Form
          form={form}
          layout="vertical"
          style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, overflow: 'hidden' }}
        >
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Form.Item
              name="fromWarehouseId"
              label={ts('fromWarehouse')}
              rules={[{ required: true }]}
              style={{ flex: '1 1 280px', marginBottom: 12 }}
            >
              <Select
                options={warehouseOptions}
                placeholder={
                  <span>
                    <ExportOutlined style={{ marginRight: 6 }} />
                    {t('selectFromWarehouse')}
                  </span>
                }
              />
            </Form.Item>
            <Form.Item
              name="toWarehouseId"
              label={ts('toWarehouse')}
              rules={[{ required: true }]}
              style={{ flex: '1 1 280px', marginBottom: 12 }}
            >
              <Select
                options={warehouseOptions}
                placeholder={
                  <span>
                    <ImportOutlined style={{ marginRight: 6 }} />
                    {t('selectToWarehouse')}
                  </span>
                }
              />
            </Form.Item>
          </div>
          <Form.Item name="notes" label={ts('notes')} style={{ marginBottom: 12 }}>
            <Input.TextArea rows={2} />
          </Form.Item>

          <div style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
            {!fromWarehouseId ? (
              <Alert type="info" showIcon message={t('notFound.selectFromWarehouseFirst')} />
            ) : (
              <>
                <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                  {t('addItem')}
                </Typography.Text>
                <TransferLineEditor
                  fromWarehouseId={fromWarehouseId}
                  value={composer}
                  onChange={setComposer}
                  onSubmit={handleAddLine}
                  submitLabel={t('addItem')}
                />

                {editingKey ? (
                  <>
                    <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                      {t('editingLine')}
                    </Typography.Text>
                    <TransferLineEditor
                      fromWarehouseId={fromWarehouseId}
                      value={editDraft}
                      onChange={setEditDraft}
                      onSubmit={saveEditLine}
                      onCancel={() => {
                        setEditingKey(null);
                        setEditDraft(emptyEditor());
                      }}
                      submitLabel={t('saveLine')}
                      cancelLabel={tc('actions.cancel')}
                    />
                  </>
                ) : null}

                <Typography.Text strong style={{ display: 'block', margin: '8px 0' }}>
                  {t('lineList')} ({lines.length})
                </Typography.Text>
                <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: 8 }}>
                  {t('clickToEdit')}
                </Typography.Paragraph>
                {lines.length === 0 ? (
                  <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('emptyLines')} />
                ) : (
                  <Table
                    rowKey="key"
                    size="small"
                    pagination={false}
                    dataSource={lines}
                    columns={lineColumns}
                    scroll={{ x: 900 }}
                    onRow={(row) => ({
                      onClick: () => startEditLine(row),
                      style: {
                        cursor: 'pointer',
                        background: editingKey === row.key ? '#e6f4ff' : undefined,
                      },
                    })}
                  />
                )}
              </>
            )}
          </div>
        </Form>
      </Drawer>

      <Drawer
        title={
          <span>
            <EyeOutlined style={{ marginRight: 8 }} />
            {detail ? t('detailTitleWithNumber', { number: detail.transferNumber }) : t('detailTitle')}
          </span>
        }
        width={TRANSFER_DRAWER_WIDTH}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail && detail.status !== 3 && detail.status !== 4 ? (
            <Space>
              <Button danger icon={<StopOutlined />} onClick={() => handleCancel(detail.id)}>
                {ts('cancel')}
              </Button>
              <Button
                type="primary"
                icon={<CheckCircleOutlined />}
                onClick={() => handleComplete(detail.id)}
              >
                {ts('complete')}
              </Button>
            </Space>
          ) : null
        }
      >
        {detail && (
          <>
            <p>
              <strong>{ts('fromWarehouse')}:</strong> {detail.fromWarehouseName}
            </p>
            <p>
              <strong>{ts('toWarehouse')}:</strong> {detail.toWarehouseName}
            </p>
            <p>
              <strong>{tc('fields.status')}:</strong> {transferStatusLabel(detail.status)}
            </p>
            {detail.notes && (
              <p>
                <strong>{ts('notes')}:</strong> {detail.notes}
              </p>
            )}
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              scroll={{ x: 780 }}
              dataSource={detail.items}
              columns={[
                { title: ts('productCode'), dataIndex: 'productCode', width: 110 },
                { title: ts('productName'), dataIndex: 'productName', ellipsis: true },
                { title: ts('unit'), dataIndex: 'unitName', width: 70 },
                { title: ts('batchAbbr'), dataIndex: 'batchNumber', width: 120 },
                {
                  title: ts('expiryAbbr'),
                  dataIndex: 'expiryDate',
                  width: 110,
                  render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
                },
                {
                  title: ts('quantityAbbr'),
                  dataIndex: 'quantity',
                  width: 80,
                  align: 'right',
                  render: (v: number) => formatDisplayQuantity(v),
                },
              ]}
            />
          </>
        )}
      </Drawer>
    </>
  );
}

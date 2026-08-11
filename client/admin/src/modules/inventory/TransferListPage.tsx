import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App,
  Button,
  Card,
  DatePicker,
  Drawer,
  Empty,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { type Dayjs } from 'dayjs';
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
  SendOutlined,
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
  receiveTransfer,
  shipTransfer,
  updateTransfer,
} from '@/shared/api/inventory.api';
import { fetchProduct } from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { TransferDetail, TransferItem, TransferListItem, Warehouse } from '@/shared/api/inventory.types';
import type { ProductUnit } from '@/shared/api/catalog.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';
import { ListFilterBar } from '@/shared/ui/ListFilterBar';
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

const transferUnitsCache = new Map<string, ProductUnit[]>();

/** Unit select for draft lines — never auto-overwrites an existing choice. */
function TransferLineUnitSelect({
  productId,
  value,
  onPick,
}: {
  productId: string;
  value: string;
  onPick: (unit: ProductUnit) => void;
}) {
  const [units, setUnits] = useState<ProductUnit[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const cached = transferUnitsCache.get(productId);
    if (cached) {
      setUnits(cached);
      return;
    }
    let cancelled = false;
    setLoading(true);
    void fetchProduct(productId)
      .then((product) => {
        if (cancelled) return;
        transferUnitsCache.set(productId, product.units);
        setUnits(product.units);
      })
      .catch(() => {
        if (!cancelled) setUnits([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [productId]);

  return (
    <Select
      size="small"
      loading={loading}
      value={value}
      style={{ width: '100%' }}
      options={units.map((u) => ({ value: u.id, label: formatUnitLabel(u) }))}
      onClick={(e) => e.stopPropagation()}
      onChange={(unitId) => {
        const unit = units.find((u) => u.id === unitId);
        if (unit) onPick(unit);
      }}
    />
  );
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
  const { message } = App.useApp();
  const { t } = useTranslation('inventory', { keyPrefix: 'transferList' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const { transferStatusLabel, transferStatusOptions } = useInventoryEnums();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<TransferListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<number | undefined>();
  const [fromWarehouseFilter, setFromWarehouseFilter] = useState<string | undefined>();
  const [toWarehouseFilter, setToWarehouseFilter] = useState<string | undefined>();
  const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [shortageOnly, setShortageOnly] = useState(false);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editingTransferId, setEditingTransferId] = useState<string | null>(null);
  const [editingTransferNumber, setEditingTransferNumber] = useState<string | null>(null);
  const skipClearLinesRef = useRef(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<TransferDetail | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const fromWarehouseId = Form.useWatch('fromWarehouseId', form) as string | undefined;
  const [lines, setLines] = useState<TransferDraftLine[]>([]);
  const [composer, setComposer] = useState<LineEditorState>(emptyEditor);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<LineEditorState>(emptyEditor);
  const [receiveOpen, setReceiveOpen] = useState(false);
  const [receiveBusy, setReceiveBusy] = useState(false);
  const [receiveNotes, setReceiveNotes] = useState('');
  const [receiveQtyByItem, setReceiveQtyByItem] = useState<Record<string, number>>({});
  const [actionBusyId, setActionBusyId] = useState<string | null>(null);
  const prevFromWarehouseRef = useRef<string | undefined>(undefined);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [paged, wh] = await Promise.all([
        fetchTransfers({
          search: search.trim() || undefined,
          status: statusFilter,
          fromWarehouseId: fromWarehouseFilter,
          toWarehouseId: toWarehouseFilter,
          dateFrom: dateRange?.[0] ? dateRange[0].format('YYYY-MM-DD') : undefined,
          dateTo: dateRange?.[1] ? dateRange[1].format('YYYY-MM-DD') : undefined,
          hasShortage: shortageOnly ? true : undefined,
          page,
          pageSize,
        }),
        fetchWarehouses(),
      ]);
      setItems(paged.items);
      setTotal(paged.total);
      setWarehouses(wh);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [
    t,
    search,
    statusFilter,
    fromWarehouseFilter,
    toWarehouseFilter,
    dateRange,
    shortageOnly,
    page,
    pageSize,
  ]);

  useEffect(() => {
    load();
  }, [load]);

  const resetFilters = () => {
    setSearchInput('');
    setSearch('');
    setStatusFilter(undefined);
    setFromWarehouseFilter(undefined);
    setToWarehouseFilter(undefined);
    setDateRange(null);
    setShortageOnly(false);
    setPage(1);
  };
  useEffect(() => {
    if (!drawerOpen) {
      prevFromWarehouseRef.current = undefined;
      return;
    }

    // Prefill (open edit) sets warehouse + lines together — skip wipe once.
    if (skipClearLinesRef.current) {
      skipClearLinesRef.current = false;
      prevFromWarehouseRef.current = fromWarehouseId;
      return;
    }

    const prev = prevFromWarehouseRef.current;
    if (prev === fromWarehouseId) return;

    // First warehouse value after open (undefined → id) must not wipe lines.
    if (prev == null) {
      prevFromWarehouseRef.current = fromWarehouseId;
      return;
    }

    prevFromWarehouseRef.current = fromWarehouseId;
    setLines([]);
    setComposer(emptyEditor());
    setEditingKey(null);
    setEditDraft(emptyEditor());
  }, [fromWarehouseId, drawerOpen]);

  const openCreate = () => {
    setEditingTransferId(null);
    setEditingTransferNumber(null);
    form.resetFields();
    setLines([]);
    setComposer(emptyEditor());
    setEditingKey(null);
    setDrawerOpen(true);
  };

  const detailToDraftLines = async (doc: TransferDetail): Promise<TransferDraftLine[]> => {
    const draftLines: TransferDraftLine[] = [];
    for (const item of doc.items) {
      const product = await fetchProduct(item.productId);
      const baseUnit =
        product.units.find((u) => u.isBaseUnit) ??
        product.units.find((u) => u.isSaleUnit) ??
        product.units[0];
      if (!baseUnit) {
        throw new Error(t('validation.selectUnit'));
      }
      let available = item.quantity;
      try {
        const batches = await fetchStockBatches({
          warehouseId: doc.fromWarehouseId,
          productId: item.productId,
          pageSize: 100,
        });
        const batch = batches.items.find((b) => b.id === item.batchId);
        if (batch) available = batch.quantityAvailable;
      } catch {
        // keep line qty as floor while editing if stock lookup fails
      }
      draftLines.push({
        key: item.id,
        productId: item.productId,
        productLabel: `${item.productCode} — ${item.productName}`,
        batchId: item.batchId,
        batchLabel: batchOptionLabel(item.batchNumber, item.expiryDate ?? undefined, available),
        productUnitId: baseUnit.id,
        unitName: formatUnitLabel(baseUnit),
        conversionFactor: 1,
        quantity: item.quantity,
        quantityAvailable: Math.max(available, item.quantity),
      });
    }
    return draftLines;
  };

  const openEdit = async (id: string) => {
    setActionBusyId(id);
    try {
      const doc = detail?.id === id ? detail : await fetchTransfer(id);
      if (doc.status !== 1) {
        message.warning(t('messages.updateFailed'));
        return;
      }
      const draftLines = await detailToDraftLines(doc);
      skipClearLinesRef.current = true;
      setEditingTransferId(doc.id);
      setEditingTransferNumber(doc.transferNumber);
      form.setFieldsValue({
        fromWarehouseId: doc.fromWarehouseId,
        toWarehouseId: doc.toWarehouseId,
        notes: doc.notes ?? undefined,
      });
      setLines(draftLines);
      setComposer(emptyEditor());
      setEditingKey(null);
      setEditDraft(emptyEditor());
      setDetailOpen(false);
      setDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    } finally {
      setActionBusyId(null);
    }
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

  const handleSave = async () => {
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
      const overStock = lines.find(
        (line) => toBaseQuantity(line.quantity, line.conversionFactor) > line.quantityAvailable,
      );
      if (overStock) {
        message.warning(
          t('validation.exceedStock', {
            qty: formatDisplayQuantity(
              toBaseQuantity(overStock.quantity, overStock.conversionFactor),
            ),
            available: formatDisplayQuantity(overStock.quantityAvailable),
          }),
        );
        return;
      }
      setSaving(true);
      const payload = {
        fromWarehouseId: values.fromWarehouseId as string,
        toWarehouseId: values.toWarehouseId as string,
        notes: values.notes as string | undefined,
        items: lines.map((line) => ({
          batchId: line.batchId,
          quantity: toBaseQuantity(line.quantity, line.conversionFactor),
        })),
      };
      if (editingTransferId) {
        const updated = await updateTransfer(editingTransferId, payload);
        message.success(t('messages.updateSuccess', { number: updated.transferNumber }));
        if (detail?.id === updated.id) {
          setDetail(updated);
        }
      } else {
        const created = await createTransfer(payload);
        message.success(t('messages.createSuccess', { number: created.transferNumber }));
      }
      setDrawerOpen(false);
      setEditingTransferId(null);
      setEditingTransferNumber(null);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(
          apiErrorMessage(
            error,
            editingTransferId ? t('messages.updateFailed') : t('messages.createFailed'),
          ),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  const handleShip = async (id: string) => {
    setActionBusyId(id);
    try {
      await shipTransfer(id);
      message.success(t('messages.shipSuccess'));
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.shipFailed')));
    } finally {
      setActionBusyId(null);
    }
  };

  const openReceive = (doc: TransferDetail) => {
    const qty: Record<string, number> = {};
    for (const line of doc.items) {
      qty[line.id] = line.quantity;
    }
    setReceiveQtyByItem(qty);
    setReceiveNotes('');
    setReceiveOpen(true);
    if (detail?.id !== doc.id) {
      setDetail(doc);
    }
  };

  const handleOpenReceive = async (id: string) => {
    try {
      const doc = detail?.id === id ? detail : await fetchTransfer(id);
      setDetail(doc);
      setDetailOpen(true);
      openReceive(doc);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    }
  };

  const hasReceiveShortage = useMemo(() => {
    if (!detail) return false;
    return detail.items.some((line) => (receiveQtyByItem[line.id] ?? line.quantity) < line.quantity);
  }, [detail, receiveQtyByItem]);

  const receiveShortageQty = useMemo(() => {
    if (!detail) return 0;
    return detail.items.reduce((sum, line) => {
      const received = receiveQtyByItem[line.id] ?? line.quantity;
      return sum + Math.max(0, line.quantity - received);
    }, 0);
  }, [detail, receiveQtyByItem]);

  const fillReceiveShipped = () => {
    if (!detail) return;
    const qty: Record<string, number> = {};
    for (const line of detail.items) qty[line.id] = line.quantity;
    setReceiveQtyByItem(qty);
  };

  const handleReceiveConfirm = async () => {
    if (!detail) return;
    if (hasReceiveShortage && !receiveNotes.trim()) {
      message.warning(t('messages.receiveNotesRequired'));
      return;
    }
    setReceiveBusy(true);
    try {
      const updated = await receiveTransfer(detail.id, {
        notes: receiveNotes.trim() || undefined,
        items: detail.items.map((line) => ({
          transferItemId: line.id,
          receivedQuantity: receiveQtyByItem[line.id] ?? line.quantity,
        })),
      });
      setDetail(updated);
      setReceiveOpen(false);
      message.success(
        hasReceiveShortage ? t('messages.receiveSuccessWithShortage') : t('messages.receiveSuccess'),
      );
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.receiveFailed')));
    } finally {
      setReceiveBusy(false);
    }
  };

  const handleComplete = async (id: string) => {
    setActionBusyId(id);
    try {
      await completeTransfer(id);
      message.success(t('messages.completeSuccess'));
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.completeFailed')));
    } finally {
      setActionBusyId(null);
    }
  };

  const handleCancel = async (id: string) => {
    setActionBusyId(id);
    try {
      await cancelTransfer(id);
      message.success(t('messages.cancelSuccess'));
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      setReceiveOpen(false);
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    } finally {
      setActionBusyId(null);
    }
  };

  const updateLineQuantity = (key: string, quantity: number | null) => {
    const qty = quantity == null || Number.isNaN(quantity) ? 0 : quantity;
    setLines((prev) =>
      prev.map((line) => {
        if (line.key !== key) return line;
        const baseQty = toBaseQuantity(qty, line.conversionFactor || 1);
        if (baseQty > line.quantityAvailable && qty > 0) {
          message.warning(
            t('validation.exceedStock', {
              qty: formatDisplayQuantity(baseQty),
              available: formatDisplayQuantity(line.quantityAvailable),
            }),
          );
        }
        return { ...line, quantity: qty };
      }),
    );
  };

  const updateLineUnit = (key: string, unit: ProductUnit) => {
    setLines((prev) =>
      prev.map((line) => {
        if (line.key !== key) return line;
        // Keep base quantity stable when switching display unit.
        const baseQty = toBaseQuantity(line.quantity, line.conversionFactor || 1);
        const nextFactor = unit.conversionFactor || 1;
        const nextQty = Math.round((baseQty / nextFactor) * 1000) / 1000;
        return {
          ...line,
          productUnitId: unit.id,
          unitName: formatUnitLabel(unit),
          conversionFactor: nextFactor,
          quantity: nextQty,
        };
      }),
    );
  };

  const lineColumns: ColumnsType<TransferDraftLine> = [
      {
        title: ts('stt'),
        key: 'stt',
        width: 52,
        align: 'center',
        render: (_: unknown, __: TransferDraftLine, index: number) => index + 1,
      },
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
        dataIndex: 'productUnitId',
        width: 140,
        render: (_: string, row) => (
          <TransferLineUnitSelect
            productId={row.productId}
            value={row.productUnitId}
            onPick={(unit) => updateLineUnit(row.key, unit)}
          />
        ),
      },
      {
        title: t('qty'),
        dataIndex: 'quantity',
        width: 110,
        align: 'right',
        render: (v: number, row) => (
          <InputNumber
            size="small"
            min={0}
            value={v}
            style={{ width: '100%' }}
            onClick={(e) => e.stopPropagation()}
            onChange={(next) => updateLineQuantity(row.key, next)}
          />
        ),
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
        width: 140,
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
    ];

  const columns: ColumnsType<TransferListItem> = [
    { title: ts('documentNumber'), dataIndex: 'transferNumber', width: 130 },
    { title: ts('fromWarehouse'), dataIndex: 'fromWarehouseName' },
    { title: ts('toWarehouse'), dataIndex: 'toWarehouseName' },
    {
      title: tc('fields.status'),
      dataIndex: 'status',
      width: 200,
      render: (v: number, row) => (
        <Space size={4} wrap>
          <Tag
            color={
              row.hasShortage && v === 3
                ? 'orange'
                : v === 3
                  ? 'green'
                  : v === 2
                    ? 'processing'
                    : v === 1
                      ? 'gold'
                      : 'default'
            }
          >
            {row.hasShortage && v === 3 ? t('completedWithShortage') : transferStatusLabel(v)}
          </Tag>
        </Space>
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
      width: 280,
      render: (_, row) => (
        <Spin size="small" spinning={actionBusyId === row.id}>
          <Space size={4} wrap onClick={(e) => e.stopPropagation()}>
            <Tag
              color="blue"
              icon={<EyeOutlined />}
              style={{ cursor: 'pointer', margin: 0 }}
              onClick={(e) => {
                e.stopPropagation();
                void openDetail(row.id);
              }}
            >
              {ts('detail')}
            </Tag>
            {row.status === 1 && (
              <>
                <Tag
                  color="geekblue"
                  icon={<EditOutlined />}
                  style={{ cursor: 'pointer', margin: 0 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    void openEdit(row.id);
                  }}
                >
                  {t('edit')}
                </Tag>
                <Tag
                  color="cyan"
                  icon={<SendOutlined />}
                  style={{ cursor: 'pointer', margin: 0 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    void handleShip(row.id);
                  }}
                >
                  {t('ship')}
                </Tag>
                <Tag
                  color="green"
                  icon={<CheckCircleOutlined />}
                  style={{ cursor: 'pointer', margin: 0 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    void handleComplete(row.id);
                  }}
                >
                  {t('completeShortcut')}
                </Tag>
                <Tag
                  color="red"
                  icon={<StopOutlined />}
                  style={{ cursor: 'pointer', margin: 0 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    void handleCancel(row.id);
                  }}
                >
                  {ts('cancel')}
                </Tag>
              </>
            )}
            {row.status === 2 && (
              <>
                <Tag
                  color="green"
                  icon={<ImportOutlined />}
                  style={{ cursor: 'pointer', margin: 0 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    void handleOpenReceive(row.id);
                  }}
                >
                  {t('receive')}
                </Tag>
                <Tag
                  color="red"
                  icon={<StopOutlined />}
                  style={{ cursor: 'pointer', margin: 0 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    void handleCancel(row.id);
                  }}
                >
                  {ts('cancel')}
                </Tag>
              </>
            )}
          </Space>
        </Spin>
      ),
    },
  ];

  const warehouseOptions = warehouses.map((w) => ({
    value: w.id,
    label: w.branchName ? `${w.warehouseName} · ${w.branchName}` : w.warehouseName,
  }));

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
        <Alert type="info" showIcon style={{ marginBottom: 12 }} message={t('workflowHint')} />
        <ListFilterBar>
          <Input.Search
            allowClear
            placeholder={t('filters.searchPlaceholder')}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onSearch={(v) => {
              setSearch(v.trim());
              setPage(1);
            }}
            style={{ width: 200 }}
          />
          <Select
            allowClear
            placeholder={t('filters.status')}
            options={transferStatusOptions}
            value={statusFilter}
            onChange={(v) => {
              setStatusFilter(v);
              setPage(1);
            }}
            style={{ width: 160 }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('filters.fromWarehouse')}
            options={warehouseOptions}
            value={fromWarehouseFilter}
            onChange={(v) => {
              setFromWarehouseFilter(v);
              setPage(1);
            }}
            style={{ width: 220 }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('filters.toWarehouse')}
            options={warehouseOptions}
            value={toWarehouseFilter}
            onChange={(v) => {
              setToWarehouseFilter(v);
              setPage(1);
            }}
            style={{ width: 220 }}
          />
          <DatePicker.RangePicker
            value={dateRange}
            onChange={(v) => {
              setDateRange(v);
              setPage(1);
            }}
            format="DD-MM-YYYY"
            placeholder={[t('filters.dateRange'), '']}
          />
          <Select
            value={shortageOnly ? 'yes' : 'all'}
            onChange={(v) => {
              setShortageOnly(v === 'yes');
              setPage(1);
            }}
            options={[
              { value: 'all', label: t('filters.all') },
              { value: 'yes', label: t('filters.shortageOnly') },
            ]}
            style={{ width: 160 }}
          />
          <Button onClick={resetFilters}>{t('filters.reset')}</Button>
        </ListFilterBar>
        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={items}
          pagination={{
            current: page,
            pageSize,
            total,
            showSizeChanger: true,
            pageSizeOptions: [10, 20, 50, 100],
            showTotal: (count) => t('paginationTotal', { count: count.toLocaleString('vi-VN') }),
            onChange: (nextPage, nextPageSize) => {
              setPage(nextPage);
              setPageSize(nextPageSize);
            },
          }}
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
            {editingTransferNumber
              ? t('editTitle', { number: editingTransferNumber })
              : t('createTitle')}
          </span>
        }
        width={TRANSFER_DRAWER_WIDTH}
        open={drawerOpen}
        onClose={() => {
          setDrawerOpen(false);
          setEditingTransferId(null);
          setEditingTransferNumber(null);
        }}
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
            <Button
              onClick={() => {
                setDrawerOpen(false);
                setEditingTransferId(null);
                setEditingTransferNumber(null);
              }}
            >
              {tc('actions.cancel')}
            </Button>
            <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void handleSave()}>
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
                  {t('inlineEditHint')}
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
          detail && detail.status === 1 ? (
            <Space>
              <Button
                icon={<EditOutlined />}
                loading={actionBusyId === detail.id}
                onClick={() => void openEdit(detail.id)}
              >
                {t('edit')}
              </Button>
              <Button
                danger
                icon={<StopOutlined />}
                loading={actionBusyId === detail.id}
                onClick={() => void handleCancel(detail.id)}
              >
                {ts('cancel')}
              </Button>
              <Button
                icon={<SendOutlined />}
                loading={actionBusyId === detail.id}
                onClick={() => void handleShip(detail.id)}
              >
                {t('ship')}
              </Button>
              <Button
                type="primary"
                icon={<CheckCircleOutlined />}
                loading={actionBusyId === detail.id}
                onClick={() => void handleComplete(detail.id)}
              >
                {t('completeShortcut')}
              </Button>
            </Space>
          ) : detail && detail.status === 2 ? (
            <Space>
              <Button
                danger
                icon={<StopOutlined />}
                loading={actionBusyId === detail.id}
                onClick={() => void handleCancel(detail.id)}
              >
                {ts('cancel')}
              </Button>
              <Button
                type="primary"
                icon={<ImportOutlined />}
                onClick={() => openReceive(detail)}
              >
                {t('receive')}
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
              <strong>{tc('fields.status')}:</strong>{' '}
              <Space size={4}>
                <Tag
                  color={
                    detail.status === 3
                      ? 'green'
                      : detail.status === 2
                        ? 'processing'
                        : detail.status === 1
                          ? 'gold'
                          : 'default'
                  }
                >
                  {detail.status === 3 &&
                  detail.items.some(
                    (line) => line.receivedQuantity != null && line.receivedQuantity < line.quantity,
                  )
                    ? t('completedWithShortage')
                    : transferStatusLabel(detail.status)}
                </Tag>
                {detail.items.some(
                  (line) => line.receivedQuantity != null && line.receivedQuantity < line.quantity,
                ) ? (
                  <Tag color="orange">{t('shortageBadge')}</Tag>
                ) : null}
              </Space>
            </p>
            {detail.notes && (
              <p>
                <strong>{ts('notes')}:</strong> {detail.notes}
              </p>
            )}
            {detail.receiveNotes && (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                message={t('receiveNotes')}
                description={detail.receiveNotes}
              />
            )}
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              scroll={{ x: 860 }}
              dataSource={detail.items}
              rowClassName={(row) =>
                row.receivedQuantity != null && row.receivedQuantity < row.quantity
                  ? 'transfer-shortage-row'
                  : ''
              }
              columns={[
                {
                  title: ts('stt'),
                  key: 'stt',
                  width: 52,
                  align: 'center',
                  render: (_: unknown, __: TransferItem, index: number) => index + 1,
                },
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
                  title: t('shippedQty'),
                  dataIndex: 'quantity',
                  width: 90,
                  align: 'right',
                  render: (v: number) => formatDisplayQuantity(v),
                },
                {
                  title: t('receivedQty'),
                  dataIndex: 'receivedQuantity',
                  width: 110,
                  align: 'right',
                  render: (v: number | null | undefined, row: TransferItem) => {
                    if (v == null) return '—';
                    const short = v < row.quantity;
                    return (
                      <Typography.Text type={short ? 'danger' : undefined} strong={short}>
                        {formatDisplayQuantity(v)}
                        {short
                          ? ` (${t('shortageRowHint', { qty: formatDisplayQuantity(row.quantity - v) })})`
                          : ''}
                      </Typography.Text>
                    );
                  },
                },
              ]}
            />
          </>
        )}
      </Drawer>

      <Drawer
        title={t('receiveTitle')}
        width={720}
        open={receiveOpen}
        onClose={() => setReceiveOpen(false)}
        extra={
          <Space>
            <Button onClick={() => setReceiveOpen(false)}>{tc('actions.cancel')}</Button>
            <Button type="primary" loading={receiveBusy} onClick={() => void handleReceiveConfirm()}>
              {t('receive')}
            </Button>
          </Space>
        }
      >
        {detail ? (
          <>
            <Alert type="info" showIcon style={{ marginBottom: 12 }} message={t('receiveHint')} />
            {hasReceiveShortage ? (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                message={t('receiveShortageWarning', { qty: formatDisplayQuantity(receiveShortageQty) })}
                description={t('receiveShortageWarningHint')}
              />
            ) : null}
            <Space style={{ marginBottom: 12 }}>
              <Button size="small" onClick={fillReceiveShipped}>
                {t('fillReceiveShipped')}
              </Button>
            </Space>
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={detail.items}
              columns={[
                {
                  title: ts('stt'),
                  key: 'stt',
                  width: 52,
                  align: 'center',
                  render: (_: unknown, __: TransferItem, index: number) => index + 1,
                },
                { title: ts('productName'), dataIndex: 'productName', ellipsis: true },
                { title: ts('batchAbbr'), dataIndex: 'batchNumber', width: 110 },
                {
                  title: t('shippedQty'),
                  dataIndex: 'quantity',
                  width: 90,
                  align: 'right',
                  render: (v: number) => formatDisplayQuantity(v),
                },
                {
                  title: t('receivedQty'),
                  key: 'recv',
                  width: 120,
                  render: (_: unknown, row: TransferItem) => (
                    <InputNumber
                      min={0}
                      max={row.quantity}
                      value={receiveQtyByItem[row.id] ?? row.quantity}
                      onChange={(v) =>
                        setReceiveQtyByItem((prev) => ({
                          ...prev,
                          [row.id]: typeof v === 'number' ? v : 0,
                        }))
                      }
                      style={{ width: '100%' }}
                    />
                  ),
                },
              ]}
            />
            <Form.Item
              label={t('receiveNotes')}
              required={hasReceiveShortage}
              style={{ marginTop: 16 }}
            >
              <Input.TextArea
                rows={3}
                value={receiveNotes}
                onChange={(e) => setReceiveNotes(e.target.value)}
                placeholder={t('receiveNotesPlaceholder')}
              />
            </Form.Item>
            {hasReceiveShortage ? (
              <Typography.Text type="secondary">{t('shortageRestoredHint')}</Typography.Text>
            ) : null}
          </>
        ) : null}
      </Drawer>
    </>
  );
}

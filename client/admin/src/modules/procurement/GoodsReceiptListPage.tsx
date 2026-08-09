import { useCallback, useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Drawer,
  Form,
  InputNumber,
  Popconfirm,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Tooltip,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { FormListFieldData } from 'antd/es/form/FormList';
import { isAxiosError } from 'axios';
import dayjs from 'dayjs';
import { PlusOutlined, EyeOutlined, DeleteOutlined, SaveOutlined, FolderOpenOutlined, CheckOutlined, CloseCircleOutlined, EyeInvisibleOutlined, PrinterOutlined, EditOutlined } from '@ant-design/icons';
import { fetchProduct, fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import {
  cancelGoodsReceipt,
  completeGoodsReceipt,
  createGoodsReceipt,
  updateGoodsReceipt,
  archiveGoodsReceipt,
  fetchGoodsReceipt,
  fetchGoodsReceipts,
  fetchLastPurchasePriceHint,
  fetchPurchaseOrder,
  fetchPurchaseOrders,
  fetchSuppliers,
  fetchVatTreatments,
  purgeGoodsReceipt,
} from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  GoodsReceiptDetail,
  GoodsReceiptListFilters,
  GoodsReceiptListItem,
  ProcurementVatTreatment,
  PurchaseOrderDetail,
  PurchaseOrderListItem,
  Supplier,
} from '@/shared/api/procurement.types';
import { GRN_STATUS_TAG } from '@/shared/api/procurement.types';
import { useProcurementEnums } from '@/shared/i18n/use-procurement-enums';
import { PurchaseOrderEditDrawer } from '@/modules/procurement/PurchaseOrderEditDrawer';
import { GoodsReceiptFormHeader } from '@/modules/procurement/GoodsReceiptFormHeader';
import { GrnPoLinesEditor } from '@/modules/procurement/GrnPoLinesEditor';
import { GrnDetailView, GrnDetailLinesPanel } from '@/modules/procurement/GrnDetailView';
import { PROCUREMENT_DRAWER_WIDTH } from '@/modules/procurement/procurement-layout';
import {
  GrnLineDiscountFields,
  GrnPricingControls,
  GrnPricingSummaryPanel,
  PROCUREMENT_LINE_ACTION_COL_WIDTH,
} from '@/modules/procurement/GrnPricingPanel';
import { defaultVatTreatmentId } from '@/modules/procurement/po-vat';
import {
  grnLineNetTotal,
  isPlaceholderSupplier,
  PROCUREMENT_DISCOUNT_TYPES,
  type ProcurementDiscountType,
} from '@/modules/procurement/grn-pricing';
import { printGoodsReceipt } from '@/shared/print/grn-print';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { ProductSearchSelect } from '@/modules/procurement/ProductSearchSelect';
import { formatUnitLabel, pickDefaultProductUnitId } from '@/modules/procurement/product-unit.helpers';
import { GrnBatchNumberField } from '@/modules/procurement/GrnBatchNumberField';
import { PharmaExpiryPicker } from '@/shared/ui/PharmaDatePicker';
import { GoodsReceiptFilterBar } from '@/modules/procurement/GoodsReceiptFilterBar';
import { formatDisplayDate } from '@/shared/utils/date';
import { downloadCsv } from '@/shared/utils/download-csv';
import {
  formatDisplayMoney,
  formatDisplayQuantity,
  moneyInputNumberPropsAllowZeroSuffix,
  quantityInputNumberProps,
} from '@/shared/utils/money';
import { useProcurementWrite, useSystemDeletePermanent } from '@/shared/auth/usePermission';
import { PROCUREMENT_MONEY_COL_WIDTH } from '@/modules/procurement/GrnPoTaxSummary';

const emptyFilters: GoodsReceiptListFilters = {};

interface GrnLineForm {
  purchaseOrderItemId?: string;
  productId: string;
  productUnitId: string;
  productCode?: string;
  productName?: string;
  unitName?: string;
  orderedQty?: number;
  receivedQty?: number;
  batchNumber: string;
  expiryDate: string;
  quantity: number;
  unitCost: number;
  discountType?: number;
  discountValue?: number;
}

function todayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

function defaultExpiryDate(): string {
  const d = new Date();
  d.setFullYear(d.getFullYear() + 2);
  return d.toISOString().slice(0, 10);
}

type ManualLineCell = 'product' | 'unit' | 'batch' | 'expiry' | 'qty' | 'unitCost' | 'discount';

function productOptionLabel(p: { productCode: string; productName: string }): string {
  return `${p.productCode} — ${p.productName}`;
}

function formatExpiryMmYyyy(iso?: string): string {
  if (!iso) return '—';
  const parsed = dayjs(iso.length >= 10 ? iso.slice(0, 10) : iso);
  return parsed.isValid() ? parsed.format('MM/YYYY') : iso;
}

function formatLineDiscountText(
  discountType: ProcurementDiscountType | undefined,
  discountValue: number | undefined,
  emDash: string,
): string {
  if (!discountType || discountValue == null || discountValue <= 0) return emDash;
  if (discountType === PROCUREMENT_DISCOUNT_TYPES.Percent) return `${discountValue}%`;
  return formatDisplayMoney(discountValue);
}

function UnitNameLabel({ productId, unitId }: { productId?: string; unitId?: string }) {
  const [label, setLabel] = useState('—');

  useEffect(() => {
    if (!productId || !unitId) {
      setLabel('—');
      return;
    }
    let cancelled = false;
    void fetchProduct(productId)
      .then((product) => {
        if (cancelled) return;
        const unit = product.units.find((u) => u.id === unitId);
        setLabel(unit ? formatUnitLabel(unit) : '—');
      })
      .catch(() => {
        if (!cancelled) setLabel('—');
      });
    return () => {
      cancelled = true;
    };
  }, [productId, unitId]);

  return <span>{label}</span>;
}

function ManualLineClickCell({
  editing,
  onEdit,
  style,
  align = 'left',
  children,
  display,
}: {
  editing: boolean;
  onEdit: () => void;
  style?: CSSProperties;
  align?: 'left' | 'right' | 'center';
  children: ReactNode;
  display: ReactNode;
}) {
  return (
    <div
      role={editing ? undefined : 'button'}
      tabIndex={editing ? undefined : 0}
      onClick={() => {
        if (!editing) onEdit();
      }}
      onKeyDown={(e) => {
        if (!editing && (e.key === 'Enter' || e.key === ' ')) {
          e.preventDefault();
          onEdit();
        }
      }}
      style={{
        minHeight: 32,
        display: 'flex',
        alignItems: 'center',
        justifyContent: align === 'right' ? 'flex-end' : align === 'center' ? 'center' : 'flex-start',
        padding: editing ? 0 : '4px 6px',
        borderRadius: 6,
        cursor: editing ? 'default' : 'pointer',
        background: editing ? undefined : 'transparent',
        ...style,
      }}
      onMouseEnter={(e) => {
        if (!editing) e.currentTarget.style.background = '#f5f5f5';
      }}
      onMouseLeave={(e) => {
        if (!editing) e.currentTarget.style.background = 'transparent';
      }}
    >
      <div style={{ display: editing ? 'block' : 'none', width: '100%' }} onClick={(e) => e.stopPropagation()}>
        {children}
      </div>
      {!editing && (
        <Typography.Text ellipsis style={{ maxWidth: '100%' }} title={typeof display === 'string' ? display : undefined}>
          {display}
        </Typography.Text>
      )}
    </div>
  );
}

function buildGrnLinesFromPo(po: PurchaseOrderDetail): GrnLineForm[] {
  const expiry = defaultExpiryDate();
  return po.items
    .filter((line) => line.receivedQty < line.orderedQty)
    .map((line) => ({
      purchaseOrderItemId: line.id,
      productId: line.productId,
      productUnitId: line.productUnitId,
      productCode: line.productCode,
      productName: line.productName,
      unitName: line.unitName,
      orderedQty: line.orderedQty,
      receivedQty: line.receivedQty,
      batchNumber: '',
      expiryDate: expiry,
      quantity: line.orderedQty - line.receivedQty,
      unitCost: line.unitPrice,
    }));
}

export function GoodsReceiptListPage() {
  const { t } = useTranslation('procurement', { keyPrefix: 'goodsReceipts' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const { grnStatusLabel } = useProcurementEnums();
  const canWrite = useProcurementWrite();
  const canPurge = useSystemDeletePermanent();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<GoodsReceiptListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [vatTreatments, setVatTreatments] = useState<ProcurementVatTreatment[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [allPurchaseOrders, setAllPurchaseOrders] = useState<PurchaseOrderListItem[]>([]);
  const [approvedPos, setApprovedPos] = useState<PurchaseOrderListItem[]>([]);
  const [filters, setFilters] = useState<GoodsReceiptListFilters>(emptyFilters);
  const [searchInput, setSearchInput] = useState('');
  const [linkedPo, setLinkedPo] = useState<PurchaseOrderDetail | null>(null);
  const [poDraftGrn, setPoDraftGrn] = useState<GoodsReceiptListItem | null>(null);
  const [poLoading, setPoLoading] = useState(false);
  const [grnDetailCache, setGrnDetailCache] = useState<Record<string, GoodsReceiptDetail>>({});
  const grnDetailCacheRef = useRef(grnDetailCache);
  grnDetailCacheRef.current = grnDetailCache;
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<GoodsReceiptDetail | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [poEditOpen, setPoEditOpen] = useState(false);
  const [editingGrnId, setEditingGrnId] = useState<string | null>(null);
  const [editingGrnNumber, setEditingGrnNumber] = useState<string | null>(null);
  const purchaseOrderId = Form.useWatch('purchaseOrderId', form);
  const supplierId = Form.useWatch('supplierId', form);
  const warehouseId = Form.useWatch('warehouseId', form) as string | undefined;
  const watchedItems = Form.useWatch('items', form) as GrnLineForm[] | undefined;

  const [draftProductId, setDraftProductId] = useState<string | undefined>();
  const [draftUnitId, setDraftUnitId] = useState<string | undefined>();
  const [draftBatch, setDraftBatch] = useState('');
  const [draftExpiry, setDraftExpiry] = useState(defaultExpiryDate);
  const [draftQty, setDraftQty] = useState(1);
  const [draftUnitCost, setDraftUnitCost] = useState(0);
  const [draftDiscountType, setDraftDiscountType] = useState<ProcurementDiscountType | undefined>();
  const [draftDiscountValue, setDraftDiscountValue] = useState<number | undefined>(undefined);
  const [editingManualCell, setEditingManualCell] = useState<{ rowKey: number; cell: ManualLineCell } | null>(
    null,
  );

  const resetManualComposer = () => {
    setDraftProductId(undefined);
    setDraftUnitId(undefined);
    setDraftBatch('');
    setDraftExpiry(defaultExpiryDate());
    setDraftQty(1);
    setDraftUnitCost(0);
    setDraftDiscountType(undefined);
    setDraftDiscountValue(undefined);
  };

  const applyManualComposerProduct = (productId: string) => {
    setDraftUnitId(undefined);
    setDraftUnitCost(0);
    void fetchProduct(productId)
      .then((product) => {
        setDraftUnitId(pickDefaultProductUnitId(product.units));
      })
      .catch(() => undefined);
    if (supplierId) {
      void fetchLastPurchasePriceHint(supplierId, productId)
        .then((h) => {
          if (h.unitPrice != null) setDraftUnitCost(h.unitPrice);
        })
        .catch(() => undefined);
    }
  };

  const loadMasterData = useCallback(async () => {
    const [sup, wh, prod, pos, pendingPos, vat] = await Promise.all([
      fetchSuppliers(true),
      fetchWarehouses(),
      fetchProducts({ page: 1, pageSize: 200 }),
      fetchPurchaseOrders({ page: 1, pageSize: 500 }),
      fetchPurchaseOrders({ pendingReceiptOnly: true, page: 1, pageSize: 500 }),
      fetchVatTreatments(),
    ]);
    setSuppliers(sup);
    setVatTreatments(vat);
    setWarehouses(wh);
    setProducts(prod.items);
    setAllPurchaseOrders(pos.items);
    setApprovedPos(pendingPos.items);
  }, []);

  const loadReceipts = useCallback(async (
    nextFilters: GoodsReceiptListFilters,
    search: string,
    nextPage = 1,
    nextPageSize = pageSize,
  ) => {
    setFilters(nextFilters);
    setSearchInput(search);
    setPage(nextPage);
    setPageSize(nextPageSize);
    setLoading(true);
    try {
      const result = await fetchGoodsReceipts({
        ...nextFilters,
        search: search.trim() || undefined,
        page: nextPage,
        pageSize: nextPageSize,
      });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [pageSize, t]);

  useEffect(() => {
    void loadMasterData().catch(() => {
      message.error(tShared('messages.loadReferenceFailed'));
    });
    void loadReceipts(emptyFilters, '');
  }, [loadMasterData, loadReceipts, tShared]);

  const resetFilters = () => {
    void loadReceipts(emptyFilters, '');
  };

  const exportReceipts = () => {
    if (items.length === 0) {
      message.info(tShared('messages.noExportData'));
      return;
    }
    downloadCsv(
      `phieu-nhap-hang-${new Date().toISOString().slice(0, 10)}.csv`,
      [
        t('exportColumns.grnNumber'),
        t('exportColumns.supplier'),
        t('exportColumns.warehouse'),
        t('exportColumns.poNumber'),
        t('exportColumns.status'),
        t('exportColumns.receiptDate'),
        t('exportColumns.itemCount'),
      ],
      items.map((row) => [
        row.grnNumber,
        row.supplierName,
        row.warehouseName,
        row.poNumber ?? tShared('emDash'),
        grnStatusLabel(row.status),
        formatDisplayDate(row.receiptDate),
        String(row.itemCount),
      ]),
    );
  };

  useEffect(() => {
    if (editingGrnId) return;

    if (!purchaseOrderId) {
      setLinkedPo(null);
      setPoDraftGrn(null);
      setPoLoading(false);
      return;
    }

    let cancelled = false;
    setPoLoading(true);
    setLinkedPo(null);
    setPoDraftGrn(null);
    form.setFieldsValue({ items: [] });

    fetchGoodsReceipts({ purchaseOrderId, status: 1, page: 1, pageSize: 20 })
      .then(async (result) => {
        if (cancelled) return;
        const draft = result.items[0];
        if (draft) {
          setPoDraftGrn(draft);
          return;
        }

        const po = await fetchPurchaseOrder(purchaseOrderId);
        if (cancelled) return;
        setLinkedPo(po);
        const lines = buildGrnLinesFromPo(po);
        form.setFieldsValue({
          supplierId: po.supplierId,
          warehouseId: po.warehouseId,
          vatTreatmentId: po.vatTreatmentId || defaultVatTreatmentId(vatTreatments),
          items: lines,
        });
        if (lines.length === 0) {
          message.info(t('poFullyReceivedInfo'));
        }
      })
      .catch(() => {
        if (!cancelled) {
          setLinkedPo(null);
          setPoDraftGrn(null);
          message.error(t('poLoadError'));
        }
      })
      .finally(() => {
        if (!cancelled) setPoLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [purchaseOrderId, editingGrnId, form, t, vatTreatments]);

  const handlePoEdited = (po: PurchaseOrderDetail) => {
    setLinkedPo(po);
    const lines = buildGrnLinesFromPo(po);
    const currentItems = (form.getFieldValue('items') as GrnLineForm[] | undefined) ?? [];
    const merged = lines.map((line) => {
      const prev = currentItems.find((i) => i.purchaseOrderItemId === line.purchaseOrderItemId);
      if (!prev) return line;
      return {
        ...line,
        batchNumber: prev.batchNumber,
        expiryDate: prev.expiryDate,
        quantity: prev.quantity,
        unitCost: prev.unitCost,
      };
    });
    form.setFieldsValue({ items: merged });
    void loadMasterData();
  };

  const openCreate = () => {
    form.resetFields();
    form.setFieldsValue({
      receiptDate: todayDateString(),
      vatTreatmentId: defaultVatTreatmentId(vatTreatments),
      items: [],
    });
    resetManualComposer();
    setEditingManualCell(null);
    setEditingGrnId(null);
    setEditingGrnNumber(null);
    setLinkedPo(null);
    setPoDraftGrn(null);
    setPoLoading(false);
    setDrawerOpen(true);
  };

  const openEdit = async (grn: GoodsReceiptDetail) => {
    if (grn.status !== 1) {
      message.warning(t('editOnlyDraft'));
      return;
    }
    setDetailOpen(false);
    setEditingGrnId(grn.id);
    setEditingGrnNumber(grn.grnNumber);
    setPoDraftGrn(null);
    resetManualComposer();
    setEditingManualCell(null);
    form.resetFields();
    form.setFieldsValue({
      purchaseOrderId: grn.purchaseOrderId,
      supplierId: grn.supplierId,
      warehouseId: grn.warehouseId,
      receiptDate: grn.receiptDate?.slice(0, 10) || todayDateString(),
      notes: grn.notes,
      supplierInvoiceNumber: grn.supplierInvoiceNumber,
      vatTreatmentId: grn.vatTreatmentId || defaultVatTreatmentId(vatTreatments),
      orderDiscountType: grn.orderDiscountType || undefined,
      orderDiscountValue: grn.orderDiscountValue || undefined,
      items: grn.items.map((line) => ({
        purchaseOrderItemId: line.purchaseOrderItemId,
        productId: line.productId,
        productUnitId: line.productUnitId,
        productCode: line.productCode,
        productName: line.productName,
        unitName: line.unitName,
        batchNumber: line.batchNumber,
        expiryDate: line.expiryDate?.slice(0, 10) || defaultExpiryDate(),
        quantity: line.quantity,
        unitCost: line.unitCost,
        discountType: line.discountType || undefined,
        discountValue: line.discountValue || undefined,
      })),
    });

    if (grn.purchaseOrderId) {
      setPoLoading(true);
      try {
        const po = await fetchPurchaseOrder(grn.purchaseOrderId);
        setLinkedPo(po);
        const byPoItemId = new Map(po.items.map((line) => [line.id, line]));
        form.setFieldsValue({
          supplierId: po.supplierId,
          warehouseId: po.warehouseId,
          items: grn.items.map((line) => {
            const poLine = line.purchaseOrderItemId
              ? byPoItemId.get(line.purchaseOrderItemId)
              : undefined;
            return {
              purchaseOrderItemId: line.purchaseOrderItemId,
              productId: line.productId,
              productUnitId: line.productUnitId,
              productCode: line.productCode || poLine?.productCode,
              productName: line.productName || poLine?.productName,
              unitName: line.unitName || poLine?.unitName,
              orderedQty: poLine?.orderedQty ?? line.quantity,
              receivedQty: poLine?.receivedQty ?? 0,
              batchNumber: line.batchNumber,
              expiryDate: line.expiryDate?.slice(0, 10) || defaultExpiryDate(),
              quantity: line.quantity,
              unitCost: line.unitCost,
              discountType: line.discountType || undefined,
              discountValue: line.discountValue || undefined,
            };
          }),
        });
      } catch {
        setLinkedPo(null);
        message.error(t('poLoadError'));
      } finally {
        setPoLoading(false);
      }
    } else {
      setLinkedPo(null);
      setPoLoading(false);
    }
    setDrawerOpen(true);
  };

  const openExistingDraftGrn = async (id: string) => {
    setDrawerOpen(false);
    await openDetail(id);
  };

  const loadGrnExpand = async (id: string) => {
    if (grnDetailCache[id]) return;
    try {
      const grn = await fetchGoodsReceipt(id);
      setGrnDetailCache((cache) => ({ ...cache, [id]: grn }));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    }
  };

  const openDetail = async (id: string) => {
    try {
      const grn = await fetchGoodsReceipt(id);
      setDetail(grn);
      setDetailOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    }
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      const lines = (values.items as GrnLineForm[]).filter((i) => i.quantity > 0);
      if (lines.length === 0) {
        message.warning(t('minOneLine'));
        return;
      }
      const supplier = suppliers.find((s) => s.id === values.supplierId);
      if (!supplier || isPlaceholderSupplier(supplier)) {
        message.warning(t('realSupplierRequired'));
        return;
      }
      setSaving(true);
      const payload = {
        supplierId: values.supplierId as string,
        warehouseId: values.warehouseId as string,
        receiptDate: values.receiptDate || todayDateString(),
        notes: values.notes as string | undefined,
        supplierInvoiceNumber: values.supplierInvoiceNumber as string | undefined,
        vatTreatmentId: values.vatTreatmentId as string,
        orderDiscountType: values.orderDiscountType as number | undefined,
        orderDiscountValue: values.orderDiscountValue as number | undefined,
        items: lines.map((i) => ({
          purchaseOrderItemId: i.purchaseOrderItemId,
          productId: i.productId,
          productUnitId: i.productUnitId,
          batchNumber: i.batchNumber,
          expiryDate: i.expiryDate,
          quantity: i.quantity,
          unitCost: i.unitCost,
          discountType: i.discountType,
          discountValue: i.discountValue,
        })),
      };

      if (editingGrnId) {
        const updated = await updateGoodsReceipt(editingGrnId, payload);
        message.success(t('messages.updated', { grnNumber: updated.grnNumber }));
        setDrawerOpen(false);
        setEditingGrnId(null);
        setEditingGrnNumber(null);
        setDetail(updated);
        setDetailOpen(true);
        void loadReceipts(filters, searchInput, page, pageSize);
        void loadMasterData();
        return;
      }

      const created = await createGoodsReceipt({
        purchaseOrderId: values.purchaseOrderId,
        ...payload,
      });
      message.success(t('messages.created', { grnNumber: created.grnNumber }));
      setDrawerOpen(false);
      void loadReceipts(filters, searchInput, page, pageSize);
      void loadMasterData();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(
          apiErrorMessage(error, editingGrnId ? t('messages.updateFailed') : t('messages.createFailed')),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  const handleComplete = async (id: string) => {
    try {
      const updated = await completeGoodsReceipt(id);
      message.success(t('messages.completed', { grnNumber: updated.grnNumber }));
      if (detail?.id === id) setDetail(updated);
      void loadReceipts(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.completeFailed')));
    }
  };

  const handleCancel = async (id: string) => {
    try {
      const updated = await cancelGoodsReceipt(id);
      message.success(t('messages.cancelled', { grnNumber: updated.grnNumber }));
      setDetail(updated);
      void loadReceipts(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    }
  };

  const handleArchive = async (id: string) => {
    try {
      await archiveGoodsReceipt(id);
      message.success(t('messages.archived'));
      setDetailOpen(false);
      setDetail(null);
      void loadReceipts(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.archiveFailed')));
    }
  };

  const handlePurge = async (id: string) => {
    try {
      await purgeGoodsReceipt(id);
      message.success(t('messages.purged'));
      setDetailOpen(false);
      setDetail(null);
      void loadReceipts(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.purgeFailed')));
    }
  };

  const canArchiveGrn = (status: number, deletedAt?: string) => status === 3 && !deletedAt;
  const showLockedDeleteGrn = (status: number, deletedAt?: string) => status === 2 && !deletedAt;

  const columns: ColumnsType<GoodsReceiptListItem> = [
    { title: tShared('columns.grnNumber'), dataIndex: 'grnNumber', width: 140 },
    { title: tShared('columns.supplierShort'), dataIndex: 'supplierName' },
    { title: tShared('columns.warehouse'), dataIndex: 'warehouseName' },
    { title: tShared('columns.purchaseOrder'), dataIndex: 'poNumber', width: 120, render: (v) => v ?? tShared('emDash') },
    {
      title: tShared('columns.receiptDate'),
      dataIndex: 'receiptDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: tShared('columns.status'),
      dataIndex: 'status',
      width: 110,
      render: (s: number, row) => (
        <Space size={4}>
          <Tag color={GRN_STATUS_TAG[s] ?? 'default'}>{grnStatusLabel(s)}</Tag>
          {row.deletedAt ? <Tag color="default">{tShared('archived')}</Tag> : null}
        </Space>
      ),
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
            void openDetail(row.id);
          }}
        >
          {tCommon('view')}
        </Button>
      ),
    },
  ];

  const renderManualLines = (
    fields: FormListFieldData[],
    add: (defaultValue?: Partial<GrnLineForm>, insertIndex?: number) => void,
    remove: (index: number) => void,
  ) => {
    const draftLineTotal = grnLineNetTotal({
      quantity: draftQty,
      unitCost: draftUnitCost,
      discountType: draftDiscountType,
      discountValue: draftDiscountValue,
    });

    const addLineFromComposer = () => {
      if (!draftProductId) {
        message.warning(tVal('selectProduct'));
        return;
      }
      if (!draftUnitId) {
        message.warning(tVal('selectUnit'));
        return;
      }
      const batch = draftBatch.trim();
      if (!batch) {
        message.warning(tVal('enterBatch'));
        return;
      }
      if (!draftExpiry) {
        message.warning(tVal('selectExpiry'));
        return;
      }
      if (draftQty == null || draftQty <= 0) {
        message.warning(tVal('qtyPositive'));
        return;
      }
      if (draftUnitCost == null || draftUnitCost < 0) {
        message.warning(tVal('enterPrice'));
        return;
      }
      const seed = products.find((p) => p.id === draftProductId);
      add({
        productId: draftProductId,
        productUnitId: draftUnitId,
        productCode: seed?.productCode,
        productName: seed?.productName,
        batchNumber: batch,
        expiryDate: draftExpiry,
        quantity: draftQty,
        unitCost: draftUnitCost,
        discountType: draftDiscountType,
        discountValue: draftDiscountValue,
      });
      resetManualComposer();
      setEditingManualCell(null);
    };

    const isCellEditing = (rowKey: number, cell: ManualLineCell) =>
      editingManualCell?.rowKey === rowKey && editingManualCell.cell === cell;

    const headerCellStyle: CSSProperties = {
      fontSize: 12,
      color: '#64748b',
      fontWeight: 500,
      padding: '0 6px 6px',
    };

    return (
      <>
        <div
          style={{
            marginBottom: 12,
            padding: 12,
            border: '1px dashed #d9d9d9',
            borderRadius: 8,
            background: '#fafafa',
          }}
        >
          <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 8 }}>
            {t('manualLinesComposerHint')}
          </Typography.Text>
          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: 8,
              alignItems: 'flex-end',
            }}
          >
            <div style={{ flex: '2 1 240px', minWidth: 200 }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.product')}</Typography.Text>
              <ProductSearchSelect
                value={draftProductId}
                seedProducts={products}
                placeholder={t('productSearchPlaceholder')}
                style={{ width: '100%' }}
                onChange={(value) => {
                  setDraftProductId(value);
                  if (!value) {
                    resetManualComposer();
                    return;
                  }
                  applyManualComposerProduct(value);
                }}
              />
            </div>
            <div style={{ flex: '0 0 84px' }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.unit')}</Typography.Text>
              <ProductUnitSelect
                productId={draftProductId}
                value={draftUnitId}
                onChange={setDraftUnitId}
                width={84}
              />
            </div>
            <div style={{ flex: '0 0 140px' }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.batchNumber')}</Typography.Text>
              <GrnBatchNumberField
                value={draftBatch}
                warehouseId={warehouseId}
                productId={draftProductId}
                onChange={setDraftBatch}
                onPickExisting={(batchPick) => {
                  setDraftBatch(batchPick.batchNumber);
                  if (batchPick.expiryDate) setDraftExpiry(batchPick.expiryDate);
                }}
              />
            </div>
            <div style={{ flex: '0 0 112px' }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.expiry')}</Typography.Text>
              <PharmaExpiryPicker value={draftExpiry} onChange={setDraftExpiry} style={{ width: 112 }} />
            </div>
            <div style={{ flex: '0 0 80px' }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.qty')}</Typography.Text>
              <InputNumber
                {...quantityInputNumberProps}
                min={0.001}
                value={draftQty}
                onChange={(v) => setDraftQty(Number(v ?? 0))}
                style={{ width: '100%' }}
              />
            </div>
            <div style={{ flex: '0 0 120px' }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.unitCost')}</Typography.Text>
              <InputNumber
                {...moneyInputNumberPropsAllowZeroSuffix}
                value={draftUnitCost}
                onChange={(v) => setDraftUnitCost(Number(v ?? 0))}
                style={{ width: '100%' }}
              />
            </div>
            <div style={{ flex: '0 0 156px' }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.lineDiscount')}</Typography.Text>
              <Space size={4}>
                <Select
                  allowClear
                  size="small"
                  placeholder={tShared('columns.discount')}
                  style={{ width: 68 }}
                  value={draftDiscountType}
                  onChange={(v) => {
                    setDraftDiscountType(v);
                    if (!v) setDraftDiscountValue(undefined);
                  }}
                  options={[
                    { value: PROCUREMENT_DISCOUNT_TYPES.Percent, label: tShared('discount.percentSymbol') },
                    { value: PROCUREMENT_DISCOUNT_TYPES.Fixed, label: tShared('discount.moneySymbol') },
                  ]}
                />
                <InputNumber
                  min={0}
                  size="small"
                  disabled={!draftDiscountType}
                  value={draftDiscountValue}
                  onChange={(v) => setDraftDiscountValue(v == null ? undefined : Number(v))}
                  style={{ width: 76 }}
                  placeholder="0"
                />
              </Space>
            </div>
            <div style={{ flex: `0 0 ${PROCUREMENT_MONEY_COL_WIDTH}px` }}>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.lineTotal')}</Typography.Text>
              <div
                style={{
                  height: 32,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'flex-end',
                  padding: '0 8px',
                  background: '#fff',
                  border: '1px solid #d9d9d9',
                  borderRadius: 6,
                  fontWeight: 600,
                }}
              >
                {formatDisplayMoney(draftLineTotal)}
              </div>
            </div>
            <Button type="primary" icon={<PlusOutlined />} onClick={addLineFromComposer}>
              {tShared('lines.addLine')}
            </Button>
          </div>
        </div>

        {fields.length === 0 ? (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
            {t('manualLinesEmpty')}
          </Typography.Text>
        ) : (
          <div
            style={{
              display: 'flex',
              gap: 8,
              alignItems: 'flex-end',
              marginBottom: 4,
              paddingRight: PROCUREMENT_LINE_ACTION_COL_WIDTH,
            }}
          >
            <div style={{ flex: '0 0 28px', ...headerCellStyle, textAlign: 'center' }}>{tShared('columns.stt')}</div>
            <div style={{ flex: '2 1 320px', minWidth: 240, ...headerCellStyle }}>{tShared('columns.product')}</div>
            <div style={{ flex: '0 0 84px', ...headerCellStyle }}>{tShared('columns.unit')}</div>
            <div style={{ flex: '0 0 140px', ...headerCellStyle }}>{tShared('columns.batchNumber')}</div>
            <div style={{ flex: '0 0 112px', ...headerCellStyle }}>{tShared('columns.expiry')}</div>
            <div style={{ flex: '0 0 80px', ...headerCellStyle, textAlign: 'right' }}>{tShared('columns.qty')}</div>
            <div style={{ flex: '0 0 120px', ...headerCellStyle, textAlign: 'right' }}>{tShared('columns.unitCost')}</div>
            <div style={{ flex: '0 0 156px', ...headerCellStyle }}>{tShared('columns.lineDiscount')}</div>
            <div style={{ flex: `0 0 ${PROCUREMENT_MONEY_COL_WIDTH}px`, ...headerCellStyle, textAlign: 'right' }}>
              {tShared('columns.lineTotal')}
            </div>
          </div>
        )}

        {fields.map((field, index) => (
          <Form.Item key={field.key} noStyle shouldUpdate>
            {() => {
              const productId = form.getFieldValue(['items', field.name, 'productId']) as string | undefined;
              const line = watchedItems?.[field.name];
              const lineTotal = grnLineNetTotal(
                line
                  ? {
                      quantity: line.quantity,
                      unitCost: line.unitCost,
                      discountType: line.discountType as ProcurementDiscountType | undefined,
                      discountValue: line.discountValue,
                    }
                  : undefined,
              );
              const productLabel =
                line?.productCode && line?.productName
                  ? productOptionLabel({ productCode: line.productCode, productName: line.productName })
                  : products.find((p) => p.id === productId)
                    ? productOptionLabel(products.find((p) => p.id === productId)!)
                    : (productId ?? tShared('emDash'));
              const openCell = (cell: ManualLineCell) => setEditingManualCell({ rowKey: field.key, cell });

              return (
                <div
                  style={{
                    display: 'flex',
                    gap: 8,
                    alignItems: 'center',
                    marginBottom: 8,
                    padding: '6px 0',
                    borderBottom: '1px solid #f0f0f0',
                  }}
                >
                  <div
                    style={{
                      flex: '0 0 28px',
                      textAlign: 'center',
                      color: '#64748b',
                      fontVariantNumeric: 'tabular-nums',
                    }}
                    title={tShared('columns.stt')}
                  >
                    {index + 1}
                  </div>

                  <ManualLineClickCell
                    editing={isCellEditing(field.key, 'product')}
                    onEdit={() => openCell('product')}
                    style={{ flex: '2 1 320px', minWidth: 240 }}
                    display={productLabel}
                  >
                    <Form.Item
                      {...field}
                      name={[field.name, 'productId']}
                      rules={[{ required: true, message: tVal('selectProduct') }]}
                      style={{ marginBottom: 0 }}
                    >
                      <ProductSearchSelect
                        seedProducts={products}
                        placeholder={t('productSearchPlaceholder')}
                        afterChange={(value) => {
                          form.setFieldValue(['items', field.name, 'productUnitId'], undefined);
                          const seed = value ? products.find((p) => p.id === value) : undefined;
                          form.setFieldValue(['items', field.name, 'productCode'], seed?.productCode);
                          form.setFieldValue(['items', field.name, 'productName'], seed?.productName);
                          setEditingManualCell(null);
                        }}
                      />
                    </Form.Item>
                  </ManualLineClickCell>

                  <ManualLineClickCell
                    editing={isCellEditing(field.key, 'unit')}
                    onEdit={() => openCell('unit')}
                    style={{ flex: '0 0 84px' }}
                    display={
                      line?.unitName ? (
                        line.unitName
                      ) : (
                        <UnitNameLabel productId={productId} unitId={line?.productUnitId} />
                      )
                    }
                  >
                    <Form.Item
                      {...field}
                      name={[field.name, 'productUnitId']}
                      rules={[{ required: true, message: tVal('selectUnit') }]}
                      style={{ marginBottom: 0 }}
                    >
                      <ProductUnitSelect
                        productId={productId}
                        width={84}
                        onChange={() => setEditingManualCell(null)}
                      />
                    </Form.Item>
                  </ManualLineClickCell>

                  <ManualLineClickCell
                    editing={isCellEditing(field.key, 'batch')}
                    onEdit={() => openCell('batch')}
                    style={{ flex: '0 0 140px' }}
                    display={line?.batchNumber || tShared('emDash')}
                  >
                    <Form.Item
                      {...field}
                      name={[field.name, 'batchNumber']}
                      rules={[{ required: true, message: tVal('enterBatch') }]}
                      style={{ marginBottom: 0 }}
                    >
                      <GrnBatchNumberField
                        warehouseId={warehouseId}
                        productId={productId}
                        onPickExisting={(batchPick) => {
                          if (batchPick.expiryDate) {
                            form.setFieldValue(['items', field.name, 'expiryDate'], batchPick.expiryDate);
                          }
                          setEditingManualCell(null);
                        }}
                      />
                    </Form.Item>
                  </ManualLineClickCell>

                  <ManualLineClickCell
                    editing={isCellEditing(field.key, 'expiry')}
                    onEdit={() => openCell('expiry')}
                    style={{ flex: '0 0 112px' }}
                    display={formatExpiryMmYyyy(line?.expiryDate)}
                  >
                    <Form.Item
                      {...field}
                      name={[field.name, 'expiryDate']}
                      rules={[{ required: true, message: tVal('selectExpiry') }]}
                      style={{ marginBottom: 0 }}
                    >
                      <PharmaExpiryPicker
                        style={{ width: 112 }}
                        onChange={() => setEditingManualCell(null)}
                      />
                    </Form.Item>
                  </ManualLineClickCell>

                  <ManualLineClickCell
                    editing={isCellEditing(field.key, 'qty')}
                    onEdit={() => openCell('qty')}
                    style={{ flex: '0 0 80px' }}
                    align="right"
                    display={formatDisplayQuantity(line?.quantity)}
                  >
                    <Form.Item
                      {...field}
                      name={[field.name, 'quantity']}
                      rules={[{ required: true }]}
                      style={{ marginBottom: 0 }}
                    >
                      <InputNumber
                        {...quantityInputNumberProps}
                        min={0.001}
                        style={{ width: '100%' }}
                        autoFocus
                        onBlur={() => setEditingManualCell(null)}
                        onPressEnter={() => setEditingManualCell(null)}
                      />
                    </Form.Item>
                  </ManualLineClickCell>

                  <ManualLineClickCell
                    editing={isCellEditing(field.key, 'unitCost')}
                    onEdit={() => openCell('unitCost')}
                    style={{ flex: '0 0 120px' }}
                    align="right"
                    display={formatDisplayMoney(line?.unitCost)}
                  >
                    <Form.Item
                      {...field}
                      name={[field.name, 'unitCost']}
                      rules={[{ required: true }]}
                      style={{ marginBottom: 0 }}
                    >
                      <InputNumber
                        {...moneyInputNumberPropsAllowZeroSuffix}
                        style={{ width: '100%' }}
                        autoFocus
                        onBlur={() => setEditingManualCell(null)}
                        onPressEnter={() => setEditingManualCell(null)}
                      />
                    </Form.Item>
                  </ManualLineClickCell>

                  <ManualLineClickCell
                    editing={isCellEditing(field.key, 'discount')}
                    onEdit={() => openCell('discount')}
                    style={{ flex: '0 0 156px' }}
                    display={formatLineDiscountText(
                      line?.discountType as ProcurementDiscountType | undefined,
                      line?.discountValue,
                      tShared('emDash'),
                    )}
                  >
                    <GrnLineDiscountFields fieldName={field.name} />
                  </ManualLineClickCell>

                  <div
                    style={{
                      flex: `0 0 ${PROCUREMENT_MONEY_COL_WIDTH}px`,
                      textAlign: 'right',
                      fontWeight: 600,
                      fontVariantNumeric: 'tabular-nums',
                      padding: '0 6px',
                    }}
                  >
                    {formatDisplayMoney(lineTotal)}
                  </div>

                  <div style={{ flex: `0 0 ${PROCUREMENT_LINE_ACTION_COL_WIDTH}px`, textAlign: 'center' }}>
                    <Button
                      type="text"
                      danger
                      icon={<DeleteOutlined />}
                      aria-label={tShared('lines.removeLineAria')}
                      onClick={() => {
                        if (editingManualCell?.rowKey === field.key) setEditingManualCell(null);
                        remove(field.name);
                      }}
                    />
                  </div>
                </div>
              );
            }}
          </Form.Item>
        ))}
      </>
    );
  };

  return (
    <Card
      title={t('title')}
      extra={
        canWrite ? (
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('create')}
          </Button>
        ) : undefined
      }
    >
      <GoodsReceiptFilterBar
        filters={filters}
        searchInput={searchInput}
        suppliers={suppliers}
        warehouses={warehouses}
        products={products}
        purchaseOrders={allPurchaseOrders}
        loading={loading}
        onSearchInputChange={setSearchInput}
        onApply={loadReceipts}
        onReset={resetFilters}
        onExport={exportReceipts}
      />

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
          showTotal: (totalCount) => tShared('pagination.receipts', { count: totalCount }),
          onChange: (nextPage, nextPageSize) => {
            void loadReceipts(filters, searchInput, nextPage, nextPageSize);
          },
        }}
        scroll={{ x: 900 }}
        onRow={(record) => ({
          onClick: () => void openDetail(record.id),
          style: { cursor: 'pointer' },
        })}
        expandable={{
          onExpand: (expanded, record) => {
            if (expanded) void loadGrnExpand(record.id);
          },
          expandedRowRender: (record) => {
            const grn = grnDetailCache[record.id];
            if (!grn) return <Spin size="small" />;
            return <GrnDetailLinesPanel detail={grn} />;
          },
        }}
      />

      <Drawer
        title={
          editingGrnId
            ? t('editDrawerWithNumber', { grnNumber: editingGrnNumber ?? '' })
            : t('createDrawer')
        }
        width={PROCUREMENT_DRAWER_WIDTH}
        open={drawerOpen}
        onClose={() => {
          setDrawerOpen(false);
          setEditingGrnId(null);
          setEditingGrnNumber(null);
          setLinkedPo(null);
          setPoDraftGrn(null);
          form.resetFields();
          resetManualComposer();
          setEditingManualCell(null);
        }}
        styles={{ body: { paddingTop: 8, paddingBottom: 8, display: 'flex', flexDirection: 'column', overflow: 'hidden' } }}
        extra={
          <Button
            type="primary"
            icon={<SaveOutlined />}
            onClick={() => void handleSave()}
            loading={saving}
            disabled={!!poDraftGrn && !editingGrnId}
          >
            {t('saveReceipt')}
          </Button>
        }
      >
        <Form
          form={form}
          layout="vertical"
          style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, overflow: 'hidden' }}
        >
          <GoodsReceiptFormHeader
            suppliers={suppliers}
            warehouses={warehouses}
            approvedPos={editingGrnId ? allPurchaseOrders : approvedPos}
            purchaseOrderId={purchaseOrderId}
            linkedPo={linkedPo}
            poLoading={poLoading}
            lockPoLink={Boolean(editingGrnId)}
            onEditPo={() => setPoEditOpen(true)}
          />
          {!poDraftGrn && <GrnPricingControls vatTreatments={vatTreatments} />}
          <div style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
            {poDraftGrn && (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 8 }}
                message={t('draftExistsTitle', { grnNumber: poDraftGrn.grnNumber })}
                description={t('draftExistsDescription')}
                action={
                  <Button size="small" icon={<FolderOpenOutlined />} onClick={() => void openExistingDraftGrn(poDraftGrn.id)}>
                    {t('openDraft')}
                  </Button>
                }
              />
            )}
            <Form.List name="items">
              {(fields, { add, remove }) => {
                if (poDraftGrn) {
                  return null;
                }
                if (!purchaseOrderId) {
                  return renderManualLines(fields, add, remove);
                }
                if (poLoading) {
                  return (
                    <div style={{ padding: '24px 0', textAlign: 'center' }}>
                      <Spin tip={tShared('messages.loadingPoLines')} />
                    </div>
                  );
                }
                if (!linkedPo) {
                  return (
                    <Typography.Text type="danger">
                      {t('poLoadFailed')}
                    </Typography.Text>
                  );
                }
                if (fields.length === 0) {
                  return (
                    <Typography.Text type="secondary">{t('poFullyReceived')}</Typography.Text>
                  );
                }
                return (
                  <GrnPoLinesEditor
                    form={form}
                    supplierId={supplierId}
                    warehouseId={warehouseId}
                    linkedPo={linkedPo}
                    fields={fields}
                    remove={remove}
                    maxScrollY={560}
                  />
                );
              }}
            </Form.List>
            {!poDraftGrn && <GrnPricingSummaryPanel form={form} vatTreatments={vatTreatments} />}
          </div>
        </Form>
      </Drawer>

      <PurchaseOrderEditDrawer
        poId={purchaseOrderId ?? null}
        open={poEditOpen}
        stackZIndex={1100}
        onClose={() => setPoEditOpen(false)}
        onSaved={handlePoEdited}
      />

      <Drawer
        title={detail ? t('viewDrawerWithNumber', { grnNumber: detail.grnNumber }) : t('viewDrawer')}
        width={PROCUREMENT_DRAWER_WIDTH}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        styles={{ body: { paddingTop: 8, paddingBottom: 8, display: 'flex', flexDirection: 'column', overflow: 'hidden' } }}
        extra={
          detail && (
            <Space>
              <Button icon={<PrinterOutlined />} onClick={() => printGoodsReceipt(detail)}>
                {t('printA4')}
              </Button>
              {canWrite && (
                <>
              {detail.status === 1 && (
                <Button icon={<EditOutlined />} onClick={() => void openEdit(detail)}>
                  {t('editReceipt')}
                </Button>
              )}
              {detail.status === 1 && (
                <Button type="primary" icon={<CheckOutlined />} onClick={() => handleComplete(detail.id)}>
                  {t('complete')}
                </Button>
              )}
              {detail.status === 1 && (
                <Popconfirm
                  title={t('cancelConfirm')}
                  okText={t('cancelOk')}
                  cancelText={tCommon('close')}
                  onConfirm={() => void handleCancel(detail.id)}
                >
                  <Button danger icon={<CloseCircleOutlined />}>
                    {t('cancelReceipt')}
                  </Button>
                </Popconfirm>
              )}
              {canArchiveGrn(detail.status, detail.deletedAt) && (
                <Popconfirm title={t('archiveConfirm')} onConfirm={() => void handleArchive(detail.id)}>
                  <Button danger icon={<EyeInvisibleOutlined />}>
                    {t('archiveReceipt')}
                  </Button>
                </Popconfirm>
              )}
              {detail.deletedAt && canPurge && (
                <Popconfirm
                  title={tShared('purgeConfirm')}
                  onConfirm={() => void handlePurge(detail.id)}
                >
                  <Button danger type="primary" icon={<DeleteOutlined />}>
                    {tShared('purgePermanent')}
                  </Button>
                </Popconfirm>
              )}
              {showLockedDeleteGrn(detail.status, detail.deletedAt) && (
                <Tooltip title={t('archiveLockedTooltip')}>
                  <Button disabled icon={<EyeInvisibleOutlined />}>
                    {t('archiveReceipt')}
                  </Button>
                </Tooltip>
              )}
                </>
              )}
            </Space>
          )
        }
      >
        {detail && <GrnDetailView detail={detail} />}
      </Drawer>
    </Card>
  );
}

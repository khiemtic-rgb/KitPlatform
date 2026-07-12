import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Drawer,
  Form,
  Input,
  InputNumber,
  Popconfirm,
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
import { PlusOutlined, EyeOutlined, DeleteOutlined, SaveOutlined, FolderOpenOutlined, CheckOutlined, CloseCircleOutlined, EyeInvisibleOutlined, PrinterOutlined } from '@ant-design/icons';
import { fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import {
  cancelGoodsReceipt,
  completeGoodsReceipt,
  createGoodsReceipt,
  archiveGoodsReceipt,
  fetchGoodsReceipt,
  fetchGoodsReceipts,
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
} from '@/modules/procurement/GrnPricingPanel';
import { defaultVatTreatmentId } from '@/modules/procurement/po-vat';
import { isPlaceholderSupplier } from '@/modules/procurement/grn-pricing';
import { printGoodsReceipt } from '@/shared/print/grn-print';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { ProductSearchSelect } from '@/modules/procurement/ProductSearchSelect';
import { PoUnitPriceField } from '@/modules/procurement/PoUnitPriceField';
import { PharmaExpiryPicker } from '@/shared/ui/PharmaDatePicker';
import { GoodsReceiptFilterBar } from '@/modules/procurement/GoodsReceiptFilterBar';
import { formatDisplayDate } from '@/shared/utils/date';
import { downloadCsv } from '@/shared/utils/download-csv';
import { quantityInputNumberProps } from '@/shared/utils/money';
import { useProcurementWrite, useSystemDeletePermanent } from '@/shared/auth/usePermission';

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

function emptyGrnLine(): GrnLineForm {
  return {
    productId: '',
    productUnitId: '',
    batchNumber: '',
    expiryDate: defaultExpiryDate(),
    quantity: 1,
    unitCost: 0,
  };
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
  const purchaseOrderId = Form.useWatch('purchaseOrderId', form);
  const supplierId = Form.useWatch('supplierId', form);

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
  }, [purchaseOrderId, form, t]);

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
      items: [emptyGrnLine()],
    });
    setLinkedPo(null);
    setPoDraftGrn(null);
    setPoLoading(false);
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

  const handleCreate = async () => {
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
      const created = await createGoodsReceipt({
        purchaseOrderId: values.purchaseOrderId,
        supplierId: values.supplierId,
        warehouseId: values.warehouseId,
        receiptDate: values.receiptDate || todayDateString(),
        notes: values.notes,
        supplierInvoiceNumber: values.supplierInvoiceNumber,
        vatTreatmentId: values.vatTreatmentId,
        orderDiscountType: values.orderDiscountType,
        orderDiscountValue: values.orderDiscountValue,
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
      });
      message.success(t('messages.created', { grnNumber: created.grnNumber }));
      setDrawerOpen(false);
      void loadReceipts(filters, searchInput, page, pageSize);
      void loadMasterData();
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
    add: (defaultValue?: Partial<GrnLineForm>) => void,
    remove: (index: number) => void,
  ) => (
    <>
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
        {t('manualLinesHint')}
      </Typography.Text>
      {fields.map((field) => (
        <Form.Item key={field.key} noStyle shouldUpdate>
          {() => {
            const productId = form.getFieldValue(['items', field.name, 'productId']) as string | undefined;
            return (
              <div
                style={{
                  display: 'flex',
                  gap: 8,
                  alignItems: 'flex-start',
                  marginBottom: 12,
                  paddingBottom: 8,
                  borderBottom: '1px solid #f0f0f0',
                }}
              >
                <Form.Item
                  {...field}
                  name={[field.name, 'productId']}
                  label={tShared('columns.product')}
                  rules={[{ required: true, message: tVal('selectProduct') }]}
                  style={{ flex: '2 1 320px', marginBottom: 0, minWidth: 240 }}
                >
                  <ProductSearchSelect
                    seedProducts={products}
                    placeholder={t('productSearchPlaceholder')}
                    afterChange={() => {
                      form.setFieldValue(['items', field.name, 'productUnitId'], undefined);
                    }}
                  />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'productUnitId']}
                  label={tShared('columns.unit')}
                  rules={[{ required: true, message: tVal('selectUnit') }]}
                  style={{ flex: '0 0 84px', marginBottom: 0 }}
                >
                  <ProductUnitSelect productId={productId} width={84} />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'batchNumber']}
                  label={tShared('columns.batchNumber')}
                  rules={[{ required: true, message: tVal('enterBatch') }]}
                  style={{ flex: '0 0 100px', marginBottom: 0 }}
                >
                  <Input placeholder={tShared('columns.batchShort')} />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'expiryDate']}
                  label={tShared('columns.expiry')}
                  rules={[{ required: true, message: tVal('selectExpiry') }]}
                  style={{ flex: '0 0 112px', marginBottom: 0 }}
                >
                  <PharmaExpiryPicker style={{ width: 112 }} />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'quantity']}
                  label={tShared('columns.qty')}
                  rules={[{ required: true }]}
                  style={{ flex: '0 0 80px', marginBottom: 0 }}
                >
                  <InputNumber {...quantityInputNumberProps} min={0.001} placeholder={tShared('columns.qty')} style={{ width: '100%' }} />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'unitCost']}
                  label={tShared('columns.unitCost')}
                  rules={[{ required: true }]}
                  style={{ flex: '0 0 120px', marginBottom: 0 }}
                >
                  <PoUnitPriceField
                    supplierId={supplierId}
                    productId={productId}
                    form={form}
                    fieldName={field.name}
                    valueFieldName="unitCost"
                  />
                </Form.Item>
                <Form.Item label={tShared('columns.lineDiscount')} style={{ flex: '0 0 156px', marginBottom: 0 }}>
                  <GrnLineDiscountFields fieldName={field.name} />
                </Form.Item>
                <Form.Item label=" " colon={false} style={{ flex: '0 0 auto', marginBottom: 0 }}>
                  <Button
                    type="text"
                    danger
                    icon={<DeleteOutlined />}
                    aria-label={tShared('lines.removeLineAria')}
                    onClick={() => remove(field.name)}
                  />
                </Form.Item>
              </div>
            );
          }}
        </Form.Item>
      ))}
      <Button
        type="dashed"
        icon={<PlusOutlined />}
        onClick={() => add(emptyGrnLine())}
        block
      >
        {t('addManualLine')}
      </Button>
    </>
  );

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
        title={t('createDrawer')}
        width={PROCUREMENT_DRAWER_WIDTH}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        styles={{ body: { paddingTop: 8, paddingBottom: 8, display: 'flex', flexDirection: 'column', overflow: 'hidden' } }}
        extra={
          <Button
            type="primary"
            icon={<SaveOutlined />}
            onClick={handleCreate}
            loading={saving}
            disabled={!!poDraftGrn}
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
            approvedPos={approvedPos}
            purchaseOrderId={purchaseOrderId}
            linkedPo={linkedPo}
            poLoading={poLoading}
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

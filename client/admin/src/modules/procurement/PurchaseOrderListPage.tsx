import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Descriptions,
  Drawer,
  Form,
  Popconfirm,
  Space,
  Spin,
  Table,
  Tag,
  Tooltip,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { PlusOutlined, EyeOutlined, EditOutlined, CheckOutlined, CloseCircleOutlined, LockOutlined, EyeInvisibleOutlined, DeleteOutlined, SaveOutlined } from '@ant-design/icons';
import { fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import {
  approvePurchaseOrder,
  archivePurchaseOrder,
  cancelPurchaseOrder,
  closePurchaseOrder,
  createPurchaseOrder,
  fetchPurchaseOrder,
  fetchPurchaseOrders,
  fetchSuppliers,
  fetchVatTreatments,
  purgePurchaseOrder,
  submitPurchaseOrderForApproval,
} from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  ProcurementVatTreatment,
  PurchaseOrderDetail,
  PurchaseOrderListFilters,
  PurchaseOrderListItem,
  Supplier,
} from '@/shared/api/procurement.types';
import { PO_STATUS_TAG, canEditPurchaseOrder } from '@/shared/api/procurement.types';
import { useProcurementEnums } from '@/shared/i18n/use-procurement-enums';
import { PurchaseOrderEditDrawer } from '@/modules/procurement/PurchaseOrderEditDrawer';
import { PoApproveSupplierModal } from '@/modules/procurement/PoApproveSupplierModal';
import { isPlaceholderSupplier } from '@/modules/procurement/grn-pricing';
import { PoReadonlyTaxSummaryFooter, PROCUREMENT_MONEY_COL_WIDTH } from '@/modules/procurement/GrnPoTaxSummary';
import { PurchaseOrderFormHeader } from '@/modules/procurement/PurchaseOrderFormHeader';
import { PurchaseOrderLinesEditor } from '@/modules/procurement/PurchaseOrderLinesEditor';
import { defaultVatTreatmentId } from '@/modules/procurement/po-vat';
import { PurchaseOrderFilterBar } from '@/modules/procurement/PurchaseOrderFilterBar';
import { downloadCsv } from '@/shared/utils/download-csv';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';
import {
  procurementQuantityColumn,
  procurementRemainingQtyColumn,
} from '@/modules/procurement/procurement-quantity-cell';
import { PoWorkflowPendingDrawer } from '@/modules/procurement/PoWorkflowPendingDrawer';
import {
  clearPoRequisitionDraft,
  readPoRequisitionDraft,
} from '@/modules/inventory/po-requisition-draft';
import { useProcurementWrite, useSystemDeletePermanent, useIsAdmin } from '@/shared/auth/usePermission';
import { useSearchParams } from 'react-router-dom';

interface PoLineForm {
  productId: string;
  productUnitId: string;
  orderedQty: number;
  unitPrice: number;
}

const emptyFilters: PurchaseOrderListFilters = {};

export function PurchaseOrderListPage() {
  const { t } = useTranslation('procurement', { keyPrefix: 'purchaseOrders' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const { poStatusLabel } = useProcurementEnums();
  const canWrite = useProcurementWrite();
  const isAdmin = useIsAdmin();
  const canPurge = useSystemDeletePermanent();
  const [searchParams] = useSearchParams();
  const initialFilters = useMemo((): PurchaseOrderListFilters => {
    const pending = searchParams.get('pendingReceipt');
    if (pending === '1' || pending === 'true') return { pendingReceiptOnly: true };
    return emptyFilters;
  }, [searchParams]);
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<PurchaseOrderListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [vatTreatments, setVatTreatments] = useState<ProcurementVatTreatment[]>([]);
  const [filters, setFilters] = useState<PurchaseOrderListFilters>(emptyFilters);
  const [searchInput, setSearchInput] = useState('');
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<PurchaseOrderDetail | null>(null);
  const [poDetailCache, setPoDetailCache] = useState<Record<string, PurchaseOrderDetail>>({});
  const poDetailCacheRef = useRef(poDetailCache);
  poDetailCacheRef.current = poDetailCache;
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [editPoOpen, setEditPoOpen] = useState(false);
  const [approvePoId, setApprovePoId] = useState<string | null>(null);
  const [approvePoNumber, setApprovePoNumber] = useState('');
  const [approveMode, setApproveMode] = useState<'approve' | 'submit'>('approve');
  const [approving, setApproving] = useState(false);
  const [workflowDrawerOpen, setWorkflowDrawerOpen] = useState(false);
  const supplierId = Form.useWatch('supplierId', form);

  const loadMasterData = useCallback(async () => {
    const [sup, wh, prod, vat] = await Promise.all([
      fetchSuppliers(true),
      fetchWarehouses(),
      fetchProducts({ page: 1, pageSize: 200 }),
      fetchVatTreatments(),
    ]);
    setSuppliers(sup);
    setWarehouses(wh);
    setProducts(prod.items);
    setVatTreatments(vat);
  }, []);

  const loadOrders = useCallback(async (
    nextFilters: PurchaseOrderListFilters,
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
      const result = await fetchPurchaseOrders({
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
    void loadOrders(initialFilters, '');
  }, [loadMasterData, loadOrders, initialFilters, tShared]);

  const resetFilters = () => {
    void loadOrders(emptyFilters, '');
  };

  const openCreate = () => {
    form.resetFields();
    const placeholder = suppliers.find((s) => isPlaceholderSupplier(s));
    form.setFieldsValue({
      supplierId: placeholder?.id,
      items: [{ orderedQty: 1, unitPrice: 0 }],
      vatTreatmentId: defaultVatTreatmentId(vatTreatments),
    });
    setDrawerOpen(true);
  };

  const openCreateFromRequisition = useCallback(() => {
    const draft = readPoRequisitionDraft();
    if (!draft?.lines.length) return false;
    form.resetFields();
    const placeholder = suppliers.find((s) => isPlaceholderSupplier(s));
    form.setFieldsValue({
      supplierId: placeholder?.id,
      warehouseId: draft.warehouseId,
      notes: draft.notes,
      vatTreatmentId: defaultVatTreatmentId(vatTreatments),
      items: draft.lines.map((line) => ({
        productId: line.productId,
        orderedQty: line.suggestedQty,
        unitPrice: 0,
      })),
    });
    setDrawerOpen(true);
    clearPoRequisitionDraft();
    message.info(t('requisitionPrefilled', { count: draft.lines.length }));
    return true;
  }, [form, suppliers, vatTreatments, t]);

  useEffect(() => {
    if (searchParams.get('requisition') !== '1') return;
    if (suppliers.length === 0 || warehouses.length === 0 || products.length === 0) return;
    openCreateFromRequisition();
  }, [searchParams, suppliers.length, warehouses.length, products.length, openCreateFromRequisition]);

  const openDetail = async (id: string) => {
    try {
      const po = await fetchPurchaseOrder(id);
      setDetail(po);
      setPoDetailCache((cache) => ({ ...cache, [id]: po }));
      setDetailOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    }
  };

  const loadPoExpand = async (id: string) => {
    if (poDetailCache[id]) return;
    try {
      const po = await fetchPurchaseOrder(id);
      setPoDetailCache((cache) => ({ ...cache, [id]: po }));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.linesLoadFailed')));
    }
  };

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      const created = await createPurchaseOrder({
        supplierId: values.supplierId,
        warehouseId: values.warehouseId,
        expectedDate: values.expectedDate || undefined,
        notes: values.notes,
        vatTreatmentId: values.vatTreatmentId,
        items: (values.items as PoLineForm[]).map((i) => ({
          productId: i.productId,
          productUnitId: i.productUnitId,
          orderedQty: i.orderedQty,
          unitPrice: i.unitPrice,
        })),
      });
      message.success(t('messages.draftSaved', { poNumber: created.poNumber }));
      setDrawerOpen(false);
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.createFailed')));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id: string) => {
    try {
      const po = detail?.id === id ? detail : await fetchPurchaseOrder(id);
      const supplier = suppliers.find((s) => s.id === po.supplierId);
      if (supplier && isPlaceholderSupplier(supplier)) {
        setApproveMode('approve');
        setApprovePoId(id);
        setApprovePoNumber(po.poNumber);
        return;
      }
      const updated = await approvePurchaseOrder(id);
      message.success(t('messages.approved', { poNumber: updated.poNumber }));
      if (detail?.id === id) setDetail(updated);
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.approveFailed')));
    }
  };

  const handleSubmitForApproval = async (id: string) => {
    try {
      const po = detail?.id === id ? detail : await fetchPurchaseOrder(id);
      const supplier = suppliers.find((s) => s.id === po.supplierId);
      if (supplier && isPlaceholderSupplier(supplier)) {
        setApproveMode('submit');
        setApprovePoId(id);
        setApprovePoNumber(po.poNumber);
        return;
      }
      await submitPurchaseOrderForApproval(id);
      message.success(t('messages.submittedForApproval', { poNumber: po.poNumber }));
      if (detail?.id === id) setDetail(await fetchPurchaseOrder(id));
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.submitFailed')));
    }
  };

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (!(e.ctrlKey || e.metaKey)) return;
      const key = e.key.toLowerCase();
      if (key === 's' && drawerOpen && canWrite) {
        e.preventDefault();
        void handleCreate();
        return;
      }
      if (key === 'enter' && detailOpen && detail?.status === 1 && canWrite) {
        e.preventDefault();
        void handleSubmitForApproval(detail.id);
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [drawerOpen, detailOpen, detail, canWrite]);

  const confirmApproveWithSupplier = async (supplierId: string) => {
    if (!approvePoId) return;
    setApproving(true);
    try {
      if (approveMode === 'submit') {
        await submitPurchaseOrderForApproval(approvePoId, { supplierId });
        message.success(t('messages.submittedForApproval', { poNumber: approvePoNumber }));
        if (detail?.id === approvePoId) setDetail(await fetchPurchaseOrder(approvePoId));
      } else {
        const updated = await approvePurchaseOrder(approvePoId, { supplierId });
        message.success(t('messages.approved', { poNumber: updated.poNumber }));
        if (detail?.id === approvePoId) setDetail(updated);
      }
      setApprovePoId(null);
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(
        apiErrorMessage(
          error,
          approveMode === 'submit' ? t('messages.submitFailed') : t('messages.approveFailed'),
        ),
      );
    } finally {
      setApproving(false);
    }
  };

  const handleCancel = async (id: string) => {
    try {
      await cancelPurchaseOrder(id);
      message.success(t('messages.cancelled'));
      if (detail?.id === id) setDetail(await fetchPurchaseOrder(id));
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    }
  };

  const handleClose = async (id: string) => {
    try {
      const updated = await closePurchaseOrder(id);
      message.success(t('messages.closed', { poNumber: updated.poNumber }));
      if (detail?.id === id) setDetail(updated);
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.closeFailed')));
    }
  };

  const handleArchive = async (id: string) => {
    try {
      await archivePurchaseOrder(id);
      message.success(t('messages.archived'));
      setDetailOpen(false);
      setDetail(null);
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.archiveFailed')));
    }
  };

  const handlePurge = async (id: string) => {
    try {
      await purgePurchaseOrder(id);
      message.success(t('messages.purged'));
      setDetailOpen(false);
      setDetail(null);
      void loadOrders(filters, searchInput, page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.purgeFailed')));
    }
  };

  const canArchivePo = (status: number, deletedAt?: string) => status === 6 && !deletedAt;
  const showLockedDeletePo = (status: number, deletedAt?: string) =>
    status !== 1 && status !== 6 && !deletedAt;

  const exportOrders = () => {
    if (items.length === 0) {
      message.info(tShared('messages.noExportData'));
      return;
    }
    downloadCsv(
      `don-dat-hang-${new Date().toISOString().slice(0, 10)}.csv`,
      [
        t('exportColumns.poNumber'),
        t('exportColumns.supplier'),
        t('exportColumns.warehouse'),
        t('exportColumns.status'),
        t('exportColumns.orderDate'),
        t('exportColumns.itemCount'),
        t('exportColumns.totalAmount'),
      ],
      items.map((row) => [
        row.poNumber,
        row.supplierName,
        row.warehouseName,
        poStatusLabel(row.status),
        formatDisplayDate(row.orderDate),
        String(row.itemCount),
        formatDisplayMoney(row.totalAmount),
      ]),
    );
  };

  const columns: ColumnsType<PurchaseOrderListItem> = [
    { title: tShared('columns.poNumber'), dataIndex: 'poNumber', width: 140 },
    { title: tShared('columns.supplierShort'), dataIndex: 'supplierName' },
    { title: tShared('columns.receiveWarehouse'), dataIndex: 'warehouseName' },
    {
      title: tShared('columns.orderDate'),
      dataIndex: 'orderDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: tShared('columns.status'),
      dataIndex: 'status',
      width: 130,
      render: (s: number, row) => (
        <Space size={4}>
          <Tag color={PO_STATUS_TAG[s] ?? 'default'}>{poStatusLabel(s)}</Tag>
          {row.deletedAt ? <Tag color="default">{tShared('archived')}</Tag> : null}
        </Space>
      ),
    },
    {
      title: tShared('columns.totalAmount'),
      dataIndex: 'totalAmount',
      width: 120,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
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

  const poLineColumns: ColumnsType<PurchaseOrderDetail['items'][number]> = [
    { title: tShared('columns.productCode'), dataIndex: 'productCode', width: 90 },
    { title: tShared('columns.productName'), dataIndex: 'productName', width: 280, ellipsis: true },
    { title: tShared('columns.unit'), dataIndex: 'unitName', width: 64 },
    procurementQuantityColumn(tShared('columns.ordered'), 'orderedQty', 68),
    procurementQuantityColumn(tShared('columns.received'), 'receivedQty', 76),
    procurementRemainingQtyColumn(),
    {
      title: tShared('columns.unitPrice'),
      dataIndex: 'unitPrice',
      width: PROCUREMENT_MONEY_COL_WIDTH,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums', display: 'block', textAlign: 'right' }}>
          {formatDisplayMoney(v)}
        </span>
      ),
    },
  ];

  return (
    <Card
      title={t('title')}
      extra={
        <Space>
          {isAdmin ? (
            <Button onClick={() => setWorkflowDrawerOpen(true)}>{t('pendingApprovals')}</Button>
          ) : null}
          {canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('create')}
            </Button>
          ) : null}
        </Space>
      }
    >
      <PurchaseOrderFilterBar
        filters={filters}
        searchInput={searchInput}
        suppliers={suppliers}
        warehouses={warehouses}
        products={products}
        loading={loading}
        onSearchInputChange={setSearchInput}
        onApply={loadOrders}
        onReset={resetFilters}
        onExport={exportOrders}
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
          showTotal: (count) => tShared('pagination.orders', { count }),
          onChange: (nextPage, nextPageSize) => {
            void loadOrders(filters, searchInput, nextPage, nextPageSize);
          },
        }}
        scroll={{ x: 1000 }}
        onRow={(record) => ({
          onClick: () => void openDetail(record.id),
          style: { cursor: 'pointer' },
        })}
        expandable={{
          onExpand: (expanded, record) => {
            if (expanded) void loadPoExpand(record.id);
          },
          expandedRowRender: (record) => {
            const po = poDetailCache[record.id];
            if (!po) return <Spin size="small" />;
            return (
              <Table
                rowKey="id"
                size="small"
                pagination={false}
                className="grn-lines-table"
                dataSource={po.items}
                columns={poLineColumns}
              />
            );
          },
        }}
      />

      <Drawer
        title={t('createDrawer')}
        width={980}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        styles={{ body: { paddingTop: 12, display: 'flex', flexDirection: 'column' } }}
        extra={
          <Button type="primary" icon={<SaveOutlined />} onClick={() => void handleCreate()} loading={saving}>
            {t('saveDraft')}
          </Button>
        }
      >
        <Form form={form} layout="vertical" style={{ display: 'flex', flexDirection: 'column', minHeight: 0 }}>
          <PurchaseOrderFormHeader
            form={form}
            mode="create"
            vatTreatments={vatTreatments}
            suppliers={suppliers}
            warehouses={warehouses}
          />
          <PurchaseOrderLinesEditor
            form={form}
            supplierId={supplierId}
            products={products}
            mode="create"
            scrollY={420}
          />
        </Form>
      </Drawer>

      <Drawer
        title={detail ? t('viewDrawerWithNumber', { poNumber: detail.poNumber }) : t('viewDrawer')}
        width={880}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail &&
          canWrite && (
            <Space>
              {canEditPurchaseOrder(detail.status) && !detail.deletedAt && (
                <Button icon={<EditOutlined />} onClick={() => setEditPoOpen(true)}>
                  {t('editOrder')}
                </Button>
              )}
              {detail.status === 1 && !isAdmin && (
                <Button type="primary" onClick={() => void handleSubmitForApproval(detail.id)}>
                  {t('submitForApproval')}
                </Button>
              )}
              {detail.status === 1 && isAdmin && (
                <Button type="primary" icon={<CheckOutlined />} onClick={() => void handleApprove(detail.id)}>
                  {t('approve')}
                </Button>
              )}
              {(detail.status === 1 || detail.status === 2) && (
                <Popconfirm title={t('cancelConfirm')} onConfirm={() => void handleCancel(detail.id)}>
                  <Button danger icon={<CloseCircleOutlined />}>
                    {t('cancelOrder')}
                  </Button>
                </Popconfirm>
              )}
              {detail.status === 4 && (
                <Button type="primary" icon={<LockOutlined />} onClick={() => handleClose(detail.id)}>
                  {t('closeOrder')}
                </Button>
              )}
              {canArchivePo(detail.status, detail.deletedAt) && (
                <Popconfirm title={t('archiveConfirm')} onConfirm={() => void handleArchive(detail.id)}>
                  <Button danger icon={<EyeInvisibleOutlined />}>
                    {t('archiveOrder')}
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
              {showLockedDeletePo(detail.status, detail.deletedAt) && (
                <Tooltip title={t('archiveLockedTooltip')}>
                  <Button disabled icon={<EyeInvisibleOutlined />}>
                    {t('archiveOrder')}
                  </Button>
                </Tooltip>
              )}
            </Space>
          )
        }
      >
        {detail && (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label={tShared('columns.supplierShort')}>{detail.supplierName}</Descriptions.Item>
              <Descriptions.Item label={tShared('columns.warehouse')}>{detail.warehouseName}</Descriptions.Item>
              <Descriptions.Item label={tShared('columns.status')}>
                <Tag color={PO_STATUS_TAG[detail.status] ?? 'default'}>{poStatusLabel(detail.status)}</Tag>
              </Descriptions.Item>
            </Descriptions>
            <div className="grn-lines-detail-panel">
              <p className="grn-lines-detail-panel__title">{tShared('lines.orderLinesTitle')}</p>
              <Table
                rowKey="id"
                size="small"
                pagination={false}
                className="grn-lines-table"
                scroll={{ x: 760 }}
                dataSource={detail.items}
                columns={poLineColumns}
                summary={() => (
                  <PoReadonlyTaxSummaryFooter
                    subtotal={detail.subtotal}
                    taxAmount={detail.taxAmount}
                    totalAmount={detail.totalAmount}
                    poNumber={detail.poNumber}
                    vatTreatmentName={detail.vatTreatmentName}
                    leadingColSpan={5}
                  />
                )}
              />
            </div>
          </>
        )}
      </Drawer>

      <PurchaseOrderEditDrawer
        poId={detail?.id ?? null}
        open={editPoOpen}
        onClose={() => setEditPoOpen(false)}
        onSaved={(po) => {
          setDetail(po);
          setPoDetailCache((cache) => ({ ...cache, [po.id]: po }));
          void loadOrders(filters, searchInput, page, pageSize);
        }}
      />

      <PoApproveSupplierModal
        open={!!approvePoId}
        poNumber={approvePoNumber}
        suppliers={suppliers}
        loading={approving}
        onCancel={() => setApprovePoId(null)}
        onConfirm={(supplierId) => void confirmApproveWithSupplier(supplierId)}
      />

      <PoWorkflowPendingDrawer
        open={workflowDrawerOpen}
        onClose={() => setWorkflowDrawerOpen(false)}
        onDecided={() => void loadOrders(filters, searchInput, page, pageSize)}
      />
    </Card>
  );
}

import { useCallback, useEffect, useRef, useState } from 'react';
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
import { PlusOutlined, EyeOutlined } from '@ant-design/icons';
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
  purgeGoodsReceipt,
} from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  GoodsReceiptDetail,
  GoodsReceiptListFilters,
  GoodsReceiptListItem,
  PurchaseOrderDetail,
  PurchaseOrderListItem,
  Supplier,
} from '@/shared/api/procurement.types';
import { GRN_STATUS_LABELS, canEditPurchaseOrder } from '@/shared/api/procurement.types';
import { PurchaseOrderEditDrawer } from '@/modules/procurement/PurchaseOrderEditDrawer';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { PoUnitPriceField } from '@/modules/procurement/PoUnitPriceField';
import { PharmaDatePicker, PharmaExpiryPicker } from '@/shared/ui/PharmaDatePicker';
import { GoodsReceiptFilterBar } from '@/modules/procurement/GoodsReceiptFilterBar';
import { filterGoodsReceiptsClient } from '@/modules/procurement/procurement-list-filters';
import { filterGoodsReceiptsByProduct } from '@/modules/procurement/procurement-product-filter';
import { formatDisplayDate } from '@/shared/utils/date';
import { downloadCsv } from '@/shared/utils/download-csv';
import { formatDisplayMoney, moneyInputNumberPropsAllowZero, moneyInputNumberStyle } from '@/shared/utils/money';
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
  const canWrite = useProcurementWrite();
  const canPurge = useSystemDeletePermanent();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<GoodsReceiptListItem[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [allPurchaseOrders, setAllPurchaseOrders] = useState<PurchaseOrderListItem[]>([]);
  const [approvedPos, setApprovedPos] = useState<PurchaseOrderListItem[]>([]);
  const [filters, setFilters] = useState<GoodsReceiptListFilters>(emptyFilters);
  const [searchInput, setSearchInput] = useState('');
  const [linkedPo, setLinkedPo] = useState<PurchaseOrderDetail | null>(null);
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
    const [sup, wh, prod, pos, pendingPos] = await Promise.all([
      fetchSuppliers(true),
      fetchWarehouses(),
      fetchProducts({ page: 1, pageSize: 200 }),
      fetchPurchaseOrders(),
      fetchPurchaseOrders({ pendingReceiptOnly: true }),
    ]);
    setSuppliers(sup);
    setWarehouses(wh);
    setProducts(prod.items);
    setAllPurchaseOrders(pos);
    setApprovedPos(pendingPos);
  }, []);

  const loadReceipts = useCallback(async (nextFilters: GoodsReceiptListFilters, search: string) => {
    setFilters(nextFilters);
    setSearchInput(search);
    setLoading(true);
    try {
      let receipts: GoodsReceiptListItem[];

      if (nextFilters.productId) {
        const byProduct = await fetchGoodsReceipts({ productId: nextFilters.productId });
        const all = await fetchGoodsReceipts();
        receipts =
          byProduct.length >= all.length
            ? await filterGoodsReceiptsByProduct(all, nextFilters.productId, grnDetailCacheRef.current)
            : byProduct;
      } else {
        receipts = await fetchGoodsReceipts({
          ...nextFilters,
          search: search.trim() || undefined,
        });
      }

      setItems(filterGoodsReceiptsClient(receipts, nextFilters, search));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được phiếu nhập hàng'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadMasterData().catch(() => {
      message.error('Không tải được dữ liệu tham chiếu');
    });
    void loadReceipts(emptyFilters, '');
  }, [loadMasterData, loadReceipts]);

  const resetFilters = () => {
    void loadReceipts(emptyFilters, '');
  };

  const exportReceipts = () => {
    if (items.length === 0) {
      message.info('Không có dữ liệu để xuất');
      return;
    }
    downloadCsv(
      `phieu-nhap-hang-${new Date().toISOString().slice(0, 10)}.csv`,
      ['Số phiếu', 'NCC', 'Kho', 'PO', 'Trạng thái', 'Ngày nhập', 'Số dòng'],
      items.map((row) => [
        row.grnNumber,
        row.supplierName,
        row.warehouseName,
        row.poNumber ?? '—',
        GRN_STATUS_LABELS[row.status] ?? String(row.status),
        formatDisplayDate(row.receiptDate),
        String(row.itemCount),
      ]),
    );
  };

  useEffect(() => {
    if (!purchaseOrderId) {
      setLinkedPo(null);
      setPoLoading(false);
      return;
    }

    let cancelled = false;
    setPoLoading(true);
    setLinkedPo(null);
    form.setFieldsValue({ items: [] });

    fetchPurchaseOrder(purchaseOrderId)
      .then((po) => {
        if (cancelled) return;
        setLinkedPo(po);
        const lines = buildGrnLinesFromPo(po);
        form.setFieldsValue({
          supplierId: po.supplierId,
          warehouseId: po.warehouseId,
          items: lines,
        });
        if (lines.length === 0) {
          message.info('PO này đã nhận đủ hàng.');
        }
      })
      .catch(() => {
        if (!cancelled) {
          setLinkedPo(null);
          message.error('Không tải được chi tiết PO.');
        }
      })
      .finally(() => {
        if (!cancelled) setPoLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [purchaseOrderId, form]);

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
    form.setFieldsValue({ receiptDate: todayDateString(), items: [emptyGrnLine()] });
    setLinkedPo(null);
    setPoLoading(false);
    setDrawerOpen(true);
  };

  const loadGrnExpand = async (id: string) => {
    if (grnDetailCache[id]) return;
    try {
      const grn = await fetchGoodsReceipt(id);
      setGrnDetailCache((cache) => ({ ...cache, [id]: grn }));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi tiết phiếu nhập'));
    }
  };

  const openDetail = async (id: string) => {
    try {
      setDetail(await fetchGoodsReceipt(id));
      setDetailOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi tiết phiếu nhập'));
    }
  };

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      const lines = (values.items as GrnLineForm[]).filter((i) => i.quantity > 0);
      if (lines.length === 0) {
        message.warning('Thêm ít nhất một dòng nhập có số lượng > 0.');
        return;
      }
      setSaving(true);
      const created = await createGoodsReceipt({
        purchaseOrderId: values.purchaseOrderId,
        supplierId: values.supplierId,
        warehouseId: values.warehouseId,
        receiptDate: values.receiptDate || todayDateString(),
        notes: values.notes,
        items: lines.map((i) => ({
          purchaseOrderItemId: i.purchaseOrderItemId,
          productId: i.productId,
          productUnitId: i.productUnitId,
          batchNumber: i.batchNumber,
          expiryDate: i.expiryDate,
          quantity: i.quantity,
          unitCost: i.unitCost,
        })),
      });
      message.success(`Đã tạo ${created.grnNumber}`);
      setDrawerOpen(false);
      void loadReceipts(filters, searchInput);
      void loadMasterData();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không tạo được phiếu nhập'));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleComplete = async (id: string) => {
    try {
      const updated = await completeGoodsReceipt(id);
      message.success(`Đã hoàn tất ${updated.grnNumber} — tồn kho đã cập nhật`);
      if (detail?.id === id) setDetail(updated);
      void loadReceipts(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hoàn tất được phiếu nhập'));
    }
  };

  const handleCancel = async (id: string) => {
    try {
      const updated = await cancelGoodsReceipt(id);
      message.success(`Đã hủy ${updated.grnNumber}`);
      setDetail(updated);
      void loadReceipts(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiếu nhập'));
    }
  };

  const handleArchive = async (id: string) => {
    try {
      await archiveGoodsReceipt(id);
      message.success('Đã ẩn phiếu nhập (có thể xem trong bản ghi đã ẩn)');
      setDetailOpen(false);
      setDetail(null);
      void loadReceipts(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ẩn được phiếu nhập'));
    }
  };

  const handlePurge = async (id: string) => {
    try {
      await purgeGoodsReceipt(id);
      message.success('Đã xóa vĩnh viễn phiếu nhập');
      setDetailOpen(false);
      setDetail(null);
      void loadReceipts(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa vĩnh viễn được phiếu'));
    }
  };

  const canArchiveGrn = (status: number, deletedAt?: string) => status === 3 && !deletedAt;
  const showLockedDeleteGrn = (status: number, deletedAt?: string) => status === 2 && !deletedAt;

  const columns: ColumnsType<GoodsReceiptListItem> = [
    { title: 'Số phiếu', dataIndex: 'grnNumber', width: 140 },
    { title: 'NCC', dataIndex: 'supplierName' },
    { title: 'Kho', dataIndex: 'warehouseName' },
    { title: 'Đơn đặt hàng', dataIndex: 'poNumber', width: 120, render: (v) => v ?? '—' },
    {
      title: 'Ngày nhập',
      dataIndex: 'receiptDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (s: number, row) => (
        <Space size={4}>
          <Tag>{GRN_STATUS_LABELS[s] ?? s}</Tag>
          {row.deletedAt ? <Tag color="default">Đã ẩn</Tag> : null}
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
          Xem
        </Button>
      ),
    },
  ];

  const grnLineColumns: ColumnsType<GoodsReceiptDetail['items'][number]> = [
    { title: 'Mã SP', dataIndex: 'productCode', width: 100 },
    { title: 'Tên SP', dataIndex: 'productName' },
    { title: 'Lô', dataIndex: 'batchNumber', width: 90 },
    { title: 'HSD', dataIndex: 'expiryDate', width: 100, render: (v: string) => formatDisplayDate(v) },
    { title: 'SL', dataIndex: 'quantity', width: 70, align: 'right' },
    {
      title: 'Giá vốn',
      dataIndex: 'unitCost',
      width: 90,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
  ];

  const renderPoLineTable = (fields: FormListFieldData[], remove: (index: number) => void) => {
    const lineColumns: ColumnsType<FormListFieldData> = [
      {
        title: 'Sản phẩm',
        width: 200,
        render: (_, field) => {
          const line = form.getFieldValue(['items', field.name]) as GrnLineForm | undefined;
          return (
            <div>
              <Typography.Text strong>{line?.productCode}</Typography.Text>
              <br />
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {line?.productName}
              </Typography.Text>
              <Form.Item {...field} name={[field.name, 'purchaseOrderItemId']} hidden>
                <Input />
              </Form.Item>
              <Form.Item {...field} name={[field.name, 'productId']} hidden>
                <Input />
              </Form.Item>
              <Form.Item {...field} name={[field.name, 'productUnitId']} hidden>
                <Input />
              </Form.Item>
            </div>
          );
        },
      },
      {
        title: 'ĐVT',
        width: 70,
        render: (_, field) => form.getFieldValue(['items', field.name, 'unitName']) ?? '—',
      },
      {
        title: 'Chưa nhận',
        width: 75,
        align: 'right',
        render: (_, field) => {
          const line = form.getFieldValue(['items', field.name]) as GrnLineForm;
          const remain = (line?.orderedQty ?? 0) - (line?.receivedQty ?? 0);
          return remain.toLocaleString('vi-VN');
        },
      },
      {
        title: 'SL nhập',
        width: 95,
        render: (_, field) => {
          const line = form.getFieldValue(['items', field.name]) as GrnLineForm | undefined;
          const remain = (line?.orderedQty ?? 0) - (line?.receivedQty ?? 0);
          return (
            <Form.Item
              {...field}
              name={[field.name, 'quantity']}
              rules={[
                { required: true, message: 'Nhập SL' },
                {
                  validator: (_, value) =>
                    value == null || value <= remain
                      ? Promise.resolve()
                      : Promise.reject(new Error(`Tối đa ${remain.toLocaleString('vi-VN')}`)),
                },
              ]}
              style={{ marginBottom: 0 }}
            >
              <InputNumber min={0.01} max={remain > 0 ? remain : undefined} style={{ width: '100%' }} />
            </Form.Item>
          );
        },
      },
      {
        title: 'Số lô',
        width: 110,
        render: (_, field) => (
          <Form.Item
            {...field}
            name={[field.name, 'batchNumber']}
            rules={[{ required: true, message: 'Nhập lô' }]}
            style={{ marginBottom: 0 }}
          >
            <Input placeholder="Lô" />
          </Form.Item>
        ),
      },
      {
        title: 'HSD (T/N)',
        width: 130,
        render: (_, field) => (
          <Form.Item
            {...field}
            name={[field.name, 'expiryDate']}
            rules={[{ required: true, message: 'HSD' }]}
            style={{ marginBottom: 0 }}
          >
            <PharmaExpiryPicker style={{ width: 130 }} inTable />
          </Form.Item>
        ),
      },
      {
        title: 'Giá vốn',
        width: 120,
        align: 'right',
        render: (_, field) => {
          const productId = form.getFieldValue(['items', field.name, 'productId']) as string | undefined;
          return (
            <Form.Item
              {...field}
              name={[field.name, 'unitCost']}
              rules={[{ required: true }]}
              style={{ marginBottom: 0 }}
            >
              <PoUnitPriceField
                supplierId={supplierId}
                productId={productId}
                form={form}
                fieldName={field.name}
                valueFieldName="unitCost"
              />
            </Form.Item>
          );
        },
      },
      {
        title: '',
        width: 72,
        render: (_, field) => (
          <Button type="link" danger size="small" onClick={() => remove(field.name)}>
            Xóa
          </Button>
        ),
      },
    ];

    return (
      <Table
        className="grn-lines-table"
        rowKey="key"
        size="small"
        pagination={fields.length > 15 ? { pageSize: 15 } : false}
        scroll={{ x: 900 }}
        dataSource={fields}
        columns={lineColumns}
      />
    );
  };

  const renderManualLines = (
    fields: FormListFieldData[],
    add: (defaultValue?: Partial<GrnLineForm>) => void,
    remove: (index: number) => void,
  ) => (
    <>
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
        Chọn đơn đặt hàng ở trên để điền tự động, hoặc nhập tay từng dòng bên dưới (số lô, HSD tháng/năm).
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
                  label="Sản phẩm"
                  rules={[{ required: true, message: 'Chọn SP' }]}
                  style={{ flex: '1 1 220px', marginBottom: 0, minWidth: 180 }}
                >
                  <Select
                    placeholder="Sản phẩm"
                    showSearch
                    optionFilterProp="label"
                    options={products.map((p) => ({
                      value: p.id,
                      label: `${p.productCode} — ${p.productName}`,
                    }))}
                  />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'productUnitId']}
                  label="ĐVT"
                  rules={[{ required: true, message: 'Chọn ĐVT' }]}
                  style={{ flex: '0 0 110px', marginBottom: 0 }}
                >
                  <ProductUnitSelect productId={productId} width={110} />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'batchNumber']}
                  label="Số lô"
                  rules={[{ required: true, message: 'Nhập lô' }]}
                  style={{ flex: '0 0 100px', marginBottom: 0 }}
                >
                  <Input placeholder="Lô" />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'expiryDate']}
                  label="HSD (T/N)"
                  rules={[{ required: true, message: 'Chọn HSD' }]}
                  style={{ flex: '0 0 140px', marginBottom: 0 }}
                >
                  <PharmaExpiryPicker style={{ width: 140 }} />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'quantity']}
                  label="SL"
                  rules={[{ required: true }]}
                  style={{ flex: '0 0 80px', marginBottom: 0 }}
                >
                  <InputNumber min={0.01} placeholder="SL" style={{ width: '100%' }} />
                </Form.Item>
                <Form.Item
                  {...field}
                  name={[field.name, 'unitCost']}
                  label="Giá vốn"
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
                <Form.Item label=" " colon={false} style={{ flex: '0 0 auto', marginBottom: 0 }}>
                  <Button type="link" danger onClick={() => remove(field.name)}>
                    Xóa
                  </Button>
                </Form.Item>
              </div>
            );
          }}
        </Form.Item>
      ))}
      <Button
        type="dashed"
        onClick={() => add(emptyGrnLine())}
        block
      >
        Thêm dòng (nhập không theo PO)
      </Button>
    </>
  );

  return (
    <Card
      title="Phiếu nhập hàng"
      extra={
        canWrite ? (
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Tạo phiếu
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
        pagination={{ pageSize: 20, showTotal: (total) => `${total} phiếu` }}
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
            return (
              <Table
                rowKey="id"
                size="small"
                pagination={false}
                dataSource={grn.items}
                columns={grnLineColumns}
              />
            );
          },
        }}
      />

      <Drawer
        title="Tạo phiếu nhập hàng"
        width={980}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" onClick={handleCreate} loading={saving}>
            Lưu nháp
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
            <Form.Item
              name="purchaseOrderId"
              label="Liên kết đơn đặt hàng (khuyến nghị)"
              style={{ flex: 1, marginBottom: 16 }}
            >
              <Select
                allowClear
                showSearch
                optionFilterProp="label"
                placeholder="Chọn đơn đã duyệt, còn hàng chưa nhận"
                options={approvedPos.map((p) => ({
                  value: p.id,
                  label: `${p.poNumber} — ${p.supplierName} (${p.itemCount} SP)`,
                }))}
              />
            </Form.Item>
            <Form.Item label=" " colon={false} style={{ marginBottom: 16 }}>
              <Button
                disabled={!purchaseOrderId || !linkedPo || !canEditPurchaseOrder(linkedPo.status)}
                onClick={() => setPoEditOpen(true)}
              >
                Điều chỉnh PO
              </Button>
            </Form.Item>
          </div>
          {purchaseOrderId && linkedPo && !poLoading && (
            <Typography.Paragraph type="secondary" style={{ marginTop: -8, marginBottom: 12 }}>
              Danh sách hàng còn phải nhận từ {linkedPo.poNumber}. Điền số lô, HSD, sửa SL nếu giao thiếu —{' '}
              <strong>Xóa</strong> nếu NCC không giao mặt hàng đó.
            </Typography.Paragraph>
          )}
          <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
            <Form.Item
              name="supplierId"
              label="Nhà cung cấp"
              rules={[{ required: true }]}
              style={{ flex: '1 1 240px', marginBottom: 16, minWidth: 180 }}
            >
              <Select
                disabled={!!purchaseOrderId}
                showSearch
                optionFilterProp="label"
                options={suppliers.map((s) => ({ value: s.id, label: s.supplierName }))}
              />
            </Form.Item>
            <Form.Item
              name="warehouseId"
              label="Kho nhận"
              rules={[{ required: true }]}
              style={{ flex: '1 1 200px', marginBottom: 16, minWidth: 160 }}
            >
              <Select
                disabled={!!purchaseOrderId}
                showSearch
                optionFilterProp="label"
                options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              />
            </Form.Item>
            <Form.Item
              name="receiptDate"
              label="Ngày nhập"
              rules={[{ required: true, message: 'Chọn ngày nhập' }]}
              style={{ flex: '0 0 180px', marginBottom: 16 }}
            >
              <PharmaDatePicker placeholder="dd/mm/yyyy" />
            </Form.Item>
          </div>
          <Form.Item name="notes" label="Ghi chú" style={{ marginBottom: 16 }}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.List name="items">
            {(fields, { add, remove }) => {
              if (!purchaseOrderId) {
                return renderManualLines(fields, add, remove);
              }
              if (poLoading) {
                return (
                  <div style={{ padding: '24px 0', textAlign: 'center' }}>
                    <Spin tip="Đang tải hàng từ PO..." />
                  </div>
                );
              }
              if (!linkedPo) {
                return (
                  <Typography.Text type="danger">
                    Không tải được PO — bỏ chọn PO hoặc chọn PO khác.
                  </Typography.Text>
                );
              }
              if (fields.length === 0) {
                return (
                  <Typography.Text type="secondary">PO đã nhận đủ — chọn PO khác.</Typography.Text>
                );
              }
              return renderPoLineTable(fields, remove);
            }}
          </Form.List>
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
        title={detail ? `Xem ${detail.grnNumber}` : 'Xem phiếu nhập hàng'}
        width={720}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail &&
          canWrite && (
            <Space>
              {detail.status === 1 && (
                <Button type="primary" onClick={() => handleComplete(detail.id)}>
                  Hoàn tất nhập kho
                </Button>
              )}
              {detail.status === 1 && (
                <Popconfirm
                  title="Hủy phiếu nhập nháp?"
                  okText="Hủy phiếu"
                  cancelText="Đóng"
                  onConfirm={() => void handleCancel(detail.id)}
                >
                  <Button danger>Hủy phiếu</Button>
                </Popconfirm>
              )}
              {canArchiveGrn(detail.status, detail.deletedAt) && (
                <Popconfirm title="Ẩn phiếu đã hủy khỏi danh sách?" onConfirm={() => void handleArchive(detail.id)}>
                  <Button danger>Ẩn phiếu</Button>
                </Popconfirm>
              )}
              {detail.deletedAt && canPurge && (
                <Popconfirm
                  title="Xóa vĩnh viễn? Không thể hoàn tác."
                  onConfirm={() => void handlePurge(detail.id)}
                >
                  <Button danger type="primary">
                    Xóa vĩnh viễn
                  </Button>
                </Popconfirm>
              )}
              {showLockedDeleteGrn(detail.status, detail.deletedAt) && (
                <Tooltip title="Không ẩn được phiếu đã ghi nhận nhập kho">
                  <Button disabled>Ẩn phiếu</Button>
                </Tooltip>
              )}
            </Space>
          )
        }
      >
        {detail && (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label="NCC">{detail.supplierName}</Descriptions.Item>
              <Descriptions.Item label="Kho">{detail.warehouseName}</Descriptions.Item>
              <Descriptions.Item label="PO">{detail.poNumber ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái">
                <Tag>{GRN_STATUS_LABELS[detail.status]}</Tag>
              </Descriptions.Item>
            </Descriptions>
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={detail.items}
              columns={grnLineColumns}
            />
          </>
        )}
      </Drawer>
    </Card>
  );
}

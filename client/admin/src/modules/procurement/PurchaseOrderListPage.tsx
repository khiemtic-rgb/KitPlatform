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
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { PlusOutlined, EyeOutlined } from '@ant-design/icons';
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
  purgePurchaseOrder,
} from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  PurchaseOrderDetail,
  PurchaseOrderListFilters,
  PurchaseOrderListItem,
  Supplier,
} from '@/shared/api/procurement.types';
import { PO_STATUS_LABELS, PO_STATUS_TAG, canEditPurchaseOrder } from '@/shared/api/procurement.types';
import { PurchaseOrderEditDrawer } from '@/modules/procurement/PurchaseOrderEditDrawer';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { PoUnitPriceField } from '@/modules/procurement/PoUnitPriceField';
import { PurchaseOrderFilterBar } from '@/modules/procurement/PurchaseOrderFilterBar';
import { filterPurchaseOrdersClient } from '@/modules/procurement/procurement-list-filters';
import { filterPurchaseOrdersByProduct } from '@/modules/procurement/procurement-product-filter';
import { downloadCsv } from '@/shared/utils/download-csv';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';
import { useProcurementWrite, useSystemDeletePermanent } from '@/shared/auth/usePermission';

interface PoLineForm {
  productId: string;
  productUnitId: string;
  orderedQty: number;
  unitPrice: number;
}

const emptyFilters: PurchaseOrderListFilters = {};

export function PurchaseOrderListPage() {
  const canWrite = useProcurementWrite();
  const canPurge = useSystemDeletePermanent();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<PurchaseOrderListItem[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
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
  const supplierId = Form.useWatch('supplierId', form);

  const loadMasterData = useCallback(async () => {
    const [sup, wh, prod] = await Promise.all([
      fetchSuppliers(true),
      fetchWarehouses(),
      fetchProducts({ page: 1, pageSize: 200 }),
    ]);
    setSuppliers(sup);
    setWarehouses(wh);
    setProducts(prod.items);
  }, []);

  const loadOrders = useCallback(async (nextFilters: PurchaseOrderListFilters, search: string) => {
    setFilters(nextFilters);
    setSearchInput(search);
    setLoading(true);
    try {
      let orders: PurchaseOrderListItem[];

      if (nextFilters.productId) {
        const byProduct = await fetchPurchaseOrders({ productId: nextFilters.productId });
        const all = await fetchPurchaseOrders();
        orders =
          byProduct.length >= all.length
            ? await filterPurchaseOrdersByProduct(all, nextFilters.productId, poDetailCacheRef.current)
            : byProduct;
      } else {
        orders = await fetchPurchaseOrders({
          ...nextFilters,
          search: search.trim() || undefined,
        });
      }

      setItems(filterPurchaseOrdersClient(orders, nextFilters, search));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được đơn đặt hàng'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadMasterData().catch(() => {
      message.error('Không tải được dữ liệu tham chiếu');
    });
    void loadOrders(emptyFilters, '');
  }, [loadMasterData, loadOrders]);

  const resetFilters = () => {
    void loadOrders(emptyFilters, '');
  };

  const openCreate = () => {
    form.resetFields();
    form.setFieldsValue({ items: [{ orderedQty: 1, unitPrice: 0 }] });
    setDrawerOpen(true);
  };

  const openDetail = async (id: string) => {
    try {
      const po = await fetchPurchaseOrder(id);
      setDetail(po);
      setPoDetailCache((cache) => ({ ...cache, [id]: po }));
      setDetailOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi tiết đơn đặt hàng'));
    }
  };

  const loadPoExpand = async (id: string) => {
    if (poDetailCache[id]) return;
    try {
      const po = await fetchPurchaseOrder(id);
      setPoDetailCache((cache) => ({ ...cache, [id]: po }));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được dòng hàng'));
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
        items: (values.items as PoLineForm[]).map((i) => ({
          productId: i.productId,
          productUnitId: i.productUnitId,
          orderedQty: i.orderedQty,
          unitPrice: i.unitPrice,
        })),
      });
      const approved = await approvePurchaseOrder(created.id);
      message.success(`Đã tạo ${approved.poNumber}`);
      setDrawerOpen(false);
      void loadOrders(filters, searchInput);
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không tạo được đơn đặt hàng'));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id: string) => {
    try {
      const updated = await approvePurchaseOrder(id);
      message.success(`Đã duyệt ${updated.poNumber}`);
      if (detail?.id === id) setDetail(updated);
      void loadOrders(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không duyệt được đơn'));
    }
  };

  const handleCancel = async (id: string) => {
    try {
      await cancelPurchaseOrder(id);
      message.success('Đã hủy đơn đặt hàng');
      if (detail?.id === id) setDetail(await fetchPurchaseOrder(id));
      void loadOrders(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được đơn'));
    }
  };

  const handleClose = async (id: string) => {
    try {
      const updated = await closePurchaseOrder(id);
      message.success(`Đã đóng ${updated.poNumber}`);
      if (detail?.id === id) setDetail(updated);
      void loadOrders(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đóng được đơn'));
    }
  };

  const handleArchive = async (id: string) => {
    try {
      await archivePurchaseOrder(id);
      message.success('Đã ẩn đơn đặt hàng (có thể xem trong bản ghi đã ẩn)');
      setDetailOpen(false);
      setDetail(null);
      void loadOrders(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ẩn được đơn'));
    }
  };

  const handlePurge = async (id: string) => {
    try {
      await purgePurchaseOrder(id);
      message.success('Đã xóa vĩnh viễn đơn đặt hàng');
      setDetailOpen(false);
      setDetail(null);
      void loadOrders(filters, searchInput);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa vĩnh viễn được đơn'));
    }
  };

  const canArchivePo = (status: number, deletedAt?: string) => status === 6 && !deletedAt;
  const showLockedDeletePo = (status: number, deletedAt?: string) =>
    status !== 1 && status !== 6 && !deletedAt;

  const exportOrders = () => {
    if (items.length === 0) {
      message.info('Không có dữ liệu để xuất');
      return;
    }
    downloadCsv(
      `don-dat-hang-${new Date().toISOString().slice(0, 10)}.csv`,
      ['Số PO', 'NCC', 'Kho nhận', 'Trạng thái', 'Ngày đặt', 'Số dòng', 'Tổng tiền'],
      items.map((row) => [
        row.poNumber,
        row.supplierName,
        row.warehouseName,
        PO_STATUS_LABELS[row.status] ?? String(row.status),
        formatDisplayDate(row.orderDate),
        String(row.itemCount),
        formatDisplayMoney(row.totalAmount),
      ]),
    );
  };

  const columns: ColumnsType<PurchaseOrderListItem> = [
    { title: 'Số PO', dataIndex: 'poNumber', width: 140 },
    { title: 'NCC', dataIndex: 'supplierName' },
    { title: 'Kho nhận', dataIndex: 'warehouseName' },
    {
      title: 'Ngày đặt',
      dataIndex: 'orderDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 130,
      render: (s: number, row) => (
        <Space size={4}>
          <Tag color={PO_STATUS_TAG[s] ?? 'default'}>{PO_STATUS_LABELS[s] ?? s}</Tag>
          {row.deletedAt ? <Tag color="default">Đã ẩn</Tag> : null}
        </Space>
      ),
    },
    {
      title: 'Tổng tiền',
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
          Xem
        </Button>
      ),
    },
  ];

  const poLineColumns: ColumnsType<PurchaseOrderDetail['items'][number]> = [
    { title: 'Mã SP', dataIndex: 'productCode', width: 100 },
    { title: 'Tên SP', dataIndex: 'productName' },
    { title: 'ĐVT', dataIndex: 'unitName', width: 70 },
    { title: 'Đặt', dataIndex: 'orderedQty', width: 70, align: 'right' },
    { title: 'Đã nhận', dataIndex: 'receivedQty', width: 80, align: 'right' },
    {
      title: 'Chưa nhận',
      width: 85,
      align: 'right',
      render: (_, row) => (row.orderedQty - row.receivedQty).toLocaleString('vi-VN'),
    },
    {
      title: 'Đơn giá',
      dataIndex: 'unitPrice',
      width: 90,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
  ];

  return (
    <Card
      title="Đơn đặt hàng"
      extra={
        canWrite ? (
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Tạo đơn
          </Button>
        ) : undefined
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
        pagination={{ pageSize: 20, showTotal: (total) => `${total} đơn` }}
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
                dataSource={po.items}
                columns={poLineColumns}
              />
            );
          },
        }}
      />

      <Drawer
        title="Tạo đơn đặt hàng"
        width={960}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" onClick={() => void handleCreate()} loading={saving}>
            Tạo đơn
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="supplierId" label="Nhà cung cấp" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={suppliers.map((s) => ({ value: s.id, label: `${s.supplierCode} — ${s.supplierName}` }))}
            />
          </Form.Item>
          <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
            <Form.Item
              name="warehouseId"
              label="Kho nhận"
              rules={[{ required: true }]}
              style={{ flex: '1 1 280px', marginBottom: 16, minWidth: 200 }}
            >
              <Select options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))} />
            </Form.Item>
            <Form.Item
              name="expectedDate"
              label="Ngày dự kiến nhận"
              style={{ flex: '0 0 200px', marginBottom: 16 }}
            >
              <PharmaDatePicker placeholder="dd/mm/yyyy" />
            </Form.Item>
          </div>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.List name="items">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <Form.Item key={field.key} noStyle shouldUpdate>
                    {() => {
                      const productId = form.getFieldValue(['items', field.name, 'productId']) as
                        | string
                        | undefined;
                      return (
                        <div
                          key={field.key}
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
                            style={{ flex: '1 1 280px', marginBottom: 0, minWidth: 220 }}
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
                            style={{ flex: '0 0 120px', marginBottom: 0 }}
                          >
                            <ProductUnitSelect productId={productId} width={120} />
                          </Form.Item>
                          <Form.Item
                            {...field}
                            name={[field.name, 'orderedQty']}
                            label="SL đặt"
                            rules={[{ required: true }]}
                            style={{ flex: '0 0 88px', marginBottom: 0 }}
                          >
                            <InputNumber min={0.01} placeholder="SL" style={{ width: '100%' }} />
                          </Form.Item>
                          <Form.Item
                            {...field}
                            name={[field.name, 'unitPrice']}
                            label="Đơn giá mua"
                            rules={[{ required: true }]}
                            style={{ flex: '0 0 140px', marginBottom: 0 }}
                          >
                            <PoUnitPriceField
                              supplierId={supplierId}
                              productId={productId}
                              form={form}
                              fieldName={field.name}
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
                <Button type="dashed" onClick={() => add({ orderedQty: 1, unitPrice: 0 })} block>
                  Thêm dòng
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Drawer>

      <Drawer
        title={detail ? `Xem ${detail.poNumber}` : 'Xem đơn đặt hàng'}
        width={720}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail &&
          canWrite && (
            <Space>
              {canEditPurchaseOrder(detail.status) && !detail.deletedAt && (
                <Button onClick={() => setEditPoOpen(true)}>Sửa đơn</Button>
              )}
              {detail.status === 1 && (
                <Button type="primary" onClick={() => handleApprove(detail.id)}>
                  Duyệt
                </Button>
              )}
              {(detail.status === 1 || detail.status === 2) && (
                <Popconfirm title="Huỷ đơn đặt hàng này?" onConfirm={() => void handleCancel(detail.id)}>
                  <Button danger>Huỷ đơn</Button>
                </Popconfirm>
              )}
              {detail.status === 4 && (
                <Button type="primary" onClick={() => handleClose(detail.id)}>
                  Đóng đơn
                </Button>
              )}
              {canArchivePo(detail.status, detail.deletedAt) && (
                <Popconfirm title="Ẩn đơn đã huỷ khỏi danh sách?" onConfirm={() => void handleArchive(detail.id)}>
                  <Button danger>Ẩn đơn</Button>
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
              {showLockedDeletePo(detail.status, detail.deletedAt) && (
                <Tooltip title="Chỉ ẩn được đơn đã huỷ">
                  <Button disabled>Ẩn đơn</Button>
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
              <Descriptions.Item label="Trạng thái">
                <Tag color={PO_STATUS_TAG[detail.status] ?? 'default'}>{PO_STATUS_LABELS[detail.status]}</Tag>
              </Descriptions.Item>
              <Descriptions.Item label="Tổng">{formatDisplayMoney(detail.totalAmount)}</Descriptions.Item>
            </Descriptions>
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={detail.items}
              columns={poLineColumns}
            />
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
          void loadOrders(filters, searchInput);
        }}
      />
    </Card>
  );
}

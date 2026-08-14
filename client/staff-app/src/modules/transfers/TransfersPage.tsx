import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Drawer,
  Empty,
  Form,
  Input,
  InputNumber,
  Popconfirm,
  Segmented,
  Select,
  Spin,
  Tag,
  Typography,
} from 'antd';
import {
  MinusCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  StopOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  cancelTransfer,
  completeTransfer,
  createTransfer,
  fetchStockBatches,
  fetchStockProducts,
  fetchTransfer,
  fetchTransfers,
  receiveTransfer,
  shipTransfer,
} from '@/shared/api/inventory.api';
import { fetchWarehouses } from '@/shared/api/sales.api';
import type { TransferDetail, TransferListItem } from '@/shared/api/inventory.types';
import type { Warehouse } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';
import { usePosSession } from '@/modules/pos/pos-session.store';
import {
  canCancelTransfer,
  canCompleteTransfer,
  canReceiveTransfer,
  canShipTransfer,
  transferStatusColor,
  transferStatusLabel,
} from '@/modules/transfers/transfer-labels';

type TransferLineForm = {
  productId?: string;
  batchId?: string;
  quantity: number;
};

type StatusFilter = 'all' | 'draft' | 'shipped' | 'completed' | 'cancelled';

function warehouseOptionLabel(w: Warehouse) {
  return w.branchName ? `${w.warehouseName} · ${w.branchName}` : w.warehouseName;
}

function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const parsed = dayjs(value);
  if (!parsed.isValid()) return '—';
  const now = dayjs();
  if (now.isSame(parsed, 'day')) return `Hôm nay ${parsed.format('HH:mm')}`;
  if (now.subtract(1, 'day').isSame(parsed, 'day')) return `Hôm qua ${parsed.format('HH:mm')}`;
  return parsed.format('DD/MM HH:mm');
}

function statusToApi(filter: StatusFilter): number | undefined {
  if (filter === 'draft') return 1;
  if (filter === 'shipped') return 2;
  if (filter === 'completed') return 3;
  if (filter === 'cancelled') return 4;
  return undefined;
}

function TransferLineRow({
  field,
  fromWarehouseId,
  remove,
}: {
  field: { name: number; key: number };
  fromWarehouseId?: string;
  remove: () => void;
}) {
  const form = Form.useFormInstance();
  const productId = Form.useWatch(['items', field.name, 'productId'], form) as string | undefined;
  const [productOptions, setProductOptions] = useState<{ value: string; label: string }[]>([]);
  const [batchOptions, setBatchOptions] = useState<{ value: string; label: string }[]>([]);
  const [productLoading, setProductLoading] = useState(false);
  const [batchLoading, setBatchLoading] = useState(false);
  const productSearchTimer = useRef<number | undefined>(undefined);

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
                label: `${p.productCode} · ${p.productName} · Tồn ${p.totalQuantity}`,
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
    async (nextProductId: string) => {
      if (!fromWarehouseId) {
        setBatchOptions([]);
        form.setFieldValue(['items', field.name, 'batchId'], undefined);
        return;
      }
      setBatchLoading(true);
      try {
        const result = await fetchStockBatches({
          warehouseId: fromWarehouseId,
          productId: nextProductId,
          page: 1,
          pageSize: 50,
        });
        const options = result.items
          .filter((b) => b.quantityAvailable > 0)
          .map((b) => ({
            value: b.id,
            label: `${b.batchNumber}${b.expiryDate ? ` · HSD ${dayjs(b.expiryDate).format('MM/YYYY')}` : ''} · ${b.quantityAvailable}`,
          }));
        setBatchOptions(options);
        const current = form.getFieldValue(['items', field.name, 'batchId']) as string | undefined;
        if (!current || !options.some((o) => o.value === current)) {
          form.setFieldValue(['items', field.name, 'batchId'], options[0]?.value);
        }
      } catch {
        setBatchOptions([]);
        form.setFieldValue(['items', field.name, 'batchId'], undefined);
      } finally {
        setBatchLoading(false);
      }
    },
    [field.name, form, fromWarehouseId],
  );

  useEffect(() => {
    searchProducts('');
    return () => window.clearTimeout(productSearchTimer.current);
  }, [fromWarehouseId, searchProducts]);

  useEffect(() => {
    if (!fromWarehouseId || !productId) {
      setBatchOptions([]);
      if (!productId) {
        form.setFieldValue(['items', field.name, 'batchId'], undefined);
      }
      return;
    }
    void loadBatches(productId);
  }, [fromWarehouseId, productId, loadBatches, field.name, form]);

  return (
    <div className="transfer-line-card">
      <div className="transfer-line-card-head">
        <Typography.Text strong>Dòng {field.name + 1}</Typography.Text>
        <Button type="text" danger icon={<MinusCircleOutlined />} onClick={remove} aria-label="Xóa dòng" />
      </div>
      <Form.Item
        name={[field.name, 'productId']}
        label="Sản phẩm"
        rules={[{ required: true, message: 'Chọn sản phẩm' }]}
      >
        <Select
          size="large"
          showSearch
          filterOption={false}
          placeholder={fromWarehouseId ? 'Tìm mã hoặc tên SP' : 'Chọn kho đi trước'}
          disabled={!fromWarehouseId}
          loading={productLoading}
          options={productOptions}
          onSearch={searchProducts}
          onDropdownVisibleChange={(open) => {
            if (open) searchProducts('');
          }}
        />
      </Form.Item>
      <Form.Item name={[field.name, 'batchId']} label="Lô" rules={[{ required: true, message: 'Chọn lô' }]}>
        <Select
          size="large"
          placeholder="Chọn lô"
          disabled={!productId || batchOptions.length === 0}
          loading={batchLoading}
          options={batchOptions}
        />
      </Form.Item>
      <Form.Item
        name={[field.name, 'quantity']}
        label="Số lượng"
        rules={[{ required: true, message: 'Nhập số lượng' }]}
      >
        <InputNumber size="large" min={0.001} style={{ width: '100%' }} />
      </Form.Item>
    </div>
  );
}

export function TransfersPage() {
  const { message } = App.useApp();
  const posWarehouseId = usePosSession((s) => s.warehouseId);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<TransferListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [query, setQuery] = useState('');
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<TransferDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [completing, setCompleting] = useState(false);
  const [actingId, setActingId] = useState<string | null>(null);
  const [receiveOpen, setReceiveOpen] = useState(false);
  const [receiveNotes, setReceiveNotes] = useState('');
  const [receiveQtyByItem, setReceiveQtyByItem] = useState<Record<string, number>>({});
  const [form] = Form.useForm();
  const fromWarehouseId = Form.useWatch('fromWarehouseId', form) as string | undefined;
  const prevFromWarehouseRef = useRef<string | undefined>(undefined);

  const load = useCallback(
    async (mode: 'full' | 'refresh' | 'more' = 'full') => {
      if (mode === 'full') {
        setLoading(true);
        setLoadError(null);
      } else if (mode === 'refresh') {
        setRefreshing(true);
      }
      const nextPage = mode === 'more' ? page + 1 : 1;
      try {
        const [paged, wh] = await Promise.all([
          fetchTransfers({ status: statusToApi(statusFilter), page: nextPage, pageSize: 20 }),
          mode === 'more' ? Promise.resolve(warehouses) : fetchWarehouses(),
        ]);
        setItems((prev) => (mode === 'more' ? [...prev, ...paged.items] : paged.items));
        setTotal(paged.total);
        setPage(nextPage);
        if (mode !== 'more') setWarehouses(wh as Warehouse[]);
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được phiếu chuyển kho');
        if (mode === 'full') {
          setItems([]);
          setLoadError(text);
        } else {
          message.error(text);
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [message, statusFilter, page, warehouses],
  );

  useEffect(() => {
    void load('full');
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reload when filter changes only
  }, [statusFilter]);

  useEffect(() => {
    if (!createOpen) {
      prevFromWarehouseRef.current = undefined;
      return;
    }
    if (prevFromWarehouseRef.current && prevFromWarehouseRef.current !== fromWarehouseId) {
      const lines = form.getFieldValue('items') as TransferLineForm[] | undefined;
      if (lines?.length) {
        form.setFieldsValue({
          items: lines.map((line) => ({ quantity: line.quantity ?? 1 })),
        });
      }
    }
    prevFromWarehouseRef.current = fromWarehouseId;
  }, [createOpen, fromWarehouseId, form]);

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (i) =>
        i.transferNumber.toLowerCase().includes(q) ||
        i.fromWarehouseName.toLowerCase().includes(q) ||
        i.toWarehouseName.toLowerCase().includes(q),
    );
  }, [items, query]);

  const openCreate = () => {
    form.resetFields();
    const defaultFrom =
      posWarehouseId && warehouses.some((w) => w.id === posWarehouseId) ? posWarehouseId : warehouses[0]?.id;
    form.setFieldsValue({
      fromWarehouseId: defaultFrom,
      items: [{ quantity: 1 }],
    });
    setCreateOpen(true);
  };

  const openDetail = async (id: string) => {
    setDetailLoading(true);
    setDetailOpen(true);
    try {
      setDetail(await fetchTransfer(id));
    } catch (error) {
      setDetailOpen(false);
      message.error(apiErrorMessage(error, 'Không tải được chi tiết phiếu'));
    } finally {
      setDetailLoading(false);
    }
  };

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      if (values.fromWarehouseId === values.toWarehouseId) {
        message.warning('Kho đi và kho đến phải khác nhau');
        return;
      }
      const lines = (values.items as TransferLineForm[]).filter((line) => line.batchId);
      if (lines.length === 0) {
        message.warning('Thêm ít nhất một dòng có lô');
        return;
      }
      setSaving(true);
      const created = await createTransfer({
        fromWarehouseId: values.fromWarehouseId,
        toWarehouseId: values.toWarehouseId,
        notes: values.notes,
        items: lines.map((line) => ({
          batchId: line.batchId!,
          quantity: line.quantity,
        })),
      });
      message.success(`Đã tạo phiếu ${created.transferNumber}`);
      setCreateOpen(false);
      await load('refresh');
    } catch (error) {
      if ((error as { errorFields?: unknown })?.errorFields) return;
      message.error(apiErrorMessage(error, 'Không tạo được phiếu chuyển kho'));
    } finally {
      setSaving(false);
    }
  };

  const handleShip = async (id: string) => {
    setActingId(id);
    setCompleting(true);
    try {
      await shipTransfer(id);
      message.success('Đã gửi hàng — chờ kho nhận xác nhận');
      if (detail?.id === id) setDetail(await fetchTransfer(id));
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được phiếu'));
    } finally {
      setCompleting(false);
      setActingId(null);
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
  };

  const handleReceiveConfirm = async () => {
    if (!detail) return;
    const hasShortage = detail.items.some(
      (line) => (receiveQtyByItem[line.id] ?? line.quantity) < line.quantity,
    );
    if (hasShortage && !receiveNotes.trim()) {
      message.warning('Khi nhận thiếu phải nhập ghi chú lý do lệch');
      return;
    }
    setCompleting(true);
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
        hasShortage ? 'Đã nhận có lệch — phần thiếu đã hoàn về kho xuất' : 'Đã xác nhận nhận hàng',
      );
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không nhận được phiếu'));
    } finally {
      setCompleting(false);
    }
  };

  const handleComplete = async (id: string) => {
    setActingId(id);
    setCompleting(true);
    try {
      await completeTransfer(id);
      message.success('Đã gửi và nhận đủ (một bước)');
      if (detail?.id === id) setDetail(await fetchTransfer(id));
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hoàn tất được phiếu'));
    } finally {
      setCompleting(false);
      setActingId(null);
    }
  };

  const handleCancel = async (id: string) => {
    setActingId(id);
    setCompleting(true);
    try {
      await cancelTransfer(id);
      message.success('Đã hủy phiếu chuyển kho');
      if (detail?.id === id) setDetail(await fetchTransfer(id));
      setReceiveOpen(false);
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiếu'));
    } finally {
      setCompleting(false);
      setActingId(null);
    }
  };

  const warehouseOptions = warehouses.map((w) => ({
    value: w.id,
    label: warehouseOptionLabel(w),
  }));

  const hasMore = items.length < total;
  const detailHasShortage =
    detail?.items.some(
      (line) => line.receivedQuantity != null && line.receivedQuantity < line.quantity,
    ) ?? false;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Chuyển kho"
        subtitle={
          loadError
            ? 'Không tải được danh sách'
            : total > 0
              ? `${total} phiếu`
              : 'Chưa có phiếu'
        }
        backTo="/"
        right={
          <Button
            type="text"
            className="chat-header-refresh"
            icon={<ReloadOutlined spin={refreshing || loading} />}
            aria-label="Tải lại"
            onClick={() => void load(loadError ? 'full' : 'refresh')}
          />
        }
      />
      <main className="staff-body">
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được chuyển kho"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load('full')}>
                Thử lại
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 10, fontSize: 12 }}>
            Tạo → Gửi (trừ kho xuất) → Nhận (cộng kho nhận). Quản lý cả 2 kho: dùng Gửi + nhận đủ.
          </Typography.Text>
        )}

        <div className="transfer-toolbar">
          <Input
            size="large"
            allowClear
            prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
            placeholder="Tìm mã phiếu, kho…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            disabled={Boolean(loadError) && items.length === 0}
          />
          <div className="transfer-toolbar__row">
            <Segmented
              size="middle"
              value={statusFilter}
              onChange={(v) => setStatusFilter(v as StatusFilter)}
              disabled={Boolean(loadError) && items.length === 0}
              options={[
                { label: 'Tất cả', value: 'all' },
                { label: 'Chờ gửi', value: 'draft' },
                { label: 'Đang chuyển', value: 'shipped' },
                { label: 'Xong', value: 'completed' },
              ]}
            />
            <Button
              type="primary"
              size="large"
              icon={<PlusOutlined />}
              onClick={openCreate}
              disabled={warehouses.length < 2}
            >
              Tạo
            </Button>
          </div>
        </div>

        {warehouses.length < 2 && !loadError ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 12 }}
            message="Cần ít nhất 2 kho"
            description="Chưa đủ kho trong phạm vi quyền để chuyển hàng giữa quầy."
          />
        ) : null}

        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : loadError && items.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Bấm Thử lại khi mạng ổn" />
        ) : visible.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={query.trim() ? 'Không tìm thấy phiếu khớp' : 'Chưa có phiếu chuyển kho'}
          />
        ) : (
          visible.map((item) => (
            <article key={item.id} className="transfer-card">
              <button type="button" className="transfer-card__main" onClick={() => void openDetail(item.id)}>
                <div className="transfer-card__head">
                  <div className="transfer-card__ids">
                    <Typography.Text strong className="transfer-card__number">
                      {item.transferNumber}
                    </Typography.Text>
                    <Typography.Text type="secondary" className="transfer-card__when">
                      {formatWhen(item.transferDate)} · {item.itemCount} dòng
                    </Typography.Text>
                  </div>
                  <Tag color={transferStatusColor(item.status, item.hasShortage)}>
                    {transferStatusLabel(item.status, item.hasShortage)}
                  </Tag>
                </div>
                <div className="transfer-card__route">
                  <div>
                    <span className="transfer-card__route-label">Từ</span> {item.fromWarehouseName}
                  </div>
                  <div>
                    <span className="transfer-card__route-label">Đến</span> {item.toWarehouseName}
                  </div>
                </div>
              </button>

              {(canShipTransfer(item.status) ||
                canCompleteTransfer(item.status) ||
                canReceiveTransfer(item.status)) && (
                <div className="transfer-card__actions">
                  {canShipTransfer(item.status) ? (
                    <Button
                      className="transfer-card__btn"
                      loading={actingId === item.id && completing}
                      onClick={(e) => {
                        e.stopPropagation();
                        void handleShip(item.id);
                      }}
                    >
                      Gửi hàng
                    </Button>
                  ) : null}
                  {canCompleteTransfer(item.status) ? (
                    <Button
                      className="transfer-card__btn"
                      type="primary"
                      loading={actingId === item.id && completing}
                      onClick={(e) => {
                        e.stopPropagation();
                        void handleComplete(item.id);
                      }}
                    >
                      Gửi + nhận đủ
                    </Button>
                  ) : null}
                  {canReceiveTransfer(item.status) ? (
                    <Button
                      className="transfer-card__btn"
                      type="primary"
                      loading={actingId === item.id && completing}
                      onClick={(e) => {
                        e.stopPropagation();
                        void (async () => {
                          setActingId(item.id);
                          try {
                            const doc = await fetchTransfer(item.id);
                            setDetail(doc);
                            setDetailOpen(true);
                            openReceive(doc);
                          } catch (error) {
                            message.error(apiErrorMessage(error, 'Không mở nhận hàng'));
                          } finally {
                            setActingId(null);
                          }
                        })();
                      }}
                    >
                      Nhận hàng
                    </Button>
                  ) : null}
                </div>
              )}
            </article>
          ))
        )}

        {hasMore && !loading && !loadError ? (
          <Button
            block
            size="large"
            className="customer-load-more"
            loading={refreshing}
            onClick={() => void load('more')}
          >
            Xem thêm ({items.length}/{total})
          </Button>
        ) : null}
      </main>

      <Drawer
        title="Tạo phiếu chuyển kho"
        placement="bottom"
        height="92%"
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        styles={{ body: { paddingBottom: 80 } }}
        footer={
          <Button type="primary" block size="large" loading={saving} onClick={() => void handleCreate()}>
            Lưu phiếu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="fromWarehouseId"
            label="Kho đi (lấy hàng)"
            rules={[{ required: true, message: 'Chọn kho đi' }]}
          >
            <Select size="large" options={warehouseOptions} placeholder="VD: Quầy 1" />
          </Form.Item>
          <Form.Item
            name="toWarehouseId"
            label="Kho đến (nhận hàng)"
            rules={[{ required: true, message: 'Chọn kho đến' }]}
          >
            <Select size="large" options={warehouseOptions} placeholder="VD: Quầy 2" />
          </Form.Item>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} placeholder="VD: Quầy 2 thiếu Paracetamol" />
          </Form.Item>
          <Form.List name="items">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <TransferLineRow
                    key={field.key}
                    field={field}
                    fromWarehouseId={fromWarehouseId}
                    remove={() => remove(field.name)}
                  />
                ))}
                <Button type="dashed" size="large" onClick={() => add({ quantity: 1 })} block icon={<PlusOutlined />}>
                  Thêm dòng
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Drawer>

      <Drawer
        title={detail ? detail.transferNumber : 'Chi tiết phiếu'}
        placement="bottom"
        height="88%"
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        styles={{ body: { paddingBottom: 96 } }}
        footer={
          detail && !detailLoading ? (
            <div className="transfer-detail-footer">
              {canCancelTransfer(detail.status) ? (
                <Popconfirm
                  title="Hủy phiếu chuyển kho?"
                  onConfirm={() => void handleCancel(detail.id)}
                >
                  <Button danger size="large" loading={completing} icon={<StopOutlined />}>
                    Hủy
                  </Button>
                </Popconfirm>
              ) : null}
              {canShipTransfer(detail.status) ? (
                <Button size="large" loading={completing} onClick={() => void handleShip(detail.id)}>
                  Gửi hàng
                </Button>
              ) : null}
              {canCompleteTransfer(detail.status) ? (
                <Button
                  type="primary"
                  size="large"
                  loading={completing}
                  onClick={() => void handleComplete(detail.id)}
                >
                  Gửi + nhận đủ
                </Button>
              ) : null}
              {canReceiveTransfer(detail.status) ? (
                <Button type="primary" size="large" loading={completing} onClick={() => openReceive(detail)}>
                  Nhận hàng
                </Button>
              ) : null}
            </div>
          ) : null
        }
      >
        {detailLoading || !detail ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : (
          <>
            <div className="transfer-detail-meta">
              <Tag color={transferStatusColor(detail.status, detailHasShortage || detail.hasShortage)}>
                {transferStatusLabel(detail.status, detailHasShortage || detail.hasShortage)}
              </Tag>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {formatWhen(detail.transferDate)}
              </Typography.Text>
            </div>
            <div className="transfer-detail-route">
              <div>
                <span className="transfer-card__route-label">Từ</span> {detail.fromWarehouseName}
              </div>
              <div>
                <span className="transfer-card__route-label">Đến</span> {detail.toWarehouseName}
              </div>
            </div>
            {detail.shippedAt ? (
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginBottom: 4 }}>
                Gửi: {formatWhen(detail.shippedAt)}
              </Typography.Text>
            ) : null}
            {detail.receivedAt ? (
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginBottom: 8 }}>
                Nhận: {formatWhen(detail.receivedAt)}
              </Typography.Text>
            ) : null}
            {detail.notes ? (
              <Typography.Paragraph style={{ marginBottom: 8, fontSize: 13 }}>
                <strong>Ghi chú:</strong> {detail.notes}
              </Typography.Paragraph>
            ) : null}
            {detail.receiveNotes ? (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                message="Ghi chú nhận thiếu"
                description={detail.receiveNotes}
              />
            ) : null}
            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              Chi tiết hàng ({detail.items.length})
            </Typography.Text>
            {detail.items.map((line) => {
              const short =
                line.receivedQuantity != null && line.receivedQuantity < line.quantity;
              return (
                <div key={line.id} className="transfer-detail-line">
                  <Typography.Text strong>{line.productName}</Typography.Text>
                  <Typography.Text
                    type={short ? 'danger' : 'secondary'}
                    style={{ display: 'block', fontSize: 12 }}
                  >
                    {line.productCode} · Lô {line.batchNumber}
                  </Typography.Text>
                  <div className="transfer-detail-line__qty">
                    <span>Phiếu {line.quantity}</span>
                    {line.receivedQuantity != null ? (
                      <span className={short ? 'is-short' : ''}>
                        Nhận {line.receivedQuantity}
                        {short ? ' (thiếu)' : ''}
                      </span>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </>
        )}
      </Drawer>

      <Drawer
        title="Xác nhận nhận hàng"
        placement="bottom"
        height="88%"
        open={receiveOpen}
        onClose={() => setReceiveOpen(false)}
        styles={{ body: { paddingBottom: 80 } }}
        footer={
          <Button type="primary" block size="large" loading={completing} onClick={() => void handleReceiveConfirm()}>
            Xác nhận nhận
          </Button>
        }
      >
        {detail ? (
          <>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 12 }}>
              Nhập SL thực nhận (≤ SL phiếu). Phần thiếu hoàn về kho xuất; phải ghi chú khi thiếu.
            </Typography.Text>
            {detail.items.map((line) => (
              <div key={line.id} className="transfer-detail-line" style={{ marginBottom: 12 }}>
                <Typography.Text strong>{line.productName}</Typography.Text>
                <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginBottom: 6 }}>
                  Lô {line.batchNumber} · SL phiếu {line.quantity}
                </Typography.Text>
                <InputNumber
                  size="large"
                  min={0}
                  max={line.quantity}
                  value={receiveQtyByItem[line.id] ?? line.quantity}
                  onChange={(v) =>
                    setReceiveQtyByItem((prev) => ({
                      ...prev,
                      [line.id]: typeof v === 'number' ? v : 0,
                    }))
                  }
                  style={{ width: '100%' }}
                  addonBefore="SL nhận"
                />
              </div>
            ))}
            <Form.Item label="Ghi chú lệch / nhận thiếu" style={{ marginTop: 8 }}>
              <Input.TextArea
                rows={3}
                value={receiveNotes}
                onChange={(e) => setReceiveNotes(e.target.value)}
                placeholder="VD: Thiếu 2 vỉ khi kiểm lại"
              />
            </Form.Item>
          </>
        ) : null}
      </Drawer>
    </div>
  );
}

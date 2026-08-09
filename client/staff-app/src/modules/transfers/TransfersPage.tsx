import { useCallback, useEffect, useRef, useState } from 'react';
import { App, Button, Drawer, Form, Input, InputNumber, Select, Space, Spin, Tag, Typography } from 'antd';
import { MinusCircleOutlined, PlusOutlined, ReloadOutlined, StopOutlined } from '@ant-design/icons';
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

function warehouseOptionLabel(w: Warehouse) {
  return w.branchName ? `${w.warehouseName} · ${w.branchName}` : w.warehouseName;
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
      <Form.Item
        name={[field.name, 'batchId']}
        label="Lô"
        rules={[{ required: true, message: 'Chọn lô' }]}
      >
        <Select
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
        <InputNumber min={0.001} style={{ width: '100%' }} />
      </Form.Item>
    </div>
  );
}

export function TransfersPage() {
  const { message } = App.useApp();
  const posWarehouseId = usePosSession((s) => s.warehouseId);
  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState<TransferListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState<number | undefined>();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<TransferDetail | null>(null);
  const [saving, setSaving] = useState(false);
  const [completing, setCompleting] = useState(false);
  const [receiveOpen, setReceiveOpen] = useState(false);
  const [receiveNotes, setReceiveNotes] = useState('');
  const [receiveQtyByItem, setReceiveQtyByItem] = useState<Record<string, number>>({});
  const [form] = Form.useForm();
  const fromWarehouseId = Form.useWatch('fromWarehouseId', form) as string | undefined;
  const prevFromWarehouseRef = useRef<string | undefined>(undefined);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [paged, wh] = await Promise.all([
        fetchTransfers({ status: statusFilter, page, pageSize: 20 }),
        fetchWarehouses(),
      ]);
      setItems(paged.items);
      setTotal(paged.total);
      setWarehouses(wh);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được phiếu chuyển kho'));
    } finally {
      setLoading(false);
    }
  }, [message, statusFilter, page]);

  useEffect(() => {
    void load();
  }, [load]);

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
    try {
      setDetail(await fetchTransfer(id));
      setDetailOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi tiết phiếu'));
    }
  };

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      if (values.fromWarehouseId === values.toWarehouseId) {
        message.warning('Kho đi và kho đến phải khác nhau');
        return;
      }
      setSaving(true);
      const created = await createTransfer({
        fromWarehouseId: values.fromWarehouseId,
        toWarehouseId: values.toWarehouseId,
        notes: values.notes,
        items: (values.items as TransferLineForm[])
          .filter((line) => line.batchId)
          .map((line) => ({
            batchId: line.batchId!,
            quantity: line.quantity,
          })),
      });
      message.success(`Đã tạo phiếu ${created.transferNumber}`);
      setCreateOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được phiếu chuyển kho'));
    } finally {
      setSaving(false);
    }
  };

  const handleShip = async (id: string) => {
    setCompleting(true);
    try {
      await shipTransfer(id);
      message.success('Đã gửi hàng — chờ kho nhận xác nhận');
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được phiếu'));
    } finally {
      setCompleting(false);
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
        hasShortage
          ? 'Đã nhận có lệch — phần thiếu đã hoàn về kho xuất'
          : 'Đã xác nhận nhận hàng',
      );
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không nhận được phiếu'));
    } finally {
      setCompleting(false);
    }
  };

  const handleComplete = async (id: string) => {
    setCompleting(true);
    try {
      await completeTransfer(id);
      message.success('Đã gửi và nhận đủ (một bước)');
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hoàn tất được phiếu'));
    } finally {
      setCompleting(false);
    }
  };

  const handleCancel = async (id: string) => {
    setCompleting(true);
    try {
      await cancelTransfer(id);
      message.success('Đã hủy phiếu chuyển kho');
      if (detail?.id === id) {
        setDetail(await fetchTransfer(id));
      }
      setReceiveOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiếu'));
    } finally {
      setCompleting(false);
    }
  };

  const warehouseOptions = warehouses.map((w) => ({
    value: w.id,
    label: warehouseOptionLabel(w),
  }));

  return (
    <div className="staff-shell">
      <StaffPageHeader title="Chuyển kho" backTo="/" />
      <main className="staff-body">
        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 12 }}>
          Quy trình: tạo phiếu → Gửi (trừ kho xuất) → Nhận (cộng kho nhận). Cùng quản lý 2 kho có thể Gửi + nhận đủ một bước.
        </Typography.Text>

        <Space style={{ marginBottom: 12, width: '100%' }} wrap>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
          <Select
            allowClear
            placeholder="Trạng thái"
            style={{ minWidth: 140 }}
            value={statusFilter}
            onChange={(v) => {
              setStatusFilter(v);
              setPage(1);
            }}
            options={[
              { value: 1, label: 'Chờ gửi' },
              { value: 2, label: 'Đang chuyển' },
              { value: 3, label: 'Hoàn tất' },
              { value: 4, label: 'Đã hủy' },
            ]}
          />
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={warehouses.length < 2}>
            Tạo phiếu
          </Button>
        </Space>

        {warehouses.length < 2 ? (
          <Typography.Text type="warning">Cần ít nhất 2 kho để chuyển hàng giữa quầy.</Typography.Text>
        ) : null}

        {loading ? (
          <Spin />
        ) : items.length === 0 ? (
          <Typography.Text type="secondary">Chưa có phiếu chuyển kho</Typography.Text>
        ) : (
          items.map((item) => (
            <button
              key={item.id}
              type="button"
              className="transfer-list-item"
              onClick={() => void openDetail(item.id)}
            >
              <div className="transfer-list-item-top">
                <Typography.Text strong>{item.transferNumber}</Typography.Text>
                <Space size={4}>
                  <Tag color={transferStatusColor(item.status, item.hasShortage)}>
                    {transferStatusLabel(item.status, item.hasShortage)}
                  </Tag>
                </Space>
              </div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {item.fromWarehouseName} → {item.toWarehouseName}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                {dayjs(item.transferDate).format('DD/MM/YYYY')} · {item.itemCount} dòng
              </Typography.Text>
            </button>
          ))
        )}

        {total > 20 ? (
          <Space style={{ marginTop: 12 }}>
            <Button disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
              Trước
            </Button>
            <Typography.Text type="secondary">
              Trang {page}/{Math.max(1, Math.ceil(total / 20))}
            </Typography.Text>
            <Button
              disabled={page >= Math.ceil(total / 20)}
              onClick={() => setPage((p) => p + 1)}
            >
              Sau
            </Button>
          </Space>
        ) : null}
      </main>

      <Drawer
        title="Tạo phiếu chuyển kho"
        placement="bottom"
        height="92%"
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        extra={
          <Button type="primary" loading={saving} onClick={() => void handleCreate()}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="fromWarehouseId"
            label="Kho đi (lấy hàng)"
            rules={[{ required: true, message: 'Chọn kho đi' }]}
          >
            <Select options={warehouseOptions} placeholder="VD: Quầy 1" />
          </Form.Item>
          <Form.Item
            name="toWarehouseId"
            label="Kho đến (nhận hàng)"
            rules={[{ required: true, message: 'Chọn kho đến' }]}
          >
            <Select options={warehouseOptions} placeholder="VD: Quầy 2" />
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
                <Button type="dashed" onClick={() => add({ quantity: 1 })} block icon={<PlusOutlined />}>
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
        height="85%"
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail && canCancelTransfer(detail.status) ? (
            <Space wrap>
              <Button danger loading={completing} icon={<StopOutlined />} onClick={() => void handleCancel(detail.id)}>
                Hủy
              </Button>
              {canShipTransfer(detail.status) ? (
                <Button loading={completing} onClick={() => void handleShip(detail.id)}>
                  Gửi hàng
                </Button>
              ) : null}
              {canCompleteTransfer(detail.status) ? (
                <Button type="primary" loading={completing} onClick={() => void handleComplete(detail.id)}>
                  Gửi + nhận đủ
                </Button>
              ) : null}
              {canReceiveTransfer(detail.status) ? (
                <Button type="primary" loading={completing} onClick={() => openReceive(detail)}>
                  Nhận hàng
                </Button>
              ) : null}
            </Space>
          ) : null
        }
      >
        {detail ? (
          <>
            <p>
              <strong>Kho đi:</strong> {detail.fromWarehouseName}
            </p>
            <p>
              <strong>Kho đến:</strong> {detail.toWarehouseName}
            </p>
            <p>
              <strong>Trạng thái:</strong>{' '}
              <Tag color={transferStatusColor(detail.status)}>{transferStatusLabel(detail.status)}</Tag>
              {detail.items.some(
                (line) => line.receivedQuantity != null && line.receivedQuantity < line.quantity,
              ) ? (
                <Tag color="orange" style={{ marginLeft: 6 }}>
                  Có nhận thiếu
                </Tag>
              ) : null}
            </p>
            {detail.notes ? (
              <p>
                <strong>Ghi chú:</strong> {detail.notes}
              </p>
            ) : null}
            {detail.receiveNotes ? (
              <p>
                <strong>Ghi chú nhận thiếu:</strong> {detail.receiveNotes}
              </p>
            ) : null}
            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              Chi tiết hàng
            </Typography.Text>
            {detail.items.map((line) => {
              const short =
                line.receivedQuantity != null && line.receivedQuantity < line.quantity;
              return (
                <div key={line.id} className="cart-line" style={{ marginBottom: 8 }}>
                  <Typography.Text strong>{line.productName}</Typography.Text>
                  <Typography.Text
                    type={short ? 'danger' : 'secondary'}
                    style={{ display: 'block', fontSize: 12 }}
                  >
                    {line.productCode} · Lô {line.batchNumber} · SL phiếu {line.quantity}
                    {line.receivedQuantity != null
                      ? ` · SL nhận ${line.receivedQuantity}${short ? ' (thiếu)' : ''}`
                      : ''}
                  </Typography.Text>
                </div>
              );
            })}
          </>
        ) : null}
      </Drawer>

      <Drawer
        title="Xác nhận nhận hàng"
        placement="bottom"
        height="88%"
        open={receiveOpen}
        onClose={() => setReceiveOpen(false)}
        extra={
          <Button type="primary" loading={completing} onClick={() => void handleReceiveConfirm()}>
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
              <div key={line.id} className="cart-line" style={{ marginBottom: 12 }}>
                <Typography.Text strong>{line.productName}</Typography.Text>
                <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginBottom: 6 }}>
                  Lô {line.batchNumber} · SL phiếu {line.quantity}
                </Typography.Text>
                <InputNumber
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

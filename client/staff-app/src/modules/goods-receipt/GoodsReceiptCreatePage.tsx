import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  App,
  Alert,
  AutoComplete,
  Button,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Spin,
  Typography,
} from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  completeGoodsReceipt,
  createGoodsReceipt,
  fetchPurchaseOrder,
  fetchPurchaseOrders,
  fetchSuppliers,
  fetchVatTreatments,
} from '@/shared/api/procurement.api';
import type { PurchaseOrderListItem } from '@/shared/api/procurement.types';
import { fetchWarehouses, lookupPosProduct, searchPosProducts } from '@/shared/api/sales.api';
import type { Warehouse } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { defaultVatTreatmentId } from '@/modules/procurement/default-vat';
import { formatMoney } from '@/shared/utils/money';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

type GrnLineForm = {
  purchaseOrderItemId?: string;
  productId?: string;
  productUnitId?: string;
  productCode?: string;
  productName?: string;
  unitName?: string;
  productQuery?: string;
  batchNumber: string;
  expiryDate?: dayjs.Dayjs;
  quantity: number;
  unitCost: number;
};

type GrnFormValues = {
  warehouseId: string;
  supplierId: string;
  purchaseOrderId?: string;
  notes?: string;
  items: GrnLineForm[];
};

function defaultExpiry() {
  return dayjs().add(2, 'year');
}

function warehouseLabel(w: Warehouse) {
  return w.branchName ? `${w.warehouseName} · ${w.branchName}` : w.warehouseName;
}

function GrnProductLine({
  field,
  warehouseId,
  remove,
  canRemove,
}: {
  field: { name: number; key: number };
  warehouseId?: string;
  remove: () => void;
  canRemove: boolean;
}) {
  const { message } = App.useApp();
  const form = Form.useFormInstance<GrnFormValues>();
  const [options, setOptions] = useState<{ value: string; label: string }[]>([]);
  const [searching, setSearching] = useState(false);
  const timer = useRef<number | undefined>(undefined);

  const searchProducts = useCallback(
    (query: string) => {
      if (!warehouseId) {
        setOptions([]);
        return;
      }
      window.clearTimeout(timer.current);
      timer.current = window.setTimeout(() => {
        void (async () => {
          setSearching(true);
          try {
            const hits = await searchPosProducts(query.trim(), warehouseId);
            setOptions(
              hits.map((hit) => ({
                value: hit.lookupCode,
                label: `${hit.productCode} · ${hit.productName} · ${hit.unitName}`,
              })),
            );
          } catch {
            setOptions([]);
          } finally {
            setSearching(false);
          }
        })();
      }, 300);
    },
    [warehouseId],
  );

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const pickProduct = async (lookupCode: string) => {
    if (!warehouseId) {
      message.warning('Chọn kho nhập trước');
      return;
    }
    try {
      const hit = await lookupPosProduct(lookupCode, warehouseId);
      form.setFieldValue(['items', field.name], {
        ...form.getFieldValue(['items', field.name]),
        productId: hit.productId,
        productUnitId: hit.productUnitId,
        productCode: hit.productCode,
        productName: hit.productName,
        unitName: hit.unitName,
        productQuery: `${hit.productCode} · ${hit.productName}`,
        unitCost: form.getFieldValue(['items', field.name, 'unitCost']) || 0,
        batchNumber: form.getFieldValue(['items', field.name, 'batchNumber']) || '',
        expiryDate: form.getFieldValue(['items', field.name, 'expiryDate']) || defaultExpiry(),
        quantity: form.getFieldValue(['items', field.name, 'quantity']) || 1,
      });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tìm được sản phẩm'));
    }
  };

  const line = Form.useWatch(['items', field.name], form) as GrnLineForm | undefined;
  const lineTotal = (Number(line?.quantity) || 0) * (Number(line?.unitCost) || 0);
  const expiry = line?.expiryDate ? dayjs(line.expiryDate) : null;
  const expirySoon =
    expiry?.isValid() && expiry.startOf('day').diff(dayjs().startOf('day'), 'day') <= 90;
  const expiryPast = expiry?.isValid() && expiry.isBefore(dayjs(), 'day');

  return (
    <div className="grn-create-line">
      <div className="grn-create-line__head">
        <Typography.Text strong>Dòng {field.name + 1}</Typography.Text>
        {canRemove ? (
          <Button type="text" danger icon={<MinusCircleOutlined />} onClick={remove} aria-label="Xóa dòng" />
        ) : null}
      </div>

      <Form.Item name={[field.name, 'purchaseOrderItemId']} hidden>
        <Input />
      </Form.Item>
      <Form.Item name={[field.name, 'productId']} hidden rules={[{ required: true, message: 'Chọn SP' }]}>
        <Input />
      </Form.Item>
      <Form.Item name={[field.name, 'productUnitId']} hidden rules={[{ required: true, message: 'Chọn SP' }]}>
        <Input />
      </Form.Item>
      <Form.Item name={[field.name, 'productCode']} hidden>
        <Input />
      </Form.Item>
      <Form.Item name={[field.name, 'productName']} hidden>
        <Input />
      </Form.Item>
      <Form.Item name={[field.name, 'unitName']} hidden>
        <Input />
      </Form.Item>

      <Form.Item
        name={[field.name, 'productQuery']}
        label="Sản phẩm"
        rules={[{ required: true, message: 'Chọn sản phẩm' }]}
        style={{ marginBottom: 8 }}
      >
        <AutoComplete
          options={options}
          disabled={!warehouseId}
          onSearch={(text) => {
            form.setFieldValue(['items', field.name, 'productQuery'], text);
            if (line?.productId) {
              form.setFieldValue(['items', field.name, 'productId'], undefined);
              form.setFieldValue(['items', field.name, 'productUnitId'], undefined);
            }
            searchProducts(text);
          }}
          onSelect={(value) => void pickProduct(String(value))}
          placeholder={warehouseId ? 'Mã / tên / barcode' : 'Chọn kho trước'}
          notFoundContent={searching ? <Spin size="small" /> : 'Không có SP'}
        />
      </Form.Item>
      {line?.productName ? (
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: -4, marginBottom: 8 }}>
          {line.productCode} · {line.productName} · {line.unitName}
        </Typography.Text>
      ) : null}

      <Form.Item
        name={[field.name, 'batchNumber']}
        label="Số lô"
        rules={[{ required: true, message: 'Nhập số lô' }]}
        style={{ marginBottom: 8 }}
      >
        <Input size="large" placeholder="VD: LOT2026A" />
      </Form.Item>

      <Form.Item
        name={[field.name, 'expiryDate']}
        label="Hạn dùng"
        rules={[{ required: true, message: 'Chọn HSD' }]}
        style={{ marginBottom: 8 }}
        extra={
          expiryPast
            ? 'HSD đã quá hạn — kiểm tra lại trước khi nhập'
            : expirySoon
              ? 'HSD còn ≤ 90 ngày'
              : undefined
        }
        validateStatus={expiryPast ? 'error' : expirySoon ? 'warning' : undefined}
      >
        <DatePicker size="large" format="DD/MM/YYYY" style={{ width: '100%' }} inputReadOnly />
      </Form.Item>

      <div className="grn-create-line__qty-row">
        <Form.Item
          name={[field.name, 'quantity']}
          label="Số lượng"
          rules={[{ required: true, message: 'Nhập SL' }]}
          style={{ flex: 1, marginBottom: 0 }}
        >
          <InputNumber size="large" min={0.01} step={1} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          name={[field.name, 'unitCost']}
          label="Giá nhập"
          rules={[{ required: true, message: 'Nhập giá' }]}
          style={{ flex: 1, marginBottom: 0 }}
        >
          <InputNumber size="large" min={0} step={1000} style={{ width: '100%' }} />
        </Form.Item>
      </div>
      <div className="grn-create-line__total">Thành tiền {formatMoney(lineTotal)}</div>
    </div>
  );
}

export function GoodsReceiptCreatePage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [form] = Form.useForm<GrnFormValues>();
  const posWarehouseId = usePosSession((s) => s.warehouseId);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [suppliers, setSuppliers] = useState<Awaited<ReturnType<typeof fetchSuppliers>>>([]);
  const [pendingPos, setPendingPos] = useState<PurchaseOrderListItem[]>([]);
  const [vatTreatmentId, setVatTreatmentId] = useState<string>();
  const warehouseId = Form.useWatch('warehouseId', form);
  const watchedItems = Form.useWatch('items', form) as GrnLineForm[] | undefined;

  const draftTotal = useMemo(() => {
    if (!watchedItems?.length) return 0;
    return watchedItems.reduce(
      (sum, line) => sum + (Number(line?.quantity) || 0) * (Number(line?.unitCost) || 0),
      0,
    );
  }, [watchedItems]);

  const filledLineCount = useMemo(
    () => (watchedItems ?? []).filter((line) => line?.productId).length,
    [watchedItems],
  );

  const bootstrap = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const [wh, sup, pos, vat] = await Promise.all([
        fetchWarehouses(),
        fetchSuppliers(true),
        fetchPurchaseOrders({ pendingReceiptOnly: true, page: 1, pageSize: 100 }),
        fetchVatTreatments(true),
      ]);
      const activeSuppliers = sup.filter((row) => !row.isPlaceholder);
      setWarehouses(wh);
      setSuppliers(activeSuppliers);
      setPendingPos(pos.items);
      const defaultVat = defaultVatTreatmentId(vat);
      setVatTreatmentId(defaultVat);
      const preferredWh =
        (posWarehouseId && wh.some((w) => w.id === posWarehouseId) ? posWarehouseId : undefined) ??
        wh[0]?.id;
      form.setFieldsValue({
        warehouseId: preferredWh,
        supplierId: activeSuppliers[0]?.id,
        items: [
          {
            batchNumber: '',
            expiryDate: defaultExpiry(),
            quantity: 1,
            unitCost: 0,
            productQuery: '',
          },
        ],
      });
      if (!preferredWh) {
        setLoadError('Chưa có kho để nhập hàng');
      } else if (activeSuppliers.length === 0) {
        setLoadError('Chưa có nhà cung cấp — cấu hình NCC trên máy tính trước');
      } else if (!defaultVat) {
        setLoadError('Chưa cấu hình thuế VAT nhập hàng');
      }
    } catch (error) {
      setLoadError(apiErrorMessage(error, 'Không tải được dữ liệu nhập hàng'));
    } finally {
      setLoading(false);
    }
  }, [form, posWarehouseId]);

  useEffect(() => {
    void bootstrap();
  }, [bootstrap]);

  const loadFromPo = async (poId: string) => {
    try {
      const po = await fetchPurchaseOrder(poId);
      const expiry = defaultExpiry();
      const lines = po.items
        .filter((line) => line.receivedQty < line.orderedQty)
        .map((line) => ({
          purchaseOrderItemId: line.id,
          productId: line.productId,
          productUnitId: line.productUnitId,
          productCode: line.productCode,
          productName: line.productName,
          unitName: line.unitName,
          productQuery: `${line.productCode} · ${line.productName}`,
          batchNumber: '',
          expiryDate: expiry,
          quantity: line.orderedQty - line.receivedQty,
          unitCost: line.unitPrice,
        }));
      form.setFieldsValue({
        purchaseOrderId: poId,
        supplierId: po.supplierId,
        warehouseId: po.warehouseId,
        items: lines.length > 0 ? lines : form.getFieldValue('items'),
      });
      if (lines.length === 0) {
        message.warning('PO không còn dòng chờ nhập');
      } else {
        message.success(`Đã nạp ${lines.length} dòng từ ${po.poNumber} — điền lô/HSD rồi lưu`);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được đơn mua'));
    }
  };

  const submit = async (values: GrnFormValues, completeAfterSave: boolean) => {
    if (!vatTreatmentId) {
      message.error('Chưa cấu hình thuế VAT nhập hàng');
      return;
    }
    const items = values.items.filter((line) => line.productId && line.productUnitId);
    if (items.length === 0) {
      message.error('Thêm ít nhất một dòng sản phẩm');
      return;
    }
    for (const line of items) {
      if (!line.batchNumber?.trim()) {
        message.error(`Dòng ${line.productCode ?? ''} thiếu số lô`);
        return;
      }
      if (!line.expiryDate) {
        message.error(`Dòng ${line.productCode ?? ''} thiếu hạn dùng`);
        return;
      }
      if (dayjs(line.expiryDate).isBefore(dayjs(), 'day')) {
        message.error(`Dòng ${line.productCode ?? ''} có HSD đã quá hạn`);
        return;
      }
      if (!line.quantity || line.quantity <= 0) {
        message.error(`Dòng ${line.productCode ?? ''} cần số lượng > 0`);
        return;
      }
    }

    setSaving(true);
    try {
      const created = await createGoodsReceipt({
        purchaseOrderId: values.purchaseOrderId,
        supplierId: values.supplierId,
        warehouseId: values.warehouseId,
        notes: values.notes?.trim() || undefined,
        vatTreatmentId,
        items: items.map((line) => ({
          purchaseOrderItemId: line.purchaseOrderItemId,
          productId: line.productId!,
          productUnitId: line.productUnitId!,
          batchNumber: line.batchNumber.trim(),
          expiryDate: dayjs(line.expiryDate).format('YYYY-MM-DD'),
          quantity: line.quantity,
          unitCost: line.unitCost,
        })),
      });
      if (completeAfterSave) {
        await completeGoodsReceipt(created.id);
        message.success(`Đã nhập kho ${created.grnNumber}`);
      } else {
        message.success(`Đã lưu nháp ${created.grnNumber} — hoàn tất sau để cộng tồn`);
      }
      navigate(`/goods-receipt/${created.id}`, { replace: true });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được phiếu nhập'));
    } finally {
      setSaving(false);
    }
  };

  const blocked = Boolean(loadError) && (warehouses.length === 0 || suppliers.length === 0 || !vatTreatmentId);

  if (loading) {
    return (
      <div className="staff-shell">
        <StaffPageHeader title="Nhập hàng mới" backTo="/goods-receipt" />
        <main className="staff-body">
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Nhập hàng mới"
        subtitle={
          filledLineCount > 0
            ? `${filledLineCount} SP · tạm tính ${formatMoney(draftTotal)}`
            : 'Chọn kho · NCC · điền lô/HSD'
        }
        backTo="/goods-receipt"
      />
      <main className="staff-body grn-create-body">
        {loadError ? (
          <Alert
            type={blocked ? 'error' : 'warning'}
            showIcon
            style={{ marginBottom: 12 }}
            message={blocked ? 'Chưa đủ điều kiện nhập hàng' : 'Cảnh báo'}
            description={loadError}
            action={
              <Button size="small" onClick={() => void bootstrap()}>
                Thử lại
              </Button>
            }
          />
        ) : (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Điều kiện nhập"
            description="Mỗi dòng cần sản phẩm, số lô, HSD còn hạn. Lưu nháp chưa cộng tồn; hoàn tất mới tăng kho."
          />
        )}

        <Form form={form} layout="vertical" disabled={blocked}>
          <Form.Item
            name="warehouseId"
            label="Kho nhập"
            rules={[{ required: true, message: 'Chọn kho' }]}
            extra="Ưu tiên kho đang mở ca POS"
          >
            <Select
              size="large"
              options={warehouses.map((w) => ({ value: w.id, label: warehouseLabel(w) }))}
              placeholder="Chọn kho"
            />
          </Form.Item>

          <Form.Item name="supplierId" label="Nhà cung cấp" rules={[{ required: true, message: 'Chọn NCC' }]}>
            <Select
              size="large"
              showSearch
              optionFilterProp="label"
              options={suppliers.map((s) => ({
                value: s.id,
                label: `${s.supplierCode} · ${s.supplierName}`,
              }))}
              placeholder="Chọn NCC"
            />
          </Form.Item>

          {pendingPos.length > 0 ? (
            <Form.Item
              name="purchaseOrderId"
              label="Từ đơn mua (tuỳ chọn)"
              extra={`${pendingPos.length} PO còn hàng chờ nhập — chọn để nạp sẵn dòng`}
            >
              <Select
                size="large"
                allowClear
                placeholder="Chọn PO chờ nhập"
                options={pendingPos.map((po) => ({
                  value: po.id,
                  label: `${po.poNumber} · ${po.supplierName} · ${po.itemCount} dòng`,
                }))}
                onChange={(value) => {
                  if (value) void loadFromPo(String(value));
                }}
              />
            </Form.Item>
          ) : (
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 12 }}>
              Không có PO chờ nhập — nhập trực tiếp theo NCC.
            </Typography.Text>
          )}

          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} placeholder="VD: giao hàng buổi sáng, thiếu 1 thùng…" />
          </Form.Item>

          <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
            Dòng hàng
          </Typography.Text>

          <Form.List name="items">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <GrnProductLine
                    key={field.key}
                    field={field}
                    warehouseId={warehouseId}
                    canRemove={fields.length > 1}
                    remove={() => remove(field.name)}
                  />
                ))}
                <Button
                  type="dashed"
                  block
                  size="large"
                  icon={<PlusOutlined />}
                  disabled={!warehouseId || blocked}
                  onClick={() =>
                    add({
                      batchNumber: '',
                      expiryDate: defaultExpiry(),
                      quantity: 1,
                      unitCost: 0,
                      productQuery: '',
                    })
                  }
                  style={{ marginBottom: 12 }}
                >
                  Thêm dòng
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </main>

      <footer className="staff-footer grn-create-footer">
        <div className="grn-create-footer__summary">
          <span>{filledLineCount} dòng đã chọn SP</span>
          <strong>{formatMoney(draftTotal)}</strong>
        </div>
        <Typography.Text type="secondary" className="grn-create-footer__hint">
          Nháp = chưa cộng tồn · Hoàn tất = cộng tồn ngay
        </Typography.Text>
        <div className="grn-create-footer__actions">
          <Button
            block
            size="large"
            loading={saving}
            disabled={blocked}
            onClick={() => {
              void form.validateFields().then((values) => void submit(values, false));
            }}
          >
            Chỉ lưu nháp
          </Button>
          <Popconfirm
            title="Lưu và hoàn tất nhập kho?"
            description={`Tồn sẽ tăng ngay · tạm tính ${formatMoney(draftTotal)}`}
            okText="Hoàn tất"
            cancelText="Đóng"
            disabled={blocked || saving}
            onConfirm={() => {
              void form.validateFields().then((values) => void submit(values, true));
            }}
          >
            <Button type="primary" block size="large" loading={saving} disabled={blocked}>
              Lưu & hoàn tất
            </Button>
          </Popconfirm>
        </div>
      </footer>
    </div>
  );
}

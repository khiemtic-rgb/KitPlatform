import { useEffect, useState } from 'react';
import { Button, Drawer, Form, Input, InputNumber, Select, Spin, Typography, message } from 'antd';
import { isAxiosError } from 'axios';
import { fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchPurchaseOrder, updatePurchaseOrder } from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { PurchaseOrderDetail } from '@/shared/api/procurement.types';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { PoUnitPriceField } from '@/modules/procurement/PoUnitPriceField';

interface PoEditLineForm {
  id?: string;
  receivedQty?: number;
  originalOrderedQty?: number;
  productId: string;
  productUnitId: string;
  orderedQty: number;
  unitPrice: number;
}

export interface PurchaseOrderEditDrawerProps {
  poId: string | null;
  open: boolean;
  onClose: () => void;
  onSaved?: (po: PurchaseOrderDetail) => void;
  /** Drawer chồng trên GRN */
  stackZIndex?: number;
}

export function PurchaseOrderEditDrawer({
  poId,
  open,
  onClose,
  onSaved,
  stackZIndex,
}: PurchaseOrderEditDrawerProps) {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [header, setHeader] = useState<PurchaseOrderDetail | null>(null);
  const supplierId = Form.useWatch('supplierId', form);

  useEffect(() => {
    if (!open || !poId) {
      setHeader(null);
      form.resetFields();
      return;
    }

    let cancelled = false;
    setLoading(true);
    void (async () => {
      try {
        const [po, catalog] = await Promise.all([
          fetchPurchaseOrder(poId),
          fetchProducts({ page: 1, pageSize: 200 }),
        ]);
        if (cancelled) return;
        setHeader(po);
        setProducts(catalog.items);
        form.setFieldsValue({
          supplierId: po.supplierId,
          warehouseId: po.warehouseId,
          expectedDate: po.expectedDate ?? undefined,
          notes: po.notes,
          items: po.items.map((line) => ({
            id: line.id,
            receivedQty: line.receivedQty,
            originalOrderedQty: line.orderedQty,
            productId: line.productId,
            productUnitId: line.productUnitId,
            orderedQty: line.orderedQty,
            unitPrice: line.unitPrice,
          })),
        });
      } catch (error) {
        if (!cancelled) {
          message.error(apiErrorMessage(error, 'Không tải được đơn đặt hàng'));
          onClose();
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [open, poId, form, onClose]);

  const handleSave = async () => {
    if (!poId) return;
    try {
      const values = await form.validateFields();
      setSaving(true);
      const updated = await updatePurchaseOrder(poId, {
        expectedDate: values.expectedDate || undefined,
        notes: values.notes,
        items: (values.items as PoEditLineForm[]).map((line) => ({
          id: line.id,
          productId: line.productId,
          productUnitId: line.productUnitId,
          orderedQty: line.orderedQty,
          unitPrice: line.unitPrice,
        })),
      });
      message.success(`Đã cập nhật ${updated.poNumber}`);
      onSaved?.(updated);
      onClose();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không cập nhật được đơn đặt hàng'));
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={header ? `Điều chỉnh ${header.poNumber}` : 'Điều chỉnh đơn đặt hàng'}
      width={960}
      open={open}
      zIndex={stackZIndex}
      onClose={onClose}
      destroyOnClose
      extra={
        <Button type="primary" onClick={() => void handleSave()} loading={saving} disabled={loading || !header}>
          Lưu thay đổi
        </Button>
      }
    >
      {loading ? (
        <Spin tip="Đang tải đơn..." />
      ) : (
        <Form form={form} layout="vertical">
          <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
            Dòng đã nhận: khóa. Dòng chưa nhận: chỉ tăng SL hoặc xóa; giá thực nhập tại phiếu nhập. Có thể thêm dòng mới.
          </Typography.Paragraph>
          <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
            <Form.Item name="supplierId" label="Nhà cung cấp" style={{ flex: 1, marginBottom: 16 }}>
              <Select disabled options={header ? [{ value: header.supplierId, label: header.supplierName }] : []} />
            </Form.Item>
            <Form.Item name="warehouseId" label="Kho nhận" style={{ flex: 1, marginBottom: 16 }}>
              <Select disabled options={header ? [{ value: header.warehouseId, label: header.warehouseName }] : []} />
            </Form.Item>
            <Form.Item name="expectedDate" label="Ngày dự kiến nhận" style={{ flex: '0 0 200px', marginBottom: 16 }}>
              <PharmaDatePicker placeholder="dd/mm/yyyy" />
            </Form.Item>
          </div>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} placeholder="VD: Điều chỉnh theo xác nhận Zalo..." />
          </Form.Item>
          <Form.List name="items">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <Form.Item key={field.key} noStyle shouldUpdate>
                    {() => {
                      const line = form.getFieldValue(['items', field.name]) as PoEditLineForm | undefined;
                      const received = line?.receivedQty ?? 0;
                      const isExistingLine = Boolean(line?.id);
                      const receivedLocked = received > 0;
                      const minOrderedQty = isExistingLine
                        ? (line?.originalOrderedQty ?? line?.orderedQty ?? 0.01)
                        : 0.01;
                      const productId = line?.productId;
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
                          <Form.Item name={[field.name, 'id']} hidden>
                            <Input />
                          </Form.Item>
                          <Form.Item name={[field.name, 'receivedQty']} hidden>
                            <InputNumber />
                          </Form.Item>
                          <Form.Item name={[field.name, 'originalOrderedQty']} hidden>
                            <InputNumber />
                          </Form.Item>
                          <Form.Item
                            {...field}
                            name={[field.name, 'productId']}
                            label={received > 0 ? `Sản phẩm (đã nhận ${received})` : 'Sản phẩm'}
                            rules={[{ required: true, message: 'Chọn SP' }]}
                            style={{ flex: '1 1 280px', marginBottom: 0, minWidth: 220 }}
                          >
                            <Select
                              disabled={isExistingLine}
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
                            <ProductUnitSelect productId={productId} width={120} disabled={isExistingLine} />
                          </Form.Item>
                          <Form.Item
                            {...field}
                            name={[field.name, 'orderedQty']}
                            label="SL đặt"
                            rules={[
                              { required: true },
                              {
                                validator: (_, value) =>
                                  value == null || value >= minOrderedQty
                                    ? Promise.resolve()
                                    : Promise.reject(new Error(`Tối thiểu ${minOrderedQty}`)),
                              },
                            ]}
                            style={{ flex: '0 0 88px', marginBottom: 0 }}
                          >
                            <InputNumber
                              disabled={receivedLocked}
                              min={minOrderedQty}
                              style={{ width: '100%' }}
                            />
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
                              disabled={isExistingLine}
                            />
                          </Form.Item>
                          <Form.Item label=" " colon={false} style={{ flex: '0 0 auto', marginBottom: 0 }}>
                            {received <= 0 ? (
                              <Button type="link" danger onClick={() => remove(field.name)}>
                                Xóa
                              </Button>
                            ) : null}
                          </Form.Item>
                        </div>
                      );
                    }}
                  </Form.Item>
                ))}
                <Button type="dashed" onClick={() => add({ orderedQty: 1, unitPrice: 0, receivedQty: 0 })} block>
                  Thêm dòng
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      )}
    </Drawer>
  );
}

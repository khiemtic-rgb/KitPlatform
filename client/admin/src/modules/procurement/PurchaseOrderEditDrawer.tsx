import { useEffect, useState } from 'react';
import { SaveOutlined } from '@ant-design/icons';
import { Button, Drawer, Form, Spin, Typography, message } from 'antd';
import { isAxiosError } from 'axios';
import { fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchPurchaseOrder, fetchVatTreatments, updatePurchaseOrder } from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { ProcurementVatTreatment, PurchaseOrderDetail } from '@/shared/api/procurement.types';
import { PurchaseOrderFormHeader } from '@/modules/procurement/PurchaseOrderFormHeader';
import { PurchaseOrderLinesEditor, type PoLineFormRow } from '@/modules/procurement/PurchaseOrderLinesEditor';

export interface PurchaseOrderEditDrawerProps {
  poId: string | null;
  open: boolean;
  onClose: () => void;
  onSaved?: (po: PurchaseOrderDetail) => void;
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
  const [vatTreatments, setVatTreatments] = useState<ProcurementVatTreatment[]>([]);
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
        const [po, catalog, vat] = await Promise.all([
          fetchPurchaseOrder(poId),
          fetchProducts({ page: 1, pageSize: 200 }),
          fetchVatTreatments(),
        ]);
        if (cancelled) return;
        setHeader(po);
        setProducts(catalog.items);
        setVatTreatments(vat);
        form.setFieldsValue({
          supplierId: po.supplierId,
          warehouseId: po.warehouseId,
          expectedDate: po.expectedDate ?? undefined,
          notes: po.notes,
          vatTreatmentId: po.vatTreatmentId,
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
        vatTreatmentId: values.vatTreatmentId,
        items: (values.items as PoLineFormRow[]).map((line) => ({
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
      width={980}
      open={open}
      zIndex={stackZIndex}
      onClose={onClose}
      destroyOnClose
      styles={{ body: { paddingTop: 12 } }}
      extra={
        <Button type="primary" icon={<SaveOutlined />} onClick={() => void handleSave()} loading={saving} disabled={loading || !header}>
          Lưu thay đổi
        </Button>
      }
    >
      {loading ? (
        <Spin tip="Đang tải đơn..." />
      ) : (
        <Form form={form} layout="vertical">
          <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: 8, fontSize: 12 }}>
            Dòng đã nhận: khóa. Dòng chưa nhận: chỉ tăng SL hoặc xóa. Giá thực nhập tại phiếu nhập.
          </Typography.Paragraph>
          <PurchaseOrderFormHeader
            form={form}
            mode="edit"
            vatTreatments={vatTreatments}
            supplierName={header?.supplierName}
            warehouseName={header?.warehouseName}
          />
          <PurchaseOrderLinesEditor
            form={form}
            supplierId={supplierId}
            products={products}
            mode="edit"
            scrollY={400}
          />
        </Form>
      )}
    </Drawer>
  );
}

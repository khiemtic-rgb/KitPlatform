import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SaveOutlined } from '@ant-design/icons';
import { Button, Drawer, Form, Spin, Typography, message } from 'antd';
import { isAxiosError } from 'axios';
import { fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchPurchaseOrder, fetchSuppliers, fetchVatTreatments, updatePurchaseOrder } from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { ProcurementVatTreatment, PurchaseOrderDetail, Supplier } from '@/shared/api/procurement.types';
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
  const { t } = useTranslation('procurement', { keyPrefix: 'purchaseOrders' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
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
        const [po, catalog, vat, supplierList] = await Promise.all([
          fetchPurchaseOrder(poId),
          fetchProducts({ page: 1, pageSize: 200 }),
          fetchVatTreatments(),
          fetchSuppliers(true),
        ]);
        if (cancelled) return;
        setHeader(po);
        setProducts(catalog.items);
        setVatTreatments(vat);
        setSuppliers(supplierList);
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
          message.error(apiErrorMessage(error, t('messages.loadFailed')));
          onClose();
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [open, poId, form, onClose, t]);

  const handleSave = async () => {
    if (!poId) return;
    try {
      const values = await form.validateFields();
      setSaving(true);
      const updated = await updatePurchaseOrder(poId, {
        supplierId: values.supplierId,
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
      message.success(t('messages.updated', { poNumber: updated.poNumber }));
      onSaved?.(updated);
      onClose();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.updateFailed')));
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={header ? t('editDrawerWithNumber', { poNumber: header.poNumber }) : t('editDrawer')}
      width={980}
      open={open}
      zIndex={stackZIndex}
      onClose={onClose}
      destroyOnClose
      styles={{ body: { paddingTop: 12 } }}
      extra={
        <Button type="primary" icon={<SaveOutlined />} onClick={() => void handleSave()} loading={saving} disabled={loading || !header}>
          {t('saveChanges')}
        </Button>
      }
    >
      {loading ? (
        <Spin tip={tShared('messages.loadingOrder')} />
      ) : (
        <Form form={form} layout="vertical">
          <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: 8, fontSize: 12 }}>
            {tShared('messages.receivedQtyLockedHint')}
          </Typography.Paragraph>
          <PurchaseOrderFormHeader
            form={form}
            mode="edit"
            vatTreatments={vatTreatments}
            suppliers={suppliers}
            supplierName={header?.supplierName}
            warehouseName={header?.warehouseName}
            allowSupplierEdit={header?.status === 1}
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

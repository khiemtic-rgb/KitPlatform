import { useTranslation } from 'react-i18next';
import { EditOutlined } from '@ant-design/icons';
import { Button, Form, Input, Select } from 'antd';
import type { Warehouse } from '@/shared/api/inventory.types';
import type { PurchaseOrderDetail, PurchaseOrderListItem, Supplier } from '@/shared/api/procurement.types';
import { canEditPurchaseOrder } from '@/shared/api/procurement.types';
import { realSuppliers } from '@/modules/procurement/grn-pricing';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';

export interface GoodsReceiptFormHeaderProps {
  suppliers: Supplier[];
  warehouses: Warehouse[];
  approvedPos: PurchaseOrderListItem[];
  purchaseOrderId?: string;
  linkedPo: PurchaseOrderDetail | null;
  poLoading: boolean;
  onEditPo: () => void;
}

export function GoodsReceiptFormHeader({
  suppliers,
  warehouses,
  approvedPos,
  purchaseOrderId,
  linkedPo,
  poLoading,
  onEditPo,
}: GoodsReceiptFormHeaderProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'goodsReceipts' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const poHint =
    purchaseOrderId && linkedPo && !poLoading
      ? t('linkPoHint', { poNumber: linkedPo.poNumber })
      : undefined;

  return (
    <div style={{ flexShrink: 0, marginBottom: 6 }}>
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: '1fr auto',
          gap: 8,
          alignItems: 'end',
        }}
      >
        <Form.Item
          name="purchaseOrderId"
          label={t('linkPo')}
          style={{ marginBottom: 0 }}
          tooltip={t('linkPoTooltip')}
          extra={poHint ? <span style={{ fontSize: 11 }}>{poHint}</span> : undefined}
        >
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('linkPoPlaceholder')}
            options={approvedPos.map((p) => ({
              value: p.id,
              label: `${p.poNumber} — ${p.supplierName} (${p.itemCount} SP)`,
            }))}
          />
        </Form.Item>
        <Button
          style={{ marginBottom: 0 }}
          icon={<EditOutlined />}
          disabled={!purchaseOrderId || !linkedPo || !canEditPurchaseOrder(linkedPo.status)}
          onClick={onEditPo}
        >
          {t('adjustPo')}
        </Button>
      </div>
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(120px, 1.1fr) minmax(100px, 0.85fr) 118px minmax(120px, 1fr)',
          gap: 8,
          marginTop: 6,
          alignItems: 'start',
        }}
      >
        <Form.Item
          name="supplierId"
          label={tShared('columns.supplierShort')}
          rules={[{ required: true, message: tVal('selectSupplier') }]}
          style={{ marginBottom: 0 }}
        >
          <Select
            showSearch
            optionFilterProp="label"
            options={realSuppliers(suppliers).map((s) => ({
              value: s.id,
              label: `${s.supplierCode} — ${s.supplierName}`,
            }))}
          />
        </Form.Item>
        <Form.Item
          name="warehouseId"
          label={tShared('columns.receiveWarehouse')}
          rules={[{ required: true, message: tVal('selectWarehouse') }]}
          style={{ marginBottom: 0 }}
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
          label={tShared('columns.receiptDate')}
          rules={[{ required: true, message: tVal('selectDate') }]}
          style={{ marginBottom: 0 }}
        >
          <PharmaDatePicker placeholder={tShared('datePlaceholder')} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          name="supplierInvoiceNumber"
          label={t('supplierInvoiceNumber')}
          tooltip={t('supplierInvoiceNumberHint')}
          style={{ marginBottom: 0 }}
        >
          <Input maxLength={20} placeholder={t('supplierInvoicePlaceholder')} allowClear />
        </Form.Item>
        <Form.Item name="notes" label={tShared('columns.notes')} style={{ marginBottom: 0 }}>
          <Input placeholder={t('notesOptional')} allowClear />
        </Form.Item>
      </div>
    </div>
  );
}

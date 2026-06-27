import { EditOutlined } from '@ant-design/icons';
import { Button, Form, Input, Select } from 'antd';
import type { Warehouse } from '@/shared/api/inventory.types';
import type { PurchaseOrderDetail, PurchaseOrderListItem, Supplier } from '@/shared/api/procurement.types';
import { canEditPurchaseOrder } from '@/shared/api/procurement.types';
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
  const poHint =
    purchaseOrderId && linkedPo && !poLoading
      ? `Còn phải nhận ${linkedPo.poNumber} — lô/HSD/SL; xóa dòng nếu NCC không giao.`
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
          label="Liên kết PO"
          style={{ marginBottom: 0 }}
          tooltip="Chọn đơn đã duyệt, còn hàng chưa nhận"
          extra={poHint ? <span style={{ fontSize: 11 }}>{poHint}</span> : undefined}
        >
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="PO — NCC (số SP)"
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
          Điều chỉnh PO
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
          label="NCC"
          rules={[{ required: true, message: 'Chọn NCC' }]}
          style={{ marginBottom: 0 }}
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
          rules={[{ required: true, message: 'Chọn kho' }]}
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
          label="Ngày nhập"
          rules={[{ required: true, message: 'Chọn ngày' }]}
          style={{ marginBottom: 0 }}
        >
          <PharmaDatePicker placeholder="dd/mm/yyyy" style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="notes" label="Ghi chú" style={{ marginBottom: 0 }}>
          <Input placeholder="Tùy chọn" allowClear />
        </Form.Item>
      </div>
    </div>
  );
}

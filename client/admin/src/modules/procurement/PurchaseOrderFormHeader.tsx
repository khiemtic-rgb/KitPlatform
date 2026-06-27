import { Form, Input, Select } from 'antd';
import type { FormInstance } from 'antd';
import type { Warehouse } from '@/shared/api/inventory.types';
import type { Supplier, ProcurementVatTreatment } from '@/shared/api/procurement.types';
import { formatVatTreatmentOptionLabel } from '@/modules/procurement/po-vat';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';

export interface PurchaseOrderFormHeaderProps {
  form: FormInstance;
  mode: 'create' | 'edit';
  vatTreatments: ProcurementVatTreatment[];
  suppliers?: Supplier[];
  warehouses?: Warehouse[];
  supplierName?: string;
  warehouseName?: string;
}

export function PurchaseOrderFormHeader({
  form,
  mode,
  vatTreatments,
  suppliers = [],
  warehouses = [],
  supplierName,
  warehouseName,
}: PurchaseOrderFormHeaderProps) {
  const isCreate = mode === 'create';

  return (
    <div style={{ marginBottom: 12 }}>
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: isCreate
            ? 'minmax(200px, 1.6fr) minmax(120px, 0.9fr) 130px 170px'
            : 'minmax(160px, 1fr) minmax(120px, 1fr) 130px 170px',
          gap: 10,
          alignItems: 'start',
        }}
      >
        <Form.Item
          name="supplierId"
          label="Nhà cung cấp"
          rules={isCreate ? [{ required: true, message: 'Chọn NCC' }] : undefined}
          style={{ marginBottom: 0 }}
        >
          {isCreate ? (
            <Select
              showSearch
              optionFilterProp="label"
              options={suppliers.map((s) => ({
                value: s.id,
                label: `${s.supplierCode} — ${s.supplierName}`,
              }))}
            />
          ) : (
            <Select disabled options={[{ value: form.getFieldValue('supplierId'), label: supplierName }]} />
          )}
        </Form.Item>
        <Form.Item
          name="warehouseId"
          label="Kho nhận"
          rules={isCreate ? [{ required: true, message: 'Chọn kho' }] : undefined}
          style={{ marginBottom: 0 }}
        >
          {isCreate ? (
            <Select options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))} />
          ) : (
            <Select disabled options={[{ value: form.getFieldValue('warehouseId'), label: warehouseName }]} />
          )}
        </Form.Item>
        <Form.Item name="expectedDate" label="Ngày nhận DK" style={{ marginBottom: 0 }}>
          <PharmaDatePicker placeholder="dd/mm/yyyy" style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          name="vatTreatmentId"
          label="Thuế GTGT"
          rules={[{ required: true, message: 'Chọn thuế' }]}
          style={{ marginBottom: 0 }}
        >
          <Select
            options={vatTreatments.map((t) => ({
              value: t.id,
              label: formatVatTreatmentOptionLabel(t),
            }))}
          />
        </Form.Item>
      </div>
      <Form.Item name="notes" label="Ghi chú" style={{ marginBottom: 0, marginTop: 10 }}>
        <Input placeholder="Ghi chú đơn (tùy chọn)" allowClear />
      </Form.Item>
    </div>
  );
}

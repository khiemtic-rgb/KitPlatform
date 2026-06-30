import { useTranslation } from 'react-i18next';
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
  allowSupplierEdit?: boolean;
}

export function PurchaseOrderFormHeader({
  form,
  mode,
  vatTreatments,
  suppliers = [],
  warehouses = [],
  supplierName,
  warehouseName,
  allowSupplierEdit = false,
}: PurchaseOrderFormHeaderProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'purchaseOrders' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const isCreate = mode === 'create';
  const supplierOptions = (suppliers ?? []).map((s) => ({
    value: s.id,
    label: `${s.supplierCode} — ${s.supplierName}`,
  }));

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
          label={tShared('filters.supplier')}
          rules={isCreate ? [{ required: true, message: tVal('selectSupplier') }] : undefined}
          style={{ marginBottom: 0 }}
        >
          {isCreate || allowSupplierEdit ? (
            <Select showSearch optionFilterProp="label" options={supplierOptions} />
          ) : (
            <Select disabled options={[{ value: form.getFieldValue('supplierId'), label: supplierName }]} />
          )}
        </Form.Item>
        <Form.Item
          name="warehouseId"
          label={tShared('columns.receiveWarehouse')}
          rules={isCreate ? [{ required: true, message: tVal('selectWarehouse') }] : undefined}
          style={{ marginBottom: 0 }}
        >
          {isCreate ? (
            <Select options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))} />
          ) : (
            <Select disabled options={[{ value: form.getFieldValue('warehouseId'), label: warehouseName }]} />
          )}
        </Form.Item>
        <Form.Item name="expectedDate" label={t('expectedDate')} style={{ marginBottom: 0 }}>
          <PharmaDatePicker placeholder={tShared('datePlaceholder')} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          name="vatTreatmentId"
          label={tShared('tax.vatReference')}
          tooltip={tShared('tax.vatReferenceTooltip')}
          rules={[{ required: true, message: tVal('selectTax') }]}
          style={{ marginBottom: 0 }}
        >
          <Select
            options={vatTreatments.map((item) => ({
              value: item.id,
              label: formatVatTreatmentOptionLabel(item),
            }))}
          />
        </Form.Item>
      </div>
      <Form.Item name="notes" label={tShared('columns.notes')} style={{ marginBottom: 0, marginTop: 10 }}>
        <Input placeholder={t('notesPlaceholder')} allowClear />
      </Form.Item>
    </div>
  );
}

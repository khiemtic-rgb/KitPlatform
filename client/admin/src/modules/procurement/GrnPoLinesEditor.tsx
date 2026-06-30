import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Form, Input, InputNumber, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { FormListFieldData } from 'antd/es/form/FormList';
import type { FormInstance } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import { grnLineNetTotal } from '@/modules/procurement/grn-pricing';
import { GrnLineDiscountFields } from '@/modules/procurement/GrnPricingPanel';
import { PROCUREMENT_MONEY_COL_WIDTH } from '@/modules/procurement/GrnPoTaxSummary';
import { ProcurementQuantityCell } from '@/modules/procurement/procurement-quantity-cell';
import { PoUnitPriceField } from '@/modules/procurement/PoUnitPriceField';
import { PharmaExpiryPicker } from '@/shared/ui/PharmaDatePicker';
import type { PurchaseOrderDetail } from '@/shared/api/procurement.types';
import { formatDisplayMoney, formatDisplayQuantity, quantityInputNumberProps } from '@/shared/utils/money';

export interface GrnLineFormRow {
  purchaseOrderItemId?: string;
  productId: string;
  productUnitId: string;
  productCode?: string;
  productName?: string;
  unitName?: string;
  orderedQty?: number;
  receivedQty?: number;
  batchNumber: string;
  expiryDate: string;
  quantity: number;
  unitCost: number;
}

const ROW_HEIGHT = 52;
const TABLE_HEADER_HEIGHT = 39;

interface GrnPoLinesEditorProps {
  form: FormInstance;
  supplierId?: string;
  linkedPo?: PurchaseOrderDetail | null;
  fields: FormListFieldData[];
  remove: (index: number) => void;
  maxScrollY?: number;
}

export function GrnPoLinesEditor({
  form,
  supplierId,
  fields,
  remove,
  maxScrollY = 520,
}: GrnPoLinesEditorProps) {
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const { t: tLines } = useTranslation('procurement', { keyPrefix: 'shared.lines' });
  const watchedItems = Form.useWatch('items', form) as GrnLineFormRow[] | undefined;

  const tableScrollY = useMemo(() => {
    const naturalHeight = fields.length * ROW_HEIGHT + TABLE_HEADER_HEIGHT;
    return naturalHeight > maxScrollY ? maxScrollY : undefined;
  }, [fields.length, maxScrollY]);

  const columns: ColumnsType<FormListFieldData> = [
    {
      title: tShared('columns.product'),
      ellipsis: true,
      render: (_, field) => {
        const line = watchedItems?.[field.name];
        return (
          <>
            <div
              style={{ lineHeight: 1.3, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
              title={line ? `${line.productCode} — ${line.productName}` : undefined}
            >
              <strong>{line?.productCode}</strong>
              <span style={{ color: '#888' }}> — {line?.productName}</span>
            </div>
            <Form.Item name={[field.name, 'purchaseOrderItemId']} hidden>
              <Input />
            </Form.Item>
            <Form.Item name={[field.name, 'productId']} hidden>
              <Input />
            </Form.Item>
            <Form.Item name={[field.name, 'productUnitId']} hidden>
              <Input />
            </Form.Item>
          </>
        );
      },
    },
    {
      title: tShared('columns.unit'),
      width: 48,
      className: 'grn-col-nowrap',
      render: (_, field) => watchedItems?.[field.name]?.unitName ?? tShared('emDash'),
    },
    {
      title: tShared('columns.remaining'),
      width: 78,
      align: 'right',
      render: (_, field) => {
        const line = watchedItems?.[field.name];
        const remain = (line?.orderedQty ?? 0) - (line?.receivedQty ?? 0);
        return <ProcurementQuantityCell value={remain} />;
      },
    },
    {
      title: tShared('columns.receiveQty'),
      width: 92,
      align: 'right',
      render: (_, field) => {
        const line = watchedItems?.[field.name];
        const remain = (line?.orderedQty ?? 0) - (line?.receivedQty ?? 0);
        return (
          <Form.Item
            name={[field.name, 'quantity']}
            rules={[
              { required: true, message: tVal('enterQty') },
              {
                validator: (_: unknown, value: number | null) =>
                  value == null || value <= remain
                    ? Promise.resolve()
                    : Promise.reject(new Error(tVal('maxQty', { qty: formatDisplayQuantity(remain) }))),
              },
            ]}
            style={{ marginBottom: 0 }}
          >
            <InputNumber
              {...quantityInputNumberProps}
              min={0.001}
              max={remain > 0 ? remain : undefined}
              style={{ width: '100%' }}
            />
          </Form.Item>
        );
      },
    },
    {
      title: tShared('columns.batchNumber'),
      width: 88,
      render: (_, field) => (
        <Form.Item
          name={[field.name, 'batchNumber']}
          rules={[{ required: true, message: tVal('enterBatch') }]}
          style={{ marginBottom: 0 }}
        >
          <Input placeholder={tShared('columns.batchShort')} />
        </Form.Item>
      ),
    },
    {
      title: tShared('columns.expiry'),
      width: 100,
      className: 'grn-col-nowrap',
      render: (_, field) => (
        <Form.Item
          name={[field.name, 'expiryDate']}
          rules={[{ required: true, message: tVal('selectExpiry') }]}
          style={{ marginBottom: 0 }}
        >
          <PharmaExpiryPicker style={{ width: '100%' }} inTable />
        </Form.Item>
      ),
    },
    {
      title: tShared('columns.lineDiscount'),
      width: 156,
      render: (_, field) => <GrnLineDiscountFields fieldName={field.name} />,
    },
    {
      title: (
        <div style={{ lineHeight: 1.25, textAlign: 'right' }}>
          <div>{tShared('columns.unitCost')}</div>
          <div style={{ fontSize: 11, fontWeight: 400, color: '#888' }}>{tShared('columns.lastPurchasePriceHint')}</div>
        </div>
      ),
      width: PROCUREMENT_MONEY_COL_WIDTH,
      align: 'right',
      render: (_, field) => {
        const productId = watchedItems?.[field.name]?.productId;
        return (
          <Form.Item
            name={[field.name, 'unitCost']}
            rules={[{ required: true, message: tVal('enterPrice') }]}
            style={{ marginBottom: 0 }}
          >
            <PoUnitPriceField
              supplierId={supplierId}
              productId={productId}
              form={form}
              fieldName={field.name}
              valueFieldName="unitCost"
            />
          </Form.Item>
        );
      },
    },
    {
      title: tShared('columns.lineTotal'),
      width: PROCUREMENT_MONEY_COL_WIDTH,
      align: 'right',
      render: (_, field) => {
        const line = watchedItems?.[field.name];
        return (
          <span
            style={{
              fontVariantNumeric: 'tabular-nums',
              whiteSpace: 'nowrap',
              display: 'block',
              textAlign: 'right',
            }}
          >
            {formatDisplayMoney(grnLineNetTotal(line))}
          </span>
        );
      },
    },
    {
      title: '',
      width: 40,
      render: (_, field) => (
        <Button
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined />}
          aria-label={tLines('removeLineAria')}
          onClick={() => remove(field.name)}
        />
      ),
    },
  ];

  return (
    <Table
      className="grn-lines-table grn-lines-table--detail"
      rowKey="key"
      size="small"
      pagination={false}
      tableLayout="fixed"
      scroll={tableScrollY ? { y: tableScrollY } : undefined}
      dataSource={fields}
      columns={columns}
    />
  );
}

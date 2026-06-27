import { useMemo } from 'react';
import { Button, Form, Input, InputNumber, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { FormListFieldData } from 'antd/es/form/FormList';
import type { FormInstance } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import { GrnPoTaxSummaryTableFooter } from '@/modules/procurement/GrnPoTaxSummary';
import { ProcurementQuantityCell } from '@/modules/procurement/procurement-quantity-cell';
import { grnLineTotal } from '@/modules/procurement/grn-po-tax';
import { PROCUREMENT_MONEY_COL_WIDTH } from '@/modules/procurement/GrnPoTaxSummary';
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
  linkedPo,
  fields,
  remove,
  maxScrollY = 520,
}: GrnPoLinesEditorProps) {
  const watchedItems = Form.useWatch('items', form) as GrnLineFormRow[] | undefined;

  const tableScrollY = useMemo(() => {
    const naturalHeight = fields.length * ROW_HEIGHT + TABLE_HEADER_HEIGHT;
    return naturalHeight > maxScrollY ? maxScrollY : undefined;
  }, [fields.length, maxScrollY]);

  const columns: ColumnsType<FormListFieldData> = [
    {
      title: 'Sản phẩm',
      width: 240,
      render: (_, field) => {
        const line = watchedItems?.[field.name];
        return (
          <>
            <div style={{ lineHeight: 1.3 }}>
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
      title: 'ĐVT',
      width: 56,
      render: (_, field) => watchedItems?.[field.name]?.unitName ?? '—',
    },
    {
      title: 'Chưa nhận',
      width: 78,
      align: 'right',
      render: (_, field) => {
        const line = watchedItems?.[field.name];
        const remain = (line?.orderedQty ?? 0) - (line?.receivedQty ?? 0);
        return <ProcurementQuantityCell value={remain} />;
      },
    },
    {
      title: 'SL nhập',
      width: 92,
      align: 'right',
      render: (_, field) => {
        const line = watchedItems?.[field.name];
        const remain = (line?.orderedQty ?? 0) - (line?.receivedQty ?? 0);
        return (
          <Form.Item
            name={[field.name, 'quantity']}
            rules={[
              { required: true, message: 'Nhập SL' },
              {
                validator: (_: unknown, value: number | null) =>
                  value == null || value <= remain
                    ? Promise.resolve()
                    : Promise.reject(new Error(`Tối đa ${formatDisplayQuantity(remain)}`)),
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
      title: 'Số lô',
      width: 88,
      render: (_, field) => (
        <Form.Item
          name={[field.name, 'batchNumber']}
          rules={[{ required: true, message: 'Lô' }]}
          style={{ marginBottom: 0 }}
        >
          <Input placeholder="Lô" />
        </Form.Item>
      ),
    },
    {
      title: 'HSD (T/N)',
      width: 110,
      render: (_, field) => (
        <Form.Item
          name={[field.name, 'expiryDate']}
          rules={[{ required: true, message: 'HSD' }]}
          style={{ marginBottom: 0 }}
        >
          <PharmaExpiryPicker style={{ width: 110 }} inTable />
        </Form.Item>
      ),
    },
    {
      title: (
        <div style={{ lineHeight: 1.25, textAlign: 'right' }}>
          <div>Giá vốn</div>
          <div style={{ fontSize: 11, fontWeight: 400, color: '#888' }}>Giá nhập gần nhất</div>
        </div>
      ),
      width: PROCUREMENT_MONEY_COL_WIDTH,
      align: 'right',
      render: (_, field) => {
        const productId = watchedItems?.[field.name]?.productId;
        return (
          <Form.Item
            name={[field.name, 'unitCost']}
            rules={[{ required: true, message: 'Giá' }]}
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
      title: 'Thành tiền',
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
            {formatDisplayMoney(grnLineTotal(line))}
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
          aria-label="Xóa dòng"
          onClick={() => remove(field.name)}
        />
      ),
    },
  ];

  return (
    <Table
      className="grn-lines-table"
      rowKey="key"
      size="small"
      pagination={false}
      scroll={{ x: 960, ...(tableScrollY ? { y: tableScrollY } : {}) }}
      dataSource={fields}
      columns={columns}
      summary={() =>
        linkedPo ? (
          <Table.Summary>
            <GrnPoTaxSummaryTableFooter form={form} linkedPo={linkedPo} />
          </Table.Summary>
        ) : null
      }
    />
  );
}

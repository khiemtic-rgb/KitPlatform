import { useTranslation } from 'react-i18next';
import { Button, Form, Input, InputNumber, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { FormListFieldData } from 'antd/es/form/FormList';
import type { FormInstance } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { PoUnitPriceField } from '@/modules/procurement/PoUnitPriceField';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { ProductSearchSelect } from '@/modules/procurement/ProductSearchSelect';
import { quantityInputNumberProps, formatDisplayQuantity } from '@/shared/utils/money';

export interface PoLineFormRow {
  id?: string;
  receivedQty?: number;
  originalOrderedQty?: number;
  productId: string;
  productUnitId: string;
  orderedQty: number;
  unitPrice: number;
}

interface PurchaseOrderLinesEditorProps {
  form: FormInstance;
  supplierId?: string;
  products: ProductListItem[];
  mode: 'create' | 'edit';
  scrollY?: number;
}

export function PurchaseOrderLinesEditor({
  form,
  supplierId,
  products,
  mode,
  scrollY = 380,
}: PurchaseOrderLinesEditorProps) {
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const { t: tLines } = useTranslation('procurement', { keyPrefix: 'shared.lines' });
  const isCreate = mode === 'create';
  const watchedItems = Form.useWatch('items', form) as PoLineFormRow[] | undefined;

  const renderTable = (
    fields: FormListFieldData[],
    add: (defaultValue?: Partial<PoLineFormRow>) => void,
    remove: (index: number) => void,
  ) => {
    const columns: ColumnsType<FormListFieldData> = [
      {
        title: tShared('columns.product'),
        width: isCreate ? 300 : 260,
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const received = line?.receivedQty ?? 0;
          const isExistingLine = Boolean(line?.id);
          return (
            <>
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
                name={[field.name, 'productId']}
                rules={[{ required: true, message: tVal('selectProduct') }]}
                style={{ marginBottom: 0 }}
              >
                <ProductSearchSelect
                  disabled={!isCreate && isExistingLine}
                  seedProducts={products}
                  placeholder={tShared('columns.product')}
                  afterChange={() => {
                    form.setFieldValue(['items', field.name, 'productUnitId'], undefined);
                  }}
                />
              </Form.Item>
              {!isCreate && received > 0 && (
                <span style={{ fontSize: 11, color: '#888' }}>
                  {tLines('receivedHint', { qty: formatDisplayQuantity(received) })}
                </span>
              )}
            </>
          );
        },
      },
      {
        title: tShared('columns.unit'),
        width: 88,
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const isExistingLine = Boolean(line?.id);
          const productId = line?.productId;
          return (
            <Form.Item
              name={[field.name, 'productUnitId']}
              rules={[{ required: true, message: tVal('selectUnit') }]}
              style={{ marginBottom: 0 }}
            >
              <ProductUnitSelect productId={productId} width={84} disabled={!isCreate && isExistingLine} />
            </Form.Item>
          );
        },
      },
      {
        title: tShared('columns.orderedQty'),
        width: 82,
        align: 'right',
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const received = line?.receivedQty ?? 0;
          const isExistingLine = Boolean(line?.id);
          const minOrderedQty = isExistingLine
            ? (line?.originalOrderedQty ?? line?.orderedQty ?? 0.01)
            : 0.01;
          return (
            <Form.Item
              name={[field.name, 'orderedQty']}
              rules={[
                { required: true, message: tVal('enterQty') },
                ...(isCreate
                  ? [{ type: 'number' as const, min: 0.01, message: tVal('qtyPositive') }]
                  : [
                      {
                        validator: (_: unknown, value: number | null) =>
                          value == null || value >= minOrderedQty
                            ? Promise.resolve()
                            : Promise.reject(new Error(`≥ ${minOrderedQty}`)),
                      },
                    ]),
              ]}
              style={{ marginBottom: 0 }}
            >
              <InputNumber
                {...quantityInputNumberProps}
                disabled={!isCreate && received > 0}
                min={minOrderedQty}
                style={{ width: '100%' }}
              />
            </Form.Item>
          );
        },
      },
      {
        title: (
          <div style={{ lineHeight: 1.25 }}>
            <div>{tShared('columns.unitPrice')}</div>
            <div style={{ fontSize: 11, fontWeight: 400, color: '#888' }}>{tShared('columns.lastPurchasePriceHint')}</div>
          </div>
        ),
        width: 130,
        align: 'right',
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const productId = line?.productId;
          const isExistingLine = Boolean(line?.id);
          return (
            <Form.Item
              name={[field.name, 'unitPrice']}
              rules={[{ required: true, message: tVal('enterPrice') }]}
              style={{ marginBottom: 0 }}
            >
              <PoUnitPriceField
                supplierId={supplierId}
                productId={productId}
                form={form}
                fieldName={field.name}
                disabled={!isCreate && isExistingLine}
              />
            </Form.Item>
          );
        },
      },
      {
        title: '',
        width: 44,
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const received = line?.receivedQty ?? 0;
          if (!isCreate && received > 0) return null;
          return (
            <Button
              type="text"
              danger
              size="small"
              icon={<DeleteOutlined />}
              aria-label={tLines('removeLineAria')}
              onClick={() => remove(field.name)}
            />
          );
        },
      },
    ];

    return (
      <>
        <Table
          className="po-lines-table"
          rowKey="key"
          size="small"
          pagination={false}
          scroll={{ x: 620, y: scrollY }}
          dataSource={fields}
          columns={columns}
        />
        <Button
          type="dashed"
          icon={<PlusOutlined />}
          onClick={() =>
            add(isCreate ? { orderedQty: 1, unitPrice: 0 } : { orderedQty: 1, unitPrice: 0, receivedQty: 0 })
          }
          block
          style={{ marginTop: 8 }}
        >
          {tLines('addLine')}
        </Button>
      </>
    );
  };

  return (
    <Form.List name="items">
      {(fields, { add, remove }) => renderTable(fields, add, remove)}
    </Form.List>
  );
}

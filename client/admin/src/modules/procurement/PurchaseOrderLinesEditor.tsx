import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Form, Input, InputNumber, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { FormListFieldData } from 'antd/es/form/FormList';
import type { FormInstance } from 'antd';
import { CheckOutlined, DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { fetchProduct } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchLastPurchasePriceHint } from '@/shared/api/procurement.api';
import { PoUnitPriceField } from '@/modules/procurement/PoUnitPriceField';
import { ProductUnitSelect } from '@/modules/procurement/ProductUnitSelect';
import { ProductSearchSelect } from '@/modules/procurement/ProductSearchSelect';
import { formatUnitLabel, pickDefaultProductUnitId } from '@/modules/procurement/product-unit.helpers';
import {
  formatDisplayMoney,
  formatDisplayQuantity,
  moneyInputNumberPropsAllowZeroSuffix,
  quantityInputNumberProps,
} from '@/shared/utils/money';

export interface PoLineFormProps {
  id?: string;
  receivedQty?: number;
  originalOrderedQty?: number;
  productId: string;
  productUnitId: string;
  orderedQty: number;
  unitPrice: number;
  /** Client-only label for compact grid. */
  productLabel?: string;
}

interface PurchaseOrderLinesEditorProps {
  form: FormInstance;
  supplierId?: string;
  products: ProductListItem[];
  mode: 'create' | 'edit';
  scrollY?: number;
}

function productOptionLabel(p: { productCode: string; productName: string }): string {
  return `${p.productCode} — ${p.productName}`;
}

function UnitNameLabel({ productId, unitId }: { productId?: string; unitId?: string }) {
  const [label, setLabel] = useState('—');

  useEffect(() => {
    if (!productId || !unitId) {
      setLabel('—');
      return;
    }
    let cancelled = false;
    void fetchProduct(productId)
      .then((product) => {
        if (cancelled) return;
        const unit = product.units.find((u) => u.id === unitId);
        setLabel(unit ? formatUnitLabel(unit) : '—');
      })
      .catch(() => {
        if (!cancelled) setLabel('—');
      });
    return () => {
      cancelled = true;
    };
  }, [productId, unitId]);

  return <span>{label}</span>;
}

export function PurchaseOrderLinesEditor({
  form,
  supplierId,
  products,
  mode,
  scrollY = 380,
}: PurchaseOrderLinesEditorProps) {
  const { message } = App.useApp();
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const { t: tLines } = useTranslation('procurement', { keyPrefix: 'shared.lines' });
  const isCreate = mode === 'create';
  const watchedItems = Form.useWatch('items', form) as PoLineFormProps[] | undefined;

  const [editingKey, setEditingKey] = useState<number | null>(null);
  const [draftProductId, setDraftProductId] = useState<string | undefined>();
  const [draftProductLabel, setDraftProductLabel] = useState<string | undefined>();
  const [draftUnitId, setDraftUnitId] = useState<string | undefined>();
  const [draftQty, setDraftQty] = useState<number>(1);
  const [draftUnitPrice, setDraftUnitPrice] = useState<number>(0);

  const resetComposer = () => {
    setDraftProductId(undefined);
    setDraftProductLabel(undefined);
    setDraftUnitId(undefined);
    setDraftQty(1);
    setDraftUnitPrice(0);
  };

  const applyProductDefaults = (productId: string) => {
    setDraftUnitId(undefined);
    setDraftUnitPrice(0);
    void fetchProduct(productId)
      .then((product) => {
        setDraftProductLabel(productOptionLabel(product));
        setDraftUnitId(pickDefaultProductUnitId(product.units));
      })
      .catch(() => undefined);
    if (supplierId) {
      void fetchLastPurchasePriceHint(supplierId, productId)
        .then((h) => {
          if (h.unitPrice != null) setDraftUnitPrice(h.unitPrice);
        })
        .catch(() => undefined);
    }
  };

  const seedById = useMemo(() => {
    const map = new Map<string, ProductListItem>();
    for (const p of products) map.set(p.id, p);
    return map;
  }, [products]);

  const resolveProductLabel = (line?: PoLineFormProps) => {
    if (!line?.productId) return tShared('emDash');
    if (line.productLabel) return line.productLabel;
    const seed = seedById.get(line.productId);
    return seed ? productOptionLabel(seed) : line.productId;
  };

  const lineAmount = (qty?: number, unitPrice?: number) =>
    Math.round(Number(qty ?? 0) * Number(unitPrice ?? 0));

  const draftLineAmount = lineAmount(draftQty, draftUnitPrice);

  const renderTable = (
    fields: FormListFieldData[],
    add: (defaultValue?: Partial<PoLineFormProps>, insertIndex?: number) => void,
    remove: (index: number) => void,
  ) => {
    const addLineFromComposer = () => {
      if (!draftProductId) {
        message.warning(tVal('selectProduct'));
        return;
      }
      if (!draftUnitId) {
        message.warning(tVal('selectUnit'));
        return;
      }
      if (draftQty == null || draftQty <= 0) {
        message.warning(tVal('qtyPositive'));
        return;
      }
      if (draftUnitPrice == null || draftUnitPrice < 0) {
        message.warning(tVal('enterPrice'));
        return;
      }
      add(
        {
          productId: draftProductId,
          productLabel: draftProductLabel,
          productUnitId: draftUnitId,
          orderedQty: draftQty,
          unitPrice: draftUnitPrice,
          ...(isCreate ? {} : { receivedQty: 0 }),
        },
        0,
      );
      resetComposer();
      setEditingKey(null);
    };

    const columns: ColumnsType<FormListFieldData> = [
      {
        title: tShared('columns.stt'),
        width: 52,
        align: 'center',
        render: (_, __, index) => index + 1,
      },
      {
        title: tShared('columns.product'),
        width: isCreate ? 300 : 260,
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const received = line?.receivedQty ?? 0;
          const isExistingLine = Boolean(line?.id);
          const editing = editingKey === field.key;
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
              <Form.Item name={[field.name, 'productLabel']} hidden>
                <Input />
              </Form.Item>
              <div style={{ display: editing ? 'block' : 'none' }}>
                <Form.Item
                  name={[field.name, 'productId']}
                  rules={[{ required: true, message: tVal('selectProduct') }]}
                  style={{ marginBottom: 0 }}
                >
                  <ProductSearchSelect
                    disabled={!isCreate && isExistingLine}
                    seedProducts={products}
                    placeholder={tShared('columns.product')}
                    afterChange={(value) => {
                      form.setFieldValue(['items', field.name, 'productUnitId'], undefined);
                      const seed = value ? seedById.get(value) : undefined;
                      form.setFieldValue(
                        ['items', field.name, 'productLabel'],
                        seed ? productOptionLabel(seed) : undefined,
                      );
                    }}
                  />
                </Form.Item>
              </div>
              {!editing && (
                <Typography.Text ellipsis style={{ maxWidth: 280 }} title={resolveProductLabel(line)}>
                  {resolveProductLabel(line)}
                </Typography.Text>
              )}
              {!isCreate && received > 0 && (
                <div style={{ fontSize: 11, color: '#888' }}>
                  {tLines('receivedHint', { qty: formatDisplayQuantity(received) })}
                </div>
              )}
            </>
          );
        },
      },
      {
        title: tShared('columns.unit'),
        width: 96,
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const isExistingLine = Boolean(line?.id);
          const productId = line?.productId;
          const editing = editingKey === field.key;
          return (
            <>
              <div style={{ display: editing ? 'block' : 'none' }}>
                <Form.Item
                  name={[field.name, 'productUnitId']}
                  rules={[{ required: true, message: tVal('selectUnit') }]}
                  style={{ marginBottom: 0 }}
                >
                  <ProductUnitSelect productId={productId} width={84} disabled={!isCreate && isExistingLine} />
                </Form.Item>
              </div>
              {!editing && <UnitNameLabel productId={productId} unitId={line?.productUnitId} />}
            </>
          );
        },
      },
      {
        title: tShared('columns.orderedQty'),
        width: 90,
        align: 'right',
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const received = line?.receivedQty ?? 0;
          const isExistingLine = Boolean(line?.id);
          const minOrderedQty = isExistingLine
            ? (line?.originalOrderedQty ?? line?.orderedQty ?? 0.01)
            : 0.01;
          const editing = editingKey === field.key;
          return (
            <>
              <div style={{ display: editing ? 'block' : 'none' }}>
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
              </div>
              {!editing && (
                <>
                  <Form.Item name={[field.name, 'orderedQty']} hidden rules={[{ required: true }]}>
                    <InputNumber />
                  </Form.Item>
                  <span>{formatDisplayQuantity(line?.orderedQty)}</span>
                </>
              )}
            </>
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
          const editing = editingKey === field.key;
          return (
            <>
              <div style={{ display: editing ? 'block' : 'none' }}>
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
              </div>
              {!editing && (
                <>
                  <Form.Item name={[field.name, 'unitPrice']} hidden rules={[{ required: true }]}>
                    <InputNumber />
                  </Form.Item>
                  <span>{formatDisplayMoney(line?.unitPrice)}</span>
                </>
              )}
            </>
          );
        },
      },
      {
        title: tShared('columns.lineTotal'),
        width: 120,
        align: 'right',
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          return <span>{formatDisplayMoney(lineAmount(line?.orderedQty, line?.unitPrice))}</span>;
        },
      },
      {
        title: '',
        width: 92,
        render: (_, field) => {
          const line = watchedItems?.[field.name];
          const received = line?.receivedQty ?? 0;
          const editing = editingKey === field.key;
          return (
            <Space size={0}>
              {editing ? (
                <Button
                  type="text"
                  size="small"
                  icon={<CheckOutlined />}
                  aria-label={tLines('doneEditAria')}
                  onClick={() => setEditingKey(null)}
                />
              ) : (
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={tLines('editLineAria')}
                  onClick={() => setEditingKey(field.key)}
                />
              )}
              {(isCreate || received <= 0) && (
                <Button
                  type="text"
                  danger
                  size="small"
                  icon={<DeleteOutlined />}
                  aria-label={tLines('removeLineAria')}
                  onClick={() => {
                    if (editingKey === field.key) setEditingKey(null);
                    remove(field.name);
                  }}
                />
              )}
            </Space>
          );
        },
      },
    ];

    const itemsTotal = (watchedItems ?? []).reduce(
      (sum, line) => sum + lineAmount(line?.orderedQty, line?.unitPrice),
      0,
    );

    return (
      <>
        <div
          style={{
            marginBottom: 10,
            padding: 12,
            border: '1px dashed #d9d9d9',
            borderRadius: 8,
            background: '#fafafa',
          }}
        >
          <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 8 }}>
            {tLines('composerHint')}
          </Typography.Text>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'minmax(200px, 1.5fr) 100px 90px 120px 120px auto',
              gap: 8,
              alignItems: 'end',
            }}
          >
            <div>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.product')}</Typography.Text>
              <ProductSearchSelect
                value={draftProductId}
                seedProducts={products}
                placeholder={tShared('columns.product')}
                style={{ width: '100%' }}
                onChange={(value) => {
                  setDraftProductId(value);
                  if (!value) {
                    resetComposer();
                    return;
                  }
                  const seed = seedById.get(value);
                  setDraftProductLabel(seed ? productOptionLabel(seed) : undefined);
                  applyProductDefaults(value);
                }}
              />
            </div>
            <div>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.unit')}</Typography.Text>
              <ProductUnitSelect
                productId={draftProductId}
                value={draftUnitId}
                onChange={setDraftUnitId}
                width={100}
              />
            </div>
            <div>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.orderedQty')}</Typography.Text>
              <InputNumber
                {...quantityInputNumberProps}
                min={0.01}
                value={draftQty}
                onChange={(v) => setDraftQty(Number(v ?? 0))}
                style={{ width: '100%' }}
              />
            </div>
            <div>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.unitPrice')}</Typography.Text>
              <InputNumber
                {...moneyInputNumberPropsAllowZeroSuffix}
                value={draftUnitPrice}
                onChange={(v) => setDraftUnitPrice(Number(v ?? 0))}
                style={{ width: '100%' }}
                placeholder={tShared('moneyPlaceholder')}
              />
            </div>
            <div>
              <Typography.Text style={{ fontSize: 12 }}>{tShared('columns.lineTotal')}</Typography.Text>
              <div
                style={{
                  height: 32,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'flex-end',
                  padding: '0 8px',
                  background: '#fff',
                  border: '1px solid #d9d9d9',
                  borderRadius: 6,
                  fontWeight: 600,
                }}
              >
                {formatDisplayMoney(draftLineAmount)}
              </div>
            </div>
            <Button type="primary" icon={<PlusOutlined />} onClick={addLineFromComposer}>
              {tLines('addLine')}
            </Button>
          </div>
        </div>

        <Table
          className="po-lines-table"
          rowKey="key"
          size="small"
          pagination={false}
          scroll={{ x: 900, y: scrollY }}
          dataSource={fields}
          columns={columns}
          locale={{ emptyText: tLines('emptyGrid') }}
          summary={() =>
            fields.length === 0 ? null : (
              <Table.Summary fixed>
                <Table.Summary.Row>
                  <Table.Summary.Cell index={0} colSpan={5} align="right">
                    <Typography.Text strong>{tShared('columns.totalAmount')}</Typography.Text>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={5} align="right">
                    <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                      {formatDisplayMoney(itemsTotal)}
                    </Typography.Text>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={6} />
                </Table.Summary.Row>
              </Table.Summary>
            )
          }
        />
      </>
    );
  };

  return (
    <Form.List name="items">
      {(fields, { add, remove }) => renderTable(fields, add, remove)}
    </Form.List>
  );
}

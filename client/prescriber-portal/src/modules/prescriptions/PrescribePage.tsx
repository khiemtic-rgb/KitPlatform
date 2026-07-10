import { useMemo, useState } from 'react';
import { Alert, Button, Card, Form, Input, InputNumber, Select, Space, Tag, Typography, message } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  createPortalPrescription,
  fetchMyPharmacies,
  getApiErrorMessage,
  searchPortalCustomers,
  searchPortalProducts,
} from '@/shared/api/prescriber-portal.api';
import type { PortalProductItem } from '@/shared/api/prescriber-portal.types';

type LineForm = {
  productId: string;
  productUnitId?: string;
  qtyPrescribed: number;
  dosageInstruction?: string;
};

type PrescribeForm = {
  tenantId: string;
  customerId: string;
  notes?: string;
  lines: LineForm[];
};

function classTag(dispensingClass: string) {
  if (dispensingClass === 'prescription') return <Tag color="blue">Rx</Tag>;
  if (dispensingClass === 'otc') return <Tag color="green">OTC</Tag>;
  return <Tag>{dispensingClass}</Tag>;
}

function formatStock(qty: number | undefined, unitName: string | null | undefined) {
  const n = Number(qty ?? 0);
  const unit = unitName ? ` ${unitName}` : '';
  if (n <= 0) return 'hết hàng';
  return `tồn ${n.toLocaleString('vi-VN')}${unit}`;
}

function LineProductFields({
  fieldName,
  tenantId,
  products,
  productsLoading,
  productsError,
  productEmpty,
  onSearchProduct,
}: {
  fieldName: number;
  tenantId?: string;
  products: PortalProductItem[];
  productsLoading: boolean;
  productsError: string | null;
  productEmpty: string;
  onSearchProduct: (q: string) => void;
}) {
  const { t } = useTranslation();
  const form = Form.useFormInstance<PrescribeForm>();
  const productId = Form.useWatch(['lines', fieldName, 'productId'], form);
  const qty = Form.useWatch(['lines', fieldName, 'qtyPrescribed'], form);
  const selected = products.find((p) => p.productId === productId);
  const units = selected?.units?.length
    ? selected.units
    : selected?.defaultUnitId
      ? [{ id: selected.defaultUnitId, unitName: selected.defaultUnitName ?? 'ĐVT', isBaseUnit: true }]
      : [];
  const stock = Number(selected?.stockAvailableQty ?? 0);
  const stockWarn =
    selected &&
    (stock <= 0
      ? t('prescriptions.stockEmpty')
      : qty != null && qty > stock
        ? t('prescriptions.stockLow', {
            stock: stock.toLocaleString('vi-VN'),
            unit: selected.defaultUnitName ?? '',
          })
        : null);

  return (
    <Space direction="vertical" size={4} style={{ width: '100%' }}>
      <Space align="start" wrap style={{ width: '100%' }}>
        <Form.Item
          name={[fieldName, 'productId']}
          label={t('prescriptions.product')}
          rules={[{ required: true }]}
        >
          <Select
            showSearch
            allowClear
            filterOption={false}
            style={{ minWidth: 280 }}
            placeholder={t('prescriptions.selectProduct')}
            disabled={!tenantId}
            loading={productsLoading}
            options={products.map((p) => ({
              value: p.productId,
              label: (
                <Space size={4}>
                  {classTag(p.dispensingClass)}
                  <span>
                    {p.productName} ({p.productCode})
                    {p.defaultUnitName ? ` · ${p.defaultUnitName}` : ''}
                    {' · '}
                    {formatStock(p.stockAvailableQty, p.defaultUnitName)}
                  </span>
                </Space>
              ),
            }))}
            onSearch={onSearchProduct}
            notFoundContent={productsError ?? (productsLoading ? t('common.loading') : productEmpty)}
            onChange={(id) => {
              const product = products.find((p) => p.productId === id);
              const unitId = product?.defaultUnitId ?? product?.units?.[0]?.id ?? null;
              form.setFieldValue(['lines', fieldName, 'productUnitId'], unitId);
            }}
          />
        </Form.Item>

        <Form.Item
          name={[fieldName, 'qtyPrescribed']}
          label={t('prescriptions.qty')}
          rules={[{ required: true }]}
        >
          <InputNumber min={0.01} step={1} style={{ width: 100 }} />
        </Form.Item>

        <Form.Item
          name={[fieldName, 'productUnitId']}
          label={t('prescriptions.unit')}
          rules={[{ required: true, message: t('prescriptions.unitRequired') }]}
        >
          <Select
            style={{ minWidth: 120 }}
            placeholder={t('prescriptions.selectUnit')}
            disabled={!productId || units.length === 0}
            options={units.map((u) => ({
              value: u.id,
              label: u.isBaseUnit ? `${u.unitName} (cơ bản)` : u.unitName,
            }))}
          />
        </Form.Item>

        <Form.Item name={[fieldName, 'dosageInstruction']} label={t('prescriptions.dosage')}>
          <Input placeholder="2 viên x 2 lần/ngày" style={{ minWidth: 180 }} />
        </Form.Item>
      </Space>
      {stockWarn ? <Alert type="warning" showIcon message={stockWarn} style={{ maxWidth: 640 }} /> : null}
    </Space>
  );
}

export function PrescribePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [form] = Form.useForm<PrescribeForm>();
  const [customerQuery, setCustomerQuery] = useState('');
  const [productQuery, setProductQuery] = useState('');
  const tenantId = Form.useWatch('tenantId', form);

  const pharmaciesQuery = useQuery({
    queryKey: ['prescriber', 'pharmacies'],
    queryFn: () => fetchMyPharmacies(true),
  });

  const customersQuery = useQuery({
    queryKey: ['prescriber', 'customers', tenantId, customerQuery],
    queryFn: () => searchPortalCustomers(tenantId, customerQuery.trim() || undefined),
    enabled: Boolean(tenantId) && customerQuery.trim().length >= 2,
  });

  const productsQuery = useQuery({
    queryKey: ['prescriber', 'products', tenantId, productQuery],
    queryFn: () => searchPortalProducts(tenantId, productQuery.trim() || undefined),
    enabled: Boolean(tenantId),
  });

  const customerOptions = useMemo(
    () =>
      (customersQuery.data ?? []).map((c) => ({
        value: c.id,
        label: `${c.fullName} · ${c.phone ?? c.customerCode}`,
      })),
    [customersQuery.data],
  );

  const products = productsQuery.data ?? [];

  const createMutation = useMutation({
    mutationFn: createPortalPrescription,
    onSuccess: (data) => {
      message.success(t('prescriptions.createSuccess', { code: data.prescriptionCode }));
      navigate('/prescriptions');
    },
    onError: (error) => message.error(getApiErrorMessage(error, t('prescriptions.createFailed'))),
  });

  const onFinish = (values: PrescribeForm) => {
    const stockIssues = values.lines
      .map((line) => {
        const product = products.find((p) => p.productId === line.productId);
        if (!product) return null;
        const stock = Number(product.stockAvailableQty ?? 0);
        if (stock <= 0) return `${product.productName}: ${t('prescriptions.stockEmpty')}`;
        if (line.qtyPrescribed > stock) {
          return `${product.productName}: ${t('prescriptions.stockLow', {
            stock: stock.toLocaleString('vi-VN'),
            unit: product.defaultUnitName ?? '',
          })}`;
        }
        return null;
      })
      .filter(Boolean);
    if (stockIssues.length > 0) {
      message.warning(t('prescriptions.stockSubmitWarn'));
    }
    createMutation.mutate({
      tenantId: values.tenantId,
      customerId: values.customerId,
      notes: values.notes,
      lines: values.lines.map((line) => ({
        productId: line.productId,
        productUnitId: line.productUnitId ?? null,
        qtyPrescribed: line.qtyPrescribed,
        dosageInstruction: line.dosageInstruction,
      })),
    });
  };

  return (
    <div>
      <Typography.Title level={4}>{t('prescriptions.newTitle')}</Typography.Title>
      <Card>
        <Form
          form={form}
          layout="vertical"
          onFinish={onFinish}
          initialValues={{ lines: [{ qtyPrescribed: 1 }] }}
        >
          <Form.Item name="tenantId" label={t('prescriptions.pharmacy')} rules={[{ required: true }]}>
            <Select
              placeholder={t('prescriptions.selectPharmacy')}
              loading={pharmaciesQuery.isLoading}
              options={(pharmaciesQuery.data ?? []).map((p) => ({
                value: p.tenantId,
                label: `${p.tenantName} (${p.tenantCode})`,
              }))}
              onChange={() => {
                setProductQuery('');
                setCustomerQuery('');
                form.setFieldsValue({ customerId: undefined, lines: [{ qtyPrescribed: 1 }] });
              }}
            />
          </Form.Item>

          {tenantId ? (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {t('prescriptions.catalogHint')}
            </Typography.Paragraph>
          ) : null}

          <Form.Item name="customerId" label={t('prescriptions.customer')} rules={[{ required: true }]}>
            <Select
              showSearch
              allowClear
              filterOption={false}
              placeholder={t('prescriptions.customerLivePlaceholder')}
              disabled={!tenantId}
              loading={customersQuery.isFetching}
              options={customerOptions}
              onSearch={setCustomerQuery}
              notFoundContent={
                !tenantId
                  ? t('prescriptions.selectPharmacyFirst')
                  : customersQuery.isError
                    ? getApiErrorMessage(customersQuery.error, t('prescriptions.customerEmpty'))
                    : customerQuery.trim().length < 2
                      ? t('prescriptions.customerTypeMore')
                      : customersQuery.isFetching
                        ? t('common.loading')
                        : t('prescriptions.customerEmpty')
              }
            />
          </Form.Item>

          <Typography.Text strong>{t('prescriptions.lines')}</Typography.Text>
          <Form.List name="lines">
            {(fields, { add, remove }) => (
              <Space direction="vertical" style={{ width: '100%', marginTop: 8 }}>
                {fields.map((field) => (
                  <Card key={field.key} size="small">
                    <Space align="start" wrap style={{ width: '100%' }}>
                      <LineProductFields
                        fieldName={field.name}
                        tenantId={tenantId}
                        products={products}
                        productsLoading={productsQuery.isFetching}
                        productsError={
                          productsQuery.isError
                            ? getApiErrorMessage(productsQuery.error, t('prescriptions.productEmpty'))
                            : null
                        }
                        productEmpty={t('prescriptions.productEmpty')}
                        onSearchProduct={setProductQuery}
                      />
                      <Button type="text" danger icon={<MinusCircleOutlined />} onClick={() => remove(field.name)} />
                    </Space>
                  </Card>
                ))}
                <Button type="dashed" onClick={() => add({ qtyPrescribed: 1 })} icon={<PlusOutlined />}>
                  {t('prescriptions.addLine')}
                </Button>
              </Space>
            )}
          </Form.List>

          <Form.Item name="notes" label={t('prescriptions.notes')} style={{ marginTop: 16 }}>
            <Input.TextArea rows={2} />
          </Form.Item>

          <Button type="primary" htmlType="submit" loading={createMutation.isPending}>
            {t('prescriptions.submitSigned')}
          </Button>
        </Form>
      </Card>
    </div>
  );
}

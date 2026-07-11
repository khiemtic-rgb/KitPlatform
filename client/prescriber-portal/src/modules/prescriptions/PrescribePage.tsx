import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Form, Input, InputNumber, Select, Space, Tag, Typography, message } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router-dom';
import {
  amendPortalPrescription,
  createPortalPrescription,
  fetchMyPharmacies,
  fetchPortalPrescription,
  getApiErrorMessage,
  searchPortalCustomers,
  searchPortalProducts,
} from '@/shared/api/prescriber-portal.api';
import { canAmendPortalPrescription, type PortalProductItem } from '@/shared/api/prescriber-portal.types';
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue';

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

function formatStock(
  t: (key: string, opts?: Record<string, string>) => string,
  qty: number | undefined,
  unitName: string | null | undefined,
) {
  const n = Number(qty ?? 0);
  if (n <= 0) return t('prescriptions.stockOut');
  return t('prescriptions.stockLabel', {
    qty: n.toLocaleString('vi-VN'),
    unit: unitName ? ` ${unitName}` : '',
  });
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
                    {formatStock(t, p.stockAvailableQty, p.defaultUnitName)}
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
              label: u.isBaseUnit ? t('prescriptions.unitBase', { name: u.unitName }) : u.unitName,
            }))}
          />
        </Form.Item>

        <Form.Item name={[fieldName, 'dosageInstruction']} label={t('prescriptions.dosage')}>
          <Input placeholder={t('prescriptions.dosagePlaceholder')} style={{ minWidth: 180 }} />
        </Form.Item>
      </Space>
      {stockWarn ? <Alert type="warning" showIcon message={stockWarn} style={{ maxWidth: 640 }} /> : null}
    </Space>
  );
}

export function PrescribePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { id: editId } = useParams<{ id?: string }>();
  const isEdit = Boolean(editId);
  const [form] = Form.useForm<PrescribeForm>();
  const [customerQuery, setCustomerQuery] = useState('');
  const [productQuery, setProductQuery] = useState('');
  const debouncedCustomerQuery = useDebouncedValue(customerQuery, 300);
  const debouncedProductQuery = useDebouncedValue(productQuery, 300);
  const tenantId = Form.useWatch('tenantId', form);

  const existingQuery = useQuery({
    queryKey: ['prescriber', 'prescription', editId],
    queryFn: () => fetchPortalPrescription(editId!),
    enabled: isEdit,
  });

  useEffect(() => {
    const detail = existingQuery.data;
    if (!detail) return;
    if (!canAmendPortalPrescription(detail)) {
      message.warning(t('prescriptions.editBlockedDispensed'));
      navigate('/prescriptions', { replace: true });
      return;
    }
    form.setFieldsValue({
      tenantId: detail.tenantId,
      customerId: detail.customerId ?? undefined,
      notes: detail.notes ?? undefined,
      lines: detail.lines.map((line) => ({
        productId: line.productId,
        productUnitId: line.productUnitId ?? undefined,
        qtyPrescribed: line.qtyPrescribed,
        dosageInstruction: line.dosageInstruction ?? undefined,
      })),
    });
    if (detail.patientName || detail.patientPhone) {
      setCustomerQuery(detail.patientPhone || detail.patientName || '');
    }
  }, [existingQuery.data, form, navigate, t]);

  const pharmaciesQuery = useQuery({
    queryKey: ['prescriber', 'pharmacies'],
    queryFn: () => fetchMyPharmacies(true),
  });

  const customersQuery = useQuery({
    queryKey: ['prescriber', 'customers', tenantId, debouncedCustomerQuery],
    queryFn: () => searchPortalCustomers(tenantId, debouncedCustomerQuery.trim() || undefined),
    enabled: Boolean(tenantId) && debouncedCustomerQuery.trim().length >= 2,
  });

  const productsQuery = useQuery({
    queryKey: ['prescriber', 'products', tenantId, debouncedProductQuery],
    queryFn: () => searchPortalProducts(tenantId, debouncedProductQuery.trim() || undefined),
    enabled: Boolean(tenantId),
    placeholderData: (prev) => prev,
  });

  const customerOptions = useMemo(() => {
    const fromSearch = (customersQuery.data ?? []).map((c) => ({
      value: c.id,
      label: `${c.fullName} · ${c.phone ?? c.customerCode}`,
    }));
    const detail = existingQuery.data;
    if (detail?.customerId && !fromSearch.some((o) => o.value === detail.customerId)) {
      fromSearch.unshift({
        value: detail.customerId,
        label: `${detail.patientName ?? 'BN'} · ${detail.patientPhone ?? detail.customerId}`,
      });
    }
    return fromSearch;
  }, [customersQuery.data, existingQuery.data]);

  const products = useMemo(() => {
    const list = [...(productsQuery.data ?? [])];
    const detail = existingQuery.data;
    if (!detail) return list;
    for (const line of detail.lines) {
      if (!list.some((p) => p.productId === line.productId)) {
        list.push({
          productId: line.productId,
          productCode: line.productCode,
          productName: line.productName,
          dispensingClass: line.lineDispensingClass ?? 'prescription',
          defaultUnitId: line.productUnitId ?? null,
          defaultUnitName: line.unitName ?? null,
          units: line.productUnitId
            ? [{ id: line.productUnitId, unitName: line.unitName ?? 'ĐVT', isBaseUnit: true }]
            : [],
          stockAvailableQty: undefined,
        });
      }
    }
    return list;
  }, [productsQuery.data, existingQuery.data]);

  const saveMutation = useMutation({
    mutationFn: async (values: PrescribeForm) => {
      const lines = values.lines.map((line) => ({
        productId: line.productId,
        productUnitId: line.productUnitId ?? null,
        qtyPrescribed: line.qtyPrescribed,
        dosageInstruction: line.dosageInstruction,
      }));
      if (isEdit && editId) {
        return amendPortalPrescription(editId, { notes: values.notes, lines });
      }
      return createPortalPrescription({
        tenantId: values.tenantId,
        customerId: values.customerId,
        notes: values.notes,
        lines,
      });
    },
    onSuccess: (data) => {
      message.success(
        isEdit
          ? t('prescriptions.amendSuccess', { code: data.prescriptionCode })
          : t('prescriptions.createSuccess', { code: data.prescriptionCode }),
      );
      navigate('/prescriptions');
    },
    onError: (error) =>
      message.error(
        getApiErrorMessage(error, isEdit ? t('prescriptions.amendFailed') : t('prescriptions.createFailed')),
      ),
  });

  const onFinish = (values: PrescribeForm) => {
    const stockIssues = values.lines
      .map((line) => {
        const product = products.find((p) => p.productId === line.productId);
        if (!product || product.stockAvailableQty == null) return null;
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
    saveMutation.mutate(values);
  };

  if (isEdit && existingQuery.isLoading) {
    return <Typography.Text type="secondary">{t('common.loading')}</Typography.Text>;
  }

  return (
    <div>
      <Typography.Title level={4}>
        {isEdit
          ? t('prescriptions.editTitle', { code: existingQuery.data?.prescriptionCode ?? '' })
          : t('prescriptions.newTitle')}
      </Typography.Title>
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
              disabled={isEdit}
              options={(pharmaciesQuery.data ?? []).map((p) => ({
                value: p.tenantId,
                label: `${p.tenantName} (${p.tenantCode})`,
              }))}
              onChange={() => {
                if (isEdit) return;
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
              disabled={!tenantId || isEdit}
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

          <Space>
            <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
              {isEdit ? t('prescriptions.saveAmend') : t('prescriptions.submitSigned')}
            </Button>
            <Button onClick={() => navigate('/prescriptions')}>{t('common.cancel')}</Button>
          </Space>
        </Form>
      </Card>
    </div>
  );
}

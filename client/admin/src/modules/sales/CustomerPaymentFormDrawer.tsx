import { memo, useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Col, Drawer, Form, Input, InputNumber, Row, Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { SaveOutlined } from '@ant-design/icons';
import { isAxiosError } from 'axios';
import {
  createCustomerPayment,
  fetchCustomerPayment,
  fetchCustomerReceivablesDetail,
  updateCustomerPayment,
} from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  CustomerListItem,
  CustomerPaymentListItem,
  CustomerReceivablesDetailLine,
} from '@/shared/api/sales.types';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';
import { formatDisplayDate } from '@/shared/utils/date';
import {
  formatDisplayMoney,
  formatMoneyInputWithSuffix,
  moneyInputClassName,
  moneyInputNumberStyle,
  parseMoneyInput,
} from '@/shared/utils/money';
import type { CustomerPaymentPrefill } from '@/modules/sales/customer-payment-nav';

const moneyInputWithSuffixProps = {
  min: 1,
  precision: 0,
  controls: false,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatMoneyInputWithSuffix(value),
  parser: (value: string | undefined) => parseMoneyInput(value) ?? 0,
} as const;

const moneyReadonlyWithSuffixProps = {
  min: 0,
  precision: 0,
  controls: false,
  readOnly: true,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatMoneyInputWithSuffix(value),
  parser: (value: string | undefined) => parseMoneyInput(value) ?? 0,
} as const;

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

function toFormPaymentDate(value?: string): string {
  if (!value) return todayIsoDate();
  return value.length >= 10 ? value.slice(0, 10) : value;
}

function resolveAmount(value: unknown): number {
  if (typeof value === 'number') return value;
  if (typeof value === 'string') return parseMoneyInput(value) ?? Number.NaN;
  return Number.NaN;
}

export type CustomerPaymentFormDrawerProps = {
  open: boolean;
  editingId: string | null;
  editingRow: CustomerPaymentListItem | null;
  customers: CustomerListItem[];
  prefill?: CustomerPaymentPrefill;
  onClose: () => void;
  onSaved: (saved: CustomerPaymentListItem) => void;
};

export const CustomerPaymentFormDrawer = memo(function CustomerPaymentFormDrawer({
  open,
  editingId,
  editingRow,
  customers,
  prefill,
  onClose,
  onSaved,
}: CustomerPaymentFormDrawerProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'customerPayments.form' });
  const { message } = App.useApp();
  const { collectionPaymentMethodOptions } = useSalesEnums();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [ordersLoading, setOrdersLoading] = useState(false);
  const [orderLines, setOrderLines] = useState<CustomerReceivablesDetailLine[]>([]);
  const customerId = Form.useWatch('customerId', form);
  const salesOrderId = Form.useWatch('salesOrderId', form) as string | undefined;

  const customerOptions = useMemo(
    () =>
      customers.map((c) => ({
        value: c.id,
        label: `${c.customerCode} — ${c.fullName}`,
      })),
    [customers],
  );

  const orderTotals = useMemo(() => {
    let orderTotal = 0;
    let paidAmount = 0;
    let outstanding = 0;
    for (const line of orderLines) {
      orderTotal += line.orderTotal;
      paidAmount += line.paidAmount;
      outstanding += line.outstanding;
    }
    return { orderTotal, paidAmount, outstanding };
  }, [orderLines]);

  const loadOrderLines = useCallback(
    async (id: string) => {
      setOrdersLoading(true);
      try {
        const detail = await fetchCustomerReceivablesDetail(id);
        setOrderLines(detail.lines.filter((line) => line.outstanding > 0.009));
      } catch (error) {
        setOrderLines([]);
        message.error(apiErrorMessage(error, t('messages.loadOrdersFailed')));
      } finally {
        setOrdersLoading(false);
      }
    },
    [message, t],
  );

  useEffect(() => {
    if (!open) return;

    if (prefill) {
      form.setFieldsValue({
        customerId: prefill.customerId,
        salesOrderId: prefill.salesOrderId,
        amount: prefill.amount,
        paymentMethod: 1,
        paymentDate: todayIsoDate(),
        notes: undefined,
      });
      return;
    }

    if (editingRow) {
      form.setFieldsValue({
        customerId: editingRow.customerId,
        salesOrderId: editingRow.salesOrderId,
        amount: editingRow.amount,
        paymentMethod: editingRow.paymentMethod,
        paymentDate: toFormPaymentDate(editingRow.paymentDate),
        notes: editingRow.notes,
      });
      return;
    }

    form.resetFields();
    form.setFieldsValue({ paymentMethod: 1, paymentDate: todayIsoDate() });
    setOrderLines([]);
  }, [open, prefill, editingRow, form]);

  useEffect(() => {
    if (!open || !editingId || editingRow) return;
    let cancelled = false;
    void fetchCustomerPayment(editingId)
      .then((row) => {
        if (cancelled) return;
        form.setFieldsValue({
          customerId: row.customerId,
          salesOrderId: row.salesOrderId,
          amount: row.amount,
          paymentMethod: row.paymentMethod,
          paymentDate: toFormPaymentDate(row.paymentDate),
          notes: row.notes,
        });
      })
      .catch((error) => {
        if (!cancelled) {
          message.error(apiErrorMessage(error, t('messages.loadDetailFailed')));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [open, editingId, editingRow, form, message, t]);

  useEffect(() => {
    if (!open) return;
    if (!customerId) {
      setOrderLines([]);
      return;
    }
    void loadOrderLines(String(customerId));
  }, [open, customerId, loadOrderLines]);

  const selectedOrder = useMemo(
    () => orderLines.find((line) => line.salesOrderId === salesOrderId),
    [orderLines, salesOrderId],
  );

  const amountCap = selectedOrder?.outstanding ?? orderTotals.outstanding;

  const selectOrder = useCallback(
    (orderId: string | undefined, fillAmount: boolean) => {
      form.setFieldsValue({ salesOrderId: orderId });
      if (!fillAmount) return;
      if (orderId) {
        const line = orderLines.find((row) => row.salesOrderId === orderId);
        if (line) form.setFieldsValue({ amount: line.outstanding });
        return;
      }
      if (orderTotals.outstanding > 0.009) {
        form.setFieldsValue({ amount: orderTotals.outstanding });
      }
    },
    [form, orderLines, orderTotals.outstanding],
  );

  const orderColumns: ColumnsType<CustomerReceivablesDetailLine> = useMemo(
    () => [
      {
        title: t('orderColumns.orderNumber'),
        dataIndex: 'orderNumber',
        key: 'orderNumber',
        width: 110,
        ellipsis: true,
      },
      {
        title: t('orderColumns.orderDate'),
        dataIndex: 'orderDate',
        key: 'orderDate',
        width: 96,
        render: (value: string) => formatDisplayDate(value),
      },
      {
        title: t('orderColumns.orderTotal'),
        dataIndex: 'orderTotal',
        key: 'orderTotal',
        align: 'right',
        width: 110,
        render: (value: number) => formatDisplayMoney(value),
      },
      {
        title: t('orderColumns.paidAmount'),
        dataIndex: 'paidAmount',
        key: 'paidAmount',
        align: 'right',
        width: 100,
        render: (value: number) => formatDisplayMoney(value),
      },
      {
        title: t('orderColumns.outstanding'),
        dataIndex: 'outstanding',
        key: 'outstanding',
        align: 'right',
        width: 110,
        render: (value: number) => (
          <Typography.Text strong>{formatDisplayMoney(value)}</Typography.Text>
        ),
      },
    ],
    [t],
  );

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      const payload = {
        customerId: values.customerId as string,
        salesOrderId: values.salesOrderId as string | undefined,
        amount: resolveAmount(values.amount),
        paymentMethod: Number(values.paymentMethod),
        paymentDate: (values.paymentDate as string | undefined) || todayIsoDate(),
        notes: values.notes as string | undefined,
      };
      if (Number.isNaN(payload.amount) || payload.amount <= 0) {
        message.error(t('messages.invalidAmount'));
        return;
      }
      if (selectedOrder && payload.amount > selectedOrder.outstanding + 0.009) {
        message.error(
          t('messages.amountExceeds', {
            outstanding: formatDisplayMoney(selectedOrder.outstanding),
          }),
        );
        return;
      }
      if (!selectedOrder && orderLines.length > 0 && payload.amount > orderTotals.outstanding + 0.009) {
        message.error(
          t('messages.amountExceedsTotal', {
            outstanding: formatDisplayMoney(orderTotals.outstanding),
          }),
        );
        return;
      }
      if (editingId) {
        const updated = await updateCustomerPayment(editingId, payload);
        message.success(t('messages.updateSuccess', { paymentNumber: updated.paymentNumber }));
        onSaved(updated);
      } else {
        const created = await createCustomerPayment(payload);
        message.success(t('messages.createSuccess', { paymentNumber: created.paymentNumber }));
        onSaved(created);
      }
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.saveFailed')));
      } else {
        message.error(t('messages.formInvalid'));
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={editingId ? t('editTitle') : t('createTitle')}
      width={720}
      open={open}
      destroyOnClose
      onClose={onClose}
      extra={
        <Button type="primary" icon={<SaveOutlined />} onClick={() => void handleSave()} loading={saving}>
          {t('save')}
        </Button>
      }
    >
      <Form form={form} layout="vertical" initialValues={{ paymentMethod: 1, paymentDate: todayIsoDate() }}>
        <Form.Item
          name="customerId"
          label={t('customer')}
          rules={[{ required: true, message: t('customerRequired') }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            options={customerOptions}
            onChange={(id) => {
              form.setFieldsValue({ salesOrderId: undefined, amount: undefined });
              if (!id) setOrderLines([]);
            }}
          />
        </Form.Item>

        <Form.Item name="salesOrderId" hidden>
          <Input />
        </Form.Item>

        <Form.Item
          label={t('orderLink')}
          extra={
            customerId
              ? selectedOrder
                ? t('orderLinkSelectedHint', { orderNumber: selectedOrder.orderNumber })
                : t('orderLinkFifoHint')
              : undefined
          }
        >
          {!customerId ? (
            <Typography.Text type="secondary">{t('orderSelectCustomerFirst')}</Typography.Text>
          ) : (
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              <Table<CustomerReceivablesDetailLine>
                size="small"
                rowKey="salesOrderId"
                loading={ordersLoading}
                pagination={false}
                scroll={{ y: 220 }}
                locale={{ emptyText: t('orderEmpty') }}
                columns={orderColumns}
                dataSource={orderLines}
                rowSelection={{
                  type: 'radio',
                  selectedRowKeys: salesOrderId ? [salesOrderId] : [],
                  onChange: (keys) => {
                    const next = keys[0] ? String(keys[0]) : undefined;
                    selectOrder(next, true);
                  },
                }}
                onRow={(record) => ({
                  onClick: () => selectOrder(record.salesOrderId, true),
                  style: { cursor: 'pointer' },
                })}
                summary={() =>
                  orderLines.length > 0 ? (
                    <Table.Summary fixed>
                      <Table.Summary.Row>
                        <Table.Summary.Cell index={0} />
                        <Table.Summary.Cell index={1} colSpan={2}>
                          <Typography.Text strong>{t('orderSummary.label')}</Typography.Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={3} align="right">
                          <Typography.Text strong>
                            {formatDisplayMoney(orderTotals.orderTotal)}
                          </Typography.Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={4} align="right">
                          <Typography.Text strong>
                            {formatDisplayMoney(orderTotals.paidAmount)}
                          </Typography.Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={5} align="right">
                          <Typography.Text strong type="danger">
                            {formatDisplayMoney(orderTotals.outstanding)}
                          </Typography.Text>
                        </Table.Summary.Cell>
                      </Table.Summary.Row>
                    </Table.Summary>
                  ) : null
                }
              />
              <Space wrap>
                {selectedOrder ? (
                  <Button type="link" size="small" style={{ padding: 0 }} onClick={() => selectOrder(undefined, false)}>
                    {t('clearOrderLink')}
                  </Button>
                ) : null}
                {orderTotals.outstanding > 0.009 ? (
                  <Button
                    type="link"
                    size="small"
                    style={{ padding: 0 }}
                    onClick={() => selectOrder(undefined, true)}
                  >
                    {t('fillAllOutstanding')}
                  </Button>
                ) : null}
              </Space>
            </Space>
          )}
        </Form.Item>

        {selectedOrder || (customerId && orderTotals.outstanding > 0.009) ? (
          <Typography.Paragraph type="secondary" style={{ marginTop: -8, marginBottom: 12 }}>
            {t('hint.description')}
          </Typography.Paragraph>
        ) : null}

        <Row gutter={12}>
          <Col xs={24} sm={12}>
            <Form.Item
              name="amount"
              label={t('amount')}
              rules={[
                { required: true, message: t('amountRequired') },
                {
                  validator: async (_, value) => {
                    const amount = resolveAmount(value);
                    if (Number.isNaN(amount) || amount <= 0) {
                      throw new Error(t('messages.invalidAmount'));
                    }
                    if (amountCap > 0 && amount > amountCap + 0.009) {
                      throw new Error(
                        t(selectedOrder ? 'messages.validatorExceeds' : 'messages.validatorExceedsTotal', {
                          outstanding: formatDisplayMoney(amountCap),
                        }),
                      );
                    }
                  },
                },
              ]}
            >
              <InputNumber
                {...moneyInputWithSuffixProps}
                style={moneyInputNumberStyle}
                min={1}
                max={amountCap > 0 ? amountCap : undefined}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item label={t('outstandingAmount')}>
              <InputNumber
                {...moneyReadonlyWithSuffixProps}
                style={moneyInputNumberStyle}
                value={customerId ? amountCap : undefined}
                placeholder={customerId ? undefined : t('orderSelectCustomerFirst')}
              />
            </Form.Item>
          </Col>
        </Row>

        <Row gutter={12}>
          <Col xs={24} sm={12}>
            <Form.Item name="paymentMethod" label={t('paymentMethod')} rules={[{ required: true }]}>
              <Select options={collectionPaymentMethodOptions} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item name="paymentDate" label={t('paymentDate')}>
              <PharmaDatePicker style={{ width: '100%' }} />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item name="notes" label={t('notes')}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Drawer>
  );
});

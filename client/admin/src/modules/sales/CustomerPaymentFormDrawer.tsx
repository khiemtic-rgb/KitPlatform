import { memo, useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Drawer, Form, Input, InputNumber, Select, message } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import { isAxiosError } from 'axios';
import { CustomerPaymentAmountHint } from '@/modules/sales/CustomerPaymentAmountHint';
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
import {
  formatDisplayMoney,
  moneyInputNumberProps,
  moneyInputNumberStyle,
  parseMoneyInput,
} from '@/shared/utils/money';
import type { CustomerPaymentPrefill } from '@/modules/sales/customer-payment-nav';

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
  const { collectionPaymentMethodOptions } = useSalesEnums();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [orderLines, setOrderLines] = useState<CustomerReceivablesDetailLine[]>([]);
  const customerId = Form.useWatch('customerId', form);
  const salesOrderId = Form.useWatch('salesOrderId', form);

  const customerOptions = useMemo(
    () =>
      customers.map((c) => ({
        value: c.id,
        label: `${c.customerCode} — ${c.fullName}`,
      })),
    [customers],
  );

  const loadOrderLines = useCallback(
    async (id: string) => {
      try {
        const detail = await fetchCustomerReceivablesDetail(id);
        setOrderLines(detail.lines.filter((line) => line.outstanding > 0.009));
      } catch (error) {
        setOrderLines([]);
        message.error(apiErrorMessage(error, t('messages.loadOrdersFailed')));
      }
    },
    [t],
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
      void loadOrderLines(prefill.customerId);
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
      void loadOrderLines(editingRow.customerId);
      return;
    }

    form.resetFields();
    form.setFieldsValue({ paymentMethod: 1, paymentDate: todayIsoDate() });
    setOrderLines([]);
  }, [open, prefill, editingRow, form, loadOrderLines]);

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
        void loadOrderLines(row.customerId);
      })
      .catch((error) => {
        if (!cancelled) {
          message.error(apiErrorMessage(error, t('messages.loadDetailFailed')));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [open, editingId, editingRow, form, loadOrderLines, t]);

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

  const orderLineOptions = useMemo(
    () =>
      orderLines.map((line) => ({
        value: line.salesOrderId,
        label: t('orderOption', {
          orderNumber: line.orderNumber,
          outstanding: formatDisplayMoney(line.outstanding),
        }),
      })),
    [orderLines, t],
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
      width={480}
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
              if (id) void loadOrderLines(String(id));
              else setOrderLines([]);
            }}
          />
        </Form.Item>
        <Form.Item name="salesOrderId" label={t('orderLink')}>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            disabled={!customerId}
            placeholder={customerId ? t('orderPlaceholder') : t('orderSelectCustomerFirst')}
            options={orderLineOptions}
            onChange={(value: string | undefined) => {
              const line = orderLines.find((row) => row.salesOrderId === value);
              if (line && !form.getFieldValue('amount')) {
                form.setFieldsValue({ amount: line.outstanding });
              }
            }}
          />
        </Form.Item>
        {selectedOrder ? (
          <CustomerPaymentAmountHint
            orderNumber={selectedOrder.orderNumber}
            outstanding={selectedOrder.outstanding}
            onFillAmount={(amount) => form.setFieldsValue({ amount })}
          />
        ) : null}
        <Form.Item
          name="amount"
          label={t('amount')}
          rules={[
            { required: true, message: t('amountRequired') },
            {
              validator: async (_, value) => {
                if (!selectedOrder) return;
                const amount = resolveAmount(value);
                if (Number.isNaN(amount) || amount <= 0) {
                  throw new Error(t('messages.invalidAmount'));
                }
                if (amount > selectedOrder.outstanding + 0.009) {
                  throw new Error(
                    t('messages.validatorExceeds', {
                      outstanding: formatDisplayMoney(selectedOrder.outstanding),
                    }),
                  );
                }
              },
            },
          ]}
        >
          <InputNumber
            {...moneyInputNumberProps}
            style={moneyInputNumberStyle}
            min={1}
            max={selectedOrder ? selectedOrder.outstanding : undefined}
          />
        </Form.Item>
        <Form.Item name="paymentMethod" label={t('paymentMethod')} rules={[{ required: true }]}>
          <Select options={collectionPaymentMethodOptions} />
        </Form.Item>
        <Form.Item name="paymentDate" label={t('paymentDate')}>
          <PharmaDatePicker style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="notes" label={t('notes')}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Drawer>
  );
});

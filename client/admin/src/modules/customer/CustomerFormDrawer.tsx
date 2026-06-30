import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Drawer, Form, Input, InputNumber, Select, Space, Switch, message } from 'antd';
import { CloseOutlined, SaveOutlined } from '@ant-design/icons';
import {
  createCustomer,
  fetchNextCustomerCode,
  updateCustomer,
} from '@/shared/api/customer-admin.api';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCustomerEnums } from '@/shared/i18n/use-customer-enums';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';

interface CustomerFormValues {
  customerCode?: string;
  fullName: string;
  phone: string;
  email?: string;
  dateOfBirth?: string;
  gender?: number;
  status?: number;
  allowCredit?: boolean;
  creditLimit?: number | null;
}

interface CustomerFormDrawerProps {
  open: boolean;
  editing: CustomerDetail | null;
  onClose: () => void;
  onSaved: (customer: CustomerDetail) => void;
  /** POS: chỉ họ tên + SĐT, mã tự sinh */
  variant?: 'full' | 'quick';
}

function normalizeCustomerCodeInput(value: string | undefined): string | undefined {
  if (value == null) return value;
  const trimmed = value.trim();
  return trimmed ? trimmed.toUpperCase() : undefined;
}

export function CustomerFormDrawer({
  open,
  editing,
  onClose,
  onSaved,
  variant = 'full',
}: CustomerFormDrawerProps) {
  const { t } = useTranslation('customer', { keyPrefix: 'formDrawer' });
  const { t: tc } = useTranslation('common');
  const { customerGenderOptions, customerStatusOptions } = useCustomerEnums();
  const isQuick = variant === 'quick' && !editing;
  const [form] = Form.useForm<CustomerFormValues>();
  const [saving, setSaving] = useState(false);
  const [loadingCode, setLoadingCode] = useState(false);

  useEffect(() => {
    if (!open) return;

    if (editing) {
      form.setFieldsValue({
        customerCode: editing.customerCode.toUpperCase(),
        fullName: editing.fullName,
        phone: editing.phone,
        email: editing.email,
        dateOfBirth: editing.dateOfBirth,
        gender: editing.gender,
        status: editing.status,
        allowCredit: editing.allowCredit,
        creditLimit: editing.creditLimit,
      });
      return;
    }

    form.resetFields();
    form.setFieldsValue({ status: 1 });
    if (isQuick) return;

    setLoadingCode(true);
    void fetchNextCustomerCode()
      .then((code) => form.setFieldsValue({ customerCode: code.toUpperCase() }))
      .catch(() => {
        /* gợi ý mã tùy chọn */
      })
      .finally(() => setLoadingCode(false));
  }, [open, editing, form, isQuick]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields(isQuick ? ['fullName', 'phone'] : undefined);
      const customerCode = isQuick ? undefined : normalizeCustomerCodeInput(values.customerCode);
      setSaving(true);
      const saved = editing
        ? await updateCustomer(editing.id, {
            customerCode: customerCode!,
            fullName: values.fullName.trim(),
            phone: values.phone.trim(),
            email: values.email?.trim() || undefined,
            dateOfBirth: values.dateOfBirth || undefined,
            gender: values.gender,
            status: values.status ?? 1,
            allowCredit: values.allowCredit ?? false,
            creditLimit: values.allowCredit ? values.creditLimit ?? null : null,
          })
        : await createCustomer({
            fullName: values.fullName.trim(),
            phone: values.phone.trim(),
            customerCode: customerCode || undefined,
            email: values.email?.trim() || undefined,
            dateOfBirth: values.dateOfBirth || undefined,
            gender: values.gender,
          });
      if (!isQuick) {
        message.success(editing ? t('messages.updateSuccess') : t('messages.createSuccess'));
      }
      onSaved(saved);
      onClose();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(apiErrorMessage(error, t('messages.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={
        editing ? t('titleEdit') : isQuick ? t('titleQuickCreate') : t('titleCreate')
      }
      width={isQuick ? 400 : 420}
      open={open}
      onClose={onClose}
      destroyOnClose
      extra={
        <Space>
          <Button icon={<CloseOutlined />} onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void handleSave()}>
            {tc('actions.save')}
          </Button>
        </Space>
      }
    >
      <Form form={form} layout="vertical" requiredMark="optional">
        {!isQuick ? (
          <Form.Item
            name="customerCode"
            label={t('fields.customerCode')}
            extra={editing ? undefined : t('hints.customerCodeSuggest')}
            normalize={(value) => normalizeCustomerCodeInput(value) ?? ''}
            rules={
              editing ? [{ required: true, message: t('validation.customerCodeRequired') }] : undefined
            }
          >
            <Input
              placeholder={t('placeholders.customerCode')}
              style={{ textTransform: 'uppercase' }}
              disabled={loadingCode && !editing}
            />
          </Form.Item>
        ) : null}
        <Form.Item
          name="fullName"
          label={t('fields.fullName')}
          rules={[{ required: true, message: t('validation.fullNameRequired') }]}
        >
          <Input placeholder={t('placeholders.fullName')} autoFocus={isQuick} />
        </Form.Item>
        <Form.Item
          name="phone"
          label={t('fields.phone')}
          rules={[{ required: true, message: t('validation.phoneRequired') }]}
        >
          <Input placeholder={t('placeholders.phone')} />
        </Form.Item>
        {!isQuick ? (
          <>
            <Form.Item name="email" label={t('fields.email')}>
              <Input placeholder={t('placeholders.email')} />
            </Form.Item>
            <Form.Item name="dateOfBirth" label={t('fields.dateOfBirth')}>
              <PharmaDatePicker placeholder={t('placeholders.dateOfBirth')} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="gender" label={t('fields.gender')}>
              <Select allowClear placeholder={t('placeholders.gender')} options={customerGenderOptions} />
            </Form.Item>
          </>
        ) : null}
        {editing ? (
          <>
            <Form.Item name="status" label={t('fields.status')} rules={[{ required: true }]}>
              <Select options={customerStatusOptions} />
            </Form.Item>
            <Form.Item name="allowCredit" label={t('fields.allowCredit')} valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item noStyle shouldUpdate={(prev, cur) => prev.allowCredit !== cur.allowCredit}>
              {({ getFieldValue }) =>
                getFieldValue('allowCredit') ? (
                  <Form.Item
                    name="creditLimit"
                    label={t('fields.creditLimit')}
                    extra={t('hints.creditLimitEmpty')}
                  >
                    <InputNumber min={0} step={1000} style={{ width: '100%' }} />
                  </Form.Item>
                ) : null
              }
            </Form.Item>
          </>
        ) : null}
      </Form>
    </Drawer>
  );
}

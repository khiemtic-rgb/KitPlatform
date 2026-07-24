import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Drawer, Form, Input, InputNumber, Select, Space, Switch } from 'antd';
import { CloseOutlined, SaveOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  createCustomer,
  fetchNextCustomerCode,
  updateCustomer,
} from '@/shared/api/customer-admin.api';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import { fetchCustomerGroups, type CustomerGroup } from '@/shared/api/customer-groups.api';
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
  addressLine?: string;
  idNumber?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  clinicalNotes?: string;
  customerGroupId?: string | null;
}

interface CustomerFormDrawerProps {
  open: boolean;
  editing: CustomerDetail | null;
  onClose: () => void;
  onSaved: (customer: CustomerDetail) => void;
  /** POS: chỉ họ tên + SĐT, mã tự sinh */
  variant?: 'full' | 'quick';
  /** Prefill when opening quick create from POS search (phone or name already typed). */
  initialPhone?: string;
  initialName?: string;
}

function normalizeCustomerCodeInput(value: string | undefined): string | undefined {
  if (value == null) return value;
  const trimmed = value.trim();
  return trimmed ? trimmed.toUpperCase() : undefined;
}

function isPhoneConflictMessage(text: string) {
  const lower = text.toLowerCase();
  return lower.includes('điện thoại') || lower.includes('phone');
}

function isCustomerCodeConflictMessage(text: string) {
  const lower = text.toLowerCase();
  return lower.includes('mã khách') || lower.includes('customer code') || lower.includes('đã tồn tại');
}

export function CustomerFormDrawer({
  open,
  editing,
  onClose,
  onSaved,
  variant = 'full',
  initialPhone,
  initialName,
}: CustomerFormDrawerProps) {
  const { t } = useTranslation('customer', { keyPrefix: 'formDrawer' });
  const { t: tc } = useTranslation('common');
  const { message } = App.useApp();
  const { customerGenderOptions, customerStatusOptions } = useCustomerEnums();
  const isQuick = variant === 'quick' && !editing;
  const [form] = Form.useForm<CustomerFormValues>();
  const [saving, setSaving] = useState(false);
  const [loadingCode, setLoadingCode] = useState(false);
  const [groups, setGroups] = useState<CustomerGroup[]>([]);

  useEffect(() => {
    if (!open || isQuick) return;
    void fetchCustomerGroups(true)
      .then(setGroups)
      .catch(() => setGroups([]));
  }, [open, isQuick]);

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
        addressLine: editing.addressLine,
        idNumber: editing.idNumber,
        emergencyContactName: editing.emergencyContactName,
        emergencyContactPhone: editing.emergencyContactPhone,
        clinicalNotes: editing.clinicalNotes,
        customerGroupId: editing.customerGroupId ?? undefined,
      });
      return;
    }

    form.resetFields();
    // New customers require a phone → default allow credit (can turn off later).
    // Use '' not undefined so Inputs actually show prefilled POS search text.
    form.setFieldsValue({
      status: 1,
      allowCredit: true,
      ...(isQuick
        ? {
            fullName: initialName?.trim() ?? '',
            phone: initialPhone?.trim() ?? '',
          }
        : {}),
    });
    if (isQuick) return;

    setLoadingCode(true);
    void fetchNextCustomerCode()
      .then((code) => form.setFieldsValue({ customerCode: code.toUpperCase() }))
      .catch(() => {
        /* gợi ý mã tùy chọn */
      })
      .finally(() => setLoadingCode(false));
  }, [open, editing, form, isQuick, initialPhone, initialName]);

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
            addressLine: values.addressLine?.trim() || undefined,
            idNumber: values.idNumber?.trim() || undefined,
            emergencyContactName: values.emergencyContactName?.trim() || undefined,
            emergencyContactPhone: values.emergencyContactPhone?.trim() || undefined,
            clinicalNotes: values.clinicalNotes?.trim() || undefined,
            customerGroupId: values.customerGroupId || null,
          })
        : await createCustomer({
            fullName: values.fullName.trim(),
            phone: values.phone.trim(),
            customerCode: customerCode || undefined,
            email: values.email?.trim() || undefined,
            dateOfBirth: values.dateOfBirth || undefined,
            gender: values.gender,
            addressLine: values.addressLine?.trim() || undefined,
            idNumber: values.idNumber?.trim() || undefined,
            emergencyContactName: values.emergencyContactName?.trim() || undefined,
            emergencyContactPhone: values.emergencyContactPhone?.trim() || undefined,
            clinicalNotes: values.clinicalNotes?.trim() || undefined,
            customerGroupId: values.customerGroupId || null,
          });
      if (!isQuick) {
        message.success(editing ? t('messages.updateSuccess') : t('messages.createSuccess'));
      }
      onSaved(saved);
      onClose();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      const text = apiErrorMessage(error, t('messages.saveFailed'));
      if (isPhoneConflictMessage(text)) {
        form.setFields([{ name: 'phone', errors: [text] }]);
      } else if (isCustomerCodeConflictMessage(text)) {
        form.setFields([{ name: 'customerCode', errors: [text] }]);
      }
      message.error(text);
    } finally {
      setSaving(false);
    }
  };

  const formGridStyle = isQuick
    ? undefined
    : ({
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        columnGap: 16,
        rowGap: 0,
      } as const);

  return (
    <Drawer
      title={
        editing ? t('titleEdit') : isQuick ? t('titleQuickCreate') : t('titleCreate')
      }
      width={isQuick ? 400 : 760}
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
      <Form form={form} layout="vertical" requiredMark="optional" style={formGridStyle}>
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
              <PharmaDatePicker
                placeholder={t('placeholders.dateOfBirth')}
                style={{ width: '100%' }}
                yearFrom={1920}
                yearTo={new Date().getFullYear()}
                disabledDate={(current) => !!current && current.isAfter(dayjs(), 'day')}
              />
            </Form.Item>
            <Form.Item name="gender" label={t('fields.gender')}>
              <Select allowClear placeholder={t('placeholders.gender')} options={customerGenderOptions} />
            </Form.Item>
            <Form.Item
              name="customerGroupId"
              label={t('fields.customerGroup')}
              extra={t('hints.customerGroup')}
            >
              <Select
                allowClear
                placeholder={t('placeholders.customerGroup')}
                options={groups.map((g) => ({
                  value: g.id,
                  label:
                    g.discountPercent > 0
                      ? `${g.groupName} (−${g.discountPercent}%)`
                      : g.groupName,
                }))}
              />
            </Form.Item>
            <Form.Item name="idNumber" label={t('fields.idNumber')}>
              <Input placeholder={t('placeholders.idNumber')} />
            </Form.Item>
            <Form.Item
              name="addressLine"
              label={t('fields.addressLine')}
              style={{ gridColumn: '1 / -1' }}
            >
              <Input.TextArea rows={2} placeholder={t('placeholders.addressLine')} />
            </Form.Item>
            <Form.Item name="emergencyContactName" label={t('fields.emergencyContactName')}>
              <Input placeholder={t('placeholders.emergencyContactName')} />
            </Form.Item>
            <Form.Item name="emergencyContactPhone" label={t('fields.emergencyContactPhone')}>
              <Input placeholder={t('placeholders.emergencyContactPhone')} />
            </Form.Item>
            <Form.Item
              name="clinicalNotes"
              label={t('fields.clinicalNotes')}
              extra={t('hints.clinicalNotes')}
              style={{ gridColumn: '1 / -1' }}
            >
              <Input.TextArea rows={3} placeholder={t('placeholders.clinicalNotes')} />
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
                    style={{ gridColumn: '1 / -1' }}
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

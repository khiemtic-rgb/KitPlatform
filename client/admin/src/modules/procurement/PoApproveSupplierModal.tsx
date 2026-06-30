import { useEffect, useState } from 'react';
import { Trans, useTranslation } from 'react-i18next';
import { Form, Modal, Select } from 'antd';
import type { Supplier } from '@/shared/api/procurement.types';
import { isPlaceholderSupplier, realSuppliers } from '@/modules/procurement/grn-pricing';

export function PoApproveSupplierModal({
  open,
  poNumber,
  suppliers,
  loading,
  onCancel,
  onConfirm,
}: {
  open: boolean;
  poNumber: string;
  suppliers: Supplier[];
  loading?: boolean;
  onCancel: () => void;
  onConfirm: (supplierId: string) => void;
}) {
  const { t } = useTranslation('procurement', { keyPrefix: 'purchaseOrders.approveModal' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const [form] = Form.useForm<{ supplierId: string }>();
  const [submitting, setSubmitting] = useState(false);
  const options = realSuppliers(suppliers);

  useEffect(() => {
    if (!open) {
      form.resetFields();
      return;
    }
    const first = options[0];
    if (first) form.setFieldsValue({ supplierId: first.id });
  }, [open, form, options]);

  return (
    <Modal
      title={t('title', { poNumber })}
      open={open}
      okText={t('okText')}
      cancelText={tCommon('cancel')}
      confirmLoading={loading || submitting}
      onCancel={onCancel}
      onOk={() => {
        void form.validateFields().then(async (values) => {
          const picked = suppliers.find((s) => s.id === values.supplierId);
          if (!picked || isPlaceholderSupplier(picked)) return;
          setSubmitting(true);
          try {
            onConfirm(values.supplierId);
          } finally {
            setSubmitting(false);
          }
        });
      }}
    >
      <p style={{ marginTop: 0 }}>
        <Trans
          t={t}
          i18nKey="body"
          values={{ supplier: tShared('placeholderSupplierName') }}
          components={{ strong: <strong /> }}
        />
      </p>
      <Form form={form} layout="vertical">
        <Form.Item
          name="supplierId"
          label={tShared('filters.supplier')}
          rules={[{ required: true, message: tVal('selectSupplier') }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            options={options.map((s) => ({
              value: s.id,
              label: `${s.supplierCode} — ${s.supplierName}`,
            }))}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}

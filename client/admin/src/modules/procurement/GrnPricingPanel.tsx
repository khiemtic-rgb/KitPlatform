import { useTranslation } from 'react-i18next';
import { Form, Input, InputNumber, Select, Space, Typography } from 'antd';
import type { FormInstance } from 'antd';
import type { ProcurementVatTreatment } from '@/shared/api/procurement.types';
import {
  computeGrnPricing,
  PROCUREMENT_DISCOUNT_TYPES,
  type GrnLinePricingLike,
  type ProcurementDiscountType,
} from '@/modules/procurement/grn-pricing';
import { GrnTaxSummaryContent, PROCUREMENT_MONEY_COL_WIDTH } from '@/modules/procurement/GrnPoTaxSummary';
import { formatVatTreatmentOptionLabel } from '@/modules/procurement/po-vat';
import { formatDisplayMoney } from '@/shared/utils/money';

/** Cột nút xóa dòng — chừa padding phải cho tổng khớp cột Thành tiền */
export const PROCUREMENT_LINE_ACTION_COL_WIDTH = 40;

interface GrnLineLike extends GrnLinePricingLike {}

export function GrnPricingControls({
  vatTreatments,
}: {
  vatTreatments: ProcurementVatTreatment[];
}) {
  const { t } = useTranslation('procurement', { keyPrefix: 'goodsReceipts' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tVal } = useTranslation('procurement', { keyPrefix: 'shared.validation' });
  const orderDiscountType = Form.useWatch('orderDiscountType') as ProcurementDiscountType | undefined;

  return (
    <div
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: 8,
        alignItems: 'flex-end',
        marginTop: 6,
        width: '100%',
      }}
    >
      <Form.Item
        name="notes"
        label={tShared('columns.notes')}
        style={{ flex: '1 1 220px', marginBottom: 0, minWidth: 180 }}
      >
        <Input placeholder={t('notesOptional')} allowClear />
      </Form.Item>
      <Form.Item
        name="vatTreatmentId"
        label={tShared('tax.vatLabel')}
        rules={[{ required: true, message: tVal('selectTax') }]}
        style={{ marginBottom: 0, width: 180 }}
      >
        <Select
          style={{ width: '100%' }}
          options={vatTreatments.map((item) => ({
            value: item.id,
            label: formatVatTreatmentOptionLabel(item),
          }))}
        />
      </Form.Item>
      <Form.Item name="orderDiscountType" label={tShared('columns.orderDiscount')} style={{ marginBottom: 0, width: 88 }}>
        <Select
          allowClear
          placeholder={tShared('discount.typePlaceholder')}
          options={[
            { value: PROCUREMENT_DISCOUNT_TYPES.Percent, label: tShared('discount.percentSymbol') },
            { value: PROCUREMENT_DISCOUNT_TYPES.Fixed, label: tShared('discount.moneySymbol') },
          ]}
        />
      </Form.Item>
      <Form.Item name="orderDiscountValue" label={tShared('discount.value')} style={{ marginBottom: 0, width: 100 }}>
        <InputNumber
          min={0}
          disabled={!orderDiscountType}
          style={{ width: '100%' }}
          placeholder={
            orderDiscountType === PROCUREMENT_DISCOUNT_TYPES.Percent
              ? tShared('discount.percentSymbol')
              : tShared('discount.moneySymbol')
          }
        />
      </Form.Item>
    </div>
  );
}

export function GrnLineDiscountFields({ fieldName }: { fieldName: number }) {
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const discountType = Form.useWatch(['items', fieldName, 'discountType']) as ProcurementDiscountType | undefined;

  return (
    <Space size={4}>
      <Form.Item name={[fieldName, 'discountType']} style={{ marginBottom: 0, width: 68 }}>
        <Select
          allowClear
          placeholder={tShared('columns.discount')}
          size="small"
          options={[
            { value: PROCUREMENT_DISCOUNT_TYPES.Percent, label: tShared('discount.percentSymbol') },
            { value: PROCUREMENT_DISCOUNT_TYPES.Fixed, label: tShared('discount.moneySymbol') },
          ]}
        />
      </Form.Item>
      <Form.Item name={[fieldName, 'discountValue']} style={{ marginBottom: 0, width: 76 }}>
        <InputNumber min={0} size="small" disabled={!discountType} style={{ width: '100%' }} placeholder="0" />
      </Form.Item>
    </Space>
  );
}

export function GrnPricingSummaryPanel({
  form,
  vatTreatments,
}: {
  form: FormInstance;
  vatTreatments: ProcurementVatTreatment[];
}) {
  const { t } = useTranslation('procurement', { keyPrefix: 'goodsReceipts' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const items = Form.useWatch('items', form) as GrnLineLike[] | undefined;
  const vatTreatmentId = Form.useWatch('vatTreatmentId', form) as string | undefined;
  const orderDiscountType = Form.useWatch('orderDiscountType', form) as ProcurementDiscountType | undefined;
  const orderDiscountValue = Form.useWatch('orderDiscountValue', form) as number | undefined;
  const vatTreatment = vatTreatments.find((item) => item.id === vatTreatmentId) ?? null;
  const pricing = computeGrnPricing(
    items,
    { discountType: orderDiscountType, discountValue: orderDiscountValue },
    vatTreatment,
  );

  return (
    <div style={{ marginTop: 8 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
        <strong>{t('taxApHintTitle')}.</strong> {t('taxApHint')}
      </Typography.Paragraph>
      <div
        style={{
          display: 'flex',
          justifyContent: 'flex-end',
          paddingRight: PROCUREMENT_LINE_ACTION_COL_WIDTH + 8,
        }}
      >
        <div>
          {pricing.lineDiscountTotal > 0 || pricing.orderDiscountAmount > 0 ? (
            <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', textAlign: 'right' }}>
              {pricing.lineDiscountTotal > 0
                ? tShared('discount.lineDiscountSummary', { amount: formatDisplayMoney(pricing.lineDiscountTotal) })
                : ''}
              {pricing.lineDiscountTotal > 0 && pricing.orderDiscountAmount > 0 ? ' · ' : ''}
              {pricing.orderDiscountAmount > 0
                ? tShared('discount.orderDiscountSummary', { amount: formatDisplayMoney(pricing.orderDiscountAmount) })
                : ''}
            </Typography.Text>
          ) : null}
          <GrnTaxSummaryContent
            subtotal={pricing.merchandiseNet}
            taxAmount={pricing.taxAmount}
            totalAmount={pricing.totalAmount}
            subtotalLabel={tShared('tax.subtotalAfterLineDiscount')}
            moneyColumnWidth={PROCUREMENT_MONEY_COL_WIDTH}
          />
        </div>
      </div>
    </div>
  );
}

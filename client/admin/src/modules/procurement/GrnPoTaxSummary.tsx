import { useTranslation } from 'react-i18next';
import { Form, Table, Typography } from 'antd';
import type { FormInstance } from 'antd';
import type { PurchaseOrderDetail } from '@/shared/api/procurement.types';
import {
  computeGrnTaxFromPo,
  computeGrnTaxTotals,
  type GrnLineCostLike,
} from '@/modules/procurement/grn-po-tax';
import { formatDisplayMoney } from '@/shared/utils/money';

/** Độ rộng cột tiền — đồng bộ giữa dòng hàng và footer tổng */
export const PROCUREMENT_MONEY_COL_WIDTH = 120;

interface GrnLineLike {
  quantity?: number;
  unitCost?: number;
}

interface GrnPoTaxSummaryProps {
  form: FormInstance;
  linkedPo: PurchaseOrderDetail;
}

function SummaryRow({
  label,
  value,
  strong,
  moneyColumnWidth,
}: {
  label: string;
  value: string;
  strong?: boolean;
  moneyColumnWidth?: number;
}) {
  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'flex-end',
        alignItems: 'baseline',
        gap: 10,
        lineHeight: 1.3,
        marginBottom: strong ? 0 : 2,
      }}
    >
      <Typography.Text type={strong ? undefined : 'secondary'} strong={strong} style={{ whiteSpace: 'nowrap' }}>
        {label}
      </Typography.Text>
      <Typography.Text
        strong={strong}
        style={{
          width: moneyColumnWidth,
          textAlign: 'right',
          fontVariantNumeric: 'tabular-nums',
          flexShrink: 0,
        }}
      >
        {value}
      </Typography.Text>
    </div>
  );
}

export function GrnTaxSummaryContent({
  subtotal,
  taxAmount,
  totalAmount,
  hint,
  subtotalLabel,
  totalLabel,
  moneyColumnWidth = PROCUREMENT_MONEY_COL_WIDTH,
}: {
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  hint?: string;
  subtotalLabel?: string;
  totalLabel?: string;
  moneyColumnWidth?: number;
}) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared.tax' });
  const resolvedSubtotalLabel = subtotalLabel ?? t('subtotal');
  const resolvedTotalLabel = totalLabel ?? t('total');
  const rowProps = { moneyColumnWidth };

  return (
    <>
      {hint ? (
        <Typography.Text
          type="secondary"
          style={{ fontSize: 11, display: 'block', marginBottom: 4, textAlign: 'right' }}
        >
          {hint}
        </Typography.Text>
      ) : null}
      <div style={{ width: '100%' }}>
        <SummaryRow label={resolvedSubtotalLabel} value={formatDisplayMoney(subtotal)} {...rowProps} />
        <SummaryRow label={t('vatLabel')} value={formatDisplayMoney(taxAmount)} {...rowProps} />
        <div style={{ borderTop: '1px solid #e8e8e8', margin: '4px 0' }} />
        <SummaryRow label={resolvedTotalLabel} value={formatDisplayMoney(totalAmount)} strong {...rowProps} />
      </div>
    </>
  );
}

interface GrnReadonlyTaxSummaryFooterProps {
  items: GrnLineCostLike[] | undefined;
  linkedPo?: PurchaseOrderDetail | null;
  leadingColSpan: number;
  summaryColSpan?: number;
  moneyColumnWidth?: number;
}

/** Footer tổng tiền — dùng trên bảng xem phiếu (read-only) */
export function GrnReadonlyTaxSummaryFooter({
  items,
  linkedPo,
  leadingColSpan,
  summaryColSpan = 2,
  moneyColumnWidth = PROCUREMENT_MONEY_COL_WIDTH,
}: GrnReadonlyTaxSummaryFooterProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { subtotal, taxAmount, totalAmount } = computeGrnTaxTotals(items, linkedPo);
  const hint = linkedPo
    ? `${linkedPo.poNumber} · ${linkedPo.vatTreatmentName} · ${t('readOnlyHint')}`
    : undefined;

  return (
    <Table.Summary>
      <Table.Summary.Row>
        <Table.Summary.Cell index={0} colSpan={leadingColSpan} />
        <Table.Summary.Cell index={leadingColSpan} colSpan={summaryColSpan} align="right">
          <GrnTaxSummaryContent
            subtotal={subtotal}
            taxAmount={taxAmount}
            totalAmount={totalAmount}
            hint={hint}
            moneyColumnWidth={moneyColumnWidth}
          />
        </Table.Summary.Cell>
      </Table.Summary.Row>
    </Table.Summary>
  );
}

/** Footer tổng tiền PO — dùng trên bảng xem đơn (read-only) */
export function PoReadonlyTaxSummaryFooter({
  subtotal,
  taxAmount,
  totalAmount,
  vatTreatmentName,
  poNumber,
  leadingColSpan,
  summaryColSpan = 2,
  moneyColumnWidth = PROCUREMENT_MONEY_COL_WIDTH,
}: {
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  vatTreatmentName?: string;
  poNumber?: string;
  leadingColSpan: number;
  summaryColSpan?: number;
  moneyColumnWidth?: number;
}) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared' });
  const hintParts = [poNumber, vatTreatmentName, t('readOnlyHint')].filter(Boolean);

  return (
    <Table.Summary>
      <Table.Summary.Row>
        <Table.Summary.Cell index={0} colSpan={leadingColSpan} />
        <Table.Summary.Cell index={leadingColSpan} colSpan={summaryColSpan} align="right">
          <GrnTaxSummaryContent
            subtotal={subtotal}
            taxAmount={taxAmount}
            totalAmount={totalAmount}
            hint={hintParts.length ? hintParts.join(' · ') : undefined}
            subtotalLabel={t('tax.provisional')}
            totalLabel={t('tax.totalShort')}
            moneyColumnWidth={moneyColumnWidth}
          />
        </Table.Summary.Cell>
      </Table.Summary.Row>
    </Table.Summary>
  );
}

/** Footer tổng tiền — gắn vào Table.Summary để sát danh sách hàng */
export function GrnPoTaxSummaryTableFooter({ form, linkedPo }: GrnPoTaxSummaryProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared' });
  const items = Form.useWatch('items', form) as GrnLineLike[] | undefined;
  const { subtotal, taxAmount, totalAmount } = computeGrnTaxFromPo(linkedPo, items);

  return (
    <Table.Summary.Row>
      <Table.Summary.Cell index={0} colSpan={6} />
      <Table.Summary.Cell index={6} colSpan={2} align="right">
        <GrnTaxSummaryContent
          subtotal={subtotal}
          taxAmount={taxAmount}
          totalAmount={totalAmount}
          hint={`${linkedPo.poNumber} · ${linkedPo.vatTreatmentName} · ${t('readOnlyHint')}`}
          moneyColumnWidth={PROCUREMENT_MONEY_COL_WIDTH}
        />
      </Table.Summary.Cell>
      <Table.Summary.Cell index={8} colSpan={1} />
    </Table.Summary.Row>
  );
}

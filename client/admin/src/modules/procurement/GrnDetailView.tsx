import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { GoodsReceiptDetail, GoodsReceiptItem } from '@/shared/api/procurement.types';
import { GRN_STATUS_TAG } from '@/shared/api/procurement.types';
import {
  computeGrnPricing,
  formatGrnDiscountDisplay,
  formatGrnLineDiscountCompact,
  formatGrnVatLabel,
  grnLineNetTotal,
  type GrnLinePricingLike,
  type ProcurementDiscountType,
} from '@/modules/procurement/grn-pricing';
import { GrnTaxSummaryContent } from '@/modules/procurement/GrnPoTaxSummary';
import { procurementT } from '@/shared/i18n';
import { useProcurementEnums } from '@/shared/i18n/use-procurement-enums';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';
import { procurementQuantityColumn } from '@/modules/procurement/procurement-quantity-cell';

const DETAIL_MONEY_COL_WIDTH = 88;
const DETAIL_CK_COL_WIDTH = 56;

const moneyCellStyle = {
  fontVariantNumeric: 'tabular-nums' as const,
  whiteSpace: 'nowrap' as const,
  display: 'block',
  textAlign: 'right' as const,
};

function toPricingLine(line: GoodsReceiptItem): GrnLinePricingLike {
  return {
    quantity: line.quantity,
    unitCost: line.unitCost,
    discountType: line.discountType as ProcurementDiscountType | undefined,
    discountValue: line.discountValue,
  };
}

function resolveGrnPricing(detail: GoodsReceiptDetail) {
  const itemNetTotal = detail.items.reduce(
    (sum, line) => sum + (line.lineTotal ?? grnLineNetTotal(toPricingLine(line))),
    0,
  );
  const hasStoredTotals = (detail.totalAmount ?? 0) > 0 || (detail.merchandiseNet ?? 0) > 0;

  if (hasStoredTotals) {
    return {
      lineDiscountTotal: detail.lineDiscountTotal ?? 0,
      orderDiscountAmount: detail.orderDiscountAmount ?? 0,
      merchandiseNet: detail.merchandiseNet ?? itemNetTotal,
      taxAmount: detail.taxAmount ?? 0,
      totalAmount: detail.totalAmount ?? itemNetTotal,
    };
  }

  return computeGrnPricing(
    detail.items.map(toPricingLine),
    {
      discountType: detail.orderDiscountType as ProcurementDiscountType | undefined,
      discountValue: detail.orderDiscountValue,
    },
    {
      isNotSubject: detail.vatIsNotSubject ?? false,
      ratePercent: detail.taxRatePercent ?? 0,
    },
  );
}

function showInventoryUnitCost(detail: GoodsReceiptDetail): boolean {
  return detail.status === 2 || detail.items.some((line) => (line.inventoryUnitCost ?? 0) > 0);
}

function metaCell(label: string, value: ReactNode, note = false) {
  return (
    <div className={`grn-detail-meta__cell${note ? ' grn-detail-meta__cell--note' : ''}`}>
      <span className="grn-detail-meta__label">{label}</span>
      <span className="grn-detail-meta__value">{value}</span>
    </div>
  );
}

export function buildGrnDetailLineColumns(detail: GoodsReceiptDetail): ColumnsType<GoodsReceiptItem> {
  const t = procurementT();
  const emDash = t('shared.emDash');
  const inventoryCostVisible = showInventoryUnitCost(detail);

  const columns: ColumnsType<GoodsReceiptItem> = [
    {
      title: t('shared.columns.product'),
      ellipsis: true,
      render: (_, row) => (
        <div
          style={{ lineHeight: 1.25, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
          title={`${row.productCode} — ${row.productName}`}
        >
          <strong>{row.productCode}</strong>
          <span style={{ color: '#888' }}> — {row.productName}</span>
        </div>
      ),
    },
    {
      title: t('shared.columns.unit'),
      dataIndex: 'unitName',
      width: 48,
      className: 'grn-col-nowrap',
      render: (v: string) => v ?? emDash,
    },
    {
      title: t('shared.columns.batchShort'),
      dataIndex: 'batchNumber',
      width: 52,
      className: 'grn-col-nowrap',
      ellipsis: true,
    },
    {
      title: t('shared.columns.expiryFull'),
      dataIndex: 'expiryDate',
      width: 86,
      className: 'grn-col-nowrap',
      render: (v: string) => formatDisplayDate(v),
    },
    procurementQuantityColumn(t('shared.columns.qty'), 'quantity', 50),
    {
      title: t('shared.columns.discount'),
      width: DETAIL_CK_COL_WIDTH,
      align: 'right',
      render: (_, row) => {
        const text = formatGrnLineDiscountCompact(
          row.discountType as ProcurementDiscountType | undefined,
          row.discountValue,
          row.discountAmount,
        );
        return (
          <span className="grn-discount-cell" title={text === emDash ? undefined : text}>
            {text}
          </span>
        );
      },
    },
    {
      title: t('shared.columns.importPrice'),
      dataIndex: 'unitCost',
      width: DETAIL_MONEY_COL_WIDTH,
      align: 'right',
      render: (v: number) => <span style={moneyCellStyle}>{formatDisplayMoney(v)}</span>,
    },
    {
      title: t('shared.columns.lineTotal'),
      width: DETAIL_MONEY_COL_WIDTH,
      align: 'right',
      render: (_, row) => (
        <span style={moneyCellStyle}>{formatDisplayMoney(row.lineTotal ?? grnLineNetTotal(toPricingLine(row)))}</span>
      ),
    },
  ];

  if (inventoryCostVisible) {
    columns.push({
      title: t('shared.columns.inventoryUnitCost'),
      dataIndex: 'inventoryUnitCost',
      width: DETAIL_MONEY_COL_WIDTH,
      align: 'right',
      render: (v: number | undefined) => (
        <span style={moneyCellStyle} title={t('shared.columns.inventoryUnitCostTooltip')}>
          {v != null && v > 0 ? formatDisplayMoney(v) : emDash}
        </span>
      ),
    });
  }

  return columns;
}

export function GrnDetailHeader({ detail }: { detail: GoodsReceiptDetail }) {
  const { t } = useTranslation('procurement', { keyPrefix: 'goodsReceipts.detailMeta' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { grnStatusLabel } = useProcurementEnums();
  const note = detail.notes?.trim();

  return (
    <div className="grn-detail-meta">
      {metaCell(tShared('columns.supplierShort'), detail.supplierName)}
      {metaCell(tShared('columns.warehouse'), detail.warehouseName)}
      {metaCell(tShared('columns.poNumber'), detail.poNumber ?? tShared('emDash'))}
      {metaCell(
        t('statusShort'),
        <Tag color={GRN_STATUS_TAG[detail.status] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {grnStatusLabel(detail.status)}
        </Tag>,
      )}
      {metaCell(t('date'), formatDisplayDate(detail.receiptDate))}
      {metaCell(t('tax'), formatGrnVatLabel(detail))}
      {metaCell(
        t('orderDiscount'),
        formatGrnDiscountDisplay(
          detail.orderDiscountType as ProcurementDiscountType | undefined,
          detail.orderDiscountValue,
          detail.orderDiscountAmount,
        ),
      )}
      {metaCell(tShared('columns.notes'), note || tShared('emDash'), true)}
    </div>
  );
}

export function GrnDetailPricingSummary({ detail }: { detail: GoodsReceiptDetail }) {
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const pricing = resolveGrnPricing(detail);

  return (
    <div style={{ marginTop: 6, display: 'flex', justifyContent: 'flex-end', flexShrink: 0 }}>
      <div style={{ minWidth: 252 }}>
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
          moneyColumnWidth={DETAIL_MONEY_COL_WIDTH}
        />
      </div>
    </div>
  );
}

interface GrnDetailLinesPanelProps {
  detail: GoodsReceiptDetail;
  showTitle?: boolean;
  compact?: boolean;
  fill?: boolean;
}

export function GrnDetailLinesPanel({
  detail,
  showTitle = true,
  compact = false,
  fill = false,
}: GrnDetailLinesPanelProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared.lines' });
  const panelClass = [
    'grn-lines-detail-panel',
    compact ? 'grn-lines-detail-panel--compact' : '',
    fill ? 'grn-lines-detail-panel--fill' : '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div className={panelClass}>
      {showTitle ? <p className="grn-lines-detail-panel__title">{t('receiptLinesTitle')}</p> : null}
      <Table
        rowKey="id"
        size="small"
        pagination={false}
        tableLayout="fixed"
        className="grn-lines-table grn-lines-table--detail"
        dataSource={detail.items}
        columns={buildGrnDetailLineColumns(detail)}
      />
      <GrnDetailPricingSummary detail={detail} />
    </div>
  );
}

export function GrnDetailView({ detail }: { detail: GoodsReceiptDetail }) {
  return (
    <div className="grn-detail-view">
      <GrnDetailHeader detail={detail} />
      <GrnDetailLinesPanel detail={detail} compact fill />
    </div>
  );
}

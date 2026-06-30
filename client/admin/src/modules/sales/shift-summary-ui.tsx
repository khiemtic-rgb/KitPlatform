import { Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { SalesShiftSummary } from '@/shared/api/sales.types';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { PosSummaryPanel, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import { formatDisplayMoney } from '@/shared/utils/money';

type Props = {
  summary: SalesShiftSummary;
  loading?: boolean;
  showCashReconciliation?: boolean;
};

export function ShiftSummaryPanel({ summary, loading, showCashReconciliation }: Props) {
  const { t } = useTranslation('sales', { keyPrefix: 'shiftSummary' });
  const { paymentMethodLabel } = useSalesEnums();

  const columns: ColumnsType<SalesShiftSummary['byMethod'][number]> = useMemo(
    () => [
      {
        title: t('paymentMethod'),
        dataIndex: 'paymentMethod',
        render: (m: number) => paymentMethodLabel(m),
      },
      {
        title: t('salesAmount'),
        dataIndex: 'salesAmount',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('refundAmount'),
        dataIndex: 'refundAmount',
        align: 'right',
        render: (v: number) => (v > 0 ? `−${formatDisplayMoney(v)}` : formatDisplayMoney(0)),
      },
      {
        title: t('netAmount'),
        dataIndex: 'netAmount',
        align: 'right',
        render: (v: number) => <Typography.Text strong>{formatDisplayMoney(v)}</Typography.Text>,
      },
    ],
    [paymentMethodLabel, t],
  );

  return (
    <>
      <PosSummaryPanel>
        <PosSummaryRow label={t('totalSales')} value={formatDisplayMoney(summary.totalSales)} />
        <PosSummaryRow
          label={t('totalRefunds')}
          value={`−${formatDisplayMoney(summary.totalRefunds)}`}
          danger
        />
        <PosSummaryRow label={t('netTotal')} value={formatDisplayMoney(summary.netTotal)} strong />
        {showCashReconciliation && (
          <>
            <PosSummaryRow label={t('openingCash')} value={formatDisplayMoney(summary.openingCash ?? 0)} />
            <PosSummaryRow label={t('cashSales')} value={formatDisplayMoney(summary.cashSales ?? 0)} />
            <PosSummaryRow
              label={t('cashRefunds')}
              value={`−${formatDisplayMoney(summary.cashRefunds ?? 0)}`}
              danger
            />
            <PosSummaryRow
              label={t('expectedCash')}
              value={formatDisplayMoney(summary.expectedCash ?? 0)}
              strong
            />
            {summary.closingCash != null && (
              <PosSummaryRow label={t('closingCash')} value={formatDisplayMoney(summary.closingCash)} />
            )}
            {summary.cashVariance != null && (
              <PosSummaryRow
                label={t('cashVariance')}
                value={formatDisplayMoney(summary.cashVariance)}
                danger={Math.abs(summary.cashVariance) > 0.009}
                strong
              />
            )}
          </>
        )}
      </PosSummaryPanel>

      <Table
        rowKey="paymentMethod"
        size="small"
        pagination={false}
        loading={loading}
        dataSource={summary.byMethod}
        columns={columns}
        style={{ marginTop: 16 }}
      />
    </>
  );
}

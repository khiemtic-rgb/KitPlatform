import { Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { SalesShiftSummary } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHOD_LABELS } from '@/shared/api/sales.types';
import { PosSummaryPanel, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import { formatDisplayMoney } from '@/shared/utils/money';

type Props = {
  summary: SalesShiftSummary;
  loading?: boolean;
  showCashReconciliation?: boolean;
};

export function ShiftSummaryPanel({ summary, loading, showCashReconciliation }: Props) {
  const columns: ColumnsType<SalesShiftSummary['byMethod'][number]> = [
    {
      title: 'Hình thức',
      dataIndex: 'paymentMethod',
      render: (m: number) => SALES_PAYMENT_METHOD_LABELS[m] ?? m,
    },
    {
      title: 'Thu bán',
      dataIndex: 'salesAmount',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: 'Hoàn khách',
      dataIndex: 'refundAmount',
      align: 'right',
      render: (v: number) => (v > 0 ? `−${formatDisplayMoney(v)}` : formatDisplayMoney(0)),
    },
    {
      title: 'Thu ròng',
      dataIndex: 'netAmount',
      align: 'right',
      render: (v: number) => <Typography.Text strong>{formatDisplayMoney(v)}</Typography.Text>,
    },
  ];

  return (
    <>
      <PosSummaryPanel>
        <PosSummaryRow label="Tổng thu bán" value={formatDisplayMoney(summary.totalSales)} />
        <PosSummaryRow
          label="Tổng hoàn khách"
          value={`−${formatDisplayMoney(summary.totalRefunds)}`}
          danger
        />
        <PosSummaryRow label="Thu ròng" value={formatDisplayMoney(summary.netTotal)} strong />
        {showCashReconciliation && (
          <>
            <PosSummaryRow label="Quỹ đầu ca (TM)" value={formatDisplayMoney(summary.openingCash ?? 0)} />
            <PosSummaryRow label="Thu tiền mặt" value={formatDisplayMoney(summary.cashSales ?? 0)} />
            <PosSummaryRow
              label="Hoàn tiền mặt"
              value={`−${formatDisplayMoney(summary.cashRefunds ?? 0)}`}
              danger
            />
            <PosSummaryRow
              label="Tiền mặt dự kiến trong két"
              value={formatDisplayMoney(summary.expectedCash ?? 0)}
              strong
            />
            {summary.closingCash != null && (
              <PosSummaryRow label="Tiền đếm cuối ca" value={formatDisplayMoney(summary.closingCash)} />
            )}
            {summary.cashVariance != null && (
              <PosSummaryRow
                label="Chênh lệch"
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

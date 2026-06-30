import { Button, Descriptions, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { SalesOrderDetail } from '@/shared/api/sales.types';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { PosSummaryPanel, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import {
  buildOrderNetPaymentLines,
  formatNetPaymentTotal,
  type NetPaymentLine,
} from '@/modules/sales/sales-payment-summary';
import { formatDisplayMoney } from '@/shared/utils/money';
import { resolveOrderPaymentSummary } from '@/modules/sales/sales-order-payment-summary';

type Props = {
  order: SalesOrderDetail;
  onCollectDebt?: () => void;
};

export function OrderDetailFinancials({ order, onCollectDebt }: Props) {
  const { t } = useTranslation('sales');
  const { t: ti } = useTranslation('sales', { keyPrefix: 'receipt.invoice' });
  const { t: tf } = useTranslation('sales', { keyPrefix: 'orderDetail.financials' });
  const { paymentMethodLabel } = useSalesEnums();

  const lineDiscountTotal =
    order.lineDiscountTotal ??
    order.items.reduce((sum, line) => sum + (line.discountAmount ?? 0), 0);
  const totalRefunded = order.totalRefunded ?? 0;
  const { amountPaid, outstanding, hasOutstanding } = resolveOrderPaymentSummary(order);
  const { lines: netPayments, refundInferred } = buildOrderNetPaymentLines(order);
  const hasReturns = order.items.some((line) => (line.returnedQuantity ?? 0) > 0);

  const paymentColumns: ColumnsType<NetPaymentLine> = useMemo(
    () => [
      {
        title: tf('paymentMethod'),
        dataIndex: 'paymentMethod',
        render: (m: number) => paymentMethodLabel(m),
      },
      {
        title: tf('collected'),
        dataIndex: 'collected',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: tf('refunded'),
        dataIndex: 'refunded',
        align: 'right',
        render: (v: number) => (v > 0 ? `−${formatDisplayMoney(v)}` : '—'),
      },
      {
        title: tf('net'),
        dataIndex: 'net',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
    ],
    [paymentMethodLabel, tf],
  );

  return (
    <>
      <PosSummaryPanel>
        <PosSummaryRow label={ti('subtotal')} value={formatDisplayMoney(order.subtotal)} />
        {lineDiscountTotal > 0 && (
          <PosSummaryRow
            label={tf('lineDiscount')}
            value={`−${formatDisplayMoney(lineDiscountTotal)}`}
            danger
          />
        )}
        {order.discountAmount > 0 && (
          <PosSummaryRow
            label={tf('orderDiscount')}
            value={`−${formatDisplayMoney(order.discountAmount)}`}
            danger
          />
        )}
        {(order.voucherDiscountAmount ?? 0) > 0 && (
          <PosSummaryRow
            label={
              order.voucherCode
                ? t('receipt.voucherCode', { code: order.voucherCode })
                : order.voucherName
                  ? t('receipt.voucherNamed', { code: order.voucherCode ?? '', name: order.voucherName })
                  : t('receipt.voucher')
            }
            value={`−${formatDisplayMoney(order.voucherDiscountAmount ?? 0)}`}
            danger
          />
        )}
        {(order.loyaltyDiscountAmount ?? 0) > 0 && (
          <PosSummaryRow
            label={tf('pointsRedeem', {
              points: (order.loyaltyPointsRedeemed ?? 0).toLocaleString(),
            })}
            value={`−${formatDisplayMoney(order.loyaltyDiscountAmount ?? 0)}`}
            danger
          />
        )}
        <PosSummaryRow label={tf('customerDue')} value={formatDisplayMoney(order.totalAmount)} strong />
        {hasOutstanding ? (
          <>
            <PosSummaryRow label={ti('paid')} value={formatDisplayMoney(amountPaid)} />
            <PosSummaryRow label={ti('outstanding')} value={formatDisplayMoney(outstanding)} strong />
            {onCollectDebt ? (
              <div style={{ marginTop: 4, marginBottom: 4 }}>
                <Button type="link" size="small" style={{ padding: 0 }} onClick={onCollectDebt}>
                  {tf('collectThisOrder')}
                </Button>
              </div>
            ) : null}
          </>
        ) : null}
        {totalRefunded > 0 && (
          <PosSummaryRow
            label={tf('refundTotal')}
            value={`−${formatDisplayMoney(totalRefunded)}`}
            danger
          />
        )}
        {order.items.some((line) => (line.returnedQuantity ?? 0) > 0) && (
          <PosSummaryRow
            label={tf('remainingGoods')}
            value={formatDisplayMoney(Math.max(0, order.totalAmount - totalRefunded))}
            strong
          />
        )}
      </PosSummaryPanel>

      {netPayments.length > 0 && (
        <div style={{ marginTop: 12, marginBottom: 12 }}>
          <Typography.Text strong>
            {hasReturns ? tf('paymentAfterReturn') : tf('payment')}
          </Typography.Text>
          {hasOutstanding ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4, marginBottom: 0, fontSize: 12 }}>
              {tf('outstandingNote')}
            </Typography.Paragraph>
          ) : null}
          {refundInferred && (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4, marginBottom: 0, fontSize: 12 }}>
              {tf('refundInferredNote')}
            </Typography.Paragraph>
          )}
          <Table
            rowKey={(row) => String(row.paymentMethod)}
            size="small"
            pagination={false}
            dataSource={netPayments}
            columns={paymentColumns}
            style={{ marginTop: 8 }}
            summary={() => {
              if (hasReturns) {
                return (
                  <Table.Summary.Row>
                    <Table.Summary.Cell index={0}>
                      <Typography.Text strong>{tf('netCollectedTotal')}</Typography.Text>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={1} />
                    <Table.Summary.Cell index={2} />
                    <Table.Summary.Cell index={3} align="right">
                      <Typography.Text strong>{formatNetPaymentTotal(netPayments)}</Typography.Text>
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                );
              }
              if (!hasOutstanding) return null;
              return (
                <>
                  <Table.Summary.Row>
                    <Table.Summary.Cell index={0}>
                      <Typography.Text type="secondary">{tf('collectedCashTransfer')}</Typography.Text>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={1} align="right">
                      {formatDisplayMoney(amountPaid)}
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={2} />
                    <Table.Summary.Cell index={3} align="right">
                      {formatDisplayMoney(amountPaid)}
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                  <Table.Summary.Row>
                    <Table.Summary.Cell index={0}>
                      <Typography.Text strong>{ti('outstanding')}</Typography.Text>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={1} align="right">
                      <Typography.Text strong>{formatDisplayMoney(outstanding)}</Typography.Text>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={2} />
                    <Table.Summary.Cell index={3} align="right">
                      <Typography.Text strong>{formatDisplayMoney(outstanding)}</Typography.Text>
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                </>
              );
            }}
          />
        </div>
      )}

      {order.notes && (
        <Descriptions size="small" column={1} style={{ marginBottom: 12 }}>
          <Descriptions.Item label={ti('notes')}>{order.notes}</Descriptions.Item>
        </Descriptions>
      )}
    </>
  );
}

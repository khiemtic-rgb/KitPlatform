import { Descriptions, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { SalesOrderDetail } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHOD_LABELS } from '@/shared/api/sales.types';
import { PosSummaryPanel, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import {
  buildOrderNetPaymentLines,
  formatNetPaymentTotal,
  type NetPaymentLine,
} from '@/modules/sales/sales-payment-summary';
import { formatDisplayMoney } from '@/shared/utils/money';

type Props = {
  order: SalesOrderDetail;
};

export function OrderDetailFinancials({ order }: Props) {
  const lineDiscountTotal =
    order.lineDiscountTotal ??
    order.items.reduce((sum, line) => sum + (line.discountAmount ?? 0), 0);
  const totalRefunded = order.totalRefunded ?? 0;
  const { lines: netPayments, refundInferred } = buildOrderNetPaymentLines(order);
  const hasReturns = order.items.some((line) => (line.returnedQuantity ?? 0) > 0);

  const paymentColumns: ColumnsType<NetPaymentLine> = [
    {
      title: 'Hình thức',
      dataIndex: 'paymentMethod',
      render: (m: number) => SALES_PAYMENT_METHOD_LABELS[m] ?? m,
    },
    {
      title: 'Thu',
      dataIndex: 'collected',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: 'Hoàn',
      dataIndex: 'refunded',
      align: 'right',
      render: (v: number) => (v > 0 ? `−${formatDisplayMoney(v)}` : '—'),
    },
    {
      title: 'Ròng',
      dataIndex: 'net',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
  ];

  return (
    <>
      <PosSummaryPanel>
        <PosSummaryRow label="Tổng tiền hàng" value={formatDisplayMoney(order.subtotal)} />
        {lineDiscountTotal > 0 && (
          <PosSummaryRow
            label="Chiết khấu sản phẩm"
            value={`−${formatDisplayMoney(lineDiscountTotal)}`}
            danger
          />
        )}
        {order.discountAmount > 0 && (
          <PosSummaryRow
            label="Chiết khấu đơn hàng"
            value={`−${formatDisplayMoney(order.discountAmount)}`}
            danger
          />
        )}
        <PosSummaryRow label="Khách phải trả" value={formatDisplayMoney(order.totalAmount)} strong />
        {totalRefunded > 0 && (
          <PosSummaryRow
            label="Đã hoàn trả (phiếu hoàn)"
            value={`−${formatDisplayMoney(totalRefunded)}`}
            danger
          />
        )}
        {order.items.some((line) => (line.returnedQuantity ?? 0) > 0) && (
          <PosSummaryRow
            label="Thành tiền hàng còn lại"
            value={formatDisplayMoney(Math.max(0, order.totalAmount - totalRefunded))}
            strong
          />
        )}
      </PosSummaryPanel>

      {netPayments.length > 0 && (
        <div style={{ marginTop: 12, marginBottom: 12 }}>
          <Typography.Text strong>{hasReturns ? 'Thanh toán sau trả hàng' : 'Thanh toán'}</Typography.Text>
          {refundInferred && (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4, marginBottom: 0, fontSize: 12 }}>
              Hoàn tiền phân bổ theo tỷ lệ thu ban đầu — xem phiếu hoàn để biết hình thức hoàn chính xác.
            </Typography.Paragraph>
          )}
          <Table
            rowKey={(row) => String(row.paymentMethod)}
            size="small"
            pagination={false}
            dataSource={netPayments}
            columns={paymentColumns}
            style={{ marginTop: 8 }}
            summary={
              hasReturns
                ? () => (
                    <Table.Summary.Row>
                      <Table.Summary.Cell index={0}>
                        <Typography.Text strong>Tổng đã thu ròng</Typography.Text>
                      </Table.Summary.Cell>
                      <Table.Summary.Cell index={1} />
                      <Table.Summary.Cell index={2} />
                      <Table.Summary.Cell index={3} align="right">
                        <Typography.Text strong>{formatNetPaymentTotal(netPayments)}</Typography.Text>
                      </Table.Summary.Cell>
                    </Table.Summary.Row>
                  )
                : undefined
            }
          />
        </div>
      )}

      {order.notes && (
        <Descriptions size="small" column={1} style={{ marginBottom: 12 }}>
          <Descriptions.Item label="Ghi chú">{order.notes}</Descriptions.Item>
        </Descriptions>
      )}
    </>
  );
}

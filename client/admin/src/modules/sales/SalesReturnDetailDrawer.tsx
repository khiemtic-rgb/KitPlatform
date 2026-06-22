import { useEffect, useState } from 'react';
import { Button, Card, Descriptions, Drawer, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PrinterOutlined } from '@ant-design/icons';
import { fetchSalesReturn } from '@/shared/api/sales.api';
import type { SalesReturnDetail } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHOD_LABELS, SALES_RETURN_STATUS_LABELS } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { printSalesReturn } from '@/modules/sales/sales-return-print';
import { sectionGapStyle, sectionGapTopStyle, TabularMoney } from '@/modules/sales/sales-ui-styles';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

type Props = {
  open: boolean;
  returnId: string | null;
  onClose: () => void;
  onOpenOrder?: (salesOrderId: string) => void;
};

export function SalesReturnDetailDrawer({ open, returnId, onClose, onOpenOrder }: Props) {
  const [detail, setDetail] = useState<SalesReturnDetail | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open || !returnId) {
      setDetail(null);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setDetail(null);
    void fetchSalesReturn(returnId)
      .then((data) => {
        if (!cancelled) setDetail(data);
      })
      .catch((error) => {
        if (!cancelled) {
          message.error(apiErrorMessage(error, 'Không tải được chi tiết phiếu trả'));
          onClose();
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, returnId, onClose]);

  const handlePrint = () => {
    if (!detail) return;
    if (!printSalesReturn(detail)) {
      message.warning('Trình duyệt chặn cửa sổ in — cho phép popup và thử lại.');
    }
  };

  const itemColumns: ColumnsType<SalesReturnDetail['items'][number]> = [
    { title: 'Mã SP', dataIndex: 'productCode', width: 90 },
    { title: 'Tên SP', dataIndex: 'productName' },
    { title: 'Lô', dataIndex: 'batchNumber', width: 90 },
    {
      title: 'SL trả',
      dataIndex: 'quantity',
      width: 80,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
    {
      title: 'Tiền hoàn',
      dataIndex: 'refundAmount',
      width: 110,
      align: 'right',
      render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
    },
  ];

  return (
    <Drawer
      title={detail ? `Phiếu trả ${detail.returnNumber}` : 'Chi tiết phiếu trả'}
      width={640}
      open={open}
      onClose={onClose}
      loading={loading}
    >
      {detail && (
        <>
          <Card size="small" title="Thao tác" style={sectionGapStyle}>
            <Space wrap>
              <Button icon={<PrinterOutlined />} onClick={handlePrint}>
                In phiếu trả
              </Button>
              {onOpenOrder && (
                <Button onClick={() => onOpenOrder(detail.salesOrderId)}>Xem đơn bán</Button>
              )}
            </Space>
          </Card>

          <Descriptions column={2} size="small" bordered style={sectionGapStyle}>
            <Descriptions.Item label="Số phiếu">{detail.returnNumber}</Descriptions.Item>
            <Descriptions.Item label="Đơn bán">{detail.orderNumber}</Descriptions.Item>
            <Descriptions.Item label="Ngày trả">{formatDisplayDate(detail.returnDate)}</Descriptions.Item>
            <Descriptions.Item label="Trạng thái">
              <Tag>{SALES_RETURN_STATUS_LABELS[detail.status] ?? detail.status}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Ca">{detail.shiftNumber ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Lý do trả" span={2}>
              {detail.reason?.trim() ? detail.reason : '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Tổng hoàn tiền" span={2}>
              <strong>{formatDisplayMoney(detail.totalRefund)}</strong>
            </Descriptions.Item>
          </Descriptions>

          <Table
            rowKey="id"
            size="small"
            pagination={false}
            dataSource={detail.items}
            columns={itemColumns}
          />

          {(detail.payments?.length ?? 0) > 0 && (
            <Card size="small" title="Hình thức hoàn tiền" style={sectionGapTopStyle}>
              <Space direction="vertical" size={4}>
                {detail.payments!.map((p, idx) => (
                  <div key={p.id ?? idx}>
                    {SALES_PAYMENT_METHOD_LABELS[p.paymentMethod] ?? p.paymentMethod}:{' '}
                    <strong>{formatDisplayMoney(p.amount)}</strong>
                  </div>
                ))}
              </Space>
            </Card>
          )}
        </>
      )}
    </Drawer>
  );
}

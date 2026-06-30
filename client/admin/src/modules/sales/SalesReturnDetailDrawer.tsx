import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Descriptions, Drawer, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PrinterOutlined } from '@ant-design/icons';
import { fetchSalesReturn } from '@/shared/api/sales.api';
import type { SalesReturnDetail } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
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
  const { t } = useTranslation('sales', { keyPrefix: 'returns.detail' });
  const { paymentMethodLabel, returnStatusLabel } = useSalesEnums();
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
          message.error(apiErrorMessage(error, t('messages.loadFailed')));
          onClose();
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, returnId, onClose, t]);

  const handlePrint = () => {
    if (!detail) return;
    void (async () => {
      if (!(await printSalesReturn(detail))) {
        message.warning(t('messages.printBlocked'));
      }
    })();
  };

  const itemColumns: ColumnsType<SalesReturnDetail['items'][number]> = useMemo(
    () => [
      { title: t('lines.productCode'), dataIndex: 'productCode', width: 90 },
      { title: t('lines.productName'), dataIndex: 'productName' },
      { title: t('lines.batch'), dataIndex: 'batchNumber', width: 90 },
      {
        title: t('lines.qty'),
        dataIndex: 'quantity',
        width: 80,
        align: 'right',
        render: (v: number) => v.toLocaleString(),
      },
      {
        title: t('lines.refundAmount'),
        dataIndex: 'refundAmount',
        width: 110,
        align: 'right',
        render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
      },
    ],
    [t],
  );

  return (
    <Drawer
      title={
        detail
          ? t('drawerTitle', { returnNumber: detail.returnNumber })
          : t('drawerTitleDefault')
      }
      width={640}
      open={open}
      onClose={onClose}
      loading={loading}
    >
      {detail && (
        <>
          <Card size="small" title={t('actions.title')} style={sectionGapStyle}>
            <Space wrap>
              <Button icon={<PrinterOutlined />} onClick={handlePrint}>
                {t('actions.print')}
              </Button>
              {onOpenOrder && (
                <Button onClick={() => onOpenOrder(detail.salesOrderId)}>
                  {t('actions.viewOrder')}
                </Button>
              )}
            </Space>
          </Card>

          <Descriptions column={2} size="small" bordered style={sectionGapStyle}>
            <Descriptions.Item label={t('descriptions.returnNumber')}>
              {detail.returnNumber}
            </Descriptions.Item>
            <Descriptions.Item label={t('descriptions.orderNumber')}>
              {detail.orderNumber}
            </Descriptions.Item>
            <Descriptions.Item label={t('descriptions.returnDate')}>
              {formatDisplayDate(detail.returnDate)}
            </Descriptions.Item>
            <Descriptions.Item label={t('descriptions.status')}>
              <Tag>{returnStatusLabel(detail.status)}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('descriptions.shift')}>
              {detail.shiftNumber ?? '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('descriptions.reason')} span={2}>
              {detail.reason?.trim() ? detail.reason : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('descriptions.refundTotal')} span={2}>
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
            <Card size="small" title={t('paymentsTitle')} style={sectionGapTopStyle}>
              <Space direction="vertical" size={4}>
                {detail.payments!.map((p, idx) => (
                  <div key={p.id ?? idx}>
                    {paymentMethodLabel(p.paymentMethod)}:{' '}
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

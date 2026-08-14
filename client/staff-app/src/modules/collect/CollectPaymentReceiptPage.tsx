import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Typography } from 'antd';
import { PrinterOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useLocation, useNavigate } from 'react-router-dom';
import { fetchReceiptSettings } from '@/shared/api/sales.api';
import type { ReceiptStoreSettings } from '@/shared/api/sales.types';
import {
  SALES_PAYMENT_BANK,
  SALES_PAYMENT_CARD,
  SALES_PAYMENT_CASH,
  SALES_PAYMENT_EWALLET,
} from '@/shared/api/sales.types';
import type { CustomerPaymentReceipt } from '@/shared/api/receivables.api';
import { buildPaymentReceiptHtml, printPaymentReceipt } from '@/modules/collect/payment-receipt-print';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';
import { formatMoney } from '@/shared/utils/money';

function methodLabel(method: number): string {
  if (method === SALES_PAYMENT_CASH) return 'Tiền mặt';
  if (method === SALES_PAYMENT_BANK) return 'Chuyển khoản';
  if (method === SALES_PAYMENT_CARD) return 'Thẻ';
  if (method === SALES_PAYMENT_EWALLET) return 'Ví điện tử';
  return 'Thanh toán';
}

export function CollectPaymentReceiptPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const payment = (location.state as { payment?: CustomerPaymentReceipt } | null)?.payment ?? null;
  const [storeSettings, setStoreSettings] = useState<ReceiptStoreSettings>({ name: 'Nhà thuốc' });

  useEffect(() => {
    if (!payment) navigate('/collect', { replace: true });
  }, [payment, navigate]);

  useEffect(() => {
    void fetchReceiptSettings().then(setStoreSettings);
  }, []);

  const receiptHtml = useMemo(() => {
    if (!payment) return '';
    return buildPaymentReceiptHtml(payment, storeSettings);
  }, [payment, storeSettings]);

  if (!payment) return null;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Phiếu thu"
        subtitle={`${payment.paymentNumber} · đã ghi sổ`}
        backTo="/collect"
      />
      <main className="staff-body collect-receipt-body">
        <Alert
          type="success"
          showIcon
          style={{ marginBottom: 12 }}
          message="Thu công nợ thành công"
          description="Phiếu đã ghi sổ. Có thể in cho khách hoặc thu tiếp khách khác."
        />
        <section className="collect-receipt-summary">
          <Typography.Text strong className="collect-receipt-summary__number">
            {payment.paymentNumber}
          </Typography.Text>
          <div className="collect-receipt-summary__meta">
            {payment.customerName}
            {payment.customerCode ? ` · ${payment.customerCode}` : ''}
          </div>
          <div className="collect-receipt-summary__amount">{formatMoney(payment.amount)}</div>
          <div className="collect-receipt-summary__meta">
            {methodLabel(payment.paymentMethod)}
            {' · '}
            {dayjs(payment.paymentDate).isValid()
              ? dayjs(payment.paymentDate).format('DD/MM/YYYY HH:mm')
              : payment.paymentDate}
            {payment.orderNumber ? ` · ${payment.orderNumber}` : ''}
          </div>
          {payment.notes ? (
            <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
              Ghi chú: {payment.notes}
            </Typography.Text>
          ) : null}
        </section>
        <div className="receipt-preview receipt-print-area collect-receipt-preview">
          <iframe title="payment-receipt" srcDoc={receiptHtml} className="collect-receipt-iframe" />
        </div>
      </main>
      <footer className="staff-footer collect-receipt-footer no-print">
        <Button
          type="primary"
          block
          size="large"
          icon={<PrinterOutlined />}
          onClick={() => printPaymentReceipt(payment, storeSettings)}
        >
          In phiếu thu
        </Button>
        <div className="collect-receipt-footer__row">
          <Button block size="large" onClick={() => navigate('/collect')}>
            Thu tiếp
          </Button>
          <Button block size="large" onClick={() => navigate('/')}>
            Về menu
          </Button>
        </div>
      </footer>
    </div>
  );
}

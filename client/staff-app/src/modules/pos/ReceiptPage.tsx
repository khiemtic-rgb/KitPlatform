import { useEffect, useMemo, useRef, useState } from 'react';
import { App, Button, Input, Select, Space, Typography } from 'antd';
import { CheckCircleOutlined, PrinterOutlined, SaveOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import type { SalesOrderDetail } from '@/shared/api/sales.types';
import { fetchReceiptSettings } from '@/shared/api/sales.api';
import {
  createDispensingNote,
  DISPENSING_NOTE_TYPES,
  fetchDispensingNotes,
  type DispensingNote,
} from '@/shared/api/pharmacy.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { buildReceiptHtml, printReceiptDocument } from '@/modules/sales/receipt-print';
import { formatMoney } from '@/shared/utils/money';
import { usePosHotkeys } from '@/shared/hooks/usePosHotkeys';

type ReceiptLocationState = {
  order?: SalesOrderDetail;
  /** Default true when arriving from checkout. */
  autoPrint?: boolean;
};

export function ReceiptPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const location = useLocation();
  const state = (location.state as ReceiptLocationState | null) ?? {};
  const order = state.order;
  const shouldAutoPrint = state.autoPrint !== false;
  const autoPrintedRef = useRef(false);
  const [storeName, setStoreName] = useState('Nhà thuốc');
  const [settingsReady, setSettingsReady] = useState(false);
  const [notes, setNotes] = useState<DispensingNote[]>([]);
  const [noteType, setNoteType] = useState<string>('counseling');
  const [noteText, setNoteText] = useState('');
  const [savingNote, setSavingNote] = useState(false);

  useEffect(() => {
    if (!order) {
      navigate('/pos', { replace: true });
      return;
    }
    void fetchReceiptSettings()
      .then((s) => setStoreName(s.name))
      .finally(() => setSettingsReady(true));
    void fetchDispensingNotes(order.id)
      .then(setNotes)
      .catch(() => setNotes([]));
  }, [navigate, order]);

  const receiptHtml = useMemo(() => {
    if (!order) return '';
    return buildReceiptHtml(order, { name: storeName });
  }, [order, storeName]);

  useEffect(() => {
    if (!shouldAutoPrint || !settingsReady || !receiptHtml || autoPrintedRef.current) return;
    autoPrintedRef.current = true;
    const timer = window.setTimeout(() => {
      try {
        printReceiptDocument(receiptHtml);
      } catch {
        message.warning('Không tự in được — bấm In bill bên dưới');
      }
    }, 350);
    return () => window.clearTimeout(timer);
  }, [shouldAutoPrint, settingsReady, receiptHtml, message]);

  if (!order) return null;

  const print = () => {
    if (!receiptHtml) return;
    try {
      printReceiptDocument(receiptHtml);
    } catch {
      message.error('Không in được — thử Chia sẻ hoặc kiểm tra máy in Bluetooth');
    }
  };

  const saveNote = async () => {
    const text = noteText.trim();
    if (!text) {
      message.warning('Nhập nội dung ghi chú tư vấn / dispense');
      return;
    }
    setSavingNote(true);
    try {
      const created = await createDispensingNote({
        salesOrderId: order.id,
        customerId: order.customerId,
        noteType,
        noteText: text,
      });
      setNotes((prev) => [created, ...prev]);
      setNoteText('');
      message.success('Đã lưu ghi chú dược');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được ghi chú'));
    } finally {
      setSavingNote(false);
    }
  };

  usePosHotkeys(
    useMemo(
      () => ({
        onPrint: print,
        onNewOrder: () => navigate('/pos', { replace: true }),
        onSaveNote: () => void saveNote(),
      }),
      [receiptHtml, noteText, noteType, order.id],
    ),
  );

  return (
    <div className="staff-shell">
      <main className="staff-body" style={{ paddingBottom: 120 }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div style={{ textAlign: 'center' }}>
            <CheckCircleOutlined style={{ fontSize: 40, color: '#16a34a' }} />
            <Typography.Title level={4} style={{ marginTop: 8 }}>
              Đã bán · {order.orderNumber}
            </Typography.Title>
            <Typography.Text type="secondary">{formatMoney(order.totalAmount)}</Typography.Text>
          </div>

          <div className="checkout-panel">
            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              Ghi chú tư vấn / dispense (GPP)
            </Typography.Text>
            <Space direction="vertical" style={{ width: '100%' }} size="small">
              <Select
                value={noteType}
                options={DISPENSING_NOTE_TYPES.map((o) => ({ value: o.value, label: o.label }))}
                onChange={setNoteType}
                style={{ width: '100%' }}
              />
              <Input.TextArea
                rows={3}
                placeholder="Nội dung tư vấn, liều dùng, lưu ý cho khách…"
                value={noteText}
                onChange={(e) => setNoteText(e.target.value)}
                maxLength={2000}
                showCount
              />
              <Button
                type="default"
                block
                icon={<SaveOutlined />}
                loading={savingNote}
                onClick={() => void saveNote()}
              >
                Lưu ghi chú
              </Button>
              {notes.length > 0 ? (
                <div style={{ marginTop: 8 }}>
                  {notes.map((n) => (
                    <div key={n.id} style={{ fontSize: 12, marginBottom: 6 }}>
                      <Typography.Text type="secondary">
                        {DISPENSING_NOTE_TYPES.find((t) => t.value === n.noteType)?.label ?? n.noteType}
                        {' · '}
                        {new Date(n.createdAt).toLocaleString('vi-VN')}
                      </Typography.Text>
                      <div>{n.noteText}</div>
                    </div>
                  ))}
                </div>
              ) : (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Chưa có ghi chú — khuyến nghị ghi nhận tư vấn thuốc kê đơn / OTC đặc biệt.
                </Typography.Text>
              )}
            </Space>
          </div>

          <div className="receipt-preview receipt-print-area">
            <iframe
              title="receipt-preview"
              srcDoc={receiptHtml}
              style={{ width: '100%', height: 320, border: 'none' }}
            />
          </div>
        </Space>
      </main>

      <footer className="staff-footer no-print">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Button type="primary" block size="large" icon={<PrinterOutlined />} onClick={print}>
            In bill
          </Button>
          <Button block size="large" onClick={() => navigate('/pos', { replace: true })}>
            Đơn mới
          </Button>
        </Space>
      </footer>
    </div>
  );
}

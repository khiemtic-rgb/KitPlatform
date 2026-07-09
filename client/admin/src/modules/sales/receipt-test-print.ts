import { loadReceiptStoreSettings } from '@/modules/sales/receipt-settings';
import {
  buildThermalReceiptDocument,
  openThermalPrintWindow,
} from '@/modules/sales/thermal-receipt-print';

/** Sample 80mm receipt for printer setup (no real order). */
export async function printReceiptTestPage(): Promise<boolean> {
  const settings = await loadReceiptStoreSettings();
  const now = new Date().toLocaleString('vi-VN');
  const bodyHtml = `
      <div class="center store-name">${settings.name}</div>
      ${settings.tagline ? `<div class="center store-sub">${settings.tagline}</div>` : ''}
      ${settings.phone ? `<div class="center store-contact">Tel: ${settings.phone}</div>` : ''}
      ${settings.address ? `<div class="center store-contact">${settings.address}</div>` : ''}
      <div class="center title">PHIEU IN THU</div>
      <div class="meta">IN THU · ${now}</div>
      <div class="rule">${'─'.repeat(32)}</div>
      <div class="row"><span>Paracetamol 500mg</span><span>x2</span></div>
      <div class="row"><span class="row-left">Thanh tien</span><span class="row-right">20.000d</span></div>
      <div class="rule">${'─'.repeat(32)}</div>
      <div class="row total"><span class="row-left">TONG</span><span class="row-right">20.000d</span></div>
      <div class="center thanks" style="margin-top:8px">Cam on quy khach!</div>
      <div class="center" style="font-size:10px;margin-top:6px">Phieu in thu — khong phai hoa don that</div>`;
  const doc = buildThermalReceiptDocument(`${settings.name} — In thu`, bodyHtml);
  return openThermalPrintWindow(doc);
}

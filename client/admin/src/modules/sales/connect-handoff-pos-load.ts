import {
  fetchConnectRxHandoff,
  type ConnectRxHandoff,
  type ConnectRxHandoffLine,
} from '@/shared/api/connect.api';
import { createCustomer, fetchNextCustomerCode } from '@/shared/api/customer-admin.api';
import { lookupPosProduct, searchCustomers, searchPosProducts, type TenantBatchModeValue } from '@/shared/api/sales.api';
import type { CartLine, CustomerListItem, PosProductLookup } from '@/shared/api/sales.types';
import { initialBatchLabelForMode } from '@/modules/sales/pos-batch-mode-ui';

export type ConnectHandoffPosLoadResult = {
  handoff: ConnectRxHandoff;
  cart: CartLine[];
  customer?: CustomerListItem;
  /** true nếu vừa tạo KH trên NT vì chưa có trong CRM nhà thuốc */
  customerCreated?: boolean;
  /** true nếu không khớp / không tạo được KH */
  customerMissing?: boolean;
  unmatched: string[];
  matchedCount: number;
  /** SP khớp nhưng tồn 0 — gợi ý nhập đúng mã hoặc hoàn tất phiếu nhập. */
  zeroStock: string[];
};

function digitsOnly(value?: string | null): string {
  return (value ?? '').replace(/\D/g, '');
}

function normalizeName(value?: string | null): string {
  return (value ?? '').trim().replace(/\s+/g, ' ').toLowerCase();
}

function drugSearchQueries(line: ConnectRxHandoffLine): string[] {
  const raw = `${line.drugName || ''} ${line.strength || ''}`.trim();
  const noParen = raw.replace(/\([^)]*\)/g, ' ').replace(/\s+/g, ' ').trim();
  const tokens = noParen.split(/\s+/).filter(Boolean);
  const queries: string[] = [];
  if (noParen) queries.push(noParen);
  const withoutUnit = noParen
    .replace(/\b(mg|g|ml|mcg|iu|vien|viên|chai|ong|ống|vi)\b/gi, '')
    .replace(/\s+/g, ' ')
    .trim();
  if (withoutUnit && withoutUnit !== noParen) queries.push(withoutUnit);
  if (tokens.length >= 1) queries.push(tokens[0]);
  if (tokens.length >= 2) queries.push(`${tokens[0]} ${tokens[1]}`);
  return [...new Set(queries.filter((q) => q.length >= 2))];
}

function scoreName(query: string, productName: string): number {
  const q = query.toLowerCase();
  const n = productName.toLowerCase();
  if (n === q) return 100;
  if (n.startsWith(q)) return 90;
  if (n.includes(q)) return 70;
  const qParts = q.split(/\s+/);
  const hit = qParts.filter((p) => n.includes(p)).length;
  return hit * 15;
}

/** Ưu tiên SP có tồn kho — tránh khớp RXDEMO / SKU demo tồn 0 khi đã nhập mã khác. */
function rankHits(
  query: string,
  hits: { productName: string; lookupCode: string; stockAvailable: number }[],
) {
  return [...hits].sort((a, b) => {
    const stockA = a.stockAvailable > 0 ? 1 : 0;
    const stockB = b.stockAvailable > 0 ? 1 : 0;
    if (stockA !== stockB) return stockB - stockA;
    const nameDiff = scoreName(query, b.productName) - scoreName(query, a.productName);
    if (nameDiff !== 0) return nameDiff;
    return b.stockAvailable - a.stockAvailable;
  });
}

async function resolveProduct(
  line: ConnectRxHandoffLine,
  warehouseId: string,
): Promise<PosProductLookup | null> {
  let fallback: PosProductLookup | null = null;
  for (const q of drugSearchQueries(line)) {
    const hits = await searchPosProducts(q, warehouseId);
    if (hits.length === 0) continue;
    const ranked = rankHits(q, hits);
    for (const candidate of ranked) {
      if (scoreName(q, candidate.productName) < 30) continue;
      try {
        const looked = await lookupPosProduct(candidate.lookupCode, warehouseId);
        if (looked.stockAvailable > 0) return looked;
        fallback ??= looked;
      } catch {
        continue;
      }
    }
  }
  return fallback;
}

function toListItem(row: {
  id: string;
  customerCode: string;
  fullName: string;
  phone: string;
  email?: string | null;
  allowCredit?: boolean;
  creditLimit?: number | null;
  currentOutstanding?: number;
}): CustomerListItem {
  return {
    id: row.id,
    customerCode: row.customerCode,
    fullName: row.fullName,
    phone: row.phone ?? '',
    email: row.email ?? undefined,
    allowCredit: Boolean(row.allowCredit),
    creditLimit: row.creditLimit ?? undefined,
    currentOutstanding: Number(row.currentOutstanding ?? 0),
  };
}

/**
 * Bệnh nhân trên đơn PK thuộc tenant Clinic — CRM nhà thuốc thường chưa có.
 * Khớp SĐT/tên trên NT; nếu chưa có thì tạo KH nhanh để POS không mất bệnh nhân.
 */
async function resolvePharmacyCustomer(handoff: ConnectRxHandoff): Promise<{
  customer?: CustomerListItem;
  created: boolean;
  missing: boolean;
}> {
  const name = handoff.patientDisplayName?.trim() || '';
  const phoneRaw = handoff.patientPhone?.trim() || '';
  const phoneDigits = digitsOnly(phoneRaw);
  const nameKey = normalizeName(name);

  if (!name && !phoneDigits) {
    return { created: false, missing: true };
  }

  if (phoneDigits.length >= 9) {
    const byPhone = await searchCustomers(phoneDigits);
    const exact =
      byPhone.find((c) => digitsOnly(c.phone) === phoneDigits) ||
      byPhone.find((c) => digitsOnly(c.phone).endsWith(phoneDigits.slice(-9))) ||
      byPhone[0];
    if (exact) return { customer: exact, created: false, missing: false };
  }

  if (nameKey.length >= 2) {
    const byName = await searchCustomers(name);
    const exactName =
      byName.find((c) => normalizeName(c.fullName) === nameKey) ||
      byName.find((c) => normalizeName(c.fullName).includes(nameKey)) ||
      (phoneDigits
        ? byName.find((c) => digitsOnly(c.phone) === phoneDigits)
        : undefined);
    if (exactName) return { customer: exactName, created: false, missing: false };
  }

  // Chưa có trên NT — tạo KH từ snapshot đơn Connect (bắt buộc có tên + SĐT đủ số).
  if (nameKey.length < 2 || phoneDigits.length < 9) {
    return { created: false, missing: true };
  }

  try {
    const code = await fetchNextCustomerCode();
    const created = await createCustomer({
      fullName: name,
      phone: phoneDigits,
      customerCode: code || undefined,
    });
    return {
      customer: toListItem(created),
      created: true,
      missing: false,
    };
  } catch {
    // Có thể trùng SĐT vừa được tạo song song — tìm lại
    const retry = await searchCustomers(phoneDigits);
    const hit =
      retry.find((c) => digitsOnly(c.phone) === phoneDigits) || retry[0];
    if (hit) return { customer: hit, created: false, missing: false };
    return { created: false, missing: true };
  }
}

export async function loadConnectHandoffForPos(
  handoffId: string,
  warehouseId: string,
  batchMode: TenantBatchModeValue = 'suggest',
): Promise<ConnectHandoffPosLoadResult> {
  const handoff = await fetchConnectRxHandoff(handoffId);
  const unmatched: string[] = [];
  const zeroStock: string[] = [];
  const cart: CartLine[] = [];

  for (const line of handoff.lines) {
    const label = `${line.drugName}${line.strength ? ` (${line.strength})` : ''}`;
    const product = await resolveProduct(line, warehouseId);
    if (!product) {
      unmatched.push(label);
      continue;
    }
    const qty = Math.max(1, Number(line.quantity) || 1);
    if (product.stockAvailable <= 0) {
      zeroStock.push(`${product.productCode} — ${product.productName}`);
    }
    const existing = cart.find((c) => c.productUnitId === product.productUnitId);
    if (existing) {
      existing.quantity += qty;
      continue;
    }
    cart.push({
      key: `connect-${handoff.id}-${product.productUnitId}-${cart.length}`,
      productId: product.productId,
      productCode: product.productCode,
      productName: product.productName,
      productUnitId: product.productUnitId,
      unitName: product.unitName,
      quantity: qty,
      unitPrice: product.unitPrice,
      dispensingClass: product.dispensingClass,
      stockAvailable: product.stockAvailable,
      batchHints: product.batchHints,
      batchLabel: initialBatchLabelForMode(batchMode, product.batchHints),
      stockSourceLabel: product.stockSourceLabel,
      qtyWarning:
        product.stockAvailable <= 0
          ? `Tồn 0 — kiểm tra đã «Hoàn tất» phiếu nhập đúng mã ${product.productCode}?`
          : undefined,
      rxLocked: true,
    });
  }

  const resolved = await resolvePharmacyCustomer(handoff);

  return {
    handoff,
    cart,
    customer: resolved.customer,
    customerCreated: resolved.created,
    customerMissing: resolved.missing,
    unmatched,
    matchedCount: cart.length,
    zeroStock,
  };
}

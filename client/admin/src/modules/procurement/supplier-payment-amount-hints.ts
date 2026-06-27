import { sumGrnSubtotal } from '@/modules/procurement/grn-po-tax';
import {
  fetchGoodsReceipt,
  fetchSupplierPayablesDetail,
} from '@/shared/api/procurement.api';
import type { PurchaseOrderListItem } from '@/shared/api/procurement.types';

export interface SupplierPaymentAmountHints {
  grnNumber?: string;
  poNumber?: string;
  /** Giá trị GRN (tổng dòng, trước thuế GTGT) */
  grnPreTax?: number;
  /** Số còn lại trên sổ công nợ (trước thuế) */
  payableOutstanding?: number;
  /** Tổng PO sau thuế — tham chiếu thanh toán thực tế */
  poPostTax?: number;
}

export async function loadSupplierPaymentAmountHints(input: {
  supplierId?: string;
  goodsReceiptId?: string;
  purchaseOrderId?: string;
  purchaseOrders: PurchaseOrderListItem[];
}): Promise<SupplierPaymentAmountHints> {
  const { supplierId, goodsReceiptId, purchaseOrderId, purchaseOrders } = input;
  if (!supplierId && !goodsReceiptId && !purchaseOrderId) return {};

  let resolvedPoId = purchaseOrderId;
  let grnNumber: string | undefined;
  let grnPreTax: number | undefined;

  if (goodsReceiptId) {
    try {
      const grn = await fetchGoodsReceipt(goodsReceiptId);
      grnNumber = grn.grnNumber;
      grnPreTax = sumGrnSubtotal(grn.items);
      resolvedPoId = resolvedPoId ?? grn.purchaseOrderId;
    } catch {
      /* form vẫn dùng được nếu không tải được GRN */
    }
  }

  let payableOutstanding: number | undefined;
  if (supplierId && goodsReceiptId) {
    try {
      const detail = await fetchSupplierPayablesDetail(supplierId);
      const line = detail.lines.find((l) => l.goodsReceiptId === goodsReceiptId);
      if (line && line.outstanding > 0.009) {
        payableOutstanding = line.outstanding;
        grnNumber = grnNumber ?? line.grnNumber;
      }
    } catch {
      /* bỏ qua */
    }
  }

  const po = resolvedPoId ? purchaseOrders.find((p) => p.id === resolvedPoId) : undefined;

  return {
    grnNumber,
    poNumber: po?.poNumber,
    grnPreTax,
    payableOutstanding,
    poPostTax: po && po.totalAmount > 0.009 ? po.totalAmount : undefined,
  };
}

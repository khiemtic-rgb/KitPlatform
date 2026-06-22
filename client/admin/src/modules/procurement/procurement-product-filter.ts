import { fetchGoodsReceipt, fetchPurchaseOrder } from '@/shared/api/procurement.api';
import type {
  GoodsReceiptDetail,
  GoodsReceiptListItem,
  PurchaseOrderDetail,
  PurchaseOrderListItem,
} from '@/shared/api/procurement.types';

type DetailCache<T> = Record<string, T>;

async function filterByProductInDetails<T extends { id: string }>(
  items: T[],
  productId: string,
  cache: DetailCache<{ items: { productId: string }[] }>,
  fetchDetail: (id: string) => Promise<{ items: { productId: string }[] }>,
): Promise<T[]> {
  const matched: T[] = [];
  for (const item of items) {
    let detail = cache[item.id];
    if (!detail) {
      detail = await fetchDetail(item.id);
      cache[item.id] = detail;
    }
    if (detail.items.some((line) => line.productId === productId)) {
      matched.push(item);
    }
  }
  return matched;
}

export async function filterPurchaseOrdersByProduct(
  orders: PurchaseOrderListItem[],
  productId: string | undefined,
  cache: DetailCache<PurchaseOrderDetail>,
): Promise<PurchaseOrderListItem[]> {
  if (!productId) return orders;
  return filterByProductInDetails(orders, productId, cache, fetchPurchaseOrder);
}

export async function filterGoodsReceiptsByProduct(
  receipts: GoodsReceiptListItem[],
  productId: string | undefined,
  cache: DetailCache<GoodsReceiptDetail>,
): Promise<GoodsReceiptListItem[]> {
  if (!productId) return receipts;
  return filterByProductInDetails(receipts, productId, cache, fetchGoodsReceipt);
}

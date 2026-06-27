export type SupplierPaymentPrefill = {
  supplierId: string;
  goodsReceiptId?: string;
  purchaseOrderId?: string;
  amount?: number;
};

export function buildSupplierPaymentCreateUrl(prefill: SupplierPaymentPrefill): string {
  const params = new URLSearchParams();
  params.set('create', '1');
  params.set('supplierId', prefill.supplierId);
  if (prefill.goodsReceiptId) params.set('goodsReceiptId', prefill.goodsReceiptId);
  if (prefill.purchaseOrderId) params.set('purchaseOrderId', prefill.purchaseOrderId);
  if (prefill.amount != null && prefill.amount > 0) {
    params.set('amount', String(Math.round(prefill.amount)));
  }
  return `/procurement/supplier-payments?${params.toString()}`;
}

export function parseSupplierPaymentPrefill(searchParams: URLSearchParams): SupplierPaymentPrefill | null {
  if (searchParams.get('create') !== '1') return null;
  const supplierId = searchParams.get('supplierId');
  if (!supplierId) return null;

  const amountRaw = searchParams.get('amount');
  const parsedAmount = amountRaw != null && amountRaw !== '' ? Number(amountRaw) : undefined;

  return {
    supplierId,
    goodsReceiptId: searchParams.get('goodsReceiptId') ?? undefined,
    purchaseOrderId: searchParams.get('purchaseOrderId') ?? undefined,
    amount: parsedAmount != null && !Number.isNaN(parsedAmount) ? parsedAmount : undefined,
  };
}

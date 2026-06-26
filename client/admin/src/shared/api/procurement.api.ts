import { http } from '@/shared/api/http';
import type {
  GoodsReceiptDetail,
  GoodsReceiptListFilters,
  GoodsReceiptListItem,
  LastPurchasePriceHint,
  PurchaseOrderDetail,
  PurchaseOrderListFilters,
  PurchaseOrderListItem,
  Supplier,
  SupplierPaymentListFilters,
  SupplierPaymentListItem,
} from '@/shared/api/procurement.types';

function normalizeSupplier(row: Record<string, unknown>): Supplier {
  return {
    id: String(row.id ?? row.Id),
    supplierCode: String(row.supplierCode ?? row.SupplierCode ?? ''),
    supplierName: String(row.supplierName ?? row.SupplierName ?? ''),
    taxCode: (row.taxCode ?? row.TaxCode) as string | undefined,
    contactName: (row.contactName ?? row.ContactName) as string | undefined,
    phone: (row.phone ?? row.Phone) as string | undefined,
    email: (row.email ?? row.Email) as string | undefined,
    address: (row.address ?? row.Address) as string | undefined,
    paymentTerms: Number(row.paymentTerms ?? row.PaymentTerms ?? 30),
    status: Number(row.status ?? row.Status ?? 1),
  };
}

function normalizePoListItem(row: Record<string, unknown>): PurchaseOrderListItem {
  return {
    id: String(row.id ?? row.Id),
    poNumber: String(row.poNumber ?? row.PoNumber ?? ''),
    supplierId: String(row.supplierId ?? row.SupplierId),
    supplierName: String(row.supplierName ?? row.SupplierName ?? ''),
    warehouseId: String(row.warehouseId ?? row.WarehouseId),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    deletedAt: (row.deletedAt ?? row.DeletedAt) as string | undefined,
  };
}

function normalizePoItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    productUnitId: String(row.productUnitId ?? row.ProductUnitId),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    orderedQty: Number(row.orderedQty ?? row.OrderedQty ?? 0),
    receivedQty: Number(row.receivedQty ?? row.ReceivedQty ?? 0),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    lineTotal: Number(row.lineTotal ?? row.LineTotal ?? 0),
  };
}

function normalizePoDetail(data: Record<string, unknown>): PurchaseOrderDetail {
  const base = normalizePoListItem(data);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    ...base,
    expectedDate: (data.expectedDate ?? data.ExpectedDate) as string | undefined,
    subtotal: Number(data.subtotal ?? data.Subtotal ?? 0),
    taxAmount: Number(data.taxAmount ?? data.TaxAmount ?? 0),
    notes: (data.notes ?? data.Notes) as string | undefined,
    items: rawItems.map(normalizePoItem),
  };
}

function normalizeGrnListItem(row: Record<string, unknown>): GoodsReceiptListItem {
  return {
    id: String(row.id ?? row.Id),
    grnNumber: String(row.grnNumber ?? row.GrnNumber ?? ''),
    supplierId: String(row.supplierId ?? row.SupplierId),
    supplierName: String(row.supplierName ?? row.SupplierName ?? ''),
    warehouseId: String(row.warehouseId ?? row.WarehouseId),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    purchaseOrderId: (row.purchaseOrderId ?? row.PurchaseOrderId) as string | undefined,
    poNumber: (row.poNumber ?? row.PoNumber) as string | undefined,
    status: Number(row.status ?? row.Status ?? 1),
    receiptDate: String(row.receiptDate ?? row.ReceiptDate ?? ''),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    deletedAt: (row.deletedAt ?? row.DeletedAt) as string | undefined,
  };
}

function normalizeGrnItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    purchaseOrderItemId: (row.purchaseOrderItemId ?? row.PurchaseOrderItemId) as string | undefined,
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    productUnitId: String(row.productUnitId ?? row.ProductUnitId),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
    manufactureDate: (row.manufactureDate ?? row.ManufactureDate) as string | undefined,
    expiryDate: String(row.expiryDate ?? row.ExpiryDate ?? ''),
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    unitCost: Number(row.unitCost ?? row.UnitCost ?? 0),
    lineTotal: Number(row.lineTotal ?? row.LineTotal ?? 0),
  };
}

function normalizeGrnDetail(data: Record<string, unknown>): GoodsReceiptDetail {
  const base = normalizeGrnListItem(data);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    ...base,
    notes: (data.notes ?? data.Notes) as string | undefined,
    items: rawItems.map(normalizeGrnItem),
  };
}

export async function fetchLastPurchasePriceHint(
  supplierId: string,
  productId: string,
): Promise<LastPurchasePriceHint> {
  const { data } = await http.get<Record<string, unknown>>('/procurement/purchase-orders/price-hint', {
    params: { supplierId, productId },
  });
  return {
    unitPrice: data.unitPrice != null ? Number(data.unitPrice ?? data.UnitPrice) : undefined,
    priceDate: (data.priceDate ?? data.PriceDate) as string | undefined,
    source: (data.source ?? data.Source) as string | undefined,
    documentNumber: (data.documentNumber ?? data.DocumentNumber) as string | undefined,
  };
}

export async function fetchSuppliers(activeOnly = false): Promise<Supplier[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/procurement/suppliers', {
    params: activeOnly ? { activeOnly: true } : undefined,
  });
  return data.map((row) => normalizeSupplier(row));
}

export async function createSupplier(payload: {
  supplierCode: string;
  supplierName: string;
  taxCode?: string;
  contactName?: string;
  phone?: string;
  email?: string;
  address?: string;
  paymentTerms?: number;
}): Promise<Supplier> {
  const { data } = await http.post<Record<string, unknown>>('/procurement/suppliers', payload);
  return normalizeSupplier(data);
}

export async function updateSupplier(
  id: string,
  payload: {
    supplierName: string;
    taxCode?: string;
    contactName?: string;
    phone?: string;
    email?: string;
    address?: string;
    paymentTerms: number;
    status: number;
  },
): Promise<Supplier> {
  const { data } = await http.put<Record<string, unknown>>(`/procurement/suppliers/${id}`, payload);
  return normalizeSupplier(data);
}

export async function deleteSupplier(id: string): Promise<void> {
  await http.delete(`/procurement/suppliers/${id}`);
}

function buildListParams(filters?: Record<string, string | number | boolean | undefined>): Record<string, string | number | boolean> | undefined {
  if (!filters) return undefined;
  const params: Record<string, string | number | boolean> = {};
  for (const [key, value] of Object.entries(filters)) {
    if (value === undefined || value === '' || value === false) continue;
    params[key] = value;
  }
  return Object.keys(params).length > 0 ? params : undefined;
}

export async function fetchPurchaseOrders(
  filters?: PurchaseOrderListFilters,
): Promise<PurchaseOrderListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/procurement/purchase-orders', {
    params: buildListParams(filters as Record<string, string | number | boolean | undefined>),
  });
  return data.map((row) => normalizePoListItem(row));
}

export async function fetchPurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/procurement/purchase-orders/${id}`);
  return normalizePoDetail(data);
}

export async function createPurchaseOrder(payload: {
  supplierId: string;
  warehouseId: string;
  expectedDate?: string;
  notes?: string;
  taxAmount?: number;
  items: { productId: string; productUnitId: string; orderedQty: number; unitPrice: number }[];
}): Promise<PurchaseOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>('/procurement/purchase-orders', payload);
  return normalizePoDetail(data);
}

export async function updatePurchaseOrder(
  id: string,
  payload: {
    expectedDate?: string;
    notes?: string;
    taxAmount?: number;
    items: {
      id?: string;
      productId: string;
      productUnitId: string;
      orderedQty: number;
      unitPrice: number;
    }[];
  },
): Promise<PurchaseOrderDetail> {
  const { data } = await http.put<Record<string, unknown>>(`/procurement/purchase-orders/${id}`, payload);
  return normalizePoDetail(data);
}

export async function approvePurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/procurement/purchase-orders/${id}/approve`);
  return normalizePoDetail(data);
}

export async function cancelPurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/procurement/purchase-orders/${id}/cancel`);
  return normalizePoDetail(data);
}

export async function closePurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/procurement/purchase-orders/${id}/close`);
  return normalizePoDetail(data);
}

export async function archivePurchaseOrder(id: string): Promise<void> {
  await http.delete(`/procurement/purchase-orders/${id}`);
}

export async function purgePurchaseOrder(id: string): Promise<void> {
  await http.delete(`/procurement/purchase-orders/${id}/purge`);
}

/** @deprecated Use archivePurchaseOrder */
export async function deletePurchaseOrder(id: string): Promise<void> {
  await archivePurchaseOrder(id);
}

export async function fetchGoodsReceipts(
  filters?: GoodsReceiptListFilters,
): Promise<GoodsReceiptListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/procurement/goods-receipts', {
    params: buildListParams(filters as Record<string, string | number | boolean | undefined>),
  });
  return data.map((row) => normalizeGrnListItem(row));
}

export async function fetchGoodsReceipt(id: string): Promise<GoodsReceiptDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/procurement/goods-receipts/${id}`);
  return normalizeGrnDetail(data);
}

export async function createGoodsReceipt(payload: {
  purchaseOrderId?: string;
  supplierId: string;
  warehouseId: string;
  receiptDate?: string;
  notes?: string;
  items: {
    purchaseOrderItemId?: string;
    productId: string;
    productUnitId: string;
    batchNumber: string;
    manufactureDate?: string;
    expiryDate: string;
    quantity: number;
    unitCost: number;
  }[];
}): Promise<GoodsReceiptDetail> {
  const { data } = await http.post<Record<string, unknown>>('/procurement/goods-receipts', payload);
  return normalizeGrnDetail(data);
}

export async function completeGoodsReceipt(id: string): Promise<GoodsReceiptDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/procurement/goods-receipts/${id}/complete`);
  return normalizeGrnDetail(data);
}

export async function cancelGoodsReceipt(id: string): Promise<GoodsReceiptDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/procurement/goods-receipts/${id}/cancel`);
  return normalizeGrnDetail(data);
}

export async function archiveGoodsReceipt(id: string): Promise<void> {
  await http.delete(`/procurement/goods-receipts/${id}`);
}

export async function purgeGoodsReceipt(id: string): Promise<void> {
  await http.delete(`/procurement/goods-receipts/${id}/purge`);
}

/** @deprecated Use archiveGoodsReceipt */
export async function deleteGoodsReceipt(id: string): Promise<void> {
  await archiveGoodsReceipt(id);
}

function normalizePayment(row: Record<string, unknown>): SupplierPaymentListItem {
  return {
    id: String(row.id ?? row.Id),
    paymentNumber: String(row.paymentNumber ?? row.PaymentNumber ?? ''),
    supplierId: String(row.supplierId ?? row.SupplierId),
    supplierName: String(row.supplierName ?? row.SupplierName ?? ''),
    amount: Number(row.amount ?? row.Amount ?? 0),
    paymentMethod: Number(row.paymentMethod ?? row.PaymentMethod ?? 1),
    status: Number(row.status ?? row.Status ?? 2),
    paymentDate: String(row.paymentDate ?? row.PaymentDate ?? ''),
    postedAt: (row.postedAt ?? row.PostedAt) as string | undefined,
    purchaseOrderId: (row.purchaseOrderId ?? row.PurchaseOrderId) as string | undefined,
    poNumber: (row.poNumber ?? row.PoNumber) as string | undefined,
    goodsReceiptId: (row.goodsReceiptId ?? row.GoodsReceiptId) as string | undefined,
    grnNumber: (row.grnNumber ?? row.GrnNumber) as string | undefined,
    notes: (row.notes ?? row.Notes) as string | undefined,
  };
}

function paymentPayload(values: {
  supplierId: string;
  purchaseOrderId?: string;
  goodsReceiptId?: string;
  amount: number;
  paymentMethod: number;
  notes?: string;
  paymentDate?: string;
}) {
  return {
    supplierId: values.supplierId,
    purchaseOrderId: values.purchaseOrderId || null,
    goodsReceiptId: values.goodsReceiptId || null,
    amount: values.amount,
    paymentMethod: values.paymentMethod,
    notes: values.notes?.trim() ? values.notes.trim() : null,
    paymentDate: values.paymentDate || null,
  };
}

function paymentListParams(filters?: SupplierPaymentListFilters) {
  return buildListParams(filters as Record<string, string | number | boolean | undefined>);
}

export async function fetchSupplierPayments(
  filters?: SupplierPaymentListFilters,
): Promise<SupplierPaymentListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/procurement/supplier-payments', {
    params: paymentListParams(filters),
  });
  return data.map((row) => normalizePayment(row));
}

export async function fetchSupplierPayment(id: string): Promise<SupplierPaymentListItem> {
  const { data } = await http.get<Record<string, unknown>>(`/procurement/supplier-payments/${id}`);
  return normalizePayment(data);
}

export async function createSupplierPayment(payload: {
  supplierId: string;
  purchaseOrderId?: string;
  goodsReceiptId?: string;
  amount: number;
  paymentMethod: number;
  notes?: string;
  paymentDate?: string;
}): Promise<SupplierPaymentListItem> {
  const { data } = await http.post<Record<string, unknown>>('/procurement/supplier-payments', paymentPayload(payload));
  return normalizePayment(data);
}

export async function updateSupplierPayment(
  id: string,
  payload: {
    supplierId: string;
    purchaseOrderId?: string;
    goodsReceiptId?: string;
    amount: number;
    paymentMethod: number;
    notes?: string;
    paymentDate?: string;
  },
): Promise<SupplierPaymentListItem> {
  const { data } = await http.put<Record<string, unknown>>(`/procurement/supplier-payments/${id}`, paymentPayload(payload));
  return normalizePayment(data);
}

export async function postSupplierPayment(id: string): Promise<SupplierPaymentListItem> {
  const { data } = await http.post<Record<string, unknown>>(`/procurement/supplier-payments/${id}/post`);
  return normalizePayment(data);
}

export async function cancelSupplierPayment(id: string): Promise<SupplierPaymentListItem> {
  const { data } = await http.post<Record<string, unknown>>(`/procurement/supplier-payments/${id}/cancel`);
  return normalizePayment(data);
}

function normalizePayablesAging(row: Record<string, unknown>) {
  const aging = (row.aging ?? row.Aging ?? {}) as Record<string, unknown>;
  return {
    current: Number(aging.current ?? aging.Current ?? 0),
    days31To60: Number(aging.days31To60 ?? aging.Days31To60 ?? 0),
    days61To90: Number(aging.days61To90 ?? aging.Days61To90 ?? 0),
    over90: Number(aging.over90 ?? aging.Over90 ?? 0),
  };
}

function normalizePayablesRow(row: Record<string, unknown>) {
  return {
    supplierId: String(row.supplierId ?? row.SupplierId),
    supplierCode: String(row.supplierCode ?? row.SupplierCode ?? ''),
    supplierName: String(row.supplierName ?? row.SupplierName ?? ''),
    paymentTerms: Number(row.paymentTerms ?? row.PaymentTerms ?? 30),
    totalPayable: Number(row.totalPayable ?? row.TotalPayable ?? 0),
    unappliedCredit: Number(row.unappliedCredit ?? row.UnappliedCredit ?? 0),
    aging: normalizePayablesAging(row),
    openDocumentCount: Number(row.openDocumentCount ?? row.OpenDocumentCount ?? 0),
  };
}

export async function fetchSupplierPayables() {
  const { data } = await http.get<Record<string, unknown>[]>('/procurement/supplier-payables');
  return data.map((row) => normalizePayablesRow(row));
}

export async function fetchSupplierPayablesDetail(supplierId: string) {
  const { data } = await http.get<Record<string, unknown>>(`/procurement/supplier-payables/${supplierId}`);
  const lines = ((data.lines ?? data.Lines ?? []) as Record<string, unknown>[]).map((line) => ({
    goodsReceiptId: String(line.goodsReceiptId ?? line.GoodsReceiptId),
    grnNumber: String(line.grnNumber ?? line.GrnNumber ?? ''),
    receiptDate: String(line.receiptDate ?? line.ReceiptDate ?? ''),
    grnTotal: Number(line.grnTotal ?? line.GrnTotal ?? 0),
    paidAmount: Number(line.paidAmount ?? line.PaidAmount ?? 0),
    outstanding: Number(line.outstanding ?? line.Outstanding ?? 0),
    daysOutstanding: Number(line.daysOutstanding ?? line.DaysOutstanding ?? 0),
  }));
  return {
    ...normalizePayablesRow(data),
    lines,
  };
}

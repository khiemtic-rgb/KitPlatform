import axios from 'axios';
import { http } from '@/shared/api/http';

export type CustomerReceivablesLine = {
  salesOrderId: string;
  orderNumber: string;
  orderDate?: string;
  orderTotal?: number;
  paidAmount?: number;
  outstanding: number;
  daysOutstanding?: number;
};

export interface CustomerReceivablesSummary {
  customerId: string;
  customerCode: string;
  customerName: string;
  customerPhone?: string | null;
  totalReceivable: number;
  unappliedCredit?: number;
  openDocumentCount?: number;
  lines: CustomerReceivablesLine[];
}

export type CustomerReceivablesRow = {
  customerId: string;
  customerCode: string;
  customerName: string;
  customerPhone?: string | null;
  totalReceivable: number;
  unappliedCredit: number;
  openDocumentCount: number;
};

export type CustomerReceivablesHint = {
  customerCode: string;
  fullName: string;
  phone?: string;
};

function normalizeLine(line: Record<string, unknown>): CustomerReceivablesLine {
  return {
    salesOrderId: String(line.salesOrderId ?? line.SalesOrderId),
    orderNumber: String(line.orderNumber ?? line.OrderNumber ?? ''),
    orderDate: (line.orderDate ?? line.OrderDate) as string | undefined,
    orderTotal: Number(line.orderTotal ?? line.OrderTotal ?? 0) || undefined,
    paidAmount: Number(line.paidAmount ?? line.PaidAmount ?? 0) || undefined,
    outstanding: Number(line.outstanding ?? line.Outstanding ?? 0),
    daysOutstanding: Number(line.daysOutstanding ?? line.DaysOutstanding ?? 0) || undefined,
  };
}

function normalizeReceivablesRow(row: Record<string, unknown>): CustomerReceivablesRow {
  return {
    customerId: String(row.customerId ?? row.CustomerId),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    customerPhone: (row.customerPhone ?? row.CustomerPhone) as string | null | undefined,
    totalReceivable: Number(row.totalReceivable ?? row.TotalReceivable ?? 0),
    unappliedCredit: Number(row.unappliedCredit ?? row.UnappliedCredit ?? 0),
    openDocumentCount: Number(row.openDocumentCount ?? row.OpenDocumentCount ?? 0),
  };
}

/** Danh sách khách còn công nợ (ưu tiên hiển thị tại quầy). */
export async function fetchReceivablesSummary(warehouseId?: string): Promise<CustomerReceivablesRow[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/customer-receivables', {
    params: warehouseId ? { warehouseId } : undefined,
  });
  return data
    .map(normalizeReceivablesRow)
    .filter((row) => row.totalReceivable > 0.009)
    .sort((a, b) => b.totalReceivable - a.totalReceivable);
}

export async function fetchCustomerReceivablesDetail(
  customerId: string,
  customerHint?: CustomerReceivablesHint,
): Promise<CustomerReceivablesSummary> {
  try {
    const { data } = await http.get<Record<string, unknown>>(`/sales/customer-receivables/${customerId}`);
    const lines = ((data.lines ?? data.Lines ?? []) as Record<string, unknown>[]).map(normalizeLine);
    return {
      customerId: String(data.customerId ?? data.CustomerId ?? customerId),
      customerCode: String(data.customerCode ?? data.CustomerCode ?? customerHint?.customerCode ?? ''),
      customerName: String(data.customerName ?? data.CustomerName ?? customerHint?.fullName ?? ''),
      customerPhone: (data.customerPhone ?? data.CustomerPhone ?? customerHint?.phone) as
        | string
        | null
        | undefined,
      totalReceivable: Number(data.totalReceivable ?? data.TotalReceivable ?? 0),
      unappliedCredit: Number(data.unappliedCredit ?? data.UnappliedCredit ?? 0) || undefined,
      openDocumentCount: lines.length || undefined,
      lines,
    };
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404 && customerHint) {
      return {
        customerId,
        customerCode: customerHint.customerCode,
        customerName: customerHint.fullName,
        customerPhone: customerHint.phone ?? null,
        totalReceivable: 0,
        lines: [],
      };
    }
    throw error;
  }
}

export interface CustomerPaymentReceipt {
  id: string;
  paymentNumber: string;
  amount: number;
  paymentMethod: number;
  paymentDate: string;
  customerName: string;
  customerCode?: string;
  orderNumber?: string;
  notes?: string;
}

function normalizePostedPayment(data: Record<string, unknown>): CustomerPaymentReceipt {
  return {
    id: String(data.id ?? data.Id),
    paymentNumber: String(data.paymentNumber ?? data.PaymentNumber ?? ''),
    amount: Number(data.amount ?? data.Amount ?? 0),
    paymentMethod: Number(data.paymentMethod ?? data.PaymentMethod ?? 1),
    paymentDate: String(data.paymentDate ?? data.PaymentDate ?? new Date().toISOString()),
    customerName: String(data.customerName ?? data.CustomerName ?? ''),
    customerCode: (data.customerCode ?? data.CustomerCode) as string | undefined,
    orderNumber: (data.orderNumber ?? data.OrderNumber) as string | undefined,
    notes: (data.notes ?? data.Notes) as string | undefined,
  };
}

export async function createAndPostCustomerPayment(payload: {
  customerId: string;
  amount: number;
  paymentMethod: number;
  salesOrderId?: string;
  notes?: string;
  customerName?: string;
  customerCode?: string;
}): Promise<CustomerPaymentReceipt> {
  const { data: created } = await http.post<Record<string, unknown>>('/sales/customer-payments', {
    customerId: payload.customerId,
    salesOrderId: payload.salesOrderId ?? null,
    amount: payload.amount,
    paymentMethod: payload.paymentMethod,
    notes: payload.notes?.trim() || null,
  });
  const id = String(created.id ?? created.Id);
  const { data: posted } = await http.post<Record<string, unknown>>(`/sales/customer-payments/${id}/post`);
  const receipt = normalizePostedPayment(posted);
  return {
    ...receipt,
    customerName: receipt.customerName || payload.customerName || '',
    customerCode: receipt.customerCode || payload.customerCode,
  };
}

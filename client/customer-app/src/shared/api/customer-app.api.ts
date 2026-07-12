import axios from 'axios';
import { http } from '@/shared/api/http';
import { apiOfflineHint } from '@/shared/api/api-network';
import i18n from '@/shared/i18n';
import type {
  CreateMedicationReminderRequest,
  CustomerChatMessage,
  CustomerChatMessageList,
  CustomerChatThread,
  CustomerConsent,
  CustomerLoginResponse,
  CustomerOtpSentResponse,
  CustomerProfile,
  CustomerVoucherList,
  FamilyMember,
  LoyaltySummary,
  MedicationReminder,
  MedicationReminderList,
  PushSubscriptionStatus,
  PagedLoyaltyTransactions,
  RepurchaseSuggestion,
  UpdateMedicationReminderRequest,
} from '@/shared/api/customer-app.types';
import { CUSTOMER_APP_CHAT_CONSENT } from '@/shared/api/customer-app.types';
import { normalizeReminder, normalizeReminderList } from '@/shared/api/reminder-normalize';

export async function requestOtp(phone: string, tenantCode: string) {
  const { data } = await http.post<CustomerOtpSentResponse>('/auth/request-otp', {
    phone,
    tenantCode,
  });
  return data;
}

export async function verifyOtp(phone: string, code: string, tenantCode: string) {
  const { data } = await http.post<CustomerLoginResponse>('/auth/verify-otp', {
    phone,
    code,
    tenantCode,
  });
  return data;
}

export async function logoutApi(refreshToken: string) {
  await http.post('/auth/logout', { refreshToken });
}

export async function fetchProfile() {
  const { data } = await http.get<CustomerProfile>('/auth/me');
  return data;
}

export async function updatePreferredLocale(preferredLocale: string) {
  const { data } = await http.patch<CustomerProfile>('/auth/locale', { preferredLocale });
  return data;
}

export async function fetchLoyaltySummary() {
  const { data } = await http.get<LoyaltySummary>('/loyalty/summary');
  return data;
}

export async function fetchLoyaltyTransactions(page = 1, pageSize = 20) {
  const { data } = await http.get<PagedLoyaltyTransactions>('/loyalty/transactions', {
    params: { page, pageSize },
  });
  return data;
}

export async function fetchVouchers(includeUsed = false) {
  const { data } = await http.get<CustomerVoucherList>('/loyalty/vouchers', {
    params: { includeUsed },
  });
  return data;
}

export async function fetchReminders(includeInactive = false) {
  const { data } = await http.get<MedicationReminderList>('/reminders', {
    params: { includeInactive },
  });
  return { items: normalizeReminderList(data) };
}

export async function createReminder(payload: CreateMedicationReminderRequest) {
  const { data } = await http.post<Record<string, unknown>>('/reminders', payload);
  return normalizeReminder(data);
}

export async function updateReminder(id: string, payload: UpdateMedicationReminderRequest) {
  const { data } = await http.put<Record<string, unknown>>(`/reminders/${id}`, payload);
  return normalizeReminder(data);
}

export async function deactivateReminder(id: string) {
  await http.delete(`/reminders/${id}`);
}

function normalizeProductSearchItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id ?? ''),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    genericName: (row.genericName ?? row.GenericName ?? null) as string | null,
    saleUnitName: (row.saleUnitName ?? row.SaleUnitName ?? null) as string | null,
  };
}

export async function searchProducts(search?: string, page = 1, pageSize = 20) {
  const { data } = await http.get<Record<string, unknown>>('/catalog/products', {
    params: { search: search?.trim() || undefined, page, pageSize },
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    items: rawItems.map(normalizeProductSearchItem),
    total: Number(data.total ?? data.Total ?? rawItems.length),
    page: Number(data.page ?? data.Page ?? page),
    pageSize: Number(data.pageSize ?? data.PageSize ?? pageSize),
  };
}

function normalizeGranted(row: Record<string, unknown>): boolean {
  const raw = row.granted ?? row.Granted;
  if (raw === true || raw === 'true') return true;
  if (raw === false || raw === 'false') return false;
  return Boolean(raw);
}

function normalizeConsent(row: Record<string, unknown>): CustomerConsent {
  return {
    id: String(row.id ?? row.Id),
    customerId: String(row.customerId ?? row.CustomerId),
    channel: Number(row.channel ?? row.Channel ?? 1),
    purpose: Number(row.purpose ?? row.Purpose ?? 1),
    granted: normalizeGranted(row),
    grantedAt: (row.grantedAt ?? row.GrantedAt) as string | undefined,
    revokedAt: (row.revokedAt ?? row.RevokedAt) as string | undefined,
    source: Number(row.source ?? row.Source ?? 3),
    notes: (row.notes ?? row.Notes) as string | undefined,
  };
}

export async function fetchConsents() {
  const { data } = await http.get<Record<string, unknown>[]>('/consents');
  return data.map(normalizeConsent);
}

export async function upsertConsents(
  items: { channel: number; purpose: number; granted: boolean; notes?: string }[],
) {
  const { data } = await http.put<Record<string, unknown>[]>('/consents', { items });
  return data.map(normalizeConsent);
}

export async function fetchCareReminderEligible() {
  const { data } = await http.get<{ eligible: boolean }>('/consents/care-reminder-eligible');
  return data.eligible;
}

export async function fetchPushStatus() {
  const { data } = await http.get<PushSubscriptionStatus>('/push/status');
  return data;
}

export async function registerPushSubscription(payload: {
  endpoint: string;
  p256dh: string;
  auth: string;
}) {
  await http.post('/push/subscribe', payload);
}

export async function unregisterPushSubscription(endpoint: string) {
  await http.delete('/push/subscribe', { params: { endpoint } });
}

function normalizeChatMessage(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    senderType: Number(row.senderType ?? row.SenderType ?? 1),
    senderName: (row.senderName ?? row.SenderName) as string | null,
    body: String(row.body ?? row.Body ?? ''),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    readAt: (row.readAt ?? row.ReadAt) as string | null,
  };
}

export async function fetchChatThread() {
  const { data } = await http.get<Record<string, unknown>>('/chat/thread');
  return {
    threadId: String(data.threadId ?? data.ThreadId ?? ''),
    unreadCount: Number(data.unreadCount ?? data.UnreadCount ?? 0),
    lastMessageAt: (data.lastMessageAt ?? data.LastMessageAt) as string | null,
    lastMessagePreview: (data.lastMessagePreview ?? data.LastMessagePreview) as string | null,
  } satisfies CustomerChatThread;
}

export async function fetchChatMessages(beforeId?: string, limit = 50) {
  const { data } = await http.get<Record<string, unknown>>('/chat/messages', {
    params: { beforeId, limit },
  });
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(normalizeChatMessage);
  return {
    items,
    hasMore: Boolean(data.hasMore ?? data.HasMore),
  } satisfies CustomerChatMessageList;
}

export async function sendChatMessage(body: string) {
  const { data } = await http.post<Record<string, unknown>>('/chat/messages', { body });
  return normalizeChatMessage(data);
}

export async function markChatRead() {
  await http.post('/chat/read');
}

function normalizeDraftOrderLine(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    lineNumber: Number(row.lineNumber ?? row.LineNumber ?? 0),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    lineAmount: Number(row.lineAmount ?? row.LineAmount ?? 0),
    dosageNote: (row.dosageNote ?? row.DosageNote) as string | null | undefined,
  };
}

function normalizeDraftOrder(row: Record<string, unknown>) {
  const items = ((row.items ?? row.Items ?? []) as Record<string, unknown>[]).map(normalizeDraftOrderLine);
  return {
    id: String(row.id ?? row.Id),
    draftNumber: String(row.draftNumber ?? row.DraftNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    subtotal: Number(row.subtotal ?? row.Subtotal ?? 0),
    discountAmount: Number(row.discountAmount ?? row.DiscountAmount ?? 0),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    notes: (row.notes ?? row.Notes) as string | null | undefined,
    sentAt: (row.sentAt ?? row.SentAt) as string | null | undefined,
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    completedAt: (row.completedAt ?? row.CompletedAt) as string | null | undefined,
    expiresAt: (row.expiresAt ?? row.ExpiresAt) as string | null | undefined,
    salesOrderNumber: (row.salesOrderNumber ?? row.SalesOrderNumber) as string | null | undefined,
    items,
  };
}

function normalizeDraftOrderListItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    draftNumber: String(row.draftNumber ?? row.DraftNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    sentAt: (row.sentAt ?? row.SentAt) as string | null | undefined,
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    expiresAt: (row.expiresAt ?? row.ExpiresAt) as string | null | undefined,
  };
}

export async function fetchDraftOrders() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/draft-orders',
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizeDraftOrderListItem);
}

export async function fetchDraftOrder(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/draft-orders/${id}`);
  return normalizeDraftOrder(data);
}

export async function confirmDraftOrder(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/draft-orders/${id}/confirm`);
  return normalizeDraftOrder(data);
}

export async function hideDraftOrder(id: string) {
  await http.post(`/draft-orders/${id}/hide`);
}

export async function cancelDraftOrder(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/draft-orders/${id}/cancel`);
  return normalizeDraftOrder(data);
}

function normalizePurchaseListItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 2),
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    amountPaid: Number(row.amountPaid ?? row.AmountPaid ?? 0),
    outstanding: Number(row.outstanding ?? row.Outstanding ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    totalRefunded: Number(row.totalRefunded ?? row.TotalRefunded ?? 0),
  };
}

function normalizePurchaseLine(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    productName: String(row.productName ?? row.ProductName ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    lineTotal: Number(row.lineTotal ?? row.LineTotal ?? 0),
    returnedQuantity: Number(row.returnedQuantity ?? row.ReturnedQuantity ?? 0),
  };
}

function normalizePurchasePayment(row: Record<string, unknown>) {
  return {
    paymentMethod: Number(row.paymentMethod ?? row.PaymentMethod ?? 1),
    amount: Number(row.amount ?? row.Amount ?? 0),
  };
}

function normalizePurchaseDetail(row: Record<string, unknown>) {
  const items = ((row.items ?? row.Items ?? []) as Record<string, unknown>[]).map(normalizePurchaseLine);
  const payments = ((row.payments ?? row.Payments ?? []) as Record<string, unknown>[]).map(normalizePurchasePayment);
  return {
    id: String(row.id ?? row.Id),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 2),
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    subtotal: Number(row.subtotal ?? row.Subtotal ?? 0),
    discountAmount: Number(row.discountAmount ?? row.DiscountAmount ?? 0),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    amountPaid: Number(row.amountPaid ?? row.AmountPaid ?? 0),
    outstanding: Number(row.outstanding ?? row.Outstanding ?? 0),
    totalRefunded: Number(row.totalRefunded ?? row.TotalRefunded ?? 0),
    notes: (row.notes ?? row.Notes) as string | null | undefined,
    loyaltyPointsEarned: (row.loyaltyPointsEarned ?? row.LoyaltyPointsEarned) as number | null | undefined,
    loyaltyPointsRedeemed: Number(row.loyaltyPointsRedeemed ?? row.LoyaltyPointsRedeemed ?? 0),
    loyaltyDiscountAmount: Number(row.loyaltyDiscountAmount ?? row.LoyaltyDiscountAmount ?? 0),
    voucherDiscountAmount: Number(row.voucherDiscountAmount ?? row.VoucherDiscountAmount ?? 0),
    voucherCode: (row.voucherCode ?? row.VoucherCode) as string | null | undefined,
    items,
    payments,
  };
}

export async function fetchPurchases() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/purchases',
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizePurchaseListItem);
}

export async function fetchPurchase(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/purchases/${id}`);
  return normalizePurchaseDetail(data);
}

function normalizeReceivableLine(row: Record<string, unknown>) {
  return {
    salesOrderId: String(row.salesOrderId ?? row.SalesOrderId ?? ''),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    orderTotal: Number(row.orderTotal ?? row.OrderTotal ?? 0),
    amountPaid: Number(row.amountPaid ?? row.AmountPaid ?? 0),
    outstanding: Number(row.outstanding ?? row.Outstanding ?? 0),
  };
}

export async function fetchReceivablesSummary() {
  const { data } = await http.get<Record<string, unknown>>('/receivables');
  const lines = ((data.lines ?? data.Lines ?? []) as Record<string, unknown>[]).map(normalizeReceivableLine);
  return {
    totalReceivable: Number(data.totalReceivable ?? data.TotalReceivable ?? 0),
    openOrderCount: Number(data.openOrderCount ?? data.OpenOrderCount ?? lines.length),
    lines,
  };
}

export async function fetchReceivableOrder(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/receivables/orders/${id}`);
  return normalizePurchaseDetail(data);
}

function normalizeAddress(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    label: String(row.label ?? row.Label ?? ''),
    recipientName: (row.recipientName ?? row.RecipientName) as string | null | undefined,
    phone: (row.phone ?? row.Phone) as string | null | undefined,
    addressLine: String(row.addressLine ?? row.AddressLine ?? ''),
    ward: (row.ward ?? row.Ward) as string | null | undefined,
    district: (row.district ?? row.District) as string | null | undefined,
    province: (row.province ?? row.Province) as string | null | undefined,
    isDefault: Boolean(row.isDefault ?? row.IsDefault),
  };
}

export async function fetchAddresses() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/addresses',
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizeAddress);
}

export async function createAddress(payload: {
  label: string;
  recipientName?: string;
  phone?: string;
  addressLine: string;
  ward?: string;
  district?: string;
  province?: string;
  isDefault?: boolean;
}) {
  const { data } = await http.post<Record<string, unknown>>('/addresses', payload);
  return normalizeAddress(data);
}

export async function updateAddress(
  id: string,
  payload: {
    label: string;
    recipientName?: string;
    phone?: string;
    addressLine: string;
    ward?: string;
    district?: string;
    province?: string;
    isDefault?: boolean;
  },
) {
  const { data } = await http.put<Record<string, unknown>>(`/addresses/${id}`, payload);
  return normalizeAddress(data);
}

export async function deleteAddress(id: string) {
  await http.delete(`/addresses/${id}`);
}

function normalizeReservationLine(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    lineNumber: Number(row.lineNumber ?? row.LineNumber ?? 0),
    productId: String(row.productId ?? row.ProductId ?? ''),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    customerNote: (row.customerNote ?? row.CustomerNote) as string | null | undefined,
  };
}

function normalizeReservationListItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    reservationNumber: String(row.reservationNumber ?? row.ReservationNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 0),
    fulfillmentType: Number(row.fulfillmentType ?? row.FulfillmentType ?? 1),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    submittedAt: String(row.submittedAt ?? row.SubmittedAt ?? ''),
    readyAt: (row.readyAt ?? row.ReadyAt) as string | null | undefined,
  };
}

function normalizeReservationDetail(row: Record<string, unknown>) {
  const items = ((row.items ?? row.Items ?? []) as Record<string, unknown>[]).map(normalizeReservationLine);
  return {
    id: String(row.id ?? row.Id),
    reservationNumber: String(row.reservationNumber ?? row.ReservationNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 0),
    fulfillmentType: Number(row.fulfillmentType ?? row.FulfillmentType ?? 1),
    addressId: (row.addressId ?? row.AddressId) as string | null | undefined,
    addressSummary: (row.addressSummary ?? row.AddressSummary) as string | null | undefined,
    notes: (row.notes ?? row.Notes) as string | null | undefined,
    staffNotes: (row.staffNotes ?? row.StaffNotes) as string | null | undefined,
    submittedAt: String(row.submittedAt ?? row.SubmittedAt ?? ''),
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    readyAt: (row.readyAt ?? row.ReadyAt) as string | null | undefined,
    collectedAt: (row.collectedAt ?? row.CollectedAt) as string | null | undefined,
    salesOrderId: (row.salesOrderId ?? row.SalesOrderId) as string | null | undefined,
    salesOrderNumber: (row.salesOrderNumber ?? row.SalesOrderNumber) as string | null | undefined,
    items,
  };
}

export async function fetchReservations() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/reservations',
  );
  const rows = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rows.map(normalizeReservationListItem);
}

export async function fetchReservation(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/reservations/${id}`);
  return normalizeReservationDetail(data);
}

export async function createReservation(payload: {
  fulfillmentType: number;
  addressId?: string;
  notes?: string;
  items: { productId: string; quantity: number; customerNote?: string }[];
}) {
  const { data } = await http.post<Record<string, unknown>>('/reservations', payload);
  return normalizeReservationDetail(data);
}

export async function cancelReservation(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/reservations/${id}/cancel`);
  return normalizeReservationDetail(data);
}

function normalizeRepurchase(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    salesOrderId: String(row.salesOrderId ?? row.SalesOrderId ?? ''),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    orderLabel: String(row.orderLabel ?? row.OrderLabel ?? i18n.t('ordersDetail.defaultOrderLabel')),
    status: String(row.status ?? row.Status ?? 'pending'),
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    reminderDaysSupply: (row.reminderDaysSupply ?? row.ReminderDaysSupply ?? null) as number | null,
    suggestedForDate: (row.suggestedForDate ?? row.SuggestedForDate ?? null) as string | null,
    snoozedUntil: (row.snoozedUntil ?? row.SnoozedUntil ?? null) as string | null,
    drinkRemindersCreatedAt: (row.drinkRemindersCreatedAt ?? row.DrinkRemindersCreatedAt ?? null) as
      | string
      | null,
  };
}

export async function fetchRepurchaseSuggestions() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/repurchase-suggestions',
  );
  const rows = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rows.map(normalizeRepurchase);
}

export async function acceptRepurchaseSuggestion(
  id: string,
  payload?: { familyMemberId?: string; remindTime?: string },
) {
  const { data } = await http.post<Record<string, unknown>>(`/repurchase-suggestions/${id}/accept`, payload ?? {});
  return normalizeRepurchase(data);
}

export async function dismissRepurchaseSuggestion(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/repurchase-suggestions/${id}/dismiss`);
  return normalizeRepurchase(data);
}

export async function snoozeRepurchaseSuggestion(id: string, snoozedUntil: string) {
  const { data } = await http.post<Record<string, unknown>>(`/repurchase-suggestions/${id}/snooze`, {
    snoozedUntil,
  });
  return normalizeRepurchase(data);
}

function normalizeFamilyMember(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    linkedCustomerId: (row.linkedCustomerId ?? row.LinkedCustomerId ?? null) as string | null,
    fullName: String(row.fullName ?? row.FullName ?? ''),
    phone: (row.phone ?? row.Phone ?? null) as string | null,
    dateOfBirth: (row.dateOfBirth ?? row.DateOfBirth ?? null) as string | null,
    gender: (row.gender ?? row.Gender ?? null) as number | null,
    relationship: String(row.relationship ?? row.Relationship ?? 'other'),
    notes: (row.notes ?? row.Notes ?? null) as string | null,
    status: Number(row.status ?? row.Status ?? 1),
    notifyCaregiver: Boolean(row.notifyCaregiver ?? row.NotifyCaregiver ?? false),
  };
}

export async function fetchFamilyMembers() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/family',
  );
  const rows = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rows.map(normalizeFamilyMember);
}

export async function createFamilyMember(payload: {
  fullName: string;
  relationship: string;
  phone?: string;
  dateOfBirth?: string;
  notes?: string;
  notifyCaregiver?: boolean;
}) {
  const { data } = await http.post<Record<string, unknown>>('/family', payload);
  return normalizeFamilyMember(data);
}

export async function updateFamilyMember(
  id: string,
  payload: {
    fullName: string;
    relationship: string;
    phone?: string;
    dateOfBirth?: string;
    notes?: string;
    status: number;
    notifyCaregiver?: boolean;
  },
) {
  const { data } = await http.put<Record<string, unknown>>(`/family/${id}`, payload);
  return normalizeFamilyMember(data);
}

export async function setFamilyNotifyCaregiver(id: string, notifyCaregiver: boolean) {
  const { data } = await http.patch<Record<string, unknown>>(`/family/${id}/notify-caregiver`, {
    notifyCaregiver,
  });
  return normalizeFamilyMember(data);
}

export async function deleteFamilyMember(id: string) {
  await http.delete(`/family/${id}`);
}

function normalizeHealthRecord(row: Record<string, unknown>) {
  const attachmentsRaw = row.attachmentsJson ?? row.AttachmentsJson ?? '[]';
  let attachments: { fileName: string; mimeType: string; url?: string; dataUrl?: string }[] = [];
  try {
    attachments = JSON.parse(String(attachmentsRaw)) as typeof attachments;
  } catch {
    attachments = [];
  }
  attachments = attachments.map((att) => ({
    fileName: String(att.fileName ?? (att as { name?: string }).name ?? 'file'),
    mimeType: String(att.mimeType ?? (att as { mime?: string }).mime ?? 'application/octet-stream'),
    url: att.url ?? (att as { Url?: string }).Url,
    dataUrl: att.dataUrl,
  }));
  return {
    id: String(row.id ?? row.Id),
    familyMemberId: (row.familyMemberId ?? row.FamilyMemberId ?? null) as string | null,
    recordType: String(row.recordType ?? row.RecordType ?? 'note'),
    title: String(row.title ?? row.Title ?? ''),
    summary: (row.summary ?? row.Summary ?? null) as string | null,
    providerName: (row.providerName ?? row.ProviderName ?? null) as string | null,
    recordedAt: String(row.recordedAt ?? row.RecordedAt ?? ''),
    attachmentsJson: String(attachmentsRaw),
    metadataJson: String(row.metadataJson ?? row.MetadataJson ?? '{}'),
    attachments,
  };
}

export async function fetchHealthRecords() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/health-records',
  );
  const rows = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rows.map(normalizeHealthRecord);
}

export async function createHealthRecord(payload: {
  familyMemberId?: string;
  recordType: string;
  title: string;
  summary?: string;
  providerName?: string;
  recordedAt: string;
  attachmentsJson?: string;
  metadataJson?: string;
}) {
  const { data } = await http.post<Record<string, unknown>>('/health-records', payload);
  return normalizeHealthRecord(data);
}

export async function deleteHealthRecord(id: string) {
  await http.delete(`/health-records/${id}`);
}

export async function updateHealthRecord(
  id: string,
  payload: {
    familyMemberId?: string | null;
    recordType: string;
    title: string;
    summary?: string | null;
    providerName?: string | null;
    recordedAt: string;
    attachmentsJson?: string;
    metadataJson?: string;
  },
) {
  const { data } = await http.put<Record<string, unknown>>(`/health-records/${id}`, payload);
  return normalizeHealthRecord(data);
}

export async function uploadHealthRecordAttachment(file: File) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await http.post<{
    url: string;
    fileName: string;
    mimeType: string;
  }>('/health-records/upload-attachment', form);
  return {
    url: String(data.url ?? ''),
    fileName: String(data.fileName ?? file.name),
    mimeType: String(data.mimeType ?? (file.type || 'application/octet-stream')),
  };
}

function normalizeCareReminder(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    familyMemberId: (row.familyMemberId ?? row.FamilyMemberId ?? null) as string | null,
    healthRecordId: (row.healthRecordId ?? row.HealthRecordId ?? null) as string | null,
    reminderType: String(row.reminderType ?? row.ReminderType ?? 'visit'),
    title: String(row.title ?? row.Title ?? ''),
    note: (row.note ?? row.Note ?? null) as string | null,
    remindAt: String(row.remindAt ?? row.RemindAt ?? ''),
    isDone: Boolean(row.isDone ?? row.IsDone ?? false),
  };
}

export async function fetchCareReminders(includeDone = false) {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/care-reminders',
    { params: { includeDone } },
  );
  const rows = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rows.map(normalizeCareReminder);
}

export async function createCareReminder(payload: {
  familyMemberId?: string;
  healthRecordId?: string;
  reminderType: string;
  title: string;
  note?: string;
  remindAt: string;
}) {
  const { data } = await http.post<Record<string, unknown>>('/care-reminders', payload);
  return normalizeCareReminder(data);
}

export async function markCareReminderDone(reminder: {
  id: string;
  familyMemberId: string | null;
  healthRecordId: string | null;
  reminderType: string;
  title: string;
  note: string | null;
  remindAt: string;
}) {
  const { data } = await http.put<Record<string, unknown>>(`/care-reminders/${reminder.id}`, {
    familyMemberId: reminder.familyMemberId,
    healthRecordId: reminder.healthRecordId,
    reminderType: reminder.reminderType,
    title: reminder.title,
    note: reminder.note,
    remindAt: reminder.remindAt,
    isDone: true,
    snoozedUntil: null,
  });
  return normalizeCareReminder(data);
}

function normalizeTimelineEvent(row: Record<string, unknown>) {
  return {
    eventType: String(row.eventType ?? row.EventType ?? ''),
    occurredAt: String(row.occurredAt ?? row.OccurredAt ?? ''),
    label: String(row.label ?? row.Label ?? ''),
  };
}

function normalizeActiveMedication(row: Record<string, unknown>) {
  const timeline = (row.timeline ?? row.Timeline ?? []) as Record<string, unknown>[];
  return {
    productId: String(row.productId ?? row.ProductId ?? ''),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    dosageNote: (row.dosageNote ?? row.DosageNote ?? null) as string | null,
    familyMemberId: (row.familyMemberId ?? row.FamilyMemberId ?? null) as string | null,
    medicationReminderId: (row.medicationReminderId ?? row.MedicationReminderId ?? null) as string | null,
    remindTime: (row.remindTime ?? row.RemindTime ?? null) as string | null,
    daysRemaining: row.daysRemaining != null || row.DaysRemaining != null
      ? Number(row.daysRemaining ?? row.DaysRemaining)
      : null,
    supplyEndDate: (row.supplyEndDate ?? row.SupplyEndDate ?? null) as string | null,
    lastOrderNumber: (row.lastOrderNumber ?? row.LastOrderNumber ?? null) as string | null,
    lastOrderDate: (row.lastOrderDate ?? row.LastOrderDate ?? null) as string | null,
    repurchaseSuggestionCount: Number(row.repurchaseSuggestionCount ?? row.RepurchaseSuggestionCount ?? 0),
    timeline: timeline.map(normalizeTimelineEvent),
  };
}

export async function fetchActiveMedications(params?: { familyMemberId?: string; forSelf?: boolean }) {
  const query: Record<string, string | boolean> = {};
  if (params?.familyMemberId) query.familyMemberId = params.familyMemberId;
  if (params?.forSelf) query.forSelf = true;
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/active-medications',
    { params: query },
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizeActiveMedication);
}

export async function fetchMedicationAdherenceSummary() {
  const { data } = await http.get<Record<string, unknown>>('/medication-adherence/summary');
  return {
    dueCount: Number(data.dueCount ?? data.DueCount ?? 0),
    takenToday: Number(data.takenToday ?? data.TakenToday ?? 0),
    skippedToday: Number(data.skippedToday ?? data.SkippedToday ?? 0),
    scheduledToday: Number(data.scheduledToday ?? data.ScheduledToday ?? 0),
    missedStreakDays: Number(data.missedStreakDays ?? data.MissedStreakDays ?? 0),
    showMissedAlert: Boolean(data.showMissedAlert ?? data.ShowMissedAlert ?? false),
  };
}

export async function fetchDueReminders() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/medication-adherence/due',
  );
  const rows = data.items ?? data.Items ?? [];
  return normalizeReminderList({ items: rows });
}

export async function fetchFamilyDueReminders() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/medication-adherence/family-due',
  );
  const rows = data.items ?? data.Items ?? [];
  return normalizeReminderList({ items: rows });
}

export async function respondMedicationReminder(
  reminderId: string,
  action: 'taken' | 'skipped' | 'snooze',
  snoozeMinutes?: number,
) {
  const { data } = await http.post<Record<string, unknown>>(
    `/medication-adherence/reminders/${reminderId}/respond`,
    { action, snoozeMinutes: snoozeMinutes ?? null },
  );
  return normalizeReminder(data);
}

function normalizeServerNotification(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    category: String(row.category ?? row.Category ?? 'general'),
    title: String(row.title ?? row.Title ?? ''),
    body: String(row.body ?? row.Body ?? ''),
    href: (row.href ?? row.Href ?? null) as string | null,
    readAt: (row.readAt ?? row.ReadAt ?? null) as string | null,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    isRead: Boolean(row.isRead ?? row.IsRead ?? false),
  };
}

export async function fetchServerNotifications(limit = 50) {
  const { data } = await http.get<{
    items?: Record<string, unknown>[];
    Items?: Record<string, unknown>[];
    unreadCount?: number;
    UnreadCount?: number;
  }>('/notifications', { params: { limit } });
  const rows = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    items: rows.map(normalizeServerNotification),
    unreadCount: Number(data.unreadCount ?? data.UnreadCount ?? 0),
  };
}

export async function markServerNotificationRead(id: string) {
  await http.post(`/notifications/${id}/read`);
}

export async function markAllServerNotificationsRead() {
  await http.post('/notifications/read-all');
}

export async function askAiHealth(question: string, productId?: string) {
  const { data } = await http.post<Record<string, unknown>>('/ai-health/ask', {
    question,
    productId: productId ?? null,
  });
  return {
    answer: String(data.answer ?? data.Answer ?? ''),
    confidence: String(data.confidence ?? data.Confidence ?? 'low'),
    suggestChat: Boolean(data.suggestChat ?? data.SuggestChat ?? false),
    disclaimer: String(data.disclaimer ?? data.Disclaimer ?? ''),
  };
}

function listItems(data: Record<string, unknown>): Record<string, unknown>[] {
  return (data.items ?? data.Items ?? []) as Record<string, unknown>[];
}

/** Gộp 4 API Home — 1 round-trip. */
export async function fetchHomeSummary() {
  const { data } = await http.get<Record<string, unknown>>('/overview/home-summary');
  const loyaltyRaw = (data.loyalty ?? data.Loyalty) as Record<string, unknown> | null | undefined;
  const programsRaw = (loyaltyRaw?.programs ?? loyaltyRaw?.Programs ?? []) as Record<string, unknown>[];
  const connectRaw = (data.connectInbox ?? data.ConnectInbox) as Record<string, unknown> | null | undefined;

  return {
    loyalty: {
      programs: programsRaw.map((p) => ({
        programId: String(p.programId ?? p.ProgramId ?? ''),
        programName: String(p.programName ?? p.ProgramName ?? ''),
        pointsBalance: Number(p.pointsBalance ?? p.PointsBalance ?? 0),
      })),
    } as LoyaltySummary,
    draftOrders: listItems((data.draftOrders ?? data.DraftOrders ?? {}) as Record<string, unknown>).map(
      normalizeDraftOrderListItem,
    ),
    repurchaseSuggestions: listItems(
      (data.repurchaseSuggestions ?? data.RepurchaseSuggestions ?? {}) as Record<string, unknown>,
    ).map(normalizeRepurchase),
    adherence: (() => {
      const adherenceRaw = (data.adherence ?? data.Adherence) as Record<string, unknown> | undefined;
      return {
      dueCount: Number(adherenceRaw?.dueCount ?? adherenceRaw?.DueCount ?? 0),
      takenToday: Number(adherenceRaw?.takenToday ?? adherenceRaw?.TakenToday ?? 0),
      skippedToday: Number(adherenceRaw?.skippedToday ?? adherenceRaw?.SkippedToday ?? 0),
      scheduledToday: Number(adherenceRaw?.scheduledToday ?? adherenceRaw?.ScheduledToday ?? 0),
      missedStreakDays: Number(adherenceRaw?.missedStreakDays ?? adherenceRaw?.MissedStreakDays ?? 0),
      showMissedAlert: Boolean(adherenceRaw?.showMissedAlert ?? adherenceRaw?.ShowMissedAlert ?? false),
      };
    })(),
    connectInbox: normalizeConnectInbox(connectRaw),
  };
}

export type CustomerConnectInboxItem = {
  kind: string;
  sourceId: string;
  detail?: string;
  clinicName?: string;
  occurredAt?: string;
  scheduledAt?: string;
  prescriptionCode?: string;
  ctaKey: string;
};

export type CustomerConnectInbox = {
  connectEnabled: boolean;
  items: CustomerConnectInboxItem[];
};

function normalizeConnectInbox(raw: Record<string, unknown> | null | undefined): CustomerConnectInbox {
  if (!raw) return { connectEnabled: false, items: [] };
  const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
  return {
    connectEnabled: Boolean(raw.connectEnabled ?? raw.ConnectEnabled ?? false),
    items: itemsRaw.map((row) => ({
      kind: String(row.kind ?? row.Kind ?? ''),
      sourceId: String(row.sourceId ?? row.SourceId ?? ''),
      detail: (row.detail ?? row.Detail) as string | undefined,
      clinicName: (row.clinicName ?? row.ClinicName) as string | undefined,
      occurredAt: (row.occurredAt ?? row.OccurredAt) as string | undefined,
      scheduledAt: (row.scheduledAt ?? row.ScheduledAt) as string | undefined,
      prescriptionCode: (row.prescriptionCode ?? row.PrescriptionCode) as string | undefined,
      ctaKey: String(row.ctaKey ?? row.CtaKey ?? 'info'),
    })),
  };
}

/** Connect inbox riêng (Option 1) — khi cần refresh độc lập. */
export async function fetchConnectInbox(): Promise<CustomerConnectInbox> {
  const { data } = await http.get<Record<string, unknown>>('/connect/inbox');
  return normalizeConnectInbox(data);
}

/** Gộp 3 API tab Đơn hàng — 1 round-trip. */
export async function fetchOrdersOverview() {
  const { data } = await http.get<Record<string, unknown>>('/overview/orders');
  const draftsRaw = (data.draftOrders ?? data.DraftOrders) as Record<string, unknown> | undefined;
  const purchasesRaw = (data.purchases ?? data.Purchases) as Record<string, unknown> | undefined;
  const reservationsRaw = (data.reservations ?? data.Reservations) as Record<string, unknown> | null | undefined;

  return {
    draftOrders: listItems(draftsRaw ?? {}).map(normalizeDraftOrderListItem),
    purchases: listItems(purchasesRaw ?? {}).map(normalizePurchaseListItem),
    reservations: reservationsRaw ? listItems(reservationsRaw).map(normalizeReservationListItem) : [],
    hasReservationsModule: reservationsRaw != null,
  };
}

export type RemindersOverview = {
  reminders: MedicationReminder[];
  adherence: {
    dueCount: number;
    takenToday: number;
    skippedToday: number;
    scheduledToday: number;
    missedStreakDays: number;
    showMissedAlert: boolean;
  };
  dueReminders: MedicationReminder[];
  repurchaseSuggestions: RepurchaseSuggestion[];
  familyMembers: FamilyMember[];
};

/** Gộp 5 API tab Nhắc thuốc — 1 round-trip. */
export async function fetchRemindersOverview(): Promise<RemindersOverview> {
  const { data } = await http.get<Record<string, unknown>>('/overview/reminders');
  const remindersRaw = (data.reminders ?? data.Reminders) as Record<string, unknown> | undefined;
  const dueRaw = (data.due ?? data.Due) as Record<string, unknown> | undefined;
  const repurchaseRaw = (data.repurchaseSuggestions ?? data.RepurchaseSuggestions) as
    | Record<string, unknown>
    | undefined;
  const familyRaw = (data.family ?? data.Family) as Record<string, unknown> | undefined;
  const adherenceRaw = (data.adherence ?? data.Adherence) as Record<string, unknown> | undefined;

  return {
    reminders: normalizeReminderList(remindersRaw ?? {}),
    adherence: {
      dueCount: Number(adherenceRaw?.dueCount ?? adherenceRaw?.DueCount ?? 0),
      takenToday: Number(adherenceRaw?.takenToday ?? adherenceRaw?.TakenToday ?? 0),
      skippedToday: Number(adherenceRaw?.skippedToday ?? adherenceRaw?.SkippedToday ?? 0),
      scheduledToday: Number(adherenceRaw?.scheduledToday ?? adherenceRaw?.ScheduledToday ?? 0),
      missedStreakDays: Number(adherenceRaw?.missedStreakDays ?? adherenceRaw?.MissedStreakDays ?? 0),
      showMissedAlert: Boolean(adherenceRaw?.showMissedAlert ?? adherenceRaw?.ShowMissedAlert ?? false),
    },
    dueReminders: normalizeReminderList(dueRaw ?? {}),
    repurchaseSuggestions: listItems(repurchaseRaw ?? {}).map(normalizeRepurchase),
    familyMembers: listItems(familyRaw ?? {}).map(normalizeFamilyMember),
  };
}

export type ChatOverview = {
  consents: CustomerConsent[];
  messages: CustomerChatMessage[];
  hasMore: boolean;
  thread: CustomerChatThread;
  chatConsentGranted: boolean;
};

/** Gộp consents + messages + mark-read — 1 round-trip. */
export async function fetchChatOverview(): Promise<ChatOverview> {
  const { data } = await http.get<Record<string, unknown>>('/overview/chat');
  const consentsRaw = (data.consents ?? data.Consents ?? []) as Record<string, unknown>[];
  const messagesRaw = (data.messages ?? data.Messages) as Record<string, unknown> | undefined;
  const threadRaw = (data.thread ?? data.Thread) as Record<string, unknown> | undefined;
  const consents = consentsRaw.map(normalizeConsent);
  const chatConsent = consents.find(
    (c) =>
      c.channel === CUSTOMER_APP_CHAT_CONSENT.channel &&
      c.purpose === CUSTOMER_APP_CHAT_CONSENT.purpose,
  );

  return {
    consents,
    messages: listItems(messagesRaw ?? {}).map((row) => normalizeChatMessage(row)),
    hasMore: Boolean(messagesRaw?.hasMore ?? messagesRaw?.HasMore),
    thread: {
      threadId: String(threadRaw?.threadId ?? threadRaw?.ThreadId ?? ''),
      unreadCount: Number(threadRaw?.unreadCount ?? threadRaw?.UnreadCount ?? 0),
      lastMessageAt: (threadRaw?.lastMessageAt ?? threadRaw?.LastMessageAt) as string | null,
      lastMessagePreview: (threadRaw?.lastMessagePreview ?? threadRaw?.LastMessagePreview) as string | null,
    },
    chatConsentGranted: chatConsent?.granted ?? false,
  };
}

export function getApiErrorMessage(error: unknown, fallback?: string) {
  const generic = fallback ?? i18n.t('errors.generic');
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return apiOfflineHint();
    }
    const data = error.response.data;
    if (typeof data === 'string' && data.trim()) return data;
    if (data && typeof data === 'object' && 'message' in data) {
      const msg = String((data as { message?: string }).message ?? '');
      if (msg.trim()) return msg;
    }
    if (error.response.status === 500) {
      return i18n.t('errors.apiServerError');
    }
    if (error.response.status === 502 || error.response.status === 503) {
      return apiOfflineHint();
    }
    return i18n.t('errors.httpStatus', { fallback: generic, status: error.response.status });
  }
  if (error instanceof Error && error.message) {
    if (/applicationServerKey is not valid/i.test(error.message)) {
      return i18n.t('errors.vapidInvalid');
    }
    return error.message;
  }
  return generic;
}

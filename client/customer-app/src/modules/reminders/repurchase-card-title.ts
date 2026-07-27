import dayjs from 'dayjs';
import type { RepurchaseSuggestion } from '@/shared/api/customer-app.types';

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** Uniform card title: keep disease/label when present; avoid duplicating order number. */
export function repurchaseCardTitle(
  item: RepurchaseSuggestion,
  labels: { orderLine: string; datedTitle: string },
): string {
  const label = (item.orderLabel ?? '').trim();
  const orderNum = (item.orderNumber ?? '').trim();
  if (!label) return labels.datedTitle || labels.orderLine;

  const redundant =
    label === orderNum ||
    label === labels.orderLine ||
    new RegExp(`^đơn\\s*${escapeRegExp(orderNum)}$`, 'i').test(label) ||
    new RegExp(`^order\\s*${escapeRegExp(orderNum)}$`, 'i').test(label);

  if (redundant) return labels.datedTitle || labels.orderLine;
  return label;
}

export function repurchaseDatedTitle(
  item: RepurchaseSuggestion,
  format: (date: string) => string,
): string {
  return item.orderDate ? format(dayjs(item.orderDate).format('DD/MM/YYYY')) : '';
}

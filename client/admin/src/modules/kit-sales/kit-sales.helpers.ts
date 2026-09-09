import dayjs from 'dayjs';
import type { KitSalesJourneyPlan, KitSalesLead } from '@/shared/api/kit-sales.api';

export function formatKitSalesDate(value?: string | null) {
  if (!value) return '—';
  const d = dayjs(value);
  return d.isValid() ? d.format('DD/MM/YYYY HH:mm') : String(value).slice(0, 16);
}

export function kitSalesTemperatureColor(code: string) {
  if (code === 'hot') return 'red';
  if (code === 'warm') return 'orange';
  return 'default';
}

export type KitSalesTodayBucket = 'due' | 'followup' | 'phc' | 'demo';

export function classifyKitSalesToday(
  leads: KitSalesLead[],
  due: KitSalesJourneyPlan[],
) {
  const now = dayjs();
  const dueIds = new Set(due.map((row) => row.leadId));
  const byId = new Map(leads.map((row) => [row.id, row]));

  const dueLeads = [
    ...due.map((row) => byId.get(row.leadId)).filter((row): row is KitSalesLead => Boolean(row)),
    ...leads.filter(
      (row) =>
        !dueIds.has(row.id) &&
        row.nextActionAt &&
        dayjs(row.nextActionAt).isBefore(now.add(20, 'minute')),
    ),
  ].filter((row, index, all) => all.findIndex((item) => item.id === row.id) === index);

  const dueSet = new Set(dueLeads.map((row) => row.id));
  const followup = leads.filter(
    (row) =>
      !dueSet.has(row.id) &&
      (row.nextActionCode === 'follow_up' ||
        row.nextActionCode?.includes('follow_up') ||
        (row.nextActionAt != null && dayjs(row.nextActionAt).isAfter(now))),
  );
  const phc = leads.filter(
    (row) =>
      row.leadStatus === 'phc' ||
      row.nextActionCode === 'phc' ||
      row.nextActionCode?.startsWith('phc_ask'),
  );
  const demo = leads.filter((row) => row.leadStatus === 'demo');
  const recent = [...leads]
    .sort((a, b) => dayjs(b.updatedAt).valueOf() - dayjs(a.updatedAt).valueOf())
    .slice(0, 6);

  return { due: dueLeads, followup, phc, demo, recent };
}

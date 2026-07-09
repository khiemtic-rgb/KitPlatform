import { http } from '@/shared/api/http';

export type DispensingNote = {
  id: string;
  salesOrderId: string;
  customerId?: string;
  noteType: string;
  noteText?: string;
  createdAt: string;
};

export type CreateDispensingNotePayload = {
  salesOrderId: string;
  customerId?: string;
  noteType?: string;
  noteText?: string;
};

function normalizeNote(row: Record<string, unknown>): DispensingNote {
  return {
    id: String(row.id ?? row.Id),
    salesOrderId: String(row.salesOrderId ?? row.SalesOrderId),
    customerId: (row.customerId ?? row.CustomerId) as string | undefined,
    noteType: String(row.noteType ?? row.NoteType ?? 'counseling'),
    noteText: (row.noteText ?? row.NoteText) as string | undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt),
  };
}

export async function fetchDispensingNotes(salesOrderId: string): Promise<DispensingNote[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/pharmacy/dispensing-notes', {
    params: { salesOrderId },
  });
  return (data ?? []).map((row) => normalizeNote(row));
}

export async function createDispensingNote(
  payload: CreateDispensingNotePayload,
): Promise<DispensingNote> {
  const { data } = await http.post<Record<string, unknown>>('/pharmacy/dispensing-notes', {
    salesOrderId: payload.salesOrderId,
    customerId: payload.customerId,
    noteType: payload.noteType ?? 'counseling',
    noteText: payload.noteText,
  });
  return normalizeNote(data);
}

export const DISPENSING_NOTE_TYPES = [
  { value: 'counseling', labelKey: 'counseling' },
  { value: 'dispensing', labelKey: 'dispensing' },
  { value: 'interaction', labelKey: 'interaction' },
  { value: 'adherence', labelKey: 'adherence' },
  { value: 'other', labelKey: 'other' },
] as const;

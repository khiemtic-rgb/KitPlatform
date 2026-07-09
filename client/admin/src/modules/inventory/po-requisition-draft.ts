const STORAGE_KEY = 'novixa:po-requisition-draft';

export type PoRequisitionLine = {
  productId: string;
  productCode: string;
  productName: string;
  warehouseId: string;
  warehouseName: string;
  suggestedQty: number;
};

export type PoRequisitionDraft = {
  warehouseId?: string;
  notes?: string;
  lines: PoRequisitionLine[];
  createdAt: string;
};

export function savePoRequisitionDraft(draft: PoRequisitionDraft): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
}

export function readPoRequisitionDraft(): PoRequisitionDraft | null {
  const raw = sessionStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as PoRequisitionDraft;
  } catch {
    return null;
  }
}

export function clearPoRequisitionDraft(): void {
  sessionStorage.removeItem(STORAGE_KEY);
}

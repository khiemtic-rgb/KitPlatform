/** Supplier list helpers. PO/GRN filtering is server-side (do not reintroduce client filters). */
export function filterSuppliersById<T extends { id: string }>(items: T[], supplierId?: string): T[] {
  if (!supplierId) return items;
  return items.filter((row) => row.id === supplierId);
}

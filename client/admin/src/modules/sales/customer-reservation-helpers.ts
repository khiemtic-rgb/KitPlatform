import { lookupPosProduct } from '@/shared/api/sales.api';
import type { CustomerReservationPosLoad } from '@/shared/api/customer-reservations.api';
import type { CartLine } from '@/shared/api/sales.types';

export async function loadCustomerReservationCartLines(
  payload: CustomerReservationPosLoad,
): Promise<CartLine[]> {
  const lines: CartLine[] = [];
  for (const item of payload.lines) {
    let stockAvailable = 0;
    let unitPrice = 0;
    try {
      const lookup = await lookupPosProduct(item.productCode, payload.warehouseId);
      stockAvailable = lookup.stockAvailable;
      unitPrice = lookup.unitPrice;
    } catch {
      stockAvailable = 0;
      unitPrice = 0;
    }
    lines.push({
      key: item.productUnitId,
      productId: item.productId,
      productCode: item.productCode,
      productName: item.productName,
      productUnitId: item.productUnitId,
      unitName: item.unitName,
      quantity: item.quantity,
      unitPrice,
      stockAvailable,
    });
  }
  return lines;
}

import { http } from './http';
import type { CustomerPosContextDto } from './pos-customer-context.types';

export async function fetchPosCustomerContext(
  customerId: string,
  warehouseId: string,
): Promise<CustomerPosContextDto> {
  const { data } = await http.get<CustomerPosContextDto>(`/sales/pos/customer-context/${customerId}`, {
    params: { warehouseId },
  });
  return data;
}

/** Cấu hình header phiếu in — sau này thay bằng API cài đặt tenant. */
export type ReceiptStoreSettings = {
  name: string;
  tagline?: string;
  phone?: string;
  address?: string;
};

const DEFAULT_RECEIPT_STORE: ReceiptStoreSettings = {
  name: 'NHÀ THUỐC NOVIXA',
  tagline: 'Chăm sóc sức khỏe cộng đồng',
  phone: '0984.660.399',
  address: '',
};

export function getReceiptStoreSettings(): ReceiptStoreSettings {
  return DEFAULT_RECEIPT_STORE;
}

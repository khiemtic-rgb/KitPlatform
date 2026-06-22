import { fetchReceiptSettings } from '@/shared/api/sales.api';
import type { ReceiptStoreSettings } from '@/shared/api/sales.types';

export type { ReceiptStoreSettings };

const DEFAULT_RECEIPT_STORE: ReceiptStoreSettings = {
  name: 'NHÀ THUỐC NOVIXA',
  tagline: 'Chăm sóc sức khỏe cộng đồng',
  phone: '0984.660.399',
  address: '',
};

let cachedSettings: ReceiptStoreSettings | null = null;

export function clearReceiptSettingsCache() {
  cachedSettings = null;
}

/** Sync fallback when API chưa load. */
export function getReceiptStoreSettings(): ReceiptStoreSettings {
  return cachedSettings ?? DEFAULT_RECEIPT_STORE;
}

export async function loadReceiptStoreSettings(force = false): Promise<ReceiptStoreSettings> {
  if (cachedSettings && !force) return cachedSettings;
  try {
    cachedSettings = await fetchReceiptSettings();
    if (!cachedSettings.name.trim()) {
      cachedSettings = DEFAULT_RECEIPT_STORE;
    }
  } catch {
    cachedSettings = DEFAULT_RECEIPT_STORE;
  }
  return cachedSettings;
}

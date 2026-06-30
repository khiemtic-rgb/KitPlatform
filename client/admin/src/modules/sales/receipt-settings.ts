import { fetchReceiptSettings } from '@/shared/api/sales.api';
import { salesT } from '@/shared/i18n';
import type { ReceiptStoreSettings } from '@/shared/api/sales.types';

export type { ReceiptStoreSettings };

function defaultReceiptStore(): ReceiptStoreSettings {
  const t = salesT();
  return {
    name: t('receiptSettings.defaults.storeName'),
    tagline: t('receiptSettings.defaults.tagline'),
    phone: t('receiptSettings.defaults.phone'),
    address: '',
  };
}

let cachedSettings: ReceiptStoreSettings | null = null;

export function clearReceiptSettingsCache() {
  cachedSettings = null;
}

/** Sync fallback when API chưa load. */
export function getReceiptStoreSettings(): ReceiptStoreSettings {
  return cachedSettings ?? defaultReceiptStore();
}

export async function loadReceiptStoreSettings(force = false): Promise<ReceiptStoreSettings> {
  if (cachedSettings && !force) return cachedSettings;
  try {
    cachedSettings = await fetchReceiptSettings();
    if (!cachedSettings.name.trim()) {
      cachedSettings = defaultReceiptStore();
    }
  } catch {
    cachedSettings = defaultReceiptStore();
  }
  return cachedSettings;
}

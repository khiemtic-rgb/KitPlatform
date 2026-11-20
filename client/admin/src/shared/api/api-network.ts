import { resolveApiOrigin } from '@/shared/api/api-base';

/** URL API hiển thị cho người dùng (dev proxy hoặc production). */
export function apiNetworkLabel(): string {
  return resolveApiOrigin() || 'http://localhost:5290';
}

export function isLocalApiOrigin(origin = resolveApiOrigin()): boolean {
  if (!origin) return true;
  try {
    const host = new URL(origin).hostname.toLowerCase();
    return host === 'localhost' || host === '127.0.0.1' || host === '::1';
  } catch {
    return true;
  }
}

export function isProductionApi(): boolean {
  const origin = resolveApiOrigin();
  if (!origin) return false;
  return !isLocalApiOrigin(origin);
}

export function apiOfflineMessage(): string {
  if (!isProductionApi()) {
    return 'Không kết nối được API (localhost:5290). Trong terminal Content: . .\\scripts\\api-dev.ps1 rồi Ensure-ApiDev. Không dùng restart-api.ps1.';
  }
  return `Không kết nối được ${apiNetworkLabel()}. Thử lại sau vài giây hoặc restart dịch vụ API trên VPS.`;
}

export function apiHealthBannerMessage(): string {
  if (!isProductionApi()) {
    return 'API backend không phản hồi (port 5290)';
  }
  return `API backend không phản hồi (${apiNetworkLabel()})`;
}

export function apiHealthBannerDescription(): string {
  if (!isProductionApi()) {
    return 'API port 5290 chưa phản hồi. Chạy .\\scripts\\api-dev.ps1 → Ensure-ApiDev (Content). Không dùng restart-api.ps1.';
  }
  return 'API có thể đang khởi động lại sau deploy. Thử "Kiểm tra lại" hoặc trên VPS: systemctl restart kit-platform-api.';
}

export function apiServerErrorMessage(): string {
  return 'API đang bảo trì hoặc khởi động lại. Thử lại sau vài giây.';
}

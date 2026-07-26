const LOCAL_BUILD = import.meta.env.VITE_APP_BUILD ?? '';

/** So khớp /version.json trên server — ép reload nếu còn bundle cũ. */
export async function enforceLatestAppBuild(): Promise<boolean> {
  if (!LOCAL_BUILD) return false;

  try {
    const res = await fetch(`/version.json?_=${Date.now()}`, { cache: 'no-store' });
    if (!res.ok) return false;
    const data = (await res.json()) as { build?: string };
    if (!data.build || data.build === LOCAL_BUILD) return false;

    if ('serviceWorker' in navigator) {
      const regs = await navigator.serviceWorker.getRegistrations();
      await Promise.all(regs.map((r) => r.unregister()));
    }
    if ('caches' in window) {
      const cacheKeys = await caches.keys();
      await Promise.all(cacheKeys.map((k) => caches.delete(k)));
    }
    window.location.reload();
    return true;
  } catch {
    return false;
  }
}

import i18n from '@/shared/i18n';
import { registerSW } from 'virtual:pwa-register';

let setupDone = false;
let readyPromise: Promise<ServiceWorkerRegistration> | null = null;
let needRefreshListeners = new Set<() => void>();
let updateSW: ((reloadPage?: boolean) => Promise<void>) | undefined;

function getServiceWorkerScriptUrl() {
  return import.meta.env.DEV ? '/dev-sw.js?dev-sw' : '/sw.js';
}

function scheduleIdle(task: () => void, timeoutMs = 2500) {
  const ric = window.requestIdleCallback?.bind(window);
  if (ric) {
    ric(() => task(), { timeout: timeoutMs });
    return;
  }
  window.setTimeout(task, Math.min(timeoutMs, 1200));
}

function createReadyPromise() {
  return new Promise<ServiceWorkerRegistration>((resolve, reject) => {
    let settled = false;
    const finish = (reg: ServiceWorkerRegistration) => {
      if (settled) return;
      settled = true;
      resolve(reg);
    };
    const fail = (error: unknown) => {
      if (settled) return;
      settled = true;
      reject(error instanceof Error ? error : new Error(i18n.t('push.swRegisterFailed')));
    };

    updateSW = registerSW({
      immediate: false,
      onRegisteredSW(_swUrl, registration) {
        if (registration?.active || registration?.installing || registration?.waiting) {
          finish(registration);
        } else {
          navigator.serviceWorker.ready.then(finish).catch(fail);
        }
        if (registration) {
          window.setInterval(() => {
            void registration.update();
          }, 60_000);
          document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible') void registration.update();
          });
        }
      },
      onNeedRefresh() {
        if (needRefreshListeners.size === 0) {
          // Chưa gắn UI — vẫn cập nhật nền; user reload lần sau sẽ nhận bản mới.
          return;
        }
        needRefreshListeners.forEach((listener) => listener());
      },
      onRegisterError(error) {
        fail(normalizeServiceWorkerError(error));
      },
    });

    window.setTimeout(() => {
      void (async () => {
        try {
          let registration = await navigator.serviceWorker.getRegistration('/');
          if (!registration) {
            registration = await navigator.serviceWorker.register(getServiceWorkerScriptUrl(), {
              scope: '/',
              type: 'module',
            });
          }
          finish(await navigator.serviceWorker.ready);
        } catch (error) {
          fail(normalizeServiceWorkerError(error));
        }
      })();
    }, 800);
  });
}

function startRegistration() {
  if (readyPromise) return;
  readyPromise = createReadyPromise();
}

/** Đăng ký SW sau load/idle — không tranh bandwidth với JS Home. */
export function setupServiceWorkerRegistration() {
  if (setupDone || !('serviceWorker' in navigator)) return;
  setupDone = true;

  const kickoff = () => scheduleIdle(startRegistration, 3000);

  if (document.readyState === 'complete') {
    kickoff();
  } else {
    window.addEventListener('load', kickoff, { once: true });
  }
}

/** Soft update: banner gọi applyPwaUpdate() thay vì hard reload ngay. */
export function subscribePwaNeedRefresh(listener: () => void) {
  needRefreshListeners.add(listener);
  return () => {
    needRefreshListeners.delete(listener);
  };
}

export function applyPwaUpdate() {
  void updateSW?.(true);
  window.setTimeout(() => {
    window.location.reload();
  }, 200);
}

export async function waitForServiceWorkerRegistration(timeoutMs = 20_000) {
  if (!('serviceWorker' in navigator)) {
    throw new Error(i18n.t('push.swUnsupported'));
  }

  if (!readyPromise) {
    setupDone = true;
    startRegistration();
  }

  const registration = await Promise.race([
    readyPromise!,
    new Promise<ServiceWorkerRegistration>((_, reject) => {
      window.setTimeout(() => reject(new Error(i18n.t('push.swNotReady'))), timeoutMs);
    }),
  ]);

  if (!registration.active) {
    return withTimeout(
      navigator.serviceWorker.ready,
      timeoutMs,
      i18n.t('push.swNotActive'),
    );
  }

  return registration;
}

function withTimeout<T>(promise: Promise<T>, ms: number, message: string): Promise<T> {
  return Promise.race([
    promise,
    new Promise<T>((_, reject) => {
      window.setTimeout(() => reject(new Error(message)), ms);
    }),
  ]);
}

function normalizeServiceWorkerError(error: unknown): Error {
  const text = error instanceof Error ? error.message : String(error);
  if (/ssl certificate error/i.test(text)) {
    return new Error(i18n.t('push.swSslError'));
  }
  return error instanceof Error ? error : new Error(text || i18n.t('push.swRegisterFailed'));
}

import { registerSW } from 'virtual:pwa-register';

let setupDone = false;

/** Đăng ký SW và tự reload khi có bản mới — tránh kẹt icon «Thêm vào màn hình chính». */
export function setupServiceWorkerUpdates() {
  if (setupDone || !('serviceWorker' in navigator)) return;
  setupDone = true;

  registerSW({
    immediate: true,
    onRegisteredSW(_swUrl, registration) {
      if (!registration) return;
      window.setInterval(() => {
        void registration.update();
      }, 60_000);
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') void registration.update();
      });
    },
    onNeedRefresh() {
      window.location.reload();
    },
  });
}

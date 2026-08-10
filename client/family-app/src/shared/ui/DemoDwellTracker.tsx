import { useEffect } from 'react';
import { heartbeatDemoHouseView } from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';

export const DEMO_SESSION_KEY = 'famixa.demo-session.v1';

/** While demoMode, heartbeat every 30s + on pagehide so admin can show dwell time. */
export function DemoDwellTracker() {
  const demoMode = useSessionStore((s) => s.demoMode);

  useEffect(() => {
    if (!demoMode) return;

    const readSession = () => {
      try {
        return sessionStorage.getItem(DEMO_SESSION_KEY)?.trim() || '';
      } catch {
        return '';
      }
    };

    const tick = () => {
      const sessionId = readSession();
      if (!sessionId) return;
      void heartbeatDemoHouseView(sessionId).catch(() => {
        /* best-effort */
      });
    };

    tick();
    const interval = window.setInterval(tick, 30_000);

    const onHide = () => {
      if (document.visibilityState === 'hidden') tick();
    };
    const onPageHide = () => tick();

    document.addEventListener('visibilitychange', onHide);
    window.addEventListener('pagehide', onPageHide);

    return () => {
      window.clearInterval(interval);
      document.removeEventListener('visibilitychange', onHide);
      window.removeEventListener('pagehide', onPageHide);
      tick();
    };
  }, [demoMode]);

  return null;
}

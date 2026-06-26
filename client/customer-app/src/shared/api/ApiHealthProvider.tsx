import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import axios from 'axios';
import { apiPath } from '@/shared/api/api-base';

const HEALTH_URL = apiPath('/api/health');
const POLL_ONLINE_MS = 15_000;
const POLL_OFFLINE_MS = 3_000;

type ApiHealthContextValue = {
  /** null = chưa kiểm tra lần đầu */
  online: boolean | null;
  checking: boolean;
  recheck: () => Promise<void>;
};

const ApiHealthContext = createContext<ApiHealthContextValue | null>(null);

export function ApiHealthProvider({ children }: { children: ReactNode }) {
  const [online, setOnline] = useState<boolean | null>(null);
  const [checking, setChecking] = useState(false);
  const onlineRef = useRef<boolean | null>(null);

  const recheck = useCallback(async () => {
    setChecking(true);
    try {
      const { data } = await axios.get<{ status?: string }>(HEALTH_URL, { timeout: 4000 });
      const next = data.status === 'ok';
      onlineRef.current = next;
      setOnline(next);
    } catch {
      onlineRef.current = false;
      setOnline(false);
    } finally {
      setChecking(false);
    }
  }, []);

  useEffect(() => {
    void recheck();
    const onVisible = () => {
      if (document.visibilityState === 'visible') void recheck();
    };
    document.addEventListener('visibilitychange', onVisible);
    const timer = window.setInterval(() => {
      if (document.visibilityState === 'visible') void recheck();
    }, POLL_ONLINE_MS);
    return () => {
      document.removeEventListener('visibilitychange', onVisible);
      window.clearInterval(timer);
    };
  }, [recheck]);

  useEffect(() => {
    if (online !== false) return;
    const fastTimer = window.setInterval(() => {
      if (document.visibilityState === 'visible') void recheck();
    }, POLL_OFFLINE_MS);
    return () => window.clearInterval(fastTimer);
  }, [online, recheck]);

  const value = useMemo(
    () => ({ online, checking, recheck }),
    [online, checking, recheck],
  );

  return <ApiHealthContext.Provider value={value}>{children}</ApiHealthContext.Provider>;
}

export function useApiHealth(): ApiHealthContextValue {
  const ctx = useContext(ApiHealthContext);
  if (!ctx) {
    throw new Error('useApiHealth must be used within ApiHealthProvider');
  }
  return ctx;
}

/** Gọi lại khi API vừa online trở lại sau khi offline. */
export function useRetryWhenApiOnline(onOnline: () => void | Promise<void>) {
  const { online } = useApiHealth();
  const wasOfflineRef = useRef(false);
  const onOnlineRef = useRef(onOnline);
  onOnlineRef.current = onOnline;

  useEffect(() => {
    if (online === false) {
      wasOfflineRef.current = true;
      return;
    }
    if (online === true && wasOfflineRef.current) {
      wasOfflineRef.current = false;
      void onOnlineRef.current();
    }
  }, [online]);
}

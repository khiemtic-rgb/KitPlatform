import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import { apiPath } from '@/shared/api/api-base';

const HEALTH_URL = apiPath('/api/health');
const POLL_ONLINE_MS = 15_000;
const POLL_OFFLINE_MS = 5_000;

export function useApiHealth() {
  const [online, setOnline] = useState(true);
  const [checking, setChecking] = useState(false);

  const recheck = useCallback(async () => {
    setChecking(true);
    try {
      const { data } = await axios.get<{ status?: string }>(HEALTH_URL, { timeout: 5000 });
      setOnline(data.status === 'ok');
    } catch {
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
    let timer = window.setInterval(() => {
      if (document.visibilityState === 'visible') void recheck();
    }, POLL_ONLINE_MS);
    return () => {
      document.removeEventListener('visibilitychange', onVisible);
      window.clearInterval(timer);
    };
  }, [recheck]);

  useEffect(() => {
    if (online) return;
    const fastTimer = window.setInterval(() => {
      if (document.visibilityState === 'visible') void recheck();
    }, POLL_OFFLINE_MS);
    return () => window.clearInterval(fastTimer);
  }, [online, recheck]);

  return { online, checking, recheck };
}

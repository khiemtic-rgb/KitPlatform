import { useEffect, useState, type ReactNode } from 'react';
import { Spin } from 'antd';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/shared/auth/auth.store';

function BootFallback() {
  const { t } = useTranslation('common', { keyPrefix: 'boot' });
  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: '#f5f5f5',
      }}
    >
      <Spin size="large" tip={t('loading')}>
        <div style={{ minHeight: 120, minWidth: 120 }} />
      </Spin>
    </div>
  );
}

export function AuthHydrationGate({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(() => useAuthStore.persist.hasHydrated());

  useEffect(() => {
    if (useAuthStore.persist.hasHydrated()) {
      setReady(true);
      return;
    }
    return useAuthStore.persist.onFinishHydration(() => setReady(true));
  }, []);

  if (!ready) {
    return <BootFallback />;
  }

  return children;
}

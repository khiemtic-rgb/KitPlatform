import { createContext, useCallback, useContext, type ReactNode } from 'react';
import { message } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/auth.store';

type VerifyAccountContextValue = {
  requireAuth: (intent?: string) => boolean;
};

const VerifyAccountContext = createContext<VerifyAccountContextValue | null>(null);

export function VerifyAccountProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  const requireAuth = useCallback(
    (intent?: string) => {
      if (isAuthenticated()) return true;
      message.info(intent?.trim() || 'Vui lòng đăng nhập để tiếp tục.');
      navigate('/login', { replace: false });
      return false;
    },
    [isAuthenticated, navigate],
  );

  return (
    <VerifyAccountContext.Provider value={{ requireAuth }}>{children}</VerifyAccountContext.Provider>
  );
}

export function useVerifyAccount() {
  const ctx = useContext(VerifyAccountContext);
  if (!ctx) {
    throw new Error('useVerifyAccount must be used within VerifyAccountProvider');
  }
  return ctx;
}

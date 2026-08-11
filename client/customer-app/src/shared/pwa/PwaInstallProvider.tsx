import { createContext, useContext, type ReactNode } from 'react';
import { usePwaInstall } from '@/shared/pwa/usePwaInstall';

type PwaInstallApi = ReturnType<typeof usePwaInstall>;

const PwaInstallContext = createContext<PwaInstallApi | null>(null);

export function PwaInstallProvider({ children }: { children: ReactNode }) {
  const api = usePwaInstall();
  return <PwaInstallContext.Provider value={api}>{children}</PwaInstallContext.Provider>;
}

export function usePwaInstallContext(): PwaInstallApi {
  const ctx = useContext(PwaInstallContext);
  if (!ctx) {
    throw new Error('usePwaInstallContext must be used within PwaInstallProvider');
  }
  return ctx;
}

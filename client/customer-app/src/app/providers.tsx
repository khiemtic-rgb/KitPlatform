import { QueryClientProvider } from '@tanstack/react-query';
import { App as AntApp } from 'antd';
import { AppRouter } from '@/app/router';
import { AppErrorBoundary } from '@/app/AppErrorBoundary';
import { AuthHydrationGate } from '@/shared/auth/AuthHydrationGate';
import { ApiHealthProvider } from '@/shared/api/ApiHealthProvider';
import { queryClient } from '@/shared/api/query-client';
import { BrandingProvider } from '@/shared/config/BrandingProvider';
import { LocaleProvider } from '@/shared/i18n/LocaleProvider';
import { PwaUpdateBanner } from '@/shared/pwa/PwaUpdateBanner';
import '@/shared/i18n';

export function AppProviders() {
  return (
    <AppErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <AntApp>
          <ApiHealthProvider>
            <BrandingProvider>
              <LocaleProvider>
                <AuthHydrationGate>
                  <AppRouter />
                  <PwaUpdateBanner />
                </AuthHydrationGate>
              </LocaleProvider>
            </BrandingProvider>
          </ApiHealthProvider>
        </AntApp>
      </QueryClientProvider>
    </AppErrorBoundary>
  );
}

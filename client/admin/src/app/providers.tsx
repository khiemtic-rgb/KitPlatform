import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { ConfigProvider, App as AntApp } from 'antd';
import viVN from 'antd/locale/vi_VN';
import enUS from 'antd/locale/en_US';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import 'dayjs/locale/en';
import { I18nextProvider, useTranslation } from 'react-i18next';
import { AppRouter } from '@/app/router';
import { AppErrorBoundary } from '@/app/AppErrorBoundary';
import { AuthHydrationGate } from '@/shared/auth/AuthHydrationGate';
import i18n from '@/shared/i18n';

const theme = {
  token: {
    colorPrimary: '#0d9488',
    borderRadius: 8,
  },
};

function AntdLocaleBridge({ children }: { children: ReactNode }) {
  const { i18n: i18nInstance } = useTranslation();
  const antdLocale = i18nInstance.language === 'en-US' ? enUS : viVN;

  useEffect(() => {
    dayjs.locale(i18nInstance.language === 'en-US' ? 'en' : 'vi');
  }, [i18nInstance.language]);

  return (
    <ConfigProvider locale={antdLocale} theme={theme}>
      <AntApp>{children}</AntApp>
    </ConfigProvider>
  );
}

export function AppProviders() {
  return (
    <AppErrorBoundary>
      <I18nextProvider i18n={i18n}>
        <AntdLocaleBridge>
          <AuthHydrationGate>
            <AppRouter />
          </AuthHydrationGate>
        </AntdLocaleBridge>
      </I18nextProvider>
    </AppErrorBoundary>
  );
}

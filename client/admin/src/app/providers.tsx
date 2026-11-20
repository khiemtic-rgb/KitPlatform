import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { ConfigProvider, App as AntApp, message } from 'antd';
import { deskToastContent } from '@/shared/api/api-error';
import viVN from 'antd/locale/vi_VN';
import enUS from 'antd/locale/en_US';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import 'dayjs/locale/en';
import { I18nextProvider, useTranslation } from 'react-i18next';
import { AppRouter } from '@/app/router';
import { AppErrorBoundary } from '@/app/AppErrorBoundary';
import { AuthHydrationGate } from '@/shared/auth/AuthHydrationGate';
import { TenantPlatformHydrator } from '@/shared/platform/TenantPlatformHydrator';
import i18n from '@/shared/i18n';

message.config({ maxCount: 2, duration: 4 });
const staticError = message.error.bind(message);
message.error = ((content: unknown, ...rest: unknown[]) => {
  const text = deskToastContent(content);
  if (content && typeof content === 'object' && 'content' in (content as object)) {
    return staticError({
      ...(content as { content?: unknown; key?: string }),
      content: text,
      key: (content as { key?: string }).key || 'famixa-desk-err',
    });
  }
  return staticError({ content: text, key: 'famixa-desk-err' }, ...(rest as []));
}) as typeof message.error;

const theme = {
  token: {
    colorPrimary: '#2563eb',
    colorSuccess: '#22c55e',
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
      <AntApp message={{ maxCount: 2 }}>{children}</AntApp>
    </ConfigProvider>
  );
}

export function AppProviders() {
  return (
    <AppErrorBoundary>
      <I18nextProvider i18n={i18n}>
        <AntdLocaleBridge>
          <AuthHydrationGate>
            <TenantPlatformHydrator />
            <AppRouter />
          </AuthHydrationGate>
        </AntdLocaleBridge>
      </I18nextProvider>
    </AppErrorBoundary>
  );
}

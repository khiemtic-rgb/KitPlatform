import { App as AntApp, ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { AppRouter } from '@/app/router';
import { AppErrorBoundary } from '@/app/AppErrorBoundary';
import { AuthHydrationGate } from '@/shared/auth/AuthHydrationGate';

dayjs.locale('vi');

const theme = {
  token: {
    colorPrimary: '#0d9488',
    borderRadius: 8,
  },
};

export function AppProviders() {
  return (
    <AppErrorBoundary>
      <ConfigProvider locale={viVN} theme={theme}>
        <AntApp>
          <AuthHydrationGate>
            <AppRouter />
          </AuthHydrationGate>
        </AntApp>
      </ConfigProvider>
    </AppErrorBoundary>
  );
}

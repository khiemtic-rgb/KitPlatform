import { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Outlet, Route, Routes, useNavigate } from 'react-router-dom';
import { App as AntApp, Button, ConfigProvider, Layout, Menu, Spin } from 'antd';
import viVN from 'antd/locale/vi_VN';
import 'antd/dist/reset.css';
import { useAuthStore } from '@/shared/auth/auth.store';
import { ModuleSubnavProvider, useModuleSubnavState } from '@/shared/components/module-subnav.context';
import { TenantPlatformHydrator } from '@/shared/platform/TenantPlatformHydrator';
import { TradingLayout } from '@/modules/second-brain/TradingLayout';
import { IntentWorkspacePage } from '@/modules/second-brain/IntentWorkspacePage';
import { TradingSessionPage } from '@/modules/second-brain/SessionPage';
import {
  TradingAgentPage,
  TradingImportPage,
  TradingJournalPage,
  TradingLedgerPage,
  TradingOverviewPage,
  TradingRiskPage,
} from '@/modules/second-brain/TradingPages';
import { TradingFuturesPage, TradingSpotPage } from '@/modules/second-brain/MarketPages';
import { TradingExchangeAccountPage, TradingExchangePage } from '@/modules/second-brain/ExchangePages';
import { LoginPage } from './LoginPage';

const areas = [
  { key: '/second-brain/session', label: 'Phiên làm việc' },
  { key: '/second-brain/agent', label: 'Agent' },
  { key: '/second-brain/overview', label: 'Tình hình' },
  { key: '/second-brain/ledger', label: 'Sổ giao dịch' },
  { key: '/second-brain/journal', label: 'Nhật ký' },
  { key: '/second-brain/risk', label: 'Rủi ro' },
];

function Gate({ children }: { children: React.ReactNode }) {
  const token = useAuthStore((state) => state.accessToken);
  const [ready, setReady] = useState(useAuthStore.persist.hasHydrated());
  useEffect(() => useAuthStore.persist.onFinishHydration(() => setReady(true)), []);
  if (!ready) return <Spin style={{ margin: 48 }} />;
  if (!token) return <Navigate to="/login" replace />;
  return children;
}

function Shell() {
  const navigate = useNavigate();
  const subnav = useModuleSubnavState();
  const clearSession = useAuthStore((state) => state.clearSession);
  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Layout.Header style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
        <span style={{ color: '#fff', fontWeight: 600 }}>Second Brain</span>
        <Menu
          theme="dark"
          mode="horizontal"
          selectable={false}
          items={areas.map((area) => ({ key: area.key, label: area.label }))}
          onClick={(event) => navigate(event.key)}
          style={{ flex: 1, minWidth: 0 }}
        />
        <Button onClick={() => clearSession()}>Thoát</Button>
      </Layout.Header>
      {subnav && subnav.tabs.length > 0 && (
        <Menu
          mode="horizontal"
          selectedKeys={[subnav.activeKey]}
          items={subnav.tabs.map((tab) => ({ key: tab.key, label: tab.label }))}
          onClick={(event) => subnav.onChange(event.key)}
        />
      )}
      <Layout.Content style={{ padding: 24 }}>
        <Outlet />
      </Layout.Content>
    </Layout>
  );
}

function App() {
  return (
    <ConfigProvider locale={viVN}>
      <AntApp>
        <ModuleSubnavProvider>
          <TenantPlatformHydrator />
          <BrowserRouter>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/second-brain" element={<Gate><TradingLayout /></Gate>}>
                <Route element={<Shell />}>
                  <Route index element={<Navigate to="session" replace />} />
                  <Route path="workspace" element={<IntentWorkspacePage />} />
                  <Route path="session" element={<TradingSessionPage />} />
                  <Route path="overview" element={<TradingOverviewPage />} />
                  <Route path="ledger" element={<TradingLedgerPage />} />
                  <Route path="spot" element={<TradingSpotPage />} />
                  <Route path="futures" element={<TradingFuturesPage />} />
                  <Route path="import" element={<TradingImportPage />} />
                  <Route path="journal" element={<TradingJournalPage />} />
                  <Route path="agent" element={<TradingAgentPage />} />
                  <Route path="risk" element={<TradingRiskPage />} />
                  <Route path="exchange" element={<TradingExchangePage />} />
                  <Route path="account" element={<TradingExchangeAccountPage />} />
                </Route>
              </Route>
              <Route path="*" element={<Navigate to="/second-brain/session" replace />} />
            </Routes>
          </BrowserRouter>
        </ModuleSubnavProvider>
      </AntApp>
    </ConfigProvider>
  );
}

createRoot(document.getElementById('root')!).render(<App />);

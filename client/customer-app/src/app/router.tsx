import { lazy, Suspense, type ReactNode } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { AuthGuard, GuestGuard } from '@/shared/auth/AuthGuard';

const CustomerAppLayout = lazy(() =>
  import('@/shared/components/CustomerAppLayout').then((m) => ({ default: m.CustomerAppLayout })),
);
const OtpLoginPage = lazy(() =>
  import('@/modules/auth/OtpLoginPage').then((m) => ({ default: m.OtpLoginPage })),
);
const ChatPage = lazy(() => import('@/modules/chat/ChatPage').then((m) => ({ default: m.ChatPage })));
const DraftOrdersPage = lazy(() =>
  import('@/modules/orders/DraftOrdersPage').then((m) => ({ default: m.DraftOrdersPage })),
);
const HomePage = lazy(() => import('@/modules/home/HomePage').then((m) => ({ default: m.HomePage })));
const LoyaltyPage = lazy(() =>
  import('@/modules/loyalty/LoyaltyPage').then((m) => ({ default: m.LoyaltyPage })),
);
const RemindersPage = lazy(() =>
  import('@/modules/reminders/RemindersPage').then((m) => ({ default: m.RemindersPage })),
);
const ProfilePage = lazy(() =>
  import('@/modules/profile/ProfilePage').then((m) => ({ default: m.ProfilePage })),
);
const NotificationsPage = lazy(() =>
  import('@/modules/profile/NotificationsPage').then((m) => ({ default: m.NotificationsPage })),
);

function RouteFallback() {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <Spin size="large" />
    </div>
  );
}

function SuspenseRoute({ children }: { children: ReactNode }) {
  return <Suspense fallback={<RouteFallback />}>{children}</Suspense>;
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <Suspense fallback={<RouteFallback />}>
        <Routes>
          <Route element={<GuestGuard />}>
            <Route
              path="/login"
              element={
                <SuspenseRoute>
                  <OtpLoginPage />
                </SuspenseRoute>
              }
            />
          </Route>

          <Route element={<AuthGuard />}>
            <Route
              element={
                <SuspenseRoute>
                  <CustomerAppLayout />
                </SuspenseRoute>
              }
            >
              <Route
                index
                element={
                  <SuspenseRoute>
                    <HomePage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="loyalty"
                element={
                  <SuspenseRoute>
                    <LoyaltyPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="reminders"
                element={
                  <SuspenseRoute>
                    <RemindersPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="chat"
                element={
                  <SuspenseRoute>
                    <ChatPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="orders"
                element={
                  <SuspenseRoute>
                    <DraftOrdersPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="profile"
                element={
                  <SuspenseRoute>
                    <ProfilePage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="notifications"
                element={
                  <SuspenseRoute>
                    <NotificationsPage />
                  </SuspenseRoute>
                }
              />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}

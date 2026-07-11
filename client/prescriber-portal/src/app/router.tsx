import { lazy, Suspense, type ReactNode } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { AuthGuard, GuestGuard } from '@/shared/auth/AuthGuard';

const PrescriberPortalLayout = lazy(() =>
  import('@/shared/components/PrescriberPortalLayout').then((m) => ({ default: m.PrescriberPortalLayout })),
);
const OtpLoginPage = lazy(() =>
  import('@/modules/auth/OtpLoginPage').then((m) => ({ default: m.OtpLoginPage })),
);
const HomePage = lazy(() => import('@/modules/home/HomePage').then((m) => ({ default: m.HomePage })));
const PharmacyLinksPage = lazy(() =>
  import('@/modules/pharmacies/PharmacyLinksPage').then((m) => ({ default: m.PharmacyLinksPage })),
);
const PharmacyDirectoryPage = lazy(() =>
  import('@/modules/pharmacies/PharmacyDirectoryPage').then((m) => ({ default: m.PharmacyDirectoryPage })),
);
const PendingInvitesPage = lazy(() =>
  import('@/modules/invites/PendingInvitesPage').then((m) => ({ default: m.PendingInvitesPage })),
);
const PrescriptionsPage = lazy(() =>
  import('@/modules/prescriptions/PrescriptionsPage').then((m) => ({ default: m.PrescriptionsPage })),
);
const PrescribePage = lazy(() =>
  import('@/modules/prescriptions/PrescribePage').then((m) => ({ default: m.PrescribePage })),
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
                  <PrescriberPortalLayout />
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
                path="pharmacies"
                element={
                  <SuspenseRoute>
                    <PharmacyLinksPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="directory"
                element={
                  <SuspenseRoute>
                    <PharmacyDirectoryPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="invites"
                element={
                  <SuspenseRoute>
                    <PendingInvitesPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="prescriptions"
                element={
                  <SuspenseRoute>
                    <PrescriptionsPage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="prescriptions/new"
                element={
                  <SuspenseRoute>
                    <PrescribePage />
                  </SuspenseRoute>
                }
              />
              <Route
                path="prescriptions/:id/edit"
                element={
                  <SuspenseRoute>
                    <PrescribePage />
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

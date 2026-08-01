import { lazy, Suspense, type ReactNode } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { AuthGuard, GuestGuard } from '@/shared/auth/AuthGuard';
import { AuthSessionValidator } from '@/shared/auth/AuthSessionValidator';
import { VerifyAccountProvider } from '@/shared/auth/VerifyAccountProvider';
import { PharmacyLinkProvider } from '@/shared/config/PharmacyLinkProvider';
import { PharmacyLinkRequired } from '@/shared/components/PharmacyLinkGate';

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
const AddressesPage = lazy(() =>
  import('@/modules/profile/AddressesPage').then((m) => ({ default: m.AddressesPage })),
);
const ReservationsPage = lazy(() =>
  import('@/modules/reservations/ReservationsPage').then((m) => ({ default: m.ReservationsPage })),
);
const ReceivablesPage = lazy(() =>
  import('@/modules/receivables/ReceivablesPage').then((m) => ({ default: m.ReceivablesPage })),
);
const HealthWalletPage = lazy(() =>
  import('@/modules/health/HealthWalletPage').then((m) => ({ default: m.HealthWalletPage })),
);
const FamilyPage = lazy(() => import('@/modules/family/FamilyPage').then((m) => ({ default: m.FamilyPage })));
const MyMedicationPage = lazy(() =>
  import('@/modules/medication/MyMedicationPage').then((m) => ({ default: m.MyMedicationPage })),
);
const PharmacyHubPage = lazy(() =>
  import('@/modules/pharmacy/PharmacyHubPage').then((m) => ({ default: m.PharmacyHubPage })),
);
const CareTimelinePage = lazy(() =>
  import('@/modules/timeline/CareTimelinePage').then((m) => ({ default: m.CareTimelinePage })),
);
const AiHealthPage = lazy(() => import('@/modules/ai/AiHealthPage').then((m) => ({ default: m.AiHealthPage })));
const RxEntryPage = lazy(() => import('@/modules/rx/RxEntryPage').then((m) => ({ default: m.RxEntryPage })));

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
      <PharmacyLinkProvider>
        <VerifyAccountProvider>
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

              <Route
                path="/rx"
                element={
                  <SuspenseRoute>
                    <RxEntryPage />
                  </SuspenseRoute>
                }
              />

              {/* Shell chung: care browse được ở Level 0 (guest); commerce vẫn AuthGuard. */}
              <Route
                element={
                  <AuthSessionValidator>
                    <SuspenseRoute>
                      <CustomerAppLayout />
                    </SuspenseRoute>
                  </AuthSessionValidator>
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
                  path="reminders"
                  element={
                    <SuspenseRoute>
                      <RemindersPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="health"
                  element={
                    <SuspenseRoute>
                      <HealthWalletPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="family"
                  element={
                    <SuspenseRoute>
                      <FamilyPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="medications"
                  element={
                    <SuspenseRoute>
                      <MyMedicationPage />
                    </SuspenseRoute>
                  }
                />
                <Route path="pharmacy" element={<Navigate to="/prescriptions" replace />} />
                <Route
                  path="prescriptions"
                  element={
                    <SuspenseRoute>
                      <PharmacyHubPage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="timeline"
                  element={
                    <SuspenseRoute>
                      <CareTimelinePage />
                    </SuspenseRoute>
                  }
                />
                <Route
                  path="ai"
                  element={
                    <SuspenseRoute>
                      <AiHealthPage />
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

                <Route element={<AuthGuard />}>
                  <Route
                    path="loyalty"
                    element={
                      <SuspenseRoute>
                        <PharmacyLinkRequired>
                          <LoyaltyPage />
                        </PharmacyLinkRequired>
                      </SuspenseRoute>
                    }
                  />
                  <Route
                    path="chat"
                    element={
                      <SuspenseRoute>
                        <PharmacyLinkRequired>
                          <ChatPage />
                        </PharmacyLinkRequired>
                      </SuspenseRoute>
                    }
                  />
                  <Route
                    path="orders"
                    element={
                      <SuspenseRoute>
                        <PharmacyLinkRequired>
                          <DraftOrdersPage />
                        </PharmacyLinkRequired>
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
                  <Route
                    path="addresses"
                    element={
                      <SuspenseRoute>
                        <AddressesPage />
                      </SuspenseRoute>
                    }
                  />
                  <Route
                    path="receivables"
                    element={
                      <SuspenseRoute>
                        <PharmacyLinkRequired>
                          <ReceivablesPage />
                        </PharmacyLinkRequired>
                      </SuspenseRoute>
                    }
                  />
                  <Route
                    path="reservations"
                    element={
                      <SuspenseRoute>
                        <PharmacyLinkRequired>
                          <ReservationsPage />
                        </PharmacyLinkRequired>
                      </SuspenseRoute>
                    }
                  />
                </Route>
              </Route>

              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </Suspense>
        </VerifyAccountProvider>
      </PharmacyLinkProvider>
    </BrowserRouter>
  );
}

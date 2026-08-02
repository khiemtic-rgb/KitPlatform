import { useEffect, type ReactNode } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { useSessionStore } from '@/shared/auth/session.store';
import { ParentUnlockPage } from '@/modules/unlock/ParentUnlockPage';
import { WhoAreYouPage } from '@/modules/who/WhoAreYouPage';
import { TodayFlowPage } from '@/modules/flow/TodayFlowPage';
import { OnboardingPage } from '@/modules/onboarding/OnboardingPage';
import { CheckoutPage } from '@/modules/pay/CheckoutPage';
import { FamilyAdminPage } from '@/modules/admin/FamilyAdminPage';
import { FamilyMembersPage } from '@/modules/admin/FamilyMembersPage';
import { FamilyRoutinePage } from '@/modules/admin/FamilyRoutinePage';
import { FamilyInvitePage } from '@/modules/admin/FamilyInvitePage';
import { FamilySettingsPage } from '@/modules/admin/FamilySettingsPage';

function RequireParent({ children }: { children: ReactNode }) {
  const token = useSessionStore((s) => s.accessToken);
  if (!token) return <Navigate to="/unlock" replace />;
  return children;
}

function RequireMember({ children }: { children: ReactNode }) {
  const member = useSessionStore((s) => s.member);
  if (!member) return <Navigate to="/who" replace />;
  return children;
}

function HomeRedirect() {
  const token = useSessionStore((s) => s.accessToken);
  const member = useSessionStore((s) => s.member);
  if (!token) return <Navigate to="/unlock" replace />;
  if (!member) return <Navigate to="/who" replace />;
  return <Navigate to="/today" replace />;
}

export function App() {
  const member = useSessionStore((s) => s.member);
  const location = useLocation();
  const kidMode = member?.roleCode === 'child';
  const homeMode = location.pathname === '/who';
  const unlockMode = location.pathname === '/unlock';
  const adminMode = location.pathname.startsWith('/family-admin');

  useEffect(() => {
    const theme = document.querySelector('meta[name="theme-color"]');
    if (theme) {
      /* Match kid sky gradient — avoids green strip clashing with notch/status bar. */
      theme.setAttribute('content', kidMode ? '#b8dff8' : '#0B5C3A');
    }
  }, [kidMode]);

  return (
    <div
      className={`app-shell${kidMode ? ' is-kid' : ''}${homeMode ? ' is-home' : ''}${unlockMode ? ' is-unlock' : ''}${adminMode ? ' is-admin' : ''}`}
    >
      <Routes>
        <Route path="/unlock" element={<ParentUnlockPage />} />
        <Route
          path="/who"
          element={
            <RequireParent>
              <WhoAreYouPage />
            </RequireParent>
          }
        />
        <Route
          path="/onboarding"
          element={
            <RequireParent>
              <RequireMember>
                <OnboardingPage />
              </RequireMember>
            </RequireParent>
          }
        />
        <Route
          path="/pay"
          element={
            <RequireParent>
              <CheckoutPage />
            </RequireParent>
          }
        />
        <Route
          path="/family-admin"
          element={
            <RequireParent>
              <FamilyAdminPage />
            </RequireParent>
          }
        />
        <Route
          path="/family-admin/members"
          element={
            <RequireParent>
              <FamilyMembersPage />
            </RequireParent>
          }
        />
        <Route
          path="/family-admin/routine"
          element={
            <RequireParent>
              <FamilyRoutinePage />
            </RequireParent>
          }
        />
        <Route
          path="/family-admin/invite"
          element={
            <RequireParent>
              <FamilyInvitePage />
            </RequireParent>
          }
        />
        <Route
          path="/family-admin/settings"
          element={
            <RequireParent>
              <FamilySettingsPage />
            </RequireParent>
          }
        />
        <Route
          path="/today"
          element={
            <RequireParent>
              <RequireMember>
                <TodayFlowPage />
              </RequireMember>
            </RequireParent>
          }
        />
        <Route path="*" element={<HomeRedirect />} />
      </Routes>
    </div>
  );
}

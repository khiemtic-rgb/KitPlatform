import type { ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { useSessionStore } from '@/shared/auth/session.store';
import { ParentUnlockPage } from '@/modules/unlock/ParentUnlockPage';
import { WhoAreYouPage } from '@/modules/who/WhoAreYouPage';
import { TodayFlowPage } from '@/modules/flow/TodayFlowPage';
import { OnboardingPage } from '@/modules/onboarding/OnboardingPage';

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
  const kidMode = member?.roleCode === 'child';

  return (
    <div className={`app-shell${kidMode ? ' is-kid' : ''}`}>
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

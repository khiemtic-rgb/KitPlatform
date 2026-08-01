import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import {
  enablePharmacyLink,
  pausePharmacyLink,
  PHARMACY_LINK_CHANGED_EVENT,
  resolvePharmacyLinkState,
  resumePharmacyLink,
  type PharmacyLinkState,
} from '@/shared/config/pharmacy-link';
import { PharmacyLinkSheet } from '@/shared/components/PharmacyLinkSheet';

type PharmacyLinkContextValue = PharmacyLinkState & {
  partnerName: string;
  refreshLink: () => void;
  /** Soft-gate: nếu chưa link thì mở sheet; trả về false. */
  requireLink: (intent?: string) => boolean;
  openLinkSheet: (intent?: string) => void;
  closeLinkSheet: () => void;
  linkNow: (tenantCode?: string) => void;
  pauseLink: () => void;
  sheetOpen: boolean;
  sheetIntent: string | null;
};

const PharmacyLinkContext = createContext<PharmacyLinkContextValue | null>(null);

export function PharmacyLinkProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const profileTenant = useAuthStore((s) => s.profile?.tenantCode);
  const pharmacyRelation = useAuthStore((s) => s.profile?.pharmacyRelation);
  const { branding, refresh } = useCustomerBranding();
  const [tick, setTick] = useState(0);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [sheetIntent, setSheetIntent] = useState<string | null>(null);

  const refreshLink = useCallback(() => setTick((n) => n + 1), []);

  useEffect(() => {
    const onChanged = () => refreshLink();
    window.addEventListener(PHARMACY_LINK_CHANGED_EVENT, onChanged);
    return () => window.removeEventListener(PHARMACY_LINK_CHANGED_EVENT, onChanged);
  }, [refreshLink]);

  const state = useMemo(
    () => resolvePharmacyLinkState(profileTenant, pharmacyRelation),
    // tick forces re-read localStorage after pause/resume
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [profileTenant, pharmacyRelation, tick],
  );

  const partnerName =
    branding.tenantName?.trim() || state.tenantCode || '';

  const openLinkSheet = useCallback((intent?: string) => {
    setSheetIntent(intent ?? null);
    setSheetOpen(true);
  }, []);

  const closeLinkSheet = useCallback(() => {
    setSheetOpen(false);
    setSheetIntent(null);
  }, []);

  const requireLink = useCallback(
    (intent?: string) => {
      const current = resolvePharmacyLinkState(profileTenant, pharmacyRelation);
      if (current.linked) return true;
      openLinkSheet(intent);
      return false;
    },
    [openLinkSheet, pharmacyRelation, profileTenant],
  );

  const linkNow = useCallback(
    (tenantCode?: string) => {
      const code = (tenantCode || state.tenantCode || profileTenant || '').trim().toUpperCase();
      const relation = (pharmacyRelation ?? '').trim().toLowerCase();

      // Chỉ resume khi đã là member (tạm ngắt trên thiết bị) — không tự nâng prospect→member.
      if (relation === 'member' || (!relation && state.paused)) {
        if (code) enablePharmacyLink(code);
        else resumePharmacyLink();
        refreshLink();
        void refresh();
        closeLinkSheet();
        return;
      }

      closeLinkSheet();
      navigate(code ? `/rx?tenant=${encodeURIComponent(code)}` : '/rx');
    },
    [
      closeLinkSheet,
      navigate,
      pharmacyRelation,
      profileTenant,
      refresh,
      refreshLink,
      state.paused,
      state.tenantCode,
    ],
  );

  const pauseLink = useCallback(() => {
    pausePharmacyLink();
    refreshLink();
    closeLinkSheet();
  }, [closeLinkSheet, refreshLink]);

  const value = useMemo<PharmacyLinkContextValue>(
    () => ({
      ...state,
      partnerName,
      refreshLink,
      requireLink,
      openLinkSheet,
      closeLinkSheet,
      linkNow,
      pauseLink,
      sheetOpen,
      sheetIntent,
    }),
    [
      state,
      partnerName,
      refreshLink,
      requireLink,
      openLinkSheet,
      closeLinkSheet,
      linkNow,
      pauseLink,
      sheetOpen,
      sheetIntent,
    ],
  );

  return (
    <PharmacyLinkContext.Provider value={value}>
      {children}
      <PharmacyLinkSheet />
    </PharmacyLinkContext.Provider>
  );
}

export function usePharmacyLink() {
  const ctx = useContext(PharmacyLinkContext);
  if (!ctx) {
    throw new Error('usePharmacyLink must be used within PharmacyLinkProvider');
  }
  return ctx;
}

/** Optional: pages outside provider (should not happen under layout). */
export function usePharmacyLinkOptional() {
  return useContext(PharmacyLinkContext);
}

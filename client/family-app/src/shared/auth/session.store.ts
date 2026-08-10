import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { FamilyMembership } from '@/shared/api/family-os.api';

const DEFAULT_PIN = '1234';

type SessionState = {
  accessToken: string | null;
  refreshToken: string | null;
  tenantCode: string | null;
  familyId: string | null;
  familyName: string | null;
  member: FamilyMembership | null;
  /** GTM / community browse — UI + APIs treat session as read-only. */
  demoMode: boolean;
  /** 4-digit parent gate PIN (device-local, not server auth). */
  parentPin: string;
  setParentSession: (input: {
    accessToken: string;
    refreshToken?: string | null;
    tenantCode: string;
    parentPin?: string;
    demoMode?: boolean;
  }) => void;
  setTokens: (input: { accessToken: string; refreshToken?: string | null }) => void;
  setFamily: (input: { familyId: string; familyName: string }) => void;
  setMember: (member: FamilyMembership | null) => void;
  setDemoMode: (demoMode: boolean) => void;
  setParentPin: (pin: string) => void;
  verifyParentPin: (pin: string) => boolean;
  /** False for viewer membership or /demo browse sessions. */
  canWrite: () => boolean;
  clear: () => void;
};

export const useSessionStore = create<SessionState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      tenantCode: null,
      familyId: null,
      familyName: null,
      member: null,
      demoMode: false,
      parentPin: DEFAULT_PIN,
      setParentSession: ({ accessToken, refreshToken, tenantCode, parentPin, demoMode }) => {
        const isDemo = Boolean(demoMode);
        set({
          accessToken,
          refreshToken: refreshToken ?? null,
          tenantCode,
          member: null,
          demoMode: isDemo,
          // Demo must always accept the published tip PIN; ignore leftover device PIN.
          parentPin: isDemo
            ? DEFAULT_PIN
            : parentPin && /^\d{4}$/.test(parentPin)
              ? parentPin
              : get().parentPin || DEFAULT_PIN,
        });
      },
      setTokens: ({ accessToken, refreshToken }) =>
        set({
          accessToken,
          refreshToken: refreshToken === undefined ? get().refreshToken : refreshToken,
        }),
      setFamily: ({ familyId, familyName }) => set({ familyId, familyName }),
      setMember: (member) => set({ member }),
      setDemoMode: (demoMode) =>
        set(demoMode ? { demoMode: true, parentPin: DEFAULT_PIN } : { demoMode: false }),
      setParentPin: (pin) => {
        if (get().demoMode) return;
        if (/^\d{4}$/.test(pin) && pin !== '1234') set({ parentPin: pin });
      },
      verifyParentPin: (pin) => {
        if (get().demoMode) return pin === DEFAULT_PIN;
        return pin === (get().parentPin || DEFAULT_PIN);
      },
      canWrite: () => {
        const s = get();
        if (s.demoMode) return false;
        if (s.member?.roleCode?.toLowerCase() === 'viewer') return false;
        return Boolean(s.accessToken);
      },
      clear: () =>
        set({
          accessToken: null,
          refreshToken: null,
          tenantCode: null,
          familyId: null,
          familyName: null,
          member: null,
          demoMode: false,
        }),
    }),
    { name: 'familyos-mobile-session' },
  ),
);

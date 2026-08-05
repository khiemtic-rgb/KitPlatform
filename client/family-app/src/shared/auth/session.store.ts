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
  /** 4-digit parent gate PIN (device-local, not server auth). */
  parentPin: string;
  setParentSession: (input: {
    accessToken: string;
    refreshToken?: string | null;
    tenantCode: string;
    parentPin?: string;
  }) => void;
  setTokens: (input: { accessToken: string; refreshToken?: string | null }) => void;
  setFamily: (input: { familyId: string; familyName: string }) => void;
  setMember: (member: FamilyMembership | null) => void;
  setParentPin: (pin: string) => void;
  verifyParentPin: (pin: string) => boolean;
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
      parentPin: DEFAULT_PIN,
      setParentSession: ({ accessToken, refreshToken, tenantCode, parentPin }) =>
        set({
          accessToken,
          refreshToken: refreshToken ?? null,
          tenantCode,
          member: null,
          parentPin:
            parentPin && /^\d{4}$/.test(parentPin) ? parentPin : get().parentPin || DEFAULT_PIN,
        }),
      setTokens: ({ accessToken, refreshToken }) =>
        set({
          accessToken,
          refreshToken: refreshToken === undefined ? get().refreshToken : refreshToken,
        }),
      setFamily: ({ familyId, familyName }) => set({ familyId, familyName }),
      setMember: (member) => set({ member }),
      setParentPin: (pin) => {
        if (/^\d{4}$/.test(pin) && pin !== '1234') set({ parentPin: pin });
      },
      verifyParentPin: (pin) => pin === (get().parentPin || DEFAULT_PIN),
      clear: () =>
        set({
          accessToken: null,
          refreshToken: null,
          tenantCode: null,
          familyId: null,
          familyName: null,
          member: null,
        }),
    }),
    { name: 'familyos-mobile-session' },
  ),
);

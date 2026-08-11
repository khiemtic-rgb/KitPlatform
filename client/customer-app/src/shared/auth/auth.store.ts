import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { CustomerLoginResponse, CustomerProfile } from '@/shared/api/customer-app.types';
import { syncPharmacyLinkFromProfile, clearPharmacyLinkLock } from '@/shared/config/pharmacy-link';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  profile: CustomerProfile | null;
  setSession: (payload: CustomerLoginResponse) => void;
  setProfile: (profile: CustomerProfile) => void;
  clearSession: () => void;
  isAuthenticated: () => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      profile: null,
      setSession: (payload) => {
        syncPharmacyLinkFromProfile(payload.profile);
        set({
          accessToken: payload.accessToken,
          refreshToken: payload.refreshToken,
          profile: payload.profile,
        });
      },
      setProfile: (profile) => {
        syncPharmacyLinkFromProfile(profile);
        set({ profile });
      },
      clearSession: () => {
        clearPharmacyLinkLock();
        set({
          accessToken: null,
          refreshToken: null,
          profile: null,
        });
      },
      isAuthenticated: () => Boolean(get().accessToken),
    }),
    {
      name: 'kitplatform-customer-auth',
      partialize: (state) => ({
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        profile: state.profile,
      }),
    },
  ),
);

import { ConfigProvider } from 'antd';
import axios from 'axios';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { apiPath } from '@/shared/api/api-base';
import i18n from '@/shared/i18n';
import { loadStoredTenantCode } from '@/shared/config/app-brand';
import { useAuthStore } from '@/shared/auth/auth.store';

export type CustomerAppBranding = {
  tenantCode: string;
  tenantName: string;
  appName: string;
  shortName: string;
  logoUrl: string | null;
  primaryColor: string;
  secondaryColor: string;
  supportPhone: string | null;
  tagline: string | null;
  defaultLocale: string;
  supportedLocales: string[];
};

const DEFAULT_BRANDING: CustomerAppBranding = {
  tenantCode: '',
  tenantName: i18n.t('branding.defaultTenantName'),
  appName: 'Novixa Care',
  shortName: 'Care',
  logoUrl: null,
  primaryColor: '#0f766e',
  secondaryColor: '#115e59',
  supportPhone: null,
  tagline: i18n.t('branding.defaultTagline'),
  defaultLocale: 'vi-VN',
  supportedLocales: ['vi-VN', 'en-US'],
};

function defaultBranding(): CustomerAppBranding {
  return {
    ...DEFAULT_BRANDING,
    tenantName: i18n.t('branding.defaultTenantName'),
    tagline: i18n.t('branding.defaultTagline'),
  };
}

type BrandingContextValue = {
  branding: CustomerAppBranding;
  loading: boolean;
  refresh: () => Promise<void>;
};

const BrandingContext = createContext<BrandingContextValue>({
  branding: DEFAULT_BRANDING,
  loading: false,
  refresh: async () => {},
});

function normalizeBranding(raw: Record<string, unknown>): CustomerAppBranding {
  return {
    tenantCode: String(raw.tenantCode ?? raw.TenantCode ?? ''),
    tenantName: String(raw.tenantName ?? raw.TenantName ?? i18n.t('branding.defaultTenantName')),
    appName: String(raw.appName ?? raw.AppName ?? DEFAULT_BRANDING.appName),
    shortName: String(raw.shortName ?? raw.ShortName ?? DEFAULT_BRANDING.shortName),
    logoUrl: (raw.logoUrl ?? raw.LogoUrl ?? null) as string | null,
    primaryColor: String(raw.primaryColor ?? raw.PrimaryColor ?? DEFAULT_BRANDING.primaryColor),
    secondaryColor: String(raw.secondaryColor ?? raw.SecondaryColor ?? DEFAULT_BRANDING.secondaryColor),
    supportPhone: (raw.supportPhone ?? raw.SupportPhone ?? null) as string | null,
    tagline: (raw.tagline ?? raw.Tagline ?? null) as string | null,
    defaultLocale: String(raw.defaultLocale ?? raw.DefaultLocale ?? 'vi-VN'),
    supportedLocales: (() => {
      const localesRaw = raw.supportedLocales ?? raw.SupportedLocales;
      return Array.isArray(localesRaw)
        ? localesRaw.map((x) => String(x))
        : ['vi-VN', 'en-US'];
    })(),
  };
}

export function BrandingProvider({ children }: { children: ReactNode }) {
  const profileTenant = useAuthStore((s) => s.profile?.tenantCode);
  const [branding, setBranding] = useState<CustomerAppBranding>(DEFAULT_BRANDING);
  const [loading, setLoading] = useState(false);

  const tenantCode = (profileTenant || loadStoredTenantCode()).trim().toUpperCase();

  const refresh = useCallback(async () => {
    if (!tenantCode) {
      setBranding(defaultBranding());
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const { data } = await axios.get<Record<string, unknown>>(
        apiPath('/api/customer-app/branding'),
        { params: { tenantCode }, timeout: 8_000 },
      );
      setBranding(normalizeBranding(data));
    } catch {
      setBranding({ ...defaultBranding(), tenantCode });
    } finally {
      setLoading(false);
    }
  }, [tenantCode]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const value = useMemo(() => ({ branding, loading, refresh }), [branding, loading, refresh]);

  const theme = useMemo(
    () => ({
      token: {
        colorPrimary: branding.primaryColor,
        borderRadius: 10,
        fontFamily: "'Inter', Helvetica, Arial, sans-serif",
      },
    }),
    [branding.primaryColor],
  );

  return (
    <BrandingContext.Provider value={value}>
      <ConfigProvider theme={theme}>{children}</ConfigProvider>
    </BrandingContext.Provider>
  );
}

export function useCustomerBranding() {
  return useContext(BrandingContext);
}

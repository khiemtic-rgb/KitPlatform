/** Shared types for multi-tenant pharmacy white-label storefronts. */

export interface PharmacyNavItem {
  key: string;
  label: string;
  href: string;
}

export interface PharmacyTrustItem {
  icon: string;
  label: string;
}

export interface PharmacyCta {
  label: string;
  href: string;
}

export interface PharmacyService {
  title: string;
  description: string;
  icon: string;
}

export interface PharmacyProduct {
  name: string;
  category: string;
  price: string;
  imageUrl: string;
  href: string;
}

export interface PharmacyArticle {
  title: string;
  date: string;
  excerpt?: string;
  imageUrl?: string;
  href: string;
}

export interface PharmacyStat {
  value: string;
  label: string;
}

export interface PharmacyFooterLinks {
  label: string;
  href: string;
}

export interface PharmacySocial {
  facebook?: string;
  zalo?: string;
  whatsapp?: string;
  tiktok?: string;
  youtube?: string;
}

export interface PharmacyTenantConfig {
  id: string;
  slug: string;
  /** Platform tenant code, e.g. NT_XUANHOA */
  tenantCode: string;
  /** Hostnames / host substrings that resolve to this tenant */
  hosts: string[];

  brand: {
    name: string;
    shortName: string;
    logoText: string;
    primaryColor: string;
    accentColor: string;
  };

  contact: {
    address: string;
    hours: string;
    phone: string;
    email: string;
    social: PharmacySocial;
  };

  nav: PharmacyNavItem[];

  hero: {
    headline: string;
    subhead: string;
    imageUrl: string;
    trustItems: PharmacyTrustItem[];
    ctaPrimary: PharmacyCta;
    ctaSecondary: PharmacyCta;
  };

  appPromo: {
    title: string;
    qrImageUrl?: string;
    appStoreUrl: string;
    playStoreUrl: string;
    /** Deep link into Novixa app with tenant query */
    appUrl: string;
  };

  services: PharmacyService[];
  whyUs: string[];
  /** Sample catalog products (home showcase) */
  products: PharmacyProduct[];
  /** Sample knowledge / news articles */
  articles: PharmacyArticle[];

  appSection: {
    bullets: string[];
    stats: PharmacyStat[];
    phoneMockImageUrl?: string;
  };

  footer: {
    aboutLinks: PharmacyFooterLinks[];
    supportLinks: PharmacyFooterLinks[];
    knowledgeLinks: PharmacyFooterLinks[];
    mission: string;
  };

  poweredBy: {
    label: string;
    href: string;
    blurb: string;
  };
}

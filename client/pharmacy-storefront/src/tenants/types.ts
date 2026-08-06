/** Shared types for multi-tenant pharmacy white-label storefronts. */

export interface PharmacyNavItem {
  key: string;
  label: string;
  href: string;
}

export interface PharmacyTrustItem {
  icon: string;
  label: string;
  /** Optional second line under label (hero trust strip) */
  sublabel?: string;
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
  slug: string;
  title: string;
  date: string;
  excerpt?: string;
  imageUrl?: string;
  href: string;
  body?: string[];
}

export interface PharmacyStat {
  value: string;
  label: string;
  /** Optional second line / supporting text */
  sublabel?: string;
  /** Optional icon key for hero stats strip */
  icon?: string;
}

export interface PharmacyFeature {
  icon: string;
  title: string;
  description?: string;
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
  instagram?: string;
}

export interface PharmacyBranch {
  name: string;
  address: string;
  hours?: string;
  phone?: string;
}

export interface PharmacyPageSection {
  id: string;
  title: string;
  body: string;
}

export interface PharmacyTenantConfig {
  id: string;
  slug: string;
  tenantCode: string;
  hosts: string[];

  brand: {
    name: string;
    shortName: string;
    logoText: string;
    logoUrl?: string;
    primaryColor: string;
    accentColor: string;
  };

  contact: {
    address: string;
    hours: string;
    phone: string;
    email: string;
    social: PharmacySocial;
    branches?: PharmacyBranch[];
  };

  nav: PharmacyNavItem[];

  hero: {
    headline: string;
    subhead: string;
    imageUrl: string;
    trustItems: PharmacyTrustItem[];
    ctaPrimary: PharmacyCta;
    ctaSecondary: PharmacyCta;
    /** Floating strip under hero photo */
    heroStats: PharmacyStat[];
  };

  /** Trust card under hero */
  trustBand: {
    title: string;
    /** Number/phrase to highlight green inside title */
    titleHighlight?: string;
    items: PharmacyStat[];
  };

  appPromo: {
    title: string;
    qrImageUrl?: string;
    appStoreUrl: string;
    playStoreUrl: string;
    appUrl: string;
  };

  services: PharmacyService[];
  whyUs: string[];
  products: PharmacyProduct[];
  articles: PharmacyArticle[];

  appSection: {
    title: string;
    titleHighlight?: string;
    features: PharmacyFeature[];
    /** Combined phone+family art (preferred when set) */
    visualImageUrl?: string;
    phoneMockImageUrl?: string;
    familyImageUrl?: string;
  };

  platformPromo: {
    eyebrow?: string;
    title: string;
    subtitle?: string;
    /** @deprecated prefer features */
    body?: string;
    bullets?: string[];
    features?: PharmacyFeature[];
    ctaLead?: string;
    ctaLeadHighlight?: string;
    ctaPrimary: PharmacyCta;
    ctaSecondary: PharmacyCta;
    imageUrl?: string;
  };

  pages: {
    about: {
      intro: string;
      sections: PharmacyPageSection[];
    };
    contact: {
      intro: string;
      mapNote?: string;
    };
  };

  footer: {
    aboutLinks: PharmacyFooterLinks[];
    supportLinks: PharmacyFooterLinks[];
    categoryLinks: PharmacyFooterLinks[];
    tagline?: string;
    mission: string;
    newsletterNote?: string;
    copyright?: string;
  };

  poweredBy: {
    label: string;
    href: string;
    blurb: string;
  };
}

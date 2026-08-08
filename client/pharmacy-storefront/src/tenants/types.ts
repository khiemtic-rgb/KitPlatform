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

export interface PharmacyServiceFeature {
  id: string;
  title: string;
  description: string;
  icon: string;
  bullets: string[];
  /** Soft pastel card tone */
  tone?: 'green' | 'blue' | 'yellow' | 'purple';
}

export interface PharmacyProductSpec {
  label: string;
  value: string;
}

export interface PharmacyProduct {
  slug: string;
  name: string;
  category: string;
  price: string;
  imageUrl: string;
  /** Detail page path or external App URL */
  href: string;
  /** Catalog filter kind: medicine | supplement | device | care */
  kind?: string;
  /** Optional card badge: authentic | bestseller | new */
  badge?: 'authentic' | 'bestseller' | 'new';
  shortDescription?: string;
  /** e.g. "/ Hộp" */
  priceUnit?: string;
  /** e.g. "Hộp 180 viên" */
  packInfo?: string;
  gallery?: string[];
  specs?: PharmacyProductSpec[];
  usage?: string[];
  notes?: string[];
  disclaimer?: string;
}

export interface PharmacyCatalogNeed {
  id: string;
  label: string;
  icon: string;
  /** Matches product.kind or free-text category filter */
  filter: string;
}

export interface PharmacyCatalogPill {
  id: string;
  label: string;
  /** Empty = all products */
  kind: string;
}

export interface PharmacyArticleSection {
  id: string;
  title: string;
  paragraphs?: string[];
  bullets?: string[];
}

export interface PharmacyArticle {
  slug: string;
  title: string;
  date: string;
  excerpt?: string;
  imageUrl?: string;
  href: string;
  body?: string[];
  /** Structured sections for TOC + rich layout */
  sections?: PharmacyArticleSection[];
  /** Topic key for filtering, e.g. flu | diabetes | bp | medicine */
  topic?: string;
  categoryLabel?: string;
  readingMinutes?: number;
}

export interface PharmacyKnowledgeTip {
  icon: string;
  text: string;
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

export interface PharmacyAboutValue {
  icon: string;
  title: string;
  description: string;
}

export interface PharmacyAboutReason {
  icon: string;
  label: string;
  description: string;
}

export interface PharmacyAboutCertificate {
  title: string;
  imageUrl: string;
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
    /** Internal notify address (BCC). Never render on the public site. */
    notifyEmail?: string;
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
      hero: {
        eyebrow: string;
        title: string;
        subtitle: string;
        body: string;
        imageUrl: string;
        imageAlt: string;
        ctaPrimary: PharmacyCta;
        ctaSecondary: PharmacyCta;
      };
      valuesTitle: string;
      values: PharmacyAboutValue[];
      reasonsTitle: string;
      reasons: PharmacyAboutReason[];
      team: {
        title: string;
        body: string;
        highlights: string[];
        ctaPrimary: PharmacyCta;
        ctaSecondary: PharmacyCta;
      };
      digital: {
        title: string;
        bullets: string[];
      };
      gallery: {
        title: string;
        images: { src: string; alt: string }[];
        ctaLabel: string;
        ctaHref: string;
      };
      certificates: {
        title: string;
        items: PharmacyAboutCertificate[];
        ctaLabel: string;
        ctaHref: string;
      };
      supportCta: {
        title: string;
        imageUrl: string;
        imageAlt: string;
        ctaPrimary: PharmacyCta;
        ctaSecondary: PharmacyCta;
      };
      sections: PharmacyPageSection[];
    };
    contact: {
      intro: string;
      mapNote?: string;
      hero: {
        eyebrow: string;
        title: string;
        subtitle: string;
        body: string;
        imageUrl: string;
        imageAlt: string;
        floatingCard: {
          title: string;
          description: string;
        };
      };
      infoTitle: string;
      formTitle: string;
      formNote: string;
      subjects: string[];
      reasonsTitle: string;
      reasons: { icon: string; title: string; description: string }[];
      mapEmbedUrl?: string;
      directionsUrl: string;
      directionsLabel: string;
    };
    products: {
      hero: {
        title: string;
        bullets: string[];
        body: string;
        ctaPrimary: PharmacyCta;
        ctaSecondary: PharmacyCta;
        trustItems: { icon: string; label: string }[];
      };
      searchPlaceholder: string;
      pills: PharmacyCatalogPill[];
      gridTitle: string;
      needsTitle: string;
      needs: PharmacyCatalogNeed[];
      trustBlocks: { icon: string; title: string; description: string }[];
      emptyMessage: string;
    };
    services: {
      hero: {
        eyebrow: string;
        title: string;
        body: string;
        imageUrl: string;
        imageAlt: string;
        ctaPrimary: PharmacyCta;
        ctaSecondary: PharmacyCta;
      };
      trustItems: { icon: string; label: string }[];
      featuredTitle: string;
      featuredSubtitle: string;
      featured: PharmacyServiceFeature[];
      processTitle: string;
      processSubtitle: string;
      process: { title: string; description: string; icon: string }[];
      appBanner: {
        title: string;
        body: string;
        highlights: { icon: string; label: string; description: string }[];
        cta: PharmacyCta;
      };
    };
    knowledge: {
      hero: {
        title: string;
        subtitle: string;
      };
      listTitle: string;
      sidebarTitle: string;
      tipsTitle: string;
      tips: PharmacyKnowledgeTip[];
      newsletter: {
        title: string;
        placeholder: string;
        ctaLabel: string;
        note: string;
      };
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

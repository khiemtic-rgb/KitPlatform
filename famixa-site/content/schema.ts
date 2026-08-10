/**
 * CMS-ready content schema.
 * Swap `getLandingContent()` to fetch from Sanity / Contentful / Payload —
 * keep this shape stable so sections stay unchanged.
 */

export type CmsImage = {
  src: string;
  alt: string;
  width?: number;
  height?: number;
};

export type CmsLink = {
  href: string;
  label: string;
};

export type CmsIconKey =
  | 'heart'
  | 'star'
  | 'shield'
  | 'sprout'
  | 'eye'
  | 'lightbulb'
  | 'sparkles'
  | 'users'
  | 'droplet'
  | 'tree'
  | 'moon'
  | 'book'
  | 'home'
  | 'chart'
  | 'smile';

export type LandingDocument = {
  /** CMS document id — useful when wiring headless */
  _id: string;
  _type: 'landingPage';
  locale: 'vi' | 'en';
  seo: {
    title: string;
    description: string;
    ogImage: string;
    keywords: string[];
  };
  brand: {
    name: string;
    tagline: string;
    /** Full lockup (icon + wordmark) */
    logo: string;
    /** Icon-only mark for compact / dark surfaces */
    logoMark?: string;
  };
  appUrl: string;
  storyChapters: Array<{
    id: string;
    label: string;
  }>;
  nav: {
    links: CmsLink[];
    login: string;
    cta: string;
    /** GTM: demo house URL; defaults to appUrl in Navbar when omitted. */
    ctaHref?: string;
  };
  hero: {
    badge: string;
    titleLine1: string;
    titleLine2: string;
    lead: string;
    primaryCta: string;
    secondaryCta?: string;
    secondaryHref?: string;
    image: CmsImage;
    /** Speech bubbles trên ảnh hero — text HTML (sửa trong landing.json) */
    bubbles?: Array<{
      text: string;
      /** % left của tâm bubble trên ảnh */
      left: string;
      /** % top của tâm bubble trên ảnh */
      top: string;
      /** % width bubble trên ảnh */
      width: string;
      /** Đuôi thoại: bl = dưới trái, br = dưới phải, bc = giữa */
      tail?: 'bl' | 'br' | 'bc';
    }>;
    proof: Array<{ title: string; sub: string; icon: CmsIconKey }>;
  };
  chapter1: {
    id: string;
    eyebrow: string;
    title: string;
    lead: string;
    cta: string;
    ctaHref: string;
    image: CmsImage;
    /** Câu quote cầu nối, hiển thị dưới cặp thẻ Chương 1–2 */
    bridgeQuote?: string;
  };
  chapter2: {
    id: string;
    eyebrow: string;
    title: string;
    lead: string;
    steps: Array<{ title: string; body: string; icon: CmsIconKey }>;
  };
  chapter3: {
    id: string;
    eyebrow: string;
    title: string;
    lead: string;
    cta: string;
    ctaHref: string;
    moments: Array<{
      title: string;
      body: string;
      image: CmsImage;
      /** Nhãn pill trên ảnh, vd. "Buổi tối" */
      tag: string;
      /** Icon dùng chung cho pill + vòng tròn overlay */
      icon: CmsIconKey;
      /** Bảng màu pill/vòng tròn theo khoảnh khắc */
      tone: 'night' | 'study' | 'home' | 'weekend';
    }>;
    trust: Array<{ icon: CmsIconKey; title: string; body: string }>;
  };
  chapter4: {
    id: string;
    eyebrow: string;
    title: string;
    /** Phần title tô xanh, vd. "gia đình bạn" */
    titleAccent?: string;
    lead: string;
    cta: string;
    ctaHref: string;
    image: CmsImage;
    stages: Array<{ title: string; hint: string; icon: CmsIconKey; image: CmsImage }>;
  };
  chapter5: {
    id: string;
    eyebrow: string;
    title: string;
    lead: string;
    cta: string;
    quote?: string;
    image: CmsImage;
  };
  chapter6: {
    id: string;
    eyebrow: string;
    title: string;
    /** Phần title tô xanh, vd. "của Famixa" */
    titleAccent?: string;
    lead: string;
    cta: string;
    ctaHref: string;
    /** Diagram cắt từ mock (5 thẻ + Fami) */
    image: CmsImage;
    mascot: CmsImage;
    mascotLabel: string;
    mascotTagline: string;
    loop: Array<{ title: string; hint: string }>;
  };
  chapter7: {
    id: string;
    eyebrow: string;
    title: string;
    titleAccent?: string;
    lead: string;
    cta?: string;
    ctaHref?: string;
    mascot?: CmsImage;
    mascotLabel?: string;
    mascotTagline?: string;
    quotes: Array<{
      name: string;
      place: string;
      text: string;
      image: CmsImage;
      rating?: number;
      highlights?: string[];
    }>;
  };
  chapter8: {
    id: string;
    eyebrow: string;
    title: string;
    lead: string;
    cta?: string;
    ctaHref?: string;
    mascot?: CmsImage;
    mascotQuote?: string;
    perks?: Array<{
      icon: 'shield' | 'refresh' | 'support';
      title: string;
      hint: string;
    }>;
    plans: Array<{
      name: string;
      tagline?: string;
      price: string;
      period: string;
      tone: 'start' | 'peace' | 'growth' | 'thrive';
      badge: string | null;
      cta: string;
      items: string[];
    }>;
  };
  faq: {
    id: string;
    eyebrow: string;
    title: string;
    lead: string;
    items: Array<{ q: string; a: string }>;
  };
  finalCta: {
    title: string;
    titleAccent?: string;
    brandLine?: string;
    lead?: string;
    cta: string;
    image: CmsImage;
  };
  footer: {
    blurb: string;
    solutions: CmsLink[];
    resources: CmsLink[];
    about: CmsLink[];
    /** Temporary social / chat contacts — editable in landing JSON */
    social?: Array<{ href: string; label: string; network: 'facebook' | 'zalo' | 'tiktok' | 'youtube' | 'instagram' }>;
    newsletter: { title: string; lead: string; placeholder: string };
    company: { heading?: string; name: string; address: string; contactLine?: string };
    copyrightSuffix: string;
    madeWith: string;
  };
};

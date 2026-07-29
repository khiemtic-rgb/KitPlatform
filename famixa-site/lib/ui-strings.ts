import type { AppLocale } from '@/lib/cms/getLanding';

/** Chrome / aria copy that is not in landing JSON */
export const UI_STRINGS = {
  vi: {
    storyAria: 'Câu chuyện Famixa',
    journeyAria: 'Hành trình & công nghệ',
    growthLoopAria: 'Vòng lặp tăng trưởng',
    testimonialsAria: 'Câu chuyện gia đình',
    storiesPagesAria: 'Trang câu chuyện',
    pageN: (n: number) => `Trang ${n}`,
    starsAria: (n: number) => `${n} sao`,
    pricingAria: 'Bảng giá',
    faqAria: 'Câu hỏi thường gặp',
    socialAria: 'Mạng xã hội',
    newsletterSubmitAria: 'Đăng ký nhận bản tin',
    footerSolutions: 'Giải pháp',
    footerResources: 'Tài nguyên',
    footerAbout: 'Về Famixa',
    bubbleCopyHint: 'Bôi đen để sao chép',
    redirecting: 'Đang chuyển tới',
  },
  en: {
    storyAria: 'Famixa story',
    journeyAria: 'Journey & technology',
    growthLoopAria: 'Growth loop',
    testimonialsAria: 'Family stories',
    storiesPagesAria: 'Story pages',
    pageN: (n: number) => `Page ${n}`,
    starsAria: (n: number) => `${n} stars`,
    pricingAria: 'Pricing',
    faqAria: 'Frequently asked questions',
    socialAria: 'Social media',
    newsletterSubmitAria: 'Subscribe to newsletter',
    footerSolutions: 'Solutions',
    footerResources: 'Resources',
    footerAbout: 'About Famixa',
    bubbleCopyHint: 'Select to copy',
    redirecting: 'Redirecting to',
  },
} as const;

export function ui(locale: AppLocale) {
  return UI_STRINGS[locale];
}

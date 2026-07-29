'use client';

import {
  ArrowRight,
  BookOpen,
  Facebook,
  Heart,
  Instagram,
  Leaf,
  Mail,
  MapPin,
  UserRound,
  Youtube,
  type LucideIcon,
} from 'lucide-react';
import { Container } from '@/components/ui/Container';
import type { LandingContent } from '@/content/landing';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';

type Props = {
  content: LandingContent['footer'];
  brand: LandingContent['brand'];
  appUrl: string;
  locale: AppLocale;
};

function TikTokIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className={className} aria-hidden>
      <path d="M19.59 6.69a4.83 4.83 0 0 1-3.77-4.25V2h-3.45v13.67a2.89 2.89 0 0 1-2.88 2.5 2.89 2.89 0 0 1-2.89-2.89 2.89 2.89 0 0 1 2.89-2.89c.28 0 .54.04.79.1v-3.5a6.37 6.37 0 0 0-.79-.05A6.34 6.34 0 0 0 3.15 15.2a6.34 6.34 0 0 0 6.34 6.34 6.34 6.34 0 0 0 6.34-6.34V8.75a8.19 8.19 0 0 0 4.76 1.52V6.84a4.84 4.84 0 0 1-1-.15Z" />
    </svg>
  );
}

type LinkItem = { href: string; label: string };

function LinkColumn({
  title,
  icon: Icon,
  links,
}: {
  title: string;
  icon: LucideIcon;
  links: LinkItem[];
}) {
  return (
    <div>
      <h4 className="mb-3 mt-0 flex items-center gap-2 text-[0.95rem] font-extrabold text-white">
        <Icon className="h-4 w-4 text-[#7DCF8A]" strokeWidth={2.2} />
        {title}
      </h4>
      <ul className="m-0 list-none p-0">
        {links.map((l, i) => (
          <li
            key={l.label}
            className={i < links.length - 1 ? 'border-b border-white/[0.08]' : undefined}
          >
            <a
              href={l.href}
              className="flex items-center gap-2.5 py-2.5 text-[0.88rem] text-white/75 transition-colors hover:text-white"
            >
              <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-[#7DCF8A]" aria-hidden />
              {l.label}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Footer — mẫu mới: 4 cột + hộp newsletter/công ty + copyright giữa */
export function Footer({ content, brand, appUrl, locale }: Props) {
  const t = ui(locale);
  return (
    <footer id="about" className="mt-0 bg-[#FBF8F1] pb-8 pt-6 md:pb-10 md:pt-7">
      <Container>
        <div className="overflow-hidden rounded-[28px] bg-[#08241C] px-6 py-9 text-white sm:rounded-[32px] sm:px-8 sm:py-10 md:rounded-[36px] md:px-10 md:py-11">
          {/* Top: brand + 3 link columns */}
          <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-4 lg:gap-7">
            <div>
              <div className="mb-3">
                <img
                  src={brand.logoMark || brand.logo}
                  alt={brand.name}
                  width={48}
                  height={48}
                  className="h-12 w-12 rounded-2xl bg-white object-contain p-1 shadow-[0_4px_14px_rgba(0,0,0,0.18)]"
                />
                <strong className="mt-3 block text-[1.1rem] font-extrabold text-white">{brand.name}</strong>
                <span className="mt-1 block text-[0.72rem] leading-snug text-white/60">{brand.tagline}</span>
              </div>
              <p className="m-0 max-w-[17rem] text-[0.88rem] leading-[1.6] text-white/70">
                {content.blurb}
              </p>
              <div className="mt-5 flex gap-2.5" aria-label={t.socialAria}>
                {[
                  { href: 'https://facebook.com', label: 'Facebook', Icon: Facebook },
                  { href: 'https://tiktok.com', label: 'TikTok', Icon: TikTokIcon },
                  { href: 'https://youtube.com', label: 'YouTube', Icon: Youtube },
                  { href: 'https://instagram.com', label: 'Instagram', Icon: Instagram },
                ].map(({ href, label, Icon }) => (
                  <a
                    key={label}
                    href={href}
                    className="grid h-9 w-9 place-items-center rounded-full border border-white/20 text-white/85 transition-colors hover:border-[#7DCF8A]/50 hover:bg-white/10 hover:text-white"
                    aria-label={label}
                    rel="noopener noreferrer"
                    target="_blank"
                  >
                    <Icon className="h-4 w-4" />
                  </a>
                ))}
              </div>
            </div>

            <LinkColumn title={t.footerSolutions} icon={Leaf} links={content.solutions} />
            <LinkColumn title={t.footerResources} icon={BookOpen} links={content.resources} />
            <LinkColumn title={t.footerAbout} icon={UserRound} links={content.about} />
          </div>

          {/* Middle: newsletter + company card */}
          <div className="mt-8 grid gap-6 rounded-[22px] border border-white/12 bg-[#0A2E24]/80 px-5 py-5 sm:mt-9 sm:px-6 sm:py-6 md:grid-cols-2 md:gap-8">
            <div>
              <div className="mb-2.5 flex items-center gap-2.5">
                <span className="grid h-9 w-9 place-items-center rounded-full bg-[#1FA45A]/20 text-[#7DCF8A]">
                  <Mail className="h-4 w-4" strokeWidth={2.2} />
                </span>
                <h4 className="m-0 text-[0.95rem] font-extrabold text-white">
                  {content.newsletter.title}
                </h4>
              </div>
              <p className="mb-3.5 mt-0 text-[0.86rem] leading-[1.55] text-white/65">
                {content.newsletter.lead}
              </p>
              <form
                className="flex max-w-md items-center overflow-hidden rounded-full border border-white/15 bg-[#061A14]"
                action={appUrl}
                method="get"
              >
                <input
                  type="email"
                  name="email"
                  placeholder={content.newsletter.placeholder}
                  aria-label="Email"
                  className="min-w-0 flex-1 border-0 bg-transparent px-4 py-2.5 text-[0.88rem] text-white outline-none placeholder:text-white/40"
                />
                <button
                  type="submit"
                  aria-label={t.newsletterSubmitAria}
                  className="m-1 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-white text-[#1FA45A] transition-colors hover:bg-[#E8F6EE]"
                >
                  <ArrowRight className="h-3.5 w-3.5" strokeWidth={2.5} />
                </button>
              </form>
            </div>

            <div>
              <div className="mb-2.5 flex items-center gap-2.5">
                <span className="grid h-9 w-9 place-items-center rounded-full bg-[#1FA45A]/20 text-[#7DCF8A]">
                  <MapPin className="h-4 w-4" strokeWidth={2.2} />
                </span>
                <h4 className="m-0 text-[0.95rem] font-extrabold text-white">
                  {content.company.heading || brand.tagline}
                </h4>
              </div>
              <p className="m-0 text-[0.88rem] leading-[1.55] text-white/75">{content.company.name}</p>
              <p className="mt-2 mb-0 text-[0.84rem] leading-[1.5] text-white/60">
                {content.company.address}
              </p>
            </div>
          </div>

          {/* Bottom copyright */}
          <p className="mb-0 mt-7 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-center text-[0.8rem] text-white/50">
            <span>© 2025 {content.copyrightSuffix}</span>
            <span className="text-white/30" aria-hidden>
              |
            </span>
            <span className="inline-flex items-center gap-1.5">
              {content.madeWith.includes('♥') ? (
                <>
                  {content.madeWith.split('♥')[0]}
                  <Heart className="h-3.5 w-3.5 fill-[#7DCF8A] text-[#7DCF8A]" />
                  {content.madeWith.split('♥')[1]}
                </>
              ) : (
                content.madeWith
              )}
            </span>
          </p>
        </div>
      </Container>
    </footer>
  );
}

import { getLandingContent, type AppLocale } from '@/lib/cms/getLanding';
import { getSpecialtyContent } from '@/content/specialty';
import { SpecialtyShell } from '@/components/specialty/SpecialtyShell';
import { PillCta } from '@/components/ui/ChapterChrome';
import { specialtyPath } from '@/lib/specialty-routes';

export async function AboutPage({ locale }: { locale: AppLocale }) {
  const c = await getLandingContent(locale);
  const s = getSpecialtyContent(locale).about;

  return (
    <SpecialtyShell locale={locale} specialty="about" eyebrow={s.eyebrow} title={s.title} lead={s.lead}>
      <div className="grid gap-4">
        {s.blocks.map((b) => (
          <section
            key={b.heading}
            className="rounded-[22px] border border-[#E4E7E2] bg-white p-5 shadow-[0_8px_24px_rgba(16,59,43,0.05)] sm:p-6"
          >
            <h2 className="m-0 text-[1.1rem] font-extrabold text-[#103B2B]">{b.heading}</h2>
            {b.body.map((p) => (
              <p key={p.slice(0, 28)} className="mt-2.5 mb-0 text-[0.95rem] leading-[1.6] text-[#2A3830]">
                {p}
              </p>
            ))}
          </section>
        ))}
      </div>

      <section className="mt-5 rounded-[22px] border border-[#103B2B]/15 bg-[#103B2B] p-5 text-white sm:p-6">
        <h2 className="m-0 text-[1.05rem] font-extrabold">{s.companyHeading}</h2>
        <p className="mt-2 mb-0 text-[0.95rem] leading-[1.55] text-white/85">{c.footer.company.name}</p>
        <p className="mt-1 mb-0 text-[0.88rem] text-white/65">{c.footer.company.address}</p>
        <div className="mt-4 flex flex-wrap gap-3 text-[0.88rem]">
          <a href={specialtyPath('privacy', locale)} className="text-[#7DCF8A] underline-offset-2 hover:underline">
            {locale === 'vi' ? 'Chính sách bảo mật' : 'Privacy policy'}
          </a>
          <a href={specialtyPath('terms', locale)} className="text-[#7DCF8A] underline-offset-2 hover:underline">
            {locale === 'vi' ? 'Điều khoản sử dụng' : 'Terms of use'}
          </a>
        </div>
      </section>

      <div className="mt-8">
        <PillCta href={c.appUrl} variant="dark">
          {s.appCta}
        </PillCta>
      </div>
    </SpecialtyShell>
  );
}

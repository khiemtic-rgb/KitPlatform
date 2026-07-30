import { getLandingContent, type AppLocale } from '@/lib/cms/getLanding';
import { getSpecialtyContent } from '@/content/specialty';
import { SpecialtyShell } from '@/components/specialty/SpecialtyShell';
import { PillCta } from '@/components/ui/ChapterChrome';

export async function StoriesPage({ locale }: { locale: AppLocale }) {
  const c = await getLandingContent(locale);
  const s = getSpecialtyContent(locale).stories;

  return (
    <SpecialtyShell
      locale={locale}
      specialty="stories"
      eyebrow={s.eyebrow}
      title={s.title}
      lead={s.lead}
    >
      <div className="grid gap-5">
        {s.articles.map((a) => (
          <article
            key={`${a.name}-${a.title}`}
            className="overflow-hidden rounded-[24px] border border-[#E4E7E2] bg-white shadow-[0_8px_24px_rgba(16,59,43,0.05)] md:grid md:grid-cols-[200px_1fr]"
          >
            <div className="aspect-[5/4] bg-[#F3F0E8] md:aspect-auto md:min-h-full">
              <img src={a.image.src} alt={a.image.alt} className="h-full w-full object-cover" />
            </div>
            <div className="p-5 sm:p-6">
              <p className="m-0 text-[0.8rem] font-bold uppercase tracking-[0.08em] text-[#1FA45A]">
                {a.name} · {a.place}
              </p>
              <h2 className="mt-1.5 mb-0 text-[1.2rem] font-extrabold text-[#103B2B]">{a.title}</h2>
              {a.body.map((p) => (
                <p key={p.slice(0, 24)} className="mt-2.5 mb-0 text-[0.95rem] leading-[1.6] text-[#2A3830]">
                  {p}
                </p>
              ))}
              {a.highlights?.length ? (
                <div className="mt-3 flex flex-wrap gap-2">
                  {a.highlights.map((h) => (
                    <span
                      key={h}
                      className="rounded-full bg-[#EAF6EE] px-2.5 py-1 text-[0.75rem] font-semibold text-[#1A7A45]"
                    >
                      {h}
                    </span>
                  ))}
                </div>
              ) : null}
            </div>
          </article>
        ))}
      </div>

      <div className="mt-8">
        <PillCta href={c.appUrl} variant="dark">
          {s.appCta}
        </PillCta>
      </div>
    </SpecialtyShell>
  );
}

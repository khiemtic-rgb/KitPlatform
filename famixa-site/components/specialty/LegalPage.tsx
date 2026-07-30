import type { AppLocale } from '@/lib/cms/getLanding';
import { getSpecialtyContent } from '@/content/specialty';
import { SpecialtyShell } from '@/components/specialty/SpecialtyShell';
import type { SpecialtyKey } from '@/lib/specialty-routes';

export async function LegalPage({
  locale,
  kind,
}: {
  locale: AppLocale;
  kind: 'privacy' | 'terms';
}) {
  const s = getSpecialtyContent(locale)[kind];
  const specialty: SpecialtyKey = kind;

  return (
    <SpecialtyShell locale={locale} specialty={specialty} eyebrow={s.eyebrow} title={s.title}>
      <p className="m-0 text-[0.88rem] font-medium text-[#5E6A63]">{s.updated}</p>
      <div className="mt-6 grid gap-5">
        {s.sections.map((sec) => (
          <section key={sec.heading}>
            <h2 className="m-0 text-[1.05rem] font-extrabold text-[#103B2B]">{sec.heading}</h2>
            {sec.body.map((p) => (
              <p key={p.slice(0, 32)} className="mt-2 mb-0 text-[0.95rem] leading-[1.65] text-[#2A3830]">
                {p}
              </p>
            ))}
          </section>
        ))}
      </div>
    </SpecialtyShell>
  );
}

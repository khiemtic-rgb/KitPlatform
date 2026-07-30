import { Check, Minus } from 'lucide-react';
import { getLandingContent, type AppLocale } from '@/lib/cms/getLanding';
import { getSpecialtyContent, type PlanCompareRow } from '@/content/specialty';
import { SpecialtyShell } from '@/components/specialty/SpecialtyShell';
import { PillCta } from '@/components/ui/ChapterChrome';

function Cell({ value }: { value: boolean | string }) {
  if (typeof value === 'string') {
    return <span className="text-[0.88rem] font-semibold text-[#103B2B]">{value}</span>;
  }
  if (value) {
    return <Check className="mx-auto h-4 w-4 text-[#1FA45A]" strokeWidth={2.6} aria-label="Yes" />;
  }
  return <Minus className="mx-auto h-4 w-4 text-[#C5CCC7]" strokeWidth={2.2} aria-label="No" />;
}

export async function PlansPage({ locale }: { locale: AppLocale }) {
  const c = await getLandingContent(locale);
  const s = getSpecialtyContent(locale).plans;

  return (
    <SpecialtyShell locale={locale} specialty="plans" eyebrow={s.eyebrow} title={s.title} lead={s.lead}>
      <p className="m-0 rounded-2xl border border-[#BFE6C9] bg-[#EAF6EE] px-4 py-3 text-[0.92rem] leading-[1.5] text-[#16412C]">
        {s.trialNote}
      </p>

      <div className="mt-6 grid gap-4 sm:grid-cols-2">
        {c.chapter8.plans.map((p) => (
          <article
            key={p.name}
            className="rounded-[22px] border border-[#E4E7E2] bg-white p-5 shadow-[0_8px_24px_rgba(16,59,43,0.05)]"
          >
            <div className="flex items-baseline justify-between gap-2">
              <h2 className="m-0 text-[1.15rem] font-extrabold text-[#103B2B]">{p.name}</h2>
              {p.badge ? (
                <span className="rounded-full bg-[#1FA45A] px-2.5 py-0.5 text-[0.7rem] font-bold text-white">
                  {p.badge}
                </span>
              ) : null}
            </div>
            {p.tagline ? <p className="mt-1 mb-0 text-[0.82rem] text-[#5E6A63]">{p.tagline}</p> : null}
            <p className="mt-3 mb-0">
              <span className="text-[1.55rem] font-extrabold text-[#1FA45A]">{p.price}</span>
              <span className="text-[0.85rem] text-[#5E6A63]">{p.period}</span>
            </p>
            <ul className="mt-3 mb-0 list-none space-y-1.5 p-0">
              {p.items.map((item) => (
                <li key={item} className="flex gap-2 text-[0.88rem] text-[#2A3830]">
                  <Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[#1FA45A]" strokeWidth={2.6} />
                  {item}
                </li>
              ))}
            </ul>
            <div className="mt-4">
              <PillCta href={c.appUrl} variant="solid">
                {p.cta}
              </PillCta>
            </div>
          </article>
        ))}
      </div>

      <h2 className="mb-2 mt-10 text-[1.2rem] font-extrabold text-[#103B2B]">{s.matrixTitle}</h2>
      <p className="mt-0 mb-4 text-[0.82rem] text-[#5E6A63]">{s.matrixCaption}</p>
      <div className="overflow-x-auto rounded-[20px] border border-[#E4E7E2] bg-white">
        <table className="w-full min-w-[640px] border-collapse text-left text-[0.88rem]">
          <thead>
            <tr className="border-b border-[#E4E7E2] bg-[#F7F5EF]">
              <th className="px-3 py-3 font-bold text-[#5E6A63]"> </th>
              {s.columns.map((col) => (
                <th key={col} className="px-3 py-3 text-center font-extrabold text-[#103B2B]">
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {s.rows.map((row: PlanCompareRow) => (
              <tr key={row.label} className="border-b border-[#EEF0ED] last:border-0">
                <td className="px-3 py-2.5 font-medium text-[#2A3830]">{row.label}</td>
                <td className="px-3 py-2.5 text-center">
                  <Cell value={row.free} />
                </td>
                <td className="px-3 py-2.5 text-center">
                  <Cell value={row.growth} />
                </td>
                <td className="px-3 py-2.5 text-center">
                  <Cell value={row.peace} />
                </td>
                <td className="px-3 py-2.5 text-center">
                  <Cell value={row.aiPlus} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-8 flex flex-wrap gap-3">
        <PillCta href={`/${locale}/`} variant="outline">
          {s.backCta}
        </PillCta>
        <PillCta href={c.appUrl} variant="dark">
          {s.appCta}
        </PillCta>
      </div>
    </SpecialtyShell>
  );
}

import { ArrowRight, Clock3, Sparkles } from 'lucide-react';
import { GuideShell } from '@/components/guide/GuideShell';
import { getGuideHubArticles, getGuideHub } from '@/lib/guide/content';

export async function GuideHubPage() {
  const hub = getGuideHub();
  const articles = getGuideHubArticles();

  return (
    <GuideShell
      eyebrow="FAMILY GUIDE"
      title={hub.title}
      lead="Những hướng dẫn ngắn gọn để cả nhà bắt đầu, cùng tạo nhịp sinh hoạt và dùng Famixa mỗi ngày."
    >
      <section aria-labelledby="guide-start">
        <div className="flex items-center gap-2 text-[#1FA45A]">
          <Sparkles className="h-5 w-5" aria-hidden />
          <h2 id="guide-start" className="m-0 text-[1.2rem] font-extrabold text-[#103B2B]">
            Bắt đầu tại đây
          </h2>
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          {articles.map((article, index) => (
            <a
              key={article.slug}
              href={article.slug}
              className="group flex min-h-28 items-center gap-4 rounded-2xl border border-[#E3EAE4] bg-white p-4 shadow-[0_5px_18px_rgba(16,59,43,0.05)] transition hover:-translate-y-0.5 hover:border-[#9ED9AF] hover:shadow-[0_10px_24px_rgba(16,59,43,0.1)]"
            >
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-[#EAF6EE] text-[0.82rem] font-extrabold text-[#1FA45A]">
                {String(index + 1).padStart(2, '0')}
              </span>
              <span className="min-w-0 flex-1">
                <strong className="block text-[0.96rem] leading-snug text-[#103B2B]">{article.title}</strong>
                <span className="mt-1 block text-[0.8rem] text-[#6B7280]">Xem hướng dẫn từng bước</span>
              </span>
              <ArrowRight className="h-4 w-4 shrink-0 text-[#1FA45A] transition-transform group-hover:translate-x-0.5" />
            </a>
          ))}
        </div>
      </section>

      <section className="mt-10 rounded-3xl border border-[#D9EDDF] bg-[#EDF8F0] p-6 sm:p-7" aria-labelledby="guide-first-day">
        <div className="flex items-center gap-2 text-[#1FA45A]">
          <Clock3 className="h-5 w-5" aria-hidden />
          <h2 id="guide-first-day" className="m-0 text-[1.2rem] font-extrabold text-[#103B2B]">
            Lộ trình 15 phút ngày đầu
          </h2>
        </div>
        <ol className="mt-5 grid gap-3 sm:grid-cols-2">
          {[
            'Tạo nhà hoặc đăng nhập',
            'Chọn hồ sơ bố/mẹ',
            'Thêm con hoặc mời người thân',
            'Chọn Routine đơn giản',
            'Mở Lịch hôm nay và làm thử một việc',
          ].map((step, index) => (
            <li key={step} className="flex items-center gap-3 rounded-xl bg-white/80 px-4 py-3 text-[0.92rem] font-semibold text-[#29493A]">
              <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-[#1FA45A] text-[0.72rem] text-white">
                {index + 1}
              </span>
              {step}
            </li>
          ))}
        </ol>
      </section>
    </GuideShell>
  );
}

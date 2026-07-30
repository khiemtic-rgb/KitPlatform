import { ArrowLeft, ArrowRight, Clock3, MessageCircleQuestion } from 'lucide-react';
import { GuideMarkdown } from '@/components/guide/GuideMarkdown';
import { GuideShell } from '@/components/guide/GuideShell';
import type { GuideDocument } from '@/lib/guide/content';

type Props = {
  article: GuideDocument;
  nextArticle?: GuideDocument;
};

function getReadingMinutes(body: string) {
  return Math.max(1, Math.ceil(body.split(/\s+/).filter(Boolean).length / 180));
}

export function GuideArticlePage({ article, nextArticle }: Props) {
  const readingMinutes = getReadingMinutes(article.body);

  return (
    <GuideShell eyebrow="FAMILY GUIDE" title={article.title}>
      <div className="mb-7 flex flex-wrap items-center gap-3 text-[0.85rem] text-[#5E6A63]">
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[#EAF6EE] px-3 py-1.5 font-semibold text-[#167A43]">
          <Clock3 className="h-3.5 w-3.5" aria-hidden />
          Đọc khoảng {readingMinutes} phút
        </span>
        <a href="/vi/huong-dan/" className="font-semibold text-[#1FA45A] hover:text-[#103B2B]">
          ← Về Family Guide
        </a>
      </div>

      <article className="rounded-3xl border border-[#E5EAE6] bg-white px-5 py-7 shadow-[0_8px_28px_rgba(16,59,43,0.05)] sm:px-8 sm:py-9">
        <GuideMarkdown content={article.body} />
      </article>

      <aside className="mt-7 rounded-2xl border border-[#D9EDDF] bg-[#EDF8F0] p-5 sm:flex sm:items-center sm:justify-between sm:gap-5" aria-label="Hỗ trợ Famixa">
        <div className="flex items-start gap-3">
          <MessageCircleQuestion className="mt-0.5 h-5 w-5 shrink-0 text-[#1FA45A]" aria-hidden />
          <div>
            <strong className="block text-[#103B2B]">Cần Famixa hỗ trợ?</strong>
            <span className="mt-1 block text-[0.88rem] leading-relaxed text-[#5E6A63]">
              Nhắn Zalo để được hướng dẫn theo tình huống của gia đình bạn.
            </span>
          </div>
        </div>
        <a
          href="https://zalo.me/0984660399"
          target="_blank"
          rel="noopener noreferrer"
          className="mt-4 inline-flex min-h-10 items-center gap-2 rounded-full bg-[#103B2B] px-4 text-[0.88rem] font-bold text-white transition hover:bg-[#0c2f22] sm:mt-0"
        >
          Chat Zalo
          <ArrowRight className="h-4 w-4" />
        </a>
      </aside>

      {nextArticle ? (
        <a
          href={nextArticle.slug}
          className="mt-7 flex items-center justify-between gap-4 rounded-2xl border border-[#E2EAE4] bg-white p-5 transition hover:border-[#9ED9AF] hover:shadow-[0_8px_22px_rgba(16,59,43,0.07)]"
        >
          <span>
            <span className="block text-[0.78rem] font-bold uppercase tracking-[0.08em] text-[#1FA45A]">Bạn nên xem tiếp</span>
            <strong className="mt-1 block text-[#103B2B]">{nextArticle.title}</strong>
          </span>
          <ArrowRight className="h-5 w-5 shrink-0 text-[#1FA45A]" />
        </a>
      ) : (
        <a
          href="/vi/huong-dan/"
          className="mt-7 inline-flex items-center gap-2 text-[0.9rem] font-bold text-[#1FA45A] hover:text-[#103B2B]"
        >
          <ArrowLeft className="h-4 w-4" />
          Xem lại tất cả hướng dẫn
        </a>
      )}
    </GuideShell>
  );
}

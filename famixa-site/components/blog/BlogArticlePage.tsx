import { ArrowLeft, ArrowRight, CalendarDays, ExternalLink } from 'lucide-react';
import { BlogMarkdown } from '@/components/blog/BlogMarkdown';
import { BlogShell } from '@/components/blog/BlogShell';
import { getBlogCategoryLabel } from '@/lib/blog/categories';
import type { BlogDocument } from '@/lib/blog/content';
import { formatBlogDate, getReadingMinutes } from '@/lib/blog/format';

type Props = {
  article: BlogDocument;
  appUrl: string;
  related?: BlogDocument[];
};

export function BlogArticlePage({ article, appUrl, related = [] }: Props) {
  const readingMinutes = getReadingMinutes(article.body);

  return (
    <BlogShell title={article.title}>
      <div className="mb-6 flex flex-wrap items-center gap-3 text-[0.85rem] text-[#5E6A63]">
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[#EAF6EE] px-3 py-1.5 font-semibold text-[#167A43]">
          {getBlogCategoryLabel(article.category)}
        </span>
        <span className="inline-flex items-center gap-1.5">
          <CalendarDays className="h-3.5 w-3.5" aria-hidden />
          {formatBlogDate(article.pubDate)}
        </span>
        <span>Đọc khoảng {readingMinutes} phút</span>
        <a href="/vi/goi-cha-me/" className="font-semibold text-[#1FA45A] hover:text-[#103B2B]">
          ← Về Góc cha mẹ
        </a>
      </div>

      <div className="overflow-hidden rounded-[24px] border border-[#E5EAE6] bg-white shadow-[0_8px_28px_rgba(16,59,43,0.05)]">
        <div className="aspect-[16/9] max-h-[420px] overflow-hidden bg-[#F3F0E8]">
          <img src={article.image} alt="" className="h-full w-full object-cover" loading="eager" />
        </div>
        <article className="px-5 py-7 sm:px-8 sm:py-9">
          {article.description ? (
            <p className="m-0 text-[1.05rem] font-medium leading-[1.65] text-[#29493A]">{article.description}</p>
          ) : null}
          <div className={article.description ? 'mt-6' : undefined}>
            <BlogMarkdown content={article.body} />
          </div>
        </article>
      </div>

      <aside className="mt-7 rounded-2xl border border-[#D9EDDF] bg-[#EDF8F0] p-5 sm:flex sm:items-center sm:justify-between sm:gap-5">
        <div>
          <strong className="block text-[#103B2B]">Thử Famixa với gia đình bạn</strong>
          <span className="mt-1 block text-[0.88rem] leading-relaxed text-[#5E6A63]">
            Bắt đầu miễn phí — dùng thử 30 ngày để trải nghiệm nhịp nhà nhẹ hơn.
          </span>
        </div>
        <a
          href={appUrl}
          className="mt-4 inline-flex min-h-10 items-center gap-2 rounded-full bg-[#103B2B] px-4 text-[0.88rem] font-bold text-white transition hover:bg-[#0c2f22] sm:mt-0"
        >
          Bắt đầu
          <ExternalLink className="h-4 w-4" />
        </a>
      </aside>

      {related.length ? (
        <section className="mt-10" aria-labelledby="blog-related">
          <h2 id="blog-related" className="m-0 text-[1.1rem] font-extrabold text-[#103B2B]">
            Bài liên quan
          </h2>
          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            {related.map((item) => (
              <a
                key={item.slug}
                href={`${item.slug}/`}
                className="rounded-2xl border border-[#E2EAE4] bg-white p-4 transition hover:border-[#9ED9AF] hover:shadow-[0_8px_22px_rgba(16,59,43,0.07)]"
              >
                <span className="block text-[0.75rem] font-bold uppercase tracking-[0.06em] text-[#1FA45A]">
                  {getBlogCategoryLabel(item.category)}
                </span>
                <strong className="mt-1 block text-[#103B2B]">{item.title}</strong>
              </a>
            ))}
          </div>
        </section>
      ) : (
        <a
          href="/vi/goi-cha-me/"
          className="mt-7 inline-flex items-center gap-2 text-[0.9rem] font-bold text-[#1FA45A] hover:text-[#103B2B]"
        >
          <ArrowLeft className="h-4 w-4" />
          Xem thêm bài viết
        </a>
      )}

      <a
        href="/vi/huong-dan/"
        className="mt-5 inline-flex items-center gap-2 text-[0.88rem] font-semibold text-[#5E6A63] hover:text-[#103B2B]"
      >
        Cần hướng dẫn dùng app?
        <ArrowRight className="h-4 w-4" />
      </a>
    </BlogShell>
  );
}

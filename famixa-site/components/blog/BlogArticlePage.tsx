import { ArrowRight, CalendarDays, ExternalLink } from 'lucide-react';
import { BlogArticleSidebar } from '@/components/blog/BlogArticleSidebar';
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
    <BlogShell title={article.title} wide>
      <div className="blog-article-layout">
        <div className="blog-article-main">
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
            <div className="overflow-hidden bg-[#F3F0E8]">
              <img
                src={article.image}
                alt=""
                className="block h-auto w-full max-h-[min(560px,72vh)] object-contain object-top"
                loading="eager"
              />
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

          <aside className="blog-article-banner mt-7">
            <div>
              <strong className="block text-[#103B2B]">Đồng hành cùng Famixa</strong>
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

          <a
            href="/vi/huong-dan/"
            className="mt-5 inline-flex items-center gap-2 text-[0.88rem] font-semibold text-[#5E6A63] hover:text-[#103B2B]"
          >
            Cần hướng dẫn dùng app?
            <ArrowRight className="h-4 w-4" />
          </a>
        </div>

        <BlogArticleSidebar category={article.category} appUrl={appUrl} related={related} />
      </div>
    </BlogShell>
  );
}

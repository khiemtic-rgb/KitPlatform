import { ArrowRight, CalendarDays, Leaf } from 'lucide-react';
import { BlogShell } from '@/components/blog/BlogShell';
import { BLOG_CATEGORIES } from '@/lib/blog/categories';
import { getBlogArticles, getBlogHubMeta } from '@/lib/blog/content';
import { formatBlogDate } from '@/lib/blog/format';

export async function BlogHubPage() {
  const hub = getBlogHubMeta();
  const articles = getBlogArticles();

  return (
    <BlogShell title={hub.title} lead={hub.description}>
      <section aria-labelledby="blog-categories">
        <div className="flex flex-wrap gap-2">
          {BLOG_CATEGORIES.map((category) => (
            <a
              key={category.id}
              href={`#cat-${category.id}`}
              className="rounded-full border border-[#D5E8DA] bg-white px-3 py-1.5 text-[0.78rem] font-semibold text-[#167A43] transition hover:border-[#9ED9AF] hover:bg-[#EAF6EE]"
            >
              {category.label}
            </a>
          ))}
        </div>
      </section>

      {articles.length === 0 ? (
        <p className="mt-8 rounded-2xl border border-dashed border-[#D5E8DA] bg-white px-5 py-8 text-center text-[#5E6A63]">
          Bài viết sắp có — quay lại sau nhé.
        </p>
      ) : (
        <div className="mt-8 grid gap-5 sm:grid-cols-2">
          {articles.map((article) => {
            const category = BLOG_CATEGORIES.find((item) => item.id === article.category);
            return (
              <article
                key={article.slug}
                id={`cat-${article.category}`}
                className="group overflow-hidden rounded-[24px] border border-[#E4E7E2] bg-white shadow-[0_8px_24px_rgba(16,59,43,0.05)] transition hover:-translate-y-0.5 hover:border-[#9ED9AF] hover:shadow-[0_12px_28px_rgba(16,59,43,0.08)]"
              >
                <a href={`${article.slug}/`} className="block">
                  <div className="aspect-[16/9] overflow-hidden bg-[#F3F0E8]">
                    <img
                      src={article.image}
                      alt=""
                      className="h-full w-full object-cover object-top transition duration-300 group-hover:scale-[1.02]"
                      loading="lazy"
                    />
                  </div>
                  <div className="p-5">
                    <div className="flex flex-wrap items-center gap-2 text-[0.75rem] font-bold uppercase tracking-[0.06em] text-[#1FA45A]">
                      <Leaf className="h-3.5 w-3.5" aria-hidden />
                      <span>{category?.label ?? article.category}</span>
                      <span className="text-[#B8C4BC]">·</span>
                      <span className="inline-flex items-center gap-1 normal-case tracking-normal text-[#6B7280]">
                        <CalendarDays className="h-3.5 w-3.5" aria-hidden />
                        {formatBlogDate(article.pubDate)}
                      </span>
                    </div>
                    <h2 className="m-0 mt-2 text-[1.08rem] font-extrabold leading-snug text-[#103B2B]">{article.title}</h2>
                    <p className="mt-2 mb-0 line-clamp-3 text-[0.92rem] leading-[1.6] text-[#5E6A63]">{article.excerpt}</p>
                    <span className="mt-4 inline-flex items-center gap-1.5 text-[0.86rem] font-bold text-[#1FA45A]">
                      Đọc tiếp
                      <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
                    </span>
                  </div>
                </a>
              </article>
            );
          })}
        </div>
      )}

      <aside className="mt-10 rounded-3xl border border-[#D9EDDF] bg-[#EDF8F0] p-6 sm:p-7">
        <h2 className="m-0 text-[1.1rem] font-extrabold text-[#103B2B]">Câu chuyện gia đình thật</h2>
        <p className="mt-2 mb-0 max-w-[36rem] text-[0.94rem] leading-[1.65] text-[#5E6A63]">
          Muốn đọc chia sẻ từ cha mẹ đã dùng Famixa? Xem mục Câu chuyện — tách riêng với bài viết tips ở đây.
        </p>
        <a
          href="/vi/cau-chuyen/"
          className="mt-4 inline-flex items-center gap-2 text-[0.9rem] font-bold text-[#1FA45A] hover:text-[#103B2B]"
        >
          Xem câu chuyện
          <ArrowRight className="h-4 w-4" />
        </a>
      </aside>
    </BlogShell>
  );
}

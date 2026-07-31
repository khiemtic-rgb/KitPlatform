import { ArrowRight, Check, Heart, Leaf, Sparkles, Users } from 'lucide-react';
import { getBlogCategoryLabel, type BlogCategoryId } from '@/lib/blog/categories';
import type { BlogDocument } from '@/lib/blog/content';
import { formatBlogDate, getReadingMinutes } from '@/lib/blog/format';
import { appLink, getBlogSidebar } from '@/lib/blog/sidebar';

type Props = {
  category: BlogCategoryId;
  appUrl: string;
  related?: BlogDocument[];
};

const WEEK_DAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

export function BlogArticleSidebar({ category, appUrl, related = [] }: Props) {
  const sidebar = getBlogSidebar(category);

  return (
    <div className="blog-sidebar space-y-4">
      <section className="blog-sidebar-card blog-sidebar-card--tip">
        <div className="blog-sidebar-card__icon" aria-hidden>
          <Sparkles className="h-4 w-4" />
        </div>
        <p className="blog-sidebar-quote">&ldquo;{sidebar.tip.quote}&rdquo;</p>
        <a href={appLink(appUrl, sidebar.tip.linkPath)} className="blog-sidebar-text-link">
          {sidebar.tip.linkLabel}
          <ArrowRight className="h-3.5 w-3.5" aria-hidden />
        </a>
      </section>

      <section className="blog-sidebar-card">
        <h2 className="blog-sidebar-heading">{sidebar.todayChecklist.title}</h2>
        <p className="blog-sidebar-sub">{sidebar.todayChecklist.subtitle}</p>
        <ul className="blog-sidebar-checklist">
          {sidebar.todayChecklist.items.map((item) => (
            <li key={item}>
              <span className="blog-sidebar-check" aria-hidden>
                <Check className="h-3 w-3" />
              </span>
              {item}
            </li>
          ))}
        </ul>
      </section>

      <section className="blog-sidebar-card blog-sidebar-card--challenge">
        <h2 className="blog-sidebar-heading">{sidebar.challenge.title}</h2>
        <p className="blog-sidebar-challenge-desc">{sidebar.challenge.description}</p>
        <div className="blog-sidebar-week" aria-hidden>
          {WEEK_DAYS.map((day, index) => (
            <span key={day} className={index < 3 ? 'is-done' : undefined}>
              <span className="blog-sidebar-week__dot">{index < 3 ? '✓' : ''}</span>
              <span className="blog-sidebar-week__label">{day}</span>
            </span>
          ))}
        </div>
        <a href={appLink(appUrl, sidebar.challenge.ctaPath)} className="blog-sidebar-outline-btn">
          {sidebar.challenge.ctaLabel}
        </a>
      </section>

      <section className="blog-sidebar-card">
        <h2 className="blog-sidebar-heading">{sidebar.coachTips.title}</h2>
        <ul className="blog-sidebar-coach">
          {sidebar.coachTips.items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
        <a href={appLink(appUrl, sidebar.coachTips.linkPath)} className="blog-sidebar-text-link">
          {sidebar.coachTips.linkLabel}
          <ArrowRight className="h-3.5 w-3.5" aria-hidden />
        </a>
      </section>

      {related.length ? (
        <section className="blog-sidebar-card">
          <h2 className="blog-sidebar-heading">Bài viết liên quan</h2>
          <ul className="blog-sidebar-related">
            {related.map((item) => (
              <li key={item.slug}>
                <a href={`${item.slug}/`} className="blog-sidebar-related__link">
                  <span className="blog-sidebar-related__meta">
                    {getBlogCategoryLabel(item.category)} · {getReadingMinutes(item.body)} phút
                  </span>
                  <strong>{item.title}</strong>
                  <span className="blog-sidebar-related__date">{formatBlogDate(item.pubDate)}</span>
                </a>
              </li>
            ))}
          </ul>
          <a href="/vi/goi-cha-me/" className="blog-sidebar-text-link">
            Xem tất cả bài viết
            <ArrowRight className="h-3.5 w-3.5" aria-hidden />
          </a>
        </section>
      ) : null}

      <section className="blog-sidebar-card blog-sidebar-card--discover">
        <div className="blog-sidebar-discover__icon" aria-hidden>
          <Leaf className="h-5 w-5" />
        </div>
        <h2 className="blog-sidebar-heading">Khám phá Famixa</h2>
        <p className="blog-sidebar-sub">
          Routine, thử thách và gợi ý nhỏ — giúp cả nhà đồng hành thay vì kiểm soát.
        </p>
        <a href={appUrl} className="blog-sidebar-solid-btn">
          Khám phá ngay
          <Heart className="h-4 w-4" aria-hidden />
        </a>
        <p className="blog-sidebar-footnote">
          <Users className="h-3.5 w-3.5" aria-hidden />
          Dùng thử 30 ngày miễn phí
        </p>
      </section>
    </div>
  );
}

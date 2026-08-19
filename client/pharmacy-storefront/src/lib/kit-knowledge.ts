import { getCollection } from 'astro:content';
import type { PharmacyArticle, PharmacyTenantConfig } from '../tenants/types';
import { xuanhoa } from '../tenants/xuanhoa';

function collectionSlug(id: string): string {
  const base = id.replace(/\.md$/i, '').split('/').pop() ?? id;
  return base.replace(/^\d{4}-\d{2}-\d{2}-/, '');
}

function isoDate(value: Date | string): string {
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    return value.toISOString().slice(0, 10);
  }
  const s = String(value);
  return s.length >= 10 ? s.slice(0, 10) : s;
}

function readingMinutesFrom(text: string, fallback?: number): number | undefined {
  if (fallback && fallback > 0) return fallback;
  const words = text.trim().split(/\s+/).filter(Boolean).length;
  if (words <= 0) return undefined;
  return Math.max(1, Math.round(words / 180));
}

/** Markdown posts published by Kit Marketing into `src/content/kien-thuc`. */
export async function loadKitKnowledgeArticles(): Promise<PharmacyArticle[]> {
  const posts = await getCollection('kien-thuc');
  return posts.map((post) => {
    const slug = collectionSlug(post.id);
    const body = typeof post.body === 'string' ? post.body : '';
    return {
      slug,
      title: post.data.title,
      date: isoDate(post.data.pubDate),
      excerpt: post.data.description,
      imageUrl: post.data.image,
      href: `/kien-thuc/${slug}`,
      body: undefined,
      categoryLabel: post.data.category,
      readingMinutes: readingMinutesFrom(body, post.data.readingMinutes),
      fromKitCollection: true,
    } satisfies PharmacyArticle;
  });
}

/**
 * Overlay Kit Marketing articles onto tenant JSON (CMS or static pilot).
 * Kit posts win on the same slug; static 2024 posts remain as fallback.
 */
export async function mergeKitKnowledge(tenant: PharmacyTenantConfig): Promise<PharmacyTenantConfig> {
  const kit = await loadKitKnowledgeArticles();
  if (kit.length === 0) {
    if (tenant.slug === 'xuanhoa' && (!tenant.articles || tenant.articles.length === 0)) {
      return { ...tenant, articles: xuanhoa.articles };
    }
    return tenant;
  }

  const bySlug = new Map<string, PharmacyArticle>();
  for (const article of tenant.articles ?? []) {
    bySlug.set(article.slug, article);
  }
  for (const article of kit) {
    bySlug.set(article.slug, article);
  }

  const articles = [...bySlug.values()].sort((a, b) => (a.date < b.date ? 1 : -1));
  return { ...tenant, articles };
}

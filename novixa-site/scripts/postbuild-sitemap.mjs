/**
 * After Astro sitemap build: also publish /sitemap.xml (same as sitemap-index).
 * Google Search Console often expects /sitemap.xml and can fail on redirects.
 */
import { copyFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';

const dist = resolve(process.cwd(), 'dist');
const indexFile = resolve(dist, 'sitemap-index.xml');
const sitemapFile = resolve(dist, 'sitemap.xml');

if (!existsSync(indexFile)) {
  console.warn('postbuild:sitemap — missing dist/sitemap-index.xml, skip');
  process.exit(0);
}

copyFileSync(indexFile, sitemapFile);
console.log('postbuild:sitemap — wrote dist/sitemap.xml');

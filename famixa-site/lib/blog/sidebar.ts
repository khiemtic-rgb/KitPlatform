import fs from 'node:fs';
import path from 'node:path';
import type { BlogCategoryId } from '@/lib/blog/categories';

const SIDEBAR_FILE = path.join(process.cwd(), 'content', 'blog', 'sidebar.json');

export type BlogSidebarTip = {
  quote: string;
  linkLabel: string;
  linkPath: string;
};

export type BlogSidebarChecklist = {
  title: string;
  subtitle: string;
  items: string[];
};

export type BlogSidebarChallenge = {
  title: string;
  description: string;
  ctaLabel: string;
  ctaPath: string;
};

export type BlogSidebarCoach = {
  title: string;
  items: string[];
  linkLabel: string;
  linkPath: string;
};

export type BlogSidebarConfig = {
  tip: BlogSidebarTip;
  todayChecklist: BlogSidebarChecklist;
  challenge: BlogSidebarChallenge;
  coachTips: BlogSidebarCoach;
};

type SidebarMap = Record<BlogCategoryId, BlogSidebarConfig>;

let cachedSidebar: SidebarMap | null = null;

function readSidebarMap(): SidebarMap {
  if (cachedSidebar) return cachedSidebar;
  const raw = fs.readFileSync(SIDEBAR_FILE, 'utf8');
  cachedSidebar = JSON.parse(raw) as SidebarMap;
  return cachedSidebar;
}

export function getBlogSidebar(category: BlogCategoryId): BlogSidebarConfig {
  const map = readSidebarMap();
  return map[category] ?? map['nuoi-day'];
}

export function appLink(appUrl: string, linkPath: string) {
  const base = appUrl.replace(/\/$/, '');
  const pathPart = linkPath.startsWith('/') ? linkPath : `/${linkPath}`;
  return `${base}${pathPart}`;
}

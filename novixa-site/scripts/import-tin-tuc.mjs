/**
 * Import tin tức từ novixa-site/import/tin-tuc.xlsx (hoặc .csv)
 * - pubDate: chỉ hiển thị trên site khi tới ngày (xem publishedNews.ts)
 * - Trùng slug hoặc title → cập nhật file .md hiện có
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import XLSX from 'xlsx';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '..');
const IMPORT_DIR = path.join(ROOT, 'import');
const OUT_DIR = path.join(ROOT, 'src/content/tin-tuc');
const INPUT_NAMES = ['tin-tuc.xlsx', 'tin-tuc.xls', 'tin-tuc.csv'];

function slugify(text) {
  return String(text)
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 80);
}

function parseDate(value) {
  if (value == null || value === '') return null;
  if (value instanceof Date && !Number.isNaN(value.valueOf())) return value;
  if (typeof value === 'number') {
    const parsed = XLSX.SSF.parse_date_code(value);
    if (parsed) return new Date(parsed.y, parsed.m - 1, parsed.d);
  }
  const s = String(value).trim();
  const dmy = s.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})$/);
  if (dmy) return new Date(+dmy[3], +dmy[2] - 1, +dmy[1]);
  const iso = Date.parse(s);
  if (!Number.isNaN(iso)) return new Date(iso);
  return null;
}

function normKey(row) {
  return Object.fromEntries(
    Object.entries(row).map(([k, v]) => [k.toLowerCase().trim(), v]),
  );
}

function extractDescFromContent(content) {
  if (!content) return '';
  for (const line of String(content).split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith('#')) continue;
    return t.replace(/\*\*/g, '').slice(0, 200);
  }
  return '';
}

function parseFrontmatter(raw) {
  const m = raw.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!m) return {};
  const title = m[1].match(/^title:\s*"(.*)"/m)?.[1];
  const slugFromFile = null;
  return { title };
}

function findExistingFile({ title, slug }) {
  const bySlug = path.join(OUT_DIR, `${slug}.md`);
  if (fs.existsSync(bySlug)) return `${slug}.md`;

  for (const file of fs.readdirSync(OUT_DIR).filter((f) => f.endsWith('.md'))) {
    const raw = fs.readFileSync(path.join(OUT_DIR, file), 'utf8');
    const meta = parseFrontmatter(raw);
    if (meta.title === title) return file;
  }
  return `${slug}.md`;
}

function formatPubDate(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function escapeYaml(s) {
  return String(s).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

function normalizeRow(row) {
  const r = normKey(row);
  const titleCol = r.title ?? r['tiêu đề'] ?? r.tieu_de;
  const descCol = r.description ?? r['mô tả'] ?? r.mo_ta;
  const title = String(titleCol ?? descCol ?? '')
    .trim()
    .replace(/\s+/g, ' ');
  if (!title) return null;

  const hasSeparateDesc = Boolean(titleCol && descCol && titleCol !== descCol);
  const description = hasSeparateDesc
    ? String(descCol).trim()
    : extractDescFromContent(r.content) || title.slice(0, 200);

  const pubDate = parseDate(r.pubdate ?? r['ngày đăng'] ?? r.ngay_dang);
  if (!pubDate) {
    console.warn(`⚠ Bỏ qua (thiếu pubDate hợp lệ): "${title}"`);
    return null;
  }

  const slug = String(r.slug ?? '').trim() || slugify(title);
  const content = String(r.content ?? r['nội dung'] ?? r.noi_dung ?? '').trim();

  if (!content) {
    console.warn(`⚠ Bỏ qua (thiếu content): "${title}"`);
    return null;
  }

  return { title, description, pubDate, slug, content };
}

function findInputFile() {
  for (const name of INPUT_NAMES) {
    const p = path.join(IMPORT_DIR, name);
    if (fs.existsSync(p)) return p;
  }
  return null;
}

function readRows(filePath) {
  const wb = XLSX.readFile(filePath, { cellDates: true });
  const sheet = wb.Sheets[wb.SheetNames[0]];
  return XLSX.utils.sheet_to_json(sheet, { defval: '' });
}

function writeMarkdown({ title, description, pubDate, content }, filename) {
  const body = `---
title: "${escapeYaml(title)}"
description: "${escapeYaml(description)}"
pubDate: ${formatPubDate(pubDate)}
lang: vi
---

${content}
`;
  fs.writeFileSync(path.join(OUT_DIR, filename), body, 'utf8');
}

function main() {
  if (!fs.existsSync(OUT_DIR)) fs.mkdirSync(OUT_DIR, { recursive: true });
  if (!fs.existsSync(IMPORT_DIR)) fs.mkdirSync(IMPORT_DIR, { recursive: true });

  const input = findInputFile();
  if (!input) {
    console.error(
      `Không tìm thấy file import. Đặt một trong các file sau vào ${IMPORT_DIR}:`,
    );
    for (const n of INPUT_NAMES) console.error(`  - ${n}`);
    process.exit(1);
  }

  const rows = readRows(input);
  let created = 0;
  let updated = 0;

  for (const row of rows) {
    const item = normalizeRow(row);
    if (!item) continue;

    const filename = findExistingFile(item);
    const target = path.join(OUT_DIR, filename);
    const exists = fs.existsSync(target);

    writeMarkdown(item, filename);
    if (exists) {
      updated++;
      console.log(`↻ Cập nhật: ${filename} (${formatPubDate(item.pubDate)})`);
    } else {
      created++;
      console.log(`+ Tạo mới: ${filename} (${formatPubDate(item.pubDate)})`);
    }
  }

  console.log(`\nXong: ${created} mới, ${updated} cập nhật (nguồn: ${path.basename(input)})`);
}

main();

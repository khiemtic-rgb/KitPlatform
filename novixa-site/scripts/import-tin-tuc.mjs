/**
 * Import tin tức từ novixa-site/import/tin-tuc.xlsx (hoặc .csv)
 * - pubDate: chỉ hiển thị trên site khi tới ngày (xem publishedNews.ts)
 * - Trùng slug hoặc title → cập nhật file .md hiện có
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import XLSX from 'xlsx';
import { loadDotEnv } from './load-env.mjs';

loadDotEnv();

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

function parseDate(value, { prevDate } = {}) {
  if (value == null || value === '') return null;
  if (typeof value === 'number') {
    const parsed = XLSX.SSF.parse_date_code(value);
    if (parsed) {
      let d = new Date(parsed.y, parsed.m - 1, parsed.d);
      // Excel m/d/yy: 1/7/2026 (VN = 1/7) hay bị đọc thành tháng 1
      if (prevDate && d.getMonth() === 0 && prevDate.getMonth() === 5) {
        d = new Date(parsed.y, 6, 1);
      }
      return d;
    }
  }
  if (value instanceof Date && !Number.isNaN(value.valueOf())) {
    let d = value;
    if (prevDate && d.getMonth() === 0 && prevDate.getMonth() === 5) {
      d = new Date(d.getFullYear(), 6, 1);
    }
    return d;
  }
  const s = String(value).trim();
  const dmy = s.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})$/);
  if (dmy) {
    const a = +dmy[1];
    const b = +dmy[2];
    const year = +dmy[3];
    let day;
    let month;
    if (a > 12) {
      day = a;
      month = b > 12 ? 6 : b;
    } else if (b > 12) {
      day = b;
      month = a;
    } else {
      day = a;
      month = b;
    }
    return new Date(year, month - 1, day);
  }
  const iso = Date.parse(s);
  if (!Number.isNaN(iso)) return new Date(iso);
  return null;
}

function normKey(row) {
  return Object.fromEntries(
    Object.entries(row).map(([k, v]) => [k.toLowerCase().trim(), v]),
  );
}

const TITLE_FROM_CONTENT = [
  [/Tồn kho là tài sản lớn nhất/i, 'Quản lý tồn kho thuốc hiệu quả cho nhà thuốc hiện đại'],
  [/chỉ theo dõi doanh thu/i, 'Các KPI quan trọng khi quản lý nhà thuốc'],
  [/Excel từng là công cụ/i, 'Vì sao Excel không còn phù hợp để quản lý nhà thuốc?'],
  [/nguyên nhân gây thất thoát lớn nhất tại nhà thuốc là hàng hóa hết hạn/i, 'Giảm thất thoát từ hàng cận date và hết hạn sử dụng'],
  [/FEFO|First Expired/i, 'FEFO là gì? Nguyên tắc bán hết hạn trước cho nhà thuốc'],
  [/chi nhánh thứ hai/i, 'Quản lý nhiều chi nhánh nhà thuốc — thách thức và giải pháp'],
  [/Chuyển đổi số đang trở thành/i, 'Chuyển đổi số cho nhà thuốc — bắt đầu từ đâu?'],
  [/Tồn kho là tài sản của nhà thuốc[\s\S]{0,400}tồn kho chết/i, 'Cách giảm tồn kho chết trong nhà thuốc'],
  [/tồn kho chết/i, 'Cách giảm tồn kho chết trong nhà thuốc'],
];

function extractTitleFromContent(content) {
  const text = String(content).trim();
  const h1 = text.match(/^#\s+(.+?)(?:\r?\n|$)/m);
  if (h1) return h1[1].trim();

  for (const [re, title] of TITLE_FROM_CONTENT) {
    if (re.test(text)) return title;
  }

  const para = text.split(/\r?\n\r?\n/).find((p) => {
    const t = p.trim();
    return t && !t.startsWith('#');
  });
  if (!para) return '';
  const sentence = para.trim().match(/^(.+?[.!?])(?:\s|$)/)?.[1] ?? para.trim();
  return sentence.replace(/\*\*/g, '').slice(0, 120);
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
  for (const file of fs.readdirSync(OUT_DIR).filter((f) => f.endsWith('.md'))) {
    const raw = fs.readFileSync(path.join(OUT_DIR, file), 'utf8');
    const meta = parseFrontmatter(raw);
    if (meta.title === title) return file;
  }

  const slugFile = `${slug}.md`;
  if (fs.existsSync(path.join(OUT_DIR, slugFile))) return slugFile;
  return slugFile;
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

function normalizeRow(row, { prevDate } = {}) {
  const r = normKey(row);
  const titleCol = r.title ?? r['tiêu đề'] ?? r.tieu_de;
  const descCol = r.description ?? r['mô tả'] ?? r.mo_ta;
  const content = String(r.content ?? r['nội dung'] ?? r.noi_dung ?? '').trim();

  let title = String(titleCol ?? '')
    .trim()
    .replace(/\s+/g, ' ');
  if (!title && descCol) title = String(descCol).trim().replace(/\s+/g, ' ');
  if (!title) title = extractTitleFromContent(content);
  if (!title) return null;

  const descText = String(descCol ?? '').trim();
  const placeholder = /^mô tả ngắn/i.test(descText);
  const hasSeparateDesc = Boolean(descText && !placeholder && descText !== title);
  const description = hasSeparateDesc
    ? descText
    : extractDescFromContent(content) || title.slice(0, 200);

  const pubDate = parseDate(r.pubdate ?? r['ngày đăng'] ?? r.ngay_dang, { prevDate });
  if (!pubDate) {
    console.warn(`⚠ Bỏ qua (thiếu pubDate hợp lệ): "${title}"`);
    return null;
  }

  const slug = String(r.slug ?? '').trim() || slugify(title);

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
  const rows = XLSX.utils.sheet_to_json(sheet, { defval: '' });
  rows.forEach((row, i) => {
    const cell = sheet[`C${i + 2}`];
    if (cell?.t === 's') row.pubDate = cell.v;
    else if (cell?.t === 'n') row.pubDate = cell.v;
  });
  return rows;
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

async function main() {
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

  let prevDate = null;
  for (const row of rows) {
    const item = normalizeRow(row, { prevDate });
    if (!item) continue;
    prevDate = item.pubDate;

    const filename = findExistingFile(item);
    const target = path.join(OUT_DIR, filename);
    const exists = fs.existsSync(target);

    writeMarkdown(item, filename);
    const slug = filename.replace(/\.md$/, '');
    console.log(`  · Ảnh sẽ tạo khi tới ngày đăng (${formatPubDate(item.pubDate)}): ${slug}`);

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

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

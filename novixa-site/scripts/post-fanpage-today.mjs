/**
 * Đăng bài tin có pubDate = hôm nay (giờ VN) lên fanpage Facebook.
 * Credentials: FB_PAGE_ID + FB_PAGE_ACCESS_TOKEN (env) hoặc import/Id_Fanpage.txt (local).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadFanpageCredentials } from './fanpage-config.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '..');
const CONTENT_DIR = path.join(ROOT, 'src/content/tin-tuc');
const POSTED_LOG = path.join(ROOT, 'import/fanpage-posted.json');
const SITE_URL = 'https://novixa.vn';
const GRAPH_VERSION = 'v21.0';

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const dateArg = args.find((a) => a.startsWith('--date='))?.split('=')[1];

function vnDateKey(d = new Date()) {
  return d.toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
}

function parseFrontmatter(raw) {
  const match = raw.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!match) return {};
  const meta = {};
  for (const line of match[1].split('\n')) {
    const m = line.match(/^(\w+):\s*(.+)$/);
    if (!m) continue;
    let val = m[2].trim();
    if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
      val = val.slice(1, -1);
    }
    meta[m[1]] = val;
  }
  return meta;
}

function loadPostedLog() {
  if (!fs.existsSync(POSTED_LOG)) return {};
  try {
    return JSON.parse(fs.readFileSync(POSTED_LOG, 'utf8'));
  } catch {
    return {};
  }
}

function savePostedLog(log) {
  fs.mkdirSync(path.dirname(POSTED_LOG), { recursive: true });
  fs.writeFileSync(POSTED_LOG, `${JSON.stringify(log, null, 2)}\n`, 'utf8');
}

function listArticlesForDate(targetDay) {
  const files = fs.readdirSync(CONTENT_DIR).filter((f) => f.endsWith('.md'));
  const articles = [];

  for (const file of files) {
    const slug = file.replace(/\.md$/, '');
    const raw = fs.readFileSync(path.join(CONTENT_DIR, file), 'utf8');
    const meta = parseFrontmatter(raw);
    if (!meta.title || !meta.pubDate) continue;

    const pub = new Date(meta.pubDate);
    if (Number.isNaN(pub.getTime())) continue;
    if (vnDateKey(pub) !== targetDay) continue;

    articles.push({
      slug,
      title: meta.title,
      description: meta.description ?? '',
      pubDate: meta.pubDate,
      url: `${SITE_URL}/vi/tin-tuc/${slug}`,
    });
  }

  return articles.sort((a, b) => a.slug.localeCompare(b.slug));
}

function buildMessage(article) {
  const lines = [article.title];
  if (article.description) lines.push('', article.description);
  lines.push('', `Đọc thêm: ${article.url}`);
  return lines.join('\n');
}

async function postToFacebook({ pageId, accessToken, article }) {
  const body = new URLSearchParams({
    message: buildMessage(article),
    link: article.url,
    access_token: accessToken,
  });

  const res = await fetch(`https://graph.facebook.com/${GRAPH_VERSION}/${pageId}/feed`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  });

  const data = await res.json();
  if (!res.ok) {
    const code = data?.error?.code;
    const msg = data?.error?.message ?? res.statusText;
    if (code === 200 || /pages_manage_posts|pages_read_engagement/i.test(msg)) {
      throw new Error(
        'Token thiếu quyền hoặc không phải Page Token. Trong Graph API Explorer: thêm pages_manage_posts + pages_read_engagement → GET me/accounts → copy access_token của fanpage Novixa (không dùng token ô "Mã người dùng").',
      );
    }
    throw new Error(`Facebook API: ${msg}`);
  }
  return data.id;
}

async function main() {
  const creds = loadFanpageCredentials();
  if (!creds) {
    console.log('Bỏ qua fanpage: chưa có FB_PAGE_ID/FB_PAGE_ACCESS_TOKEN hoặc import/Id_Fanpage.txt');
    process.exit(0);
  }

  const targetDay = dateArg ?? vnDateKey();
  const articles = listArticlesForDate(targetDay);
  const posted = loadPostedLog();

  if (articles.length === 0) {
    console.log(`Không có bài tin pubDate = ${targetDay} (VN).`);
    return;
  }

  console.log(`Fanpage: ${articles.length} bài cho ngày ${targetDay} (nguồn creds: ${creds.source})`);

  for (const article of articles) {
    const logKey = `${article.slug}:${targetDay}`;
    if (posted[logKey]) {
      console.log(`  skip (đã đăng): ${article.slug} → post ${posted[logKey].postId}`);
      continue;
    }

    if (dryRun) {
      console.log(`  [dry-run] ${article.title}`);
      console.log(`    ${article.url}`);
      continue;
    }

    try {
      const postId = await postToFacebook({ ...creds, article });
      posted[logKey] = {
        postId,
        slug: article.slug,
        pubDate: targetDay,
        postedAt: new Date().toISOString(),
      };
      savePostedLog(posted);
      console.log(`  posted: ${article.slug} → ${postId}`);
    } catch (err) {
      console.error(`  lỗi ${article.slug}:`, err.message);
      process.exitCode = 1;
    }
  }
}

main();

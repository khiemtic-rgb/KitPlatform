import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const LOCAL_FILE = path.join(ROOT, 'import', 'Id_Fanpage.txt');

/** Đọc từ env (CI) hoặc import/Id_Fanpage.txt (local, gitignored). */
export function loadFanpageCredentials() {
  const pageId = process.env.FB_PAGE_ID?.trim();
  const accessToken = process.env.FB_PAGE_ACCESS_TOKEN?.trim();
  if (pageId && accessToken) {
    return { pageId, accessToken, source: 'env' };
  }

  if (!fs.existsSync(LOCAL_FILE)) {
    return null;
  }

  const raw = fs.readFileSync(LOCAL_FILE, 'utf8');
  const idMatch = raw.match(/Page\s*ID\s*:\s*(\d+)/i);
  const tokenMatch = raw.match(/Page\s*Access\s*Token\s*:\s*(\S+)/i);
  if (!idMatch || !tokenMatch) {
    throw new Error(`Không đọc được Page ID / Token từ ${LOCAL_FILE}`);
  }

  return {
    pageId: idMatch[1],
    accessToken: tokenMatch[1],
    source: 'file',
  };
}

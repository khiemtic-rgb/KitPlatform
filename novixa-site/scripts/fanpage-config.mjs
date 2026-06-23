import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const LOCAL_FILE = path.join(ROOT, 'import', 'Id_Fanpage.txt');

function parseCredentialsFile(raw) {
  const trimmed = raw.trim();

  // Format 1: Page ID: ... / Page Access Token: ...
  const idLabel = trimmed.match(/Page\s*ID\s*:\s*(\d+)/i);
  const tokenLabel = trimmed.match(/Page\s*Access\s*Token\s*:\s*(\S+)/i);
  if (idLabel && tokenLabel) {
    return { pageId: idLabel[1], accessToken: tokenLabel[1] };
  }

  // Format 2: JSON từ GET me/accounts (copy 1 object fanpage)
  const idJson = trimmed.match(/"id"\s*:\s*"(\d+)"/);
  const tokenJson = trimmed.match(/"access_token"\s*:\s*"([^"]+)"/);
  if (idJson && tokenJson) {
    return { pageId: idJson[1], accessToken: tokenJson[1] };
  }

  return null;
}

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

  const parsed = parseCredentialsFile(fs.readFileSync(LOCAL_FILE, 'utf8'));
  if (!parsed) {
    throw new Error(
      `Không đọc được Page ID / Token từ ${LOCAL_FILE}. Dùng Id_Fanpage.template.txt hoặc dán JSON từ GET me/accounts.`,
    );
  }

  return { ...parsed, source: 'file' };
}

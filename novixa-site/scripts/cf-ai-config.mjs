import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '..');
const LOCAL_FILE = path.join(ROOT, 'import/cf-workers-ai.txt');

/** Đọc Account ID + API Token từ env hoặc import/cf-workers-ai.txt (local, gitignored). */
export function loadCfAiCredentials() {
  const accountId = process.env.CLOUDFLARE_ACCOUNT_ID?.trim();
  const apiToken = process.env.CLOUDFLARE_API_TOKEN?.trim();
  if (accountId && apiToken) {
    return { accountId, apiToken, source: 'env' };
  }

  if (!fs.existsSync(LOCAL_FILE)) return null;

  const text = fs.readFileSync(LOCAL_FILE, 'utf8');
  const lines = {};
  for (const line of text.split(/\r?\n/)) {
    const m = line.match(/^([^:=]+)[:=]\s*(.+)$/);
    if (m) lines[m[1].trim().toLowerCase()] = m[2].trim();
  }

  const id = lines['account id'] ?? lines.accountid ?? lines.account_id;
  const token = lines['api token'] ?? lines.apitoken ?? lines.token;
  if (id && token) return { accountId: id, apiToken: token, source: 'file' };

  return null;
}

export function hasCfAiCredentials() {
  return Boolean(loadCfAiCredentials());
}

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const out = join(root, 'src', 'data', 'public-feed.json');
const api = (process.env.PUBLIC_LOCAL_OS_API || 'http://127.0.0.1:5290/api/public/local-os').replace(/\/$/, '');

async function getJson(path) {
  const res = await fetch(`${api}${path}`);
  if (!res.ok) throw new Error(`${path} ${res.status}`);
  return res.json();
}

mkdirSync(dirname(out), { recursive: true });

try {
  const [listings, groups] = await Promise.all([
    getJson('/listings'),
    getJson('/groups'),
  ]);
  const feed = { listings, groups, exportedAt: new Date().toISOString() };
  writeFileSync(out, JSON.stringify(feed));
  const counts = listings.reduce((acc, row) => {
    acc[row.kind] = (acc[row.kind] || 0) + 1;
    return acc;
  }, {});
  console.log(`public-feed: ${listings.length} listings`, counts, `${groups.length} groups`);
} catch (err) {
  writeFileSync(out, JSON.stringify({ listings: [], groups: [], exportedAt: null }));
  console.warn(`public-feed: empty (${err.message})`);
}

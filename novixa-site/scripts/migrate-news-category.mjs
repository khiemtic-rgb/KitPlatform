/**
 * One-off: ensure every tin-tuc markdown has category: tin-tuc-novixa
 * Run: node scripts/migrate-news-category.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dir = path.join(root, 'src/content/tin-tuc');

let updated = 0;
for (const name of fs.readdirSync(dir).filter((f) => f.endsWith('.md'))) {
  const filePath = path.join(dir, name);
  const raw = fs.readFileSync(filePath, 'utf8');
  const match = raw.match(/^---\r?\n([\s\S]*?)\r?\n---(\r?\n[\s\S]*)$/);
  if (!match) {
    console.warn('skip (no frontmatter):', name);
    continue;
  }
  const block = match[1];
  const rest = match[2];
  if (/^category:\s*\S+/m.test(block)) continue;

  let nextBlock = block;
  if (/^lang:\s*\S+/m.test(block)) {
    nextBlock = block.replace(/^(lang:\s*\S+)/m, '$1\ncategory: tin-tuc-novixa');
  } else {
    nextBlock = `${block.trimEnd()}\ncategory: tin-tuc-novixa`;
  }
  fs.writeFileSync(filePath, `---\n${nextBlock}\n---${rest}`, 'utf8');
  updated++;
  console.log('+', name);
}

console.log(`Done. Updated ${updated} files.`);

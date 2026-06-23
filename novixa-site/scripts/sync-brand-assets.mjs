import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const importDir = path.join(root, 'import');
const outDir = path.join(root, 'public', 'images');

const files = ['logo.png', 'banner.png'];

fs.mkdirSync(outDir, { recursive: true });

for (const name of files) {
  const src = path.join(importDir, name);
  const dest = path.join(outDir, name);
  if (!fs.existsSync(src)) {
    console.warn(`skip: ${src} not found`);
    continue;
  }
  fs.copyFileSync(src, dest);
  console.log(`synced ${name} → public/images/`);
}

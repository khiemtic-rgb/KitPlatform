/**
 * Hạ font-weight cực đoan (650–900) về thang giống Facebook:
 * 400 body · 600 semibold · 700 bold. Chạy được nhiều lần (idempotent).
 */
import { readFileSync, writeFileSync } from 'node:fs';

const FILE = new URL('../src/styles/app.css', import.meta.url);
const MAP = { 900: 700, 850: 700, 800: 600, 750: 600, 650: 600 };

const css = readFileSync(FILE, 'utf8');
let changed = 0;

const out = css.replace(/([^{}]+)\{([^{}]*)\}/g, (full, sel, body) => {
  const next = body.replace(/font-weight:\s*(\d{3})/g, (decl, weight) => {
    const mapped = MAP[Number(weight)];
    if (!mapped) return decl;
    changed += 1;
    return decl.replace(weight, String(mapped));
  });
  return next === body ? full : `${sel}{${next}}`;
});

writeFileSync(FILE, out);
console.log(`font-weight declarations normalized: ${changed}`);

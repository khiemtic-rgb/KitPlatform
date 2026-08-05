/**
 * Lightweight assertions (no Vitest) — run: node scripts/check-routine-focus.mjs
 * Mirrors sanitize/filter rules used by FamilyRoutine admin.
 */
import assert from 'node:assert/strict';

/** Copied logic — keep in sync with src/modules/admin/routine-focus.ts */
const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function templateVisibleForFocus(t, focus) {
  if (focus === 'all') return true;
  return !t.memberId || t.memberId === focus;
}

function sanitizeRoutineFocus(raw, childIds) {
  if (!raw || raw === 'all') return 'all';
  if (!UUID_RE.test(raw)) return 'all';
  if (childIds.length === 0) return raw;
  return childIds.includes(raw) ? raw : 'all';
}

const childA = '11111111-1111-4111-8111-111111111111';
const childB = '22222222-2222-4222-8222-222222222222';

assert.equal(sanitizeRoutineFocus(null, [childA]), 'all');
assert.equal(sanitizeRoutineFocus('all', [childA]), 'all');
assert.equal(sanitizeRoutineFocus('not-a-uuid', [childA]), 'all');
assert.equal(sanitizeRoutineFocus(childA, [childA, childB]), childA);
assert.equal(sanitizeRoutineFocus(childB, [childA]), 'all');
assert.equal(sanitizeRoutineFocus(childA, []), childA);

const templates = [
  { isActive: true, memberId: undefined },
  { isActive: true, memberId: childA },
  { isActive: true, memberId: childB },
  { isActive: false, memberId: childA },
];

assert.equal(templates.filter((t) => templateVisibleForFocus(t, 'all')).length, 4);
assert.equal(templates.filter((t) => templateVisibleForFocus(t, childA)).length, 3);
assert.equal(templates.filter((t) => templateVisibleForFocus(t, childB)).length, 2);
assert.ok(templateVisibleForFocus({ memberId: undefined }, childA));
assert.ok(!templateVisibleForFocus({ memberId: childB }, childA));

console.log('routine-focus checks OK');

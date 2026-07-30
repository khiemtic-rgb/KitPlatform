/**
 * Wave B smoke — pure helpers (no browser).
 * Run: node scripts/smoke-blueprint-wave-b.mjs
 */
import assert from 'node:assert/strict';

// Inline mirrors of resolvePlaybookId / because rules for CI-less smoke.
function looksBrush(t = '') {
  return /đánh răng|danh rang|brush/i.test(t);
}
function looksPack(t = '') {
  return /cặp|balo|chuẩn bị|chuan bi|đồng phục/i.test(t);
}
function resolvePlaybookId(input) {
  const title = input.focusTitle ?? '';
  if ((input.patternForgotCount ?? 0) >= 3) {
    if (looksBrush(title)) return 'PB0001';
    if (looksPack(title)) return 'PB0002';
    return 'PB0008';
  }
  const code = (input.proposalCode ?? '').trim();
  if (code === 'suggest_move_after_dinner' || code === 'suggest_move_after_school') {
    if (looksBrush(title)) return 'PB0001';
    if (looksPack(title)) return 'PB0002';
    return 'PB0008';
  }
  if (input.blueprintSparse) return 'PB0020';
  return null;
}

function becauseFromDna(dna) {
  if (!dna?.hasBlueprint) return null;
  const values = (dna.valuesLabelsVi ?? []).filter(Boolean).slice(0, 2);
  const focus = (dna.focusLabelsVi ?? []).filter(Boolean).slice(0, 2);
  if (values.length || focus.length) {
    const parts = [];
    if (values.length) parts.push(values.length === 1 ? `chọn giá trị ${values[0]}` : `chọn ${values.join(' & ')}`);
    if (focus.length) parts.push(focus.length === 1 ? `đang tập trung ${focus[0]}` : `đang tập trung ${focus.join(' & ')}`);
    return `Vì nhà bạn ${parts.join(' và ')}.`;
  }
  return null;
}

// 1) Playbook map
assert.equal(
  resolvePlaybookId({ patternForgotCount: 3, focusTitle: 'Đánh răng sáng' }),
  'PB0001',
);
assert.equal(
  resolvePlaybookId({
    proposalCode: 'suggest_move_after_dinner',
    focusTitle: 'Chuẩn bị cặp',
  }),
  'PB0002',
);
assert.equal(
  resolvePlaybookId({ patternForgotCount: 4, focusTitle: 'Uống sữa' }),
  'PB0008',
);
assert.equal(resolvePlaybookId({ blueprintSparse: true }), 'PB0020');

// 2) Sparse — never invent because
assert.equal(becauseFromDna(null), null);
assert.equal(becauseFromDna({ hasBlueprint: false }), null);

// 3) Hydrated DNA — because present
const because = becauseFromDna({
  hasBlueprint: true,
  valuesLabelsVi: ['Học tập', 'Tôn trọng'],
  focusLabelsVi: ['Tự lập'],
});
assert.ok(because && because.includes('Vì nhà bạn'));
assert.ok(because.includes('Học tập'));
assert.ok(because.includes('Tự lập'));

console.log('OK smoke-blueprint-wave-b: PB map + sparse + because');

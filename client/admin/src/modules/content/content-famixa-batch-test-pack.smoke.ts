import { parseEpisodeStory } from './content-famixa-story-parse';
import { emptyPilot, parseFamixaPack, shotHasValidAction, wrapStaffScriptForParse } from './content-famixa-series';
import { needsInheritanceReview } from './content-famixa-story-memory';
import { applyBatchTestPack, BATCH_TEST_PACK } from './content-famixa-batch-test-pack';

const fail: string[] = [];
const doc = parseEpisodeStory(BATCH_TEST_PACK);
if (!doc) fail.push('test pack must parse');
else {
  if (doc.shots.length !== 3) fail.push(`want 3 shots, got ${doc.shots.length}: ${doc.shots.map((s) => s.story).join(' | ')}`);
  if (doc.shots.some((s) => !shotHasValidAction(s))) fail.push('empty production shot');
  if (doc.shots.some((s) => s.splitReason === 'INSERT')) fail.push('test pack must not INSERT');
  const spoken = (doc.lines ?? []).filter((l) => l.text?.trim());
  if (spoken.length < 3) fail.push(`want 3 spoken lines, got ${spoken.length}`);
  if (spoken.some((l) => /ôm|xin lỗi|bài học/i.test(l.text))) fail.push('must not invent hug/apology');
}

const pack = parseFamixaPack(BATCH_TEST_PACK);
if (pack.error) fail.push(pack.error);
if ((pack.episode?.shots.length ?? 0) !== 3) fail.push(`pack shots ${pack.episode?.shots.length}`);

const built = applyBatchTestPack(emptyPilot());
if ((built.episode?.shots.length ?? 0) !== 3) fail.push(`apply shots ${built.episode?.shots.length}`);
if (needsInheritanceReview(built)) fail.push('empty prev must not block inheritance review');
if ((built.packDraft ?? '') !== BATCH_TEST_PACK) fail.push('packDraft must keep test pack');

const fromEp01 = applyBatchTestPack({
  ...emptyPilot(),
  episode: { episode: 'EP01', title: 'EP01', shots: [] },
  storyMemory: {
    season: 'Season 01',
    ledger: [{ episode: 'EP01', approved: true }],
    threads: [{ id: 't1', name: 'lời hứa', status: 'OPEN' as const, createdEpisode: 'EP01' }],
    inheritReviewed: false,
    inheritFromEpisode: 'EP01',
    characterStates: [],
    relationships: [],
  },
});
if (needsInheritanceReview(fromEp01)) fail.push('test pack must drop EP01 memory so lock is not blocked');
if (fromEp01.storyMemory?.inheritFromEpisode === 'EP01') fail.push('must not inherit EP01');
if (fromEp01.storyMemory?.ledger?.some((e) => e.episode === 'EP01')) fail.push('must not keep EP01 ledger');

const staffBody = `Phòng khách. Minh cầm giấy kiểm tra, đứng trước mẹ.
Minh (khẽ):
Mẹ ơi… con được 6.
Mẹ:
Mẹ không hỏi điểm. Mẹ hỏi con ổn không.`;
const staffRaw = parseFamixaPack(staffBody);
if (staffRaw.error) {
  const wrapped = wrapStaffScriptForParse({ title: 'Phía Sau Điểm Số', episode: '100', body: staffBody });
  if (wrapped === staffBody) fail.push('wrap must add SC01 for staff prose');
  const staffPack = parseFamixaPack(wrapped);
  if (staffPack.error) fail.push(`staff wrap parse: ${staffPack.error}`);
  if ((staffPack.episode?.shots.length ?? 0) < 1) fail.push('staff wrap must emit shots');
  if ((staffPack.scenes?.length ?? 0) < 1) fail.push('staff wrap must emit scenes');
} else if ((staffRaw.episode?.shots.length ?? 0) < 1) {
  fail.push('staff prose must emit shots');
}

if (fail.length) {
  console.error('BATCH TEST PACK FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('BATCH TEST PACK PASS · 3 shot · 3 thoại');

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  diagnoseEpisodeStory,
  isNonCinematicAction,
  isNonStoryLine,
  looksLikeActingParenthetical,
  looksLikeScriptMetaHeading,
  looksLikeSpokenLine,
  parseEpisodeStory,
  planBeatShots,
} from './content-famixa-story-parse';

const here = dirname(fileURLToPath(import.meta.url));
const golden = readFileSync(join(here, 'content-famixa-ep01-golden.txt'), 'utf8');
const poisoned =
  golden +
  '\n\nSC10 — decomposition must not generate an arbitrary large number of shots.\nDO NOT copy SC01 into SC02.\n';

const doc = parseEpisodeStory(poisoned);
if (!doc) throw new Error('Golden EP01 failed to parse.');

const fail: string[] = [];
const headingPack = parseEpisodeStory(`KỊCH BẢN PHIM NGẮN / DRAMA VOICE FAMIXA

### SH01 — MINH MUỐN KHOE
Minh đặt bài kiểm tra trước mặt mẹ, đẩy nhẹ về phía Linh.
MINH
Mẹ ơi mẹ ơi, cô ghi cái này cho con nè!
`);
if (!looksLikeScriptMetaHeading('Tone: Tàn nhẫn, thực dụng, nghẹt thở')) {
  fail.push('Tone: heading is meta');
}
const tonePack = parseEpisodeStory(`SC01 — BỮA CƠM
Tone: Tàn nhẫn, thực dụng, nghẹt thở, chạm đáy tự ái của đứa trẻ.
Thời lượng mục tiêu: 130s
Minh chạy ùa từ cửa vào.
MINH
Mẹ! Con gỡ được điểm Toán rồi!
`);
if (tonePack?.shots.some((s) => /^tone\s*:/i.test(s.story || '') || /thời lượng/i.test(s.story || ''))) {
  fail.push('Tone:/Thời lượng must not become SH Action');
}
if (tonePack?.scenes.some((sc) => (sc.dialogue ?? []).some((d) => /tàn nhẫn|thời lượng/i.test(d.text) || /tone/i.test(d.characterId + d.text)))) {
  fail.push('Tone heading must not become TTS dialogue');
}
if (!tonePack?.shots.some((s) => /chạy ùa|cửa vào/i.test(s.story || ''))) {
  fail.push('first real action must stay');
}
if (headingPack?.shots.some((s) => /kịch bản phim|drama voice/i.test(s.story || ''))) {
  fail.push('pack title must not become SH01 Action');
}
if (!headingPack?.shots.some((s) => /đặt bài|khoe|Minh/i.test(s.story || ''))) {
  fail.push('SH01 must keep Minh showing the test');
}
if (doc.scenes.length !== 7) {
  fail.push(`expected 7 script scenes, got ${doc.scenes.map((s) => s.id + ':' + (s.title || '')).join(' | ')}`);
}
const titles = doc.scenes.map((s) => `${s.id} ${(s.title ?? '').toUpperCase()}`);
for (const want of [
  'LỜI HỨA',
  'TIN NHẮN',
  'SÁNG THỨ BẢY',
  'KHÔNG SAO',
  'MỘT NGÀY RẤT BÌNH THƯỜNG',
  'BỐ VỀ',
  'KẾT',
]) {
  if (!titles.some((t) => t.includes(want))) fail.push(`missing scene ${want}`);
}
if (doc.scenes.some((s) => s.id === 'SC10' || s.id === 'SC08')) fail.push('invented scenes beyond script');
if (!/BỐ ĐỪNG HỨA NỮA/i.test(doc.episode.title || '')) fail.push(`wrong title: ${doc.episode.title}`);

const dump = JSON.stringify(doc);
if (/STORY INTENT|Không được diễn thành|EMOTIONAL END STATE|decomposition must not|BÀN ĂN|CUỘC TRÒ CHUYỆN/i.test(dump)) {
  fail.push('other-script or instruction leaked into parsed graph');
}

const shotsByScene = new Map<string, number>();
for (const s of doc.shots) {
  const id = s.sceneId || s.scene || '';
  shotsByScene.set(id, (shotsByScene.get(id) ?? 0) + 1);
}
for (const [id, n] of shotsByScene) {
  if (n > 24) fail.push(`${id} exploded to ${n} shots`);
}
if (doc.shots.length > 96) fail.push(`too many shots: ${doc.shots.length}`);
if (doc.shots.some((s) => !(s.story || '').trim())) fail.push('empty shot without Action');
if (doc.shots.some((s) => !s.beatId)) fail.push('shot without Script Beat');
if (doc.shots.some((s) => s.inheritFromShotId)) fail.push('parser auto-inherited every previous shot');
if (new Set(doc.shots.map((s) => s.shot)).size !== doc.shots.length) fail.push('shot codes reused');
if (new Set(doc.shots.map((s) => s.id)).size !== doc.shots.length) fail.push('shot ids reused');
if (!doc.shots.every((s) => s.id.startsWith('EP01-'))) fail.push('shot ids missing EP01 prefix');

const byId = Object.fromEntries(doc.scenes.map((s) => [s.id, s]));
const texts = (id: string) => (byId[id]?.dialogue ?? []).map((d) => d.text).join('\n');

if (/thứ bảy bố nhớ|ngoắc tay|xem trận bóng/i.test(texts('SC02'))) fail.push('SC02 contains SC01 dialogue');
if (/mai bố vẫn đi|sao chưa ngủ/i.test(texts('SC01'))) fail.push('SC01 contains SC02 dialogue');
if (/lần sau bố đừng hứa/i.test(texts('SC01'))) fail.push('SC01 contains SC04 payoff');
if (/con thích bố hứa|bố là bố con/i.test(texts('SC07'))) fail.push('SC07 contains SC06 dialogue');
if (!/ngoắc tay/i.test(texts('SC01'))) fail.push('SC01 lost Ngoắc tay');
if (!/mai bố vẫn đi với con/i.test(texts('SC02'))) fail.push('SC02 lost Mai bố vẫn đi');
if (!/bố ơi/i.test(texts('SC03'))) fail.push('SC03 lost Bố ơi');
if (!/lần sau bố đừng hứa nữa/i.test(texts('SC04'))) fail.push('SC04 lost the line');
if ((byId.SC05?.dialogue?.length ?? 0) > 0) fail.push('SC05 invented dialogue');
if (!/con thích bố hứa/i.test(texts('SC06'))) fail.push('SC06 lost Con thích bố hứa');
if (!/không hứa những chuyện bố chưa chắc/i.test(texts('SC07'))) fail.push('SC07 lost the vow');

if (doc.lines.length !== 71) fail.push(`expected 71 spoken lines, got ${doc.lines.length}`);
const speakers = new Set(doc.lines.map((l) => l.characterId));
if (speakers.size < 3) fail.push(`expected 3 speakers, got ${[...speakers].join(',')}`);
if (doc.characters.some((c) => /^CHAR-00[5-9]$/i.test(c.id))) {
  fail.push(`golden invented extra CHAR: ${doc.characters.map((c) => c.id).join()}`);
}

const anPack = parseEpisodeStory(`SC01 — PHÒNG ĂN
Tone: Tàn nhẫn.
Bạn An được nhắc trong thoại, không vào khung.
MINH
Mẹ, bạn An được chín!
LINH
Bạn An chín rưỡi.
`);
if (anPack?.characters.some((c) => c.id === 'CHAR-005' || /tone/i.test(c.name))) {
  fail.push('An pack must not invent Tone/CHAR-005');
}
if (anPack?.shots.some((s) => (s.characterIds ?? s.characters ?? []).includes('CHAR-004'))) {
  fail.push('An mention must not land on shot characterIds');
}
if ((anPack?.shots[0]?.characterIds ?? []).filter((id) => id !== 'CHAR-VO').length > 2) {
  fail.push(`An pack shot has too many bodies: ${(anPack?.shots[0]?.characterIds ?? []).join()}`);
}

const room = parseEpisodeStory(`SC01 — PHÒNG KHÁCH
FORMAT: SHORT-FORM / SOCIAL DRAMA
NỘI. PHÒNG KHÁCH - TỐI
Ánh sáng vàng.
Linh ngồi sofa nhìn điện thoại.
Minh chạy vào phòng khách, đưa bài kiểm tra cho mẹ và đứng chờ phản ứng.
MINH
Nhưng...
Con đã tiến bộ rồi mẹ ạ.
Linh buông tay. Tờ giấy rơi xuống mặt kính. Linh tiếp tục bấm điện thoại.

Tờ bài nằm yên trên mặt kính.
`);
if (room?.shots.some((s) => /short-?form|nội\.|ánh sáng vàng|khoảng lặng|tiếng bàn/i.test(s.story || ''))) {
  fail.push(`slug/lighting/SFX must not be shots: ${room?.shots.map((s) => s.story).join(' | ')}`);
}
if (room?.shots.filter((s) => /^nhưng/i.test((s.story || '').replace(/^Minh:\s*/i, ''))).length) {
  fail.push('dialogue fragment Nhưng… must not be its own shot');
}
const roomStories = (room?.shots ?? []).map((s) => s.story).join(' || ');
if (!/Linh ngồi|nhìn điện thoại/i.test(roomStories)) fail.push('Linh on phone must stay one action shot');
if ((room?.shots.filter((s) => /chạy vào|đưa bài|chờ/i.test(s.story || '')).length ?? 0) !== 1) {
  fail.push(`run+hand+wait must stay 1 shot, got ${roomStories}`);
}
if (!room?.shots.some((s) => s.splitReason === 'INSERT' && /tờ giấy|tờ bài|mặt kính/i.test(s.story || ''))) {
  fail.push(`paper fall / lying paper should be INSERT: ${roomStories}`);
}
if (!looksLikeActingParenthetical('(hào hức, thở dốc)')) fail.push('acting paren is direction');
if (looksLikeSpokenLine('(phẳng lì, không cảm xúc)', 'Minh')) fail.push('acting paren must not be spoken');
const acted = parseEpisodeStory(`SC01 — PHÒNG
MINH: (hào hức, thở dốc)
Mẹ ơi con được chín điểm!
LINH: (phẳng lì, không cảm xúc)
Ừ.
`);
if (acted?.lines.some((l) => /hào hức|phẳng lì|thở dốc/i.test(l.text))) {
  fail.push(`acting note leaked into dialogue: ${acted.lines.map((l) => l.text).join(' | ')}`);
}
if (!acted?.lines.some((l) => /chín điểm/i.test(l.text))) fail.push('spoken line after acting paren must stay');
if ((acted?.lines.length ?? 0) !== 2) fail.push(`expected 2 spoken lines after parens, got ${acted?.lines.map((l) => l.text).join(' | ')}`);

if (!isNonCinematicAction('Ánh sáng vàng.')) fail.push('lighting is not cinematic');
if (planBeatShots(['Ánh sáng vàng.', 'Linh nhìn điện thoại.'], []).length !== 1) {
  fail.push('lighting + mother on phone = 1 shot');
}

fail.push(...diagnoseEpisodeStory(doc));
if (!isNonStoryLine('SC10 — decomposition must not generate an arbitrary large number of shots.')) {
  fail.push('instruction line not filtered');
}

if (fail.length) {
  console.error('GOLDEN EP01 FAIL');
  for (const f of fail) console.error(' -', f);
  console.error(
    JSON.stringify(
      {
        title: doc.episode.title,
        scenes: doc.scenes.map((s) => ({
          id: s.id,
          title: s.title,
          lines: (s.dialogue ?? []).map((d) => d.text),
        })),
        shots: doc.shots.map((s) => s.id),
        lineN: doc.lines.length,
        warnings: doc.warnings,
      },
      null,
      2,
    ),
  );
  process.exit(1);
}
console.log(
  `GOLDEN EP01 PASS · ${doc.scenes.length} scenes · ${doc.shots.length} shots · ${doc.lines.length} lines · warnings=${doc.warnings.length}`,
);

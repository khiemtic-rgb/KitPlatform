import { looksLikeSpokenLine, parseEpisodeStory } from './content-famixa-story-parse';
import { deriveVoiceScript, formatVoiceScriptPreview } from './content-famixa-voice-script';
import type { SeriesPilotState } from './content-famixa-series';

const excerpt = `
SC01 — BÀI KIỂM TRA

GÕ
Mẹ ơi, con được 8 điểm!
Minh định gửi.
Dừng lại.
Xóa tin nhắn.
Cất điện thoại.
Minh nhìn bài kiểm tra lần nữa.

MINH
Về khoe mới được.
Minh chạy về.
Mẹ!
Linh đang chuẩn bị cơm.

LINH
Gì mà chạy như ma đuổi thế?
Minh chìa bài kiểm tra.

MINH
Mẹ xem này.
Linh nhận lấy.
Nhìn điểm.

LINH
Tám?
Minh cười rất tươi.

MINH
Tám.

LINH
Lần trước?

MINH
Năm.

LINH
Bạn An?
Minh khựng lại.

MINH
Con không biết.

LINH
Chín rưỡi.
Minh nhìn mẹ.

MINH
Thì sao?

LINH
Thì con nhìn bạn mà học.
Cố thêm đi.
Linh đặt bài kiểm tra xuống.
Nụ cười lúc mới về nhà đã không còn.

NAM
Anh về rồi.

MINH
Bố xem này!

NAM
Ồ, tám à?
Ghê đấy.
Thế là lên ba điểm rồi còn gì.

LINH
Anh đừng khen nó vội.
Linh từ bếp nói vọng ra:
Bạn An chín rưỡi. Bạn Hoàng mười.

MINH
Bố mẹ đừng nói nữa được không?
Con được tám mà.
Thế phải bao nhiêu mới vừa ý mẹ?

NAM
Ngủ sớm đi.
Nam đóng cửa.
Minh ngồi im.
Điện thoại sáng lên.
Tin nhắn từ bạn:
Mai cô trả bài Anh nhé.
Nhớ câu:
Bạn An chín rưỡi.
Minh nhìn xuống.

LINH
Em chỉ muốn nó tốt hơn thôi.

NAM
Anh biết.

LINH
Thế sao anh cứ làm như em đang làm điều gì sai?
Minh gấp bài kiểm tra lại.
Cho vào cặp.

CẠCH.
`;

const tagged = `
[GÕ]
Mẹ ơi, con được 8 điểm!

[GÕ]
Minh định gửi.

[MINH]
Về khoe mới được.

[MINH]
Minh chạy về.

[MINH]
Mẹ!

[LINH]
Gì mà chạy như ma đuổi thế?

[LINH]
Minh chìa bài kiểm tra.

[MINH]
Mẹ xem này.
`;

const fail: string[] = [];

const neverVoice: { text: string; who?: string }[] = [
  { text: 'Minh chạy về.' },
  { text: 'Linh đang chuẩn bị cơm.' },
  { text: 'Cho vào cặp.' },
  { text: 'Minh gấp bài kiểm tra lại.' },
  { text: 'Mai cô trả bài Anh nhé.' },
  { text: 'Bạn An chín rưỡi.', who: 'Nam' },
  { text: 'GÕ' },
  { text: 'CẠCH.' },
  { text: 'Dừng lại.' },
  { text: 'Xóa tin nhắn.' },
  { text: 'Cất điện thoại.' },
  { text: 'Nhìn điểm.' },
  { text: 'Tin nhắn từ bạn:' },
  { text: 'Nhớ câu:' },
  { text: 'Linh từ bếp nói vọng ra:' },
];
for (const row of neverVoice) {
  if (looksLikeSpokenLine(row.text, row.who)) {
    fail.push(`classifier leaked speech: ${row.who ?? ''} ${row.text}`);
  }
}
for (const keep of ['Mẹ!', 'Ngoắc tay.', 'Tám?', 'Cố thêm đi.', 'Ngủ sớm đi.', 'Bạn An chín rưỡi. Bạn Hoàng mười.']) {
  if (!looksLikeSpokenLine(keep, keep.startsWith('Bạn An') ? 'Linh' : 'Minh')) {
    fail.push(`classifier dropped speech: ${keep}`);
  }
}

function check(label: string, source: string, keep: string[]) {
  const parsed = parseEpisodeStory(source);
  if (!parsed) {
    fail.push(`${label}: parse failed`);
    return;
  }
  const state = {
    roles: parsed.roles,
    runs: {},
    episode: parsed.episode,
    characters: parsed.characters,
    scenes: parsed.scenes,
    lines: parsed.lines,
  } as SeriesPilotState;
  const script = deriveVoiceScript(state);
  const preview = formatVoiceScriptPreview(script);
  const spoken = script.lines.map((l) => l.text);

  if (script.lines.some((l) => /gõ|cạch/i.test(l.name) || /gõ|cạch/i.test(l.characterId))) {
    fail.push(`${label}: SFX slug became a speaker`);
  }
  for (const leak of [
    'Minh chạy về',
    'Linh đang chuẩn bị cơm',
    'Minh chìa bài kiểm tra',
    'Linh nhận lấy',
    'Nhìn điểm',
    'Minh cười rất tươi',
    'Minh định gửi',
    'Dừng lại',
    'Xóa tin nhắn',
    'Cất điện thoại',
    'Nụ cười lúc mới về',
    'Linh từ bếp',
    'CẠCH',
    'Cho vào cặp',
    'Minh gấp bài kiểm tra lại',
    'Mai cô trả bài Anh nhé',
  ]) {
    if (spoken.some((t) => t.includes(leak)) || preview.includes(leak)) {
      fail.push(`${label}: action leaked — ${leak}`);
    }
  }
  if (script.lines.some((l) => /^Bạn An chín rưỡi\.?$/i.test(l.text) && /nam/i.test(l.name))) {
    fail.push(`${label}: recalled line leaked as Nam dialogue`);
  }
  for (const line of keep) {
    if (!spoken.includes(line)) fail.push(`${label}: lost spoken line — ${line}`);
  }
}

check('screenplay', excerpt, [
  'Về khoe mới được.',
  'Mẹ!',
  'Gì mà chạy như ma đuổi thế?',
  'Tám?',
  'Cố thêm đi.',
  'Em chỉ muốn nó tốt hơn thôi.',
  'Thế sao anh cứ làm như em đang làm điều gì sai?',
  'Bạn An chín rưỡi. Bạn Hoàng mười.',
  'Ngủ sớm đi.',
]);
check('tagged-voice-paste', tagged, ['Về khoe mới được.', 'Mẹ!', 'Gì mà chạy như ma đuổi thế?']);

const voPack = `
SC08 — LỜI BÌNH
VOICE-OVER (Giọng đọc trầm ấm, truyền cảm):

Áp lực thành tích không dạy con trưởng thành. Sự ghi nhận tiến trình mới nuôi dưỡng tính tự giác.
`;
const voDoc = parseEpisodeStory(voPack);
const voScript = deriveVoiceScript({
  roles: [],
  runs: {},
  episode: voDoc?.episode,
  characters: voDoc?.characters ?? [],
  scenes: voDoc?.scenes ?? [],
  lines: voDoc?.lines ?? [],
} as SeriesPilotState);
if (!voScript.lines.some((l) => /Áp lực thành tích không dạy con trưởng thành/i.test(l.text))) {
  fail.push('VOICE-OVER spoken line must be in Voice Script');
}
if (voScript.lines.some((l) => /Giọng đọc trầm ấm/i.test(l.text))) {
  fail.push('VOICE-OVER must not speak the voice direction');
}
if (!voScript.lines.some((l) => l.characterId === 'CHAR-VO')) {
  fail.push('VOICE-OVER speaker must be CHAR-VO');
}
if (voDoc?.roles.some((r) => r.characterId === 'CHAR-VO') !== true) {
  fail.push('Lời bình must be a Cast role');
}

const bible = `
SC01 — BÀI KIỂM TRA ĐIỂM 9
Minh: Giọng háo hức, mong manh, khao khát được công nhận.
Linh: Giọng sắc lạnh, thực dụng, mệt mỏi, không hề nghĩ mình sai.
Nam: Giọng kiệt sức, bất lực nhưng thương con.
Linh: Để lên bàn.
Linh: Mấy?
`;
check('voice-bible', bible, ['Để lên bàn.', 'Mấy?']);
const bibleDoc = parseEpisodeStory(bible);
const bibleScript = deriveVoiceScript({
  roles: [],
  runs: {},
  episode: bibleDoc?.episode,
  characters: bibleDoc?.characters ?? [],
  scenes: bibleDoc?.scenes ?? [],
  lines: bibleDoc?.lines ?? [],
} as SeriesPilotState);
for (const leak of ['Giọng háo hức', 'Giọng sắc lạnh', 'Giọng kiệt sức']) {
  if (bibleScript.lines.some((l) => l.text.includes(leak))) fail.push(`bible leaked — ${leak}`);
}

const leakedGraph = {
  roles: [],
  runs: {},
  episode: { episode: 'EP02', title: 'Tám điểm' },
  characters: [
    { id: 'CHAR-001', name: 'Minh' },
    { id: 'CHAR-002', name: 'Nam' },
    { id: 'CHAR-003', name: 'Linh' },
    { id: 'CHAR-004', name: 'GÕ' },
  ],
  scenes: [
    {
      id: 'SC01',
      dialogue: [
        { id: 'a', characterId: 'CHAR-004', text: 'Mẹ ơi, con được 8 điểm!' },
        { id: 'b', characterId: 'CHAR-001', text: 'Minh chạy về.' },
        { id: 'c', characterId: 'CHAR-001', text: 'Mẹ!' },
        { id: 'd', characterId: 'CHAR-003', text: 'Cho vào cặp.' },
        { id: 'e', characterId: 'CHAR-002', text: 'Bạn An chín rưỡi.' },
        { id: 'f', characterId: 'CHAR-002', text: 'Mai cô trả bài Anh nhé.' },
        { id: 'g', characterId: 'CHAR-003', text: 'Bạn An chín rưỡi. Bạn Hoàng mười.' },
      ],
    },
  ],
  lines: [],
} as unknown as SeriesPilotState;

const cleaned = deriveVoiceScript(leakedGraph);
const cleanedTexts = cleaned.lines.map((l) => l.text);
if (!cleanedTexts.includes('Mẹ!') || !cleanedTexts.includes('Bạn An chín rưỡi. Bạn Hoàng mười.')) {
  fail.push(`leaked-graph lost spoken: ${cleanedTexts.join(' | ')}`);
}
if (cleanedTexts.some((t) => t === 'Bạn An chín rưỡi.' || /Mai cô trả bài/i.test(t) || t === 'Cho vào cặp.' || t === 'Minh chạy về.')) {
  fail.push(`leaked-graph still has action/SMS: ${cleanedTexts.join(' | ')}`);
}

if (fail.length) {
  console.error('VOICE ACTION FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('VOICE ACTION PASS');

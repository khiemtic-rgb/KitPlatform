/** Video ngắn theo cảnh. Run: npx tsx src/modules/content/novixa-shorts.smoke.ts */
import { spokenLine } from './film-production-orchestrator';
import { findDisclaimer, parseExtraLines, resolveShortPlan, scriptScenes, splitIntoShorts } from './novixa-shorts';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

// Nhãn đánh số là lời đọc; tên người nói vẫn bị bỏ.
ok(spokenLine('Một: chỉ mang vào thị trường số tiền mất hết vẫn ngủ được.').startsWith('Một:'), 'giữ "Một:"');
ok(spokenLine('Thứ hai: không nạp thêm.').startsWith('Thứ hai:'), 'giữ "Thứ hai:"');
ok(spokenLine('Bước 3: ghi nhật ký.').startsWith('Bước 3:'), 'giữ "Bước 3:"');
ok(spokenLine('Lan: Chào bạn.') === 'Chào bạn.', 'bỏ tên người nói');
ok(spokenLine('Cảnh 2. Cháy ví') === '', 'tiêu đề cảnh không đọc');

const raw = [
  '## Cảnh 1. Mở đầu',
  'Câu một của cảnh một?',
  'Câu hai của cảnh một.',
  '',
  'Cảnh 2 – Ví dụ',
  'Hãy hình dung một ví dụ.',
  'Lệnh đi ngược và người đó nạp thêm.',
  'Cháy ví là lấy tiền sống để cứu tài khoản.',
  '**Cảnh 3.** Ba quy tắc',
  'Thứ nhất, mất hết vẫn ngủ được.',
  'Thứ ba, không vay để giao dịch.',
  'Cảnh 4. Kết',
  'Giữ vốn trước, tính lãi sau.',
  'Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư.',
  'Giao dịch có rủi ro mất vốn.',
];
const lines = raw.map(spokenLine).filter(Boolean);
const scenes = scriptScenes(raw, spokenLine);
ok(scenes.length === 4, 'bốn cảnh');
ok(scenes[1].n === 2 && scenes[1].from === 2 && scenes[1].to === 4 && scenes[1].title === 'Ví dụ', 'cảnh 2 = câu 3–5');
ok(scenes[2].title === 'Ba quy tắc', 'tiêu đề cảnh trong **');
ok(scriptScenes(['Không có tiêu đề cảnh.', 'Câu hai.'], spokenLine).length === 0, 'không có cảnh');

const disclaimer = findDisclaimer(lines);
ok(disclaimer?.from === 8 && disclaimer?.to === 9, 'câu miễn trừ = hai câu cuối');

const one = resolveShortPlan({ id: 'P01', title: 'Cháy ví', scenes: [2], extra: '', disclaimer: true }, scenes, lines);
ok(JSON.stringify(one.parts) === JSON.stringify([{ from: 2, to: 4 }, { from: 8, to: 9 }]), 'cảnh 2 + miễn trừ');
ok(one.errors.length === 0 && one.hasDisclaimer, 'cảnh 2 hợp lệ');

const three = resolveShortPlan({ id: 'P02', title: 'Tiền vay', scenes: [2], extra: '7', disclaimer: true }, scenes, lines);
ok(three.parts.length === 3 && three.parts[1].from === 6, 'ghép câu không liền');

const last = resolveShortPlan({ id: 'P03', title: 'Kết', scenes: [4], extra: '', disclaimer: true }, scenes, lines);
ok(last.parts.length === 1 && last.parts[0].from === 7 && last.parts[0].to === 9, 'cảnh chứa miễn trừ không lặp');

const none = resolveShortPlan({ id: 'P04', title: '', scenes: [2], extra: '', disclaimer: false }, scenes, lines);
ok(none.riskFlags.some((flag) => flag.includes('miễn trừ')), 'cảnh báo khi thiếu miễn trừ');

const bad = resolveShortPlan({ id: 'P05', title: '', scenes: [9], extra: 'x, 99', disclaimer: true }, scenes, lines);
ok(bad.errors.length >= 3, 'báo lỗi cảnh và câu sai');
ok(parseExtraLines('2, 4-5', 11).parts.length === 2, 'đọc câu thêm');

ok(splitIntoShorts(lines).length >= 1, 'gợi ý tự động vẫn chạy');

if (fail.length) {
  console.error('FAIL', fail);
  process.exit(1);
}
console.log('novixa-shorts smoke: OK');

/** Ngắt nghỉ lời đọc NGV. Run: npx tsx src/modules/content/ngv-spoken-breaks.smoke.ts */
import { insertSpokenBreaks, sceneEndsForLines } from './ngv-spoken-breaks';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};
const tags = (text: string) => text.match(/<break/g)?.length ?? 0;

const comma = insertSpokenBreaks('Xin chào, bạn nhé.', false, true);
ok(comma.includes(', <break time="0.3s" />'), 'phẩy 0.3');
ok(comma.endsWith('<break time="0.6s" />'), 'hết câu 0.6');
ok(tags(comma) === 2, 'phẩy không chồng');

const question = insertSpokenBreaks('Bạn có nghe không?', false, true);
ok(question.includes('?'), 'giữ dấu hỏi');
ok(question.endsWith('<break time="0.6s" />'), 'hết câu hỏi');
ok(tags(question) === 1, 'một nhịp hỏi');

const sceneA = insertSpokenBreaks('Câu một.', true, true);
const sceneB = insertSpokenBreaks('Câu hai?', false, true);
ok(sceneA.endsWith('<break time="1.2s" />'), 'hết cảnh 1.2');
ok(!sceneA.includes('0.6s'), 'hết cảnh không kèm 0.6');
ok(tags(sceneA) === 1, 'một nhịp hết cảnh');
ok(sceneB.endsWith('<break time="0.6s" />') && tags(sceneB) === 1, 'cảnh sau 0.6');

const decimal = insertSpokenBreaks('Khoảng 0,3 giây.', false, true);
ok(decimal.includes('0,3'), 'giữ số thập phân');
ok(!decimal.includes('0, <break'), 'không ngắt trong số');
ok(tags(decimal) === 1, 'chỉ ngắt hết câu');

const noComma = insertSpokenBreaks('Xin chào, bạn nhé.', false, false);
ok(!noComma.includes('0.3s'), 'tắt ngắt phẩy');
ok(noComma.endsWith('<break time="0.6s" />'), 'vẫn ngắt hết câu');

const script = ['Cảnh 1. Mở', 'Câu một.', 'Cảnh 2. Tiếp', 'Câu hai?'].join('\n');
const spoken = (raw: string) => (/^Cảnh\s+\d+/i.test(raw.trim()) ? '' : raw.trim());
ok(sceneEndsForLines(script, ['Câu một.', 'Câu hai?'], spoken).join(',') === '0,1', 'hai cảnh');

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('ngv-spoken-breaks ok');

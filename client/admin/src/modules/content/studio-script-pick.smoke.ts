/** Chọn kịch bản khi tạo phim. Run: npx tsx src/modules/content/studio-script-pick.smoke.ts */
import { defaultScriptPick, filmsByScript, orderScripts, suggestFilmTitle } from './studio-script-pick';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const scripts = [
  { id: 's1', code: '01', title: 'Hộp hết hạn', approved: true },
  { id: 's2', code: '02', title: 'Hỏi đúng loại hàng', approved: true },
  { id: 's3', code: '03', title: 'Két thiếu', approved: true },
  { id: 's4', code: '04', title: 'Lệch một chút', approved: true },
  { id: 's7', code: '07', title: 'Vắng một buổi', approved: false },
  { id: 's5', code: '05', title: 'Món giữ tiền', approved: true },
];
const films = [
  { id: 'f1', episodeCode: 'E01', source: { id: 's1', code: '01' } },
  { id: 'f2', episodeCode: 'E02', source: { id: 's2', code: '02' } },
  { id: 'f3', episodeCode: 'E03', source: { code: '03' } }, // phim cũ chỉ lưu mã
  { id: 'fx', episodeCode: 'EX', source: null }, // dán tay
];

const rows = filmsByScript(scripts, films);
ok(rows.find((r) => r.script.id === 's1')!.films.length === 1, 'ghép theo id');
ok(rows.find((r) => r.script.id === 's3')!.films.length === 1, 'ghép theo mã khi thiếu id');
ok(rows.every((r) => !r.films.some((f) => f.id === 'fx')), 'phim dán tay không gắn vào kịch bản nào');

const hidden = orderScripts(rows, false).map((r) => r.script.code);
ok(JSON.stringify(hidden) === JSON.stringify(['04', '05', '07']), `ẩn kịch bản đã có phim, đã duyệt lên trước: ${hidden.join(',')}`);

const all = orderScripts(rows, true).map((r) => r.script.code);
ok(JSON.stringify(all) === JSON.stringify(['04', '05', '07', '01', '02', '03']), `hiện cả đã làm phim ở cuối: ${all.join(',')}`);

ok(defaultScriptPick(orderScripts(rows, false)) === 's4', 'chọn sẵn 04');
ok(defaultScriptPick(orderScripts(filmsByScript([scripts[4]], []), false)) === '', 'chỉ có chờ duyệt thì không chọn sẵn');

ok(suggestFilmTitle(rows.find((r) => r.script.id === 's4')!) === '04 – Lệch một chút', 'tên phim mới');
ok(suggestFilmTitle(rows.find((r) => r.script.id === 's1')!) === '01 – Hộp hết hạn (bản 2)', 'tên bản 2');

if (fail.length) {
  console.error('FAIL:\n' + fail.join('\n'));
  process.exit(1);
}
console.log('studio-script-pick OK');

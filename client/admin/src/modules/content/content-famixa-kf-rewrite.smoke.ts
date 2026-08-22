import { compileKfRewrite, parseKfRewriteJson, sanitizeKfRewrite } from './content-famixa-kf-rewrite';

const fail: string[] = [];
const night = compileKfRewrite('Phòng này sáng ban ngày, shot trước đang ăn tối.', {
  action: 'Minh chìa bài kiểm tra.',
  location: 'Bữa cơm tối',
});
if (!night.lighting) fail.push('day-vs-night must flag lighting');
if (!night.place) fail.push('room complaint must flag place');
if (!night.inherit) fail.push('default inherit');
if (!/đêm|ấm|shot trước/i.test(night.instruction)) fail.push(`instruction ${night.instruction}`);
if (/ôm|xin lỗi/.test(night.instruction)) fail.push('must not invent plot');

const wrongPic = compileKfRewrite('Ảnh Minh khoe điểm với mẹ mà? vẽ cái gì đấy?', {
  action: 'Minh đặt bài kiểm tra trước mặt mẹ.',
});
if (!/live-action|không vẽ character|film still/i.test(wrongPic.instruction)) {
  fail.push('wrong-draw complaint must ban character sheet');
}
if (/ôm|xin lỗi/.test(wrongPic.instruction)) fail.push('complaint must not invent plot');

const empty = compileKfRewrite('');
if (!empty.instruction.includes('shot trước')) fail.push('empty note still inherits');

const banned = sanitizeKfRewrite({ instruction: 'Minh ôm mẹ và xin lỗi.', inherit: true, source: 'ai' }, night);
if (banned.instruction.includes('ôm')) fail.push('sanitize must drop banned plot');

const parsed = parseKfRewriteJson(
  '{"instruction":"Cùng bàn tối, giữ áo. Chỉ đổi Action.","place":true,"lighting":true,"wardrobe":true,"camera":false,"inherit":true}',
  night,
);
if (parsed.source !== 'ai' || !parsed.wardrobe) fail.push('parse ai json');

if (fail.length) {
  console.error('KF REWRITE FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log('KF REWRITE PASS');

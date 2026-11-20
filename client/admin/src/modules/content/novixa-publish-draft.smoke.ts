/** Bước 5 — soạn bài đăng video ngắn. Run: npx tsx src/modules/content/novixa-publish-draft.smoke.ts */
import { draftShortPublish } from './novixa-publish-draft';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const lines = [
  'Mình gọi đó là cháy ví khi tiền sống đã hết.',
  'Hai mươi triệu tiền sống, đừng đưa vào một lệnh.',
  'Giữ phần tiền sinh hoạt ở ngoài tài khoản giao dịch.',
];

const novixa = draftShortPublish({
  lines,
  headline: lines[0],
  filmTitle: 'A2 – Vốn sống và vốn giao dịch',
  brandCode: 'novixa',
  long: { title: 'Vốn sống | Novixa', youtubeDescription: '', tiktokCaption: '', facebookCaption: '', facebookComment: '' },
});
const novixaText = [novixa.title, novixa.youtube, novixa.tiktok, novixa.facebook, novixa.fbComment].join('\n');

ok(!novixaText.toLowerCase().includes('lời khuyên đầu tư'), 'Novixa không có "lời khuyên đầu tư"');
ok(!novixa.title.startsWith('Mình gọi đó là'), 'tiêu đề không bắt đầu "Mình gọi đó là"');
ok(novixa.title.startsWith('Hai mươi triệu'), 'tiêu đề là câu móc có số');
ok(!novixa.title.includes('|'), 'tiêu đề ngắn không kèm tên kênh');
ok(!novixaText.includes('Bạn thấy điều này có đúng với mình không?'), 'không chèn câu hỏi chung chung');
ok(novixa.warnings.some((item) => item.includes('câu hỏi cuối bài')), 'thiếu câu hỏi thì có cảnh báo');

const ngv = draftShortPublish({
  lines,
  headline: lines[0],
  filmTitle: 'A2 – Vốn sống và vốn giao dịch',
  brandCode: 'ngv',
});
const ngvText = [ngv.youtube, ngv.tiktok, ngv.facebook].join('\n');
ok(ngvText.includes('không phải lời khuyên đầu tư'), 'NGV vẫn có câu miễn trừ đầu tư');
ok(!ngv.title.startsWith('Mình gọi đó là') && !ngv.title.includes('|'), 'NGV cũng lấy câu móc, không gắn tên kênh');

if (fail.length) {
  console.error('FAIL', fail);
  process.exit(1);
}
console.log('novixa-publish-draft smoke: OK');

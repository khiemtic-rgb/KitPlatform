/**
 * PDF in quầy: Hướng dẫn nhân viên giới thiệu Novixa Health.
 * Chạy: node generate-novixa-health-staff-script-pdf.mjs
 */
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);

const playwrightRoots = [
  path.join(__dirname, 'node_modules', 'playwright'),
  path.resolve(__dirname, '../../../06-compliance/cuc-qld-lien-thong/print/node_modules/playwright'),
];

let chromium;
for (const root of playwrightRoots) {
  try {
    ({ chromium } = require(root));
    break;
  } catch {
    /* try next */
  }
}
if (!chromium) {
  try {
    ({ chromium } = require('playwright'));
  } catch {
    console.error('Install: cd docs/novixa/07-customer/guides/print && npm i playwright && npx playwright install chromium');
    process.exit(1);
  }
}

const logoPath = path.resolve(
  __dirname,
  '../../../06-compliance/cuc-qld-lien-thong/assets/logo-novixa.png',
);
if (!fs.existsSync(logoPath)) {
  console.error('Missing logo:', logoPath);
  process.exit(1);
}
const logoDataUri = `data:image/png;base64,${fs.readFileSync(logoPath).toString('base64')}`;

function fontDataUri(fontPath) {
  return `data:font/ttf;base64,${fs.readFileSync(fontPath).toString('base64')}`;
}

const fontRegular = fontDataUri('C:/Windows/Fonts/segoeui.ttf');
const fontBold = fontDataUri('C:/Windows/Fonts/segoeuib.ttf');
const fontSemibold = fs.existsSync('C:/Windows/Fonts/seguisb.ttf')
  ? fontDataUri('C:/Windows/Fonts/seguisb.ttf')
  : fontBold;

const htmlPath = path.join(__dirname, 'novixa-health-staff-script.html');
const pdfPath = path.join(
  __dirname,
  'NVX-CS-STAFF-01-Huong-dan-NV-Gioi-thieu-Novixa-Health.pdf',
);

const html = `<!DOCTYPE html>
<html lang="vi">
<head>
<meta charset="utf-8" />
<title>Hướng dẫn nhân viên — Novixa Health</title>
<style>
  @font-face {
    font-family: "UI";
    src: url("${fontRegular}") format("truetype");
    font-weight: 400;
  }
  @font-face {
    font-family: "UI";
    src: url("${fontSemibold}") format("truetype");
    font-weight: 600;
  }
  @font-face {
    font-family: "UI";
    src: url("${fontBold}") format("truetype");
    font-weight: 700;
  }

  @page {
    size: A4;
    margin: 12mm 14mm 14mm;
  }

  * { box-sizing: border-box; }
  html, body {
    margin: 0;
    padding: 0;
    font-family: "UI", "Segoe UI", Tahoma, sans-serif;
    color: #0f172a;
    background: #fff;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
    font-size: 10.5pt;
    line-height: 1.45;
  }

  .page {
    width: 100%;
    min-height: 269mm;
    page-break-after: always;
    position: relative;
    padding-bottom: 12mm;
  }
  .page:last-child { page-break-after: auto; }

  .topbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding-bottom: 8px;
    border-bottom: 2.5px solid #1d4ed8;
    margin-bottom: 10px;
  }
  .brand {
    display: flex;
    align-items: center;
    gap: 10px;
  }
  .brand img {
    height: 28px;
    width: auto;
  }
  .brand-text {
    display: flex;
    flex-direction: column;
    gap: 1px;
  }
  .brand-name {
    font-weight: 700;
    font-size: 11pt;
    color: #1e3a8a;
    letter-spacing: 0.02em;
  }
  .doc-code {
    font-size: 8pt;
    color: #64748b;
    font-weight: 600;
    text-align: right;
    line-height: 1.35;
  }

  h1 {
    margin: 6px 0 4px;
    font-size: 16.5pt;
    font-weight: 700;
    color: #0f172a;
    letter-spacing: -0.01em;
    line-height: 1.25;
  }
  .subtitle {
    margin: 0 0 12px;
    color: #475569;
    font-size: 9.5pt;
  }

  h2 {
    margin: 12px 0 6px;
    font-size: 10.5pt;
    font-weight: 700;
    color: #1e40af;
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }

  .principle {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
    margin-bottom: 10px;
  }
  .chip {
    border: 1px solid #cbd5e1;
    border-radius: 8px;
    padding: 8px 10px;
    background: #f8fafc;
  }
  .chip strong {
    display: block;
    font-size: 9pt;
    color: #1e40af;
    margin-bottom: 2px;
  }
  .chip p {
    margin: 0;
    font-size: 9.5pt;
    color: #334155;
  }

  .script-box {
    background: linear-gradient(135deg, #eff6ff 0%, #f0f9ff 100%);
    border: 1.5px solid #93c5fd;
    border-left: 5px solid #2563eb;
    border-radius: 10px;
    padding: 12px 14px;
    margin: 8px 0 12px;
  }
  .script-label {
    font-size: 8pt;
    font-weight: 700;
    color: #1d4ed8;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    margin-bottom: 6px;
  }
  .script-line {
    font-size: 13pt;
    font-weight: 700;
    color: #0f172a;
    line-height: 1.4;
    margin: 0;
  }
  .script-note {
    margin: 8px 0 0;
    font-size: 9pt;
    color: #475569;
  }

  .flow {
    display: flex;
    align-items: stretch;
    gap: 4px;
    margin: 8px 0 12px;
  }
  .flow-step {
    flex: 1;
    text-align: center;
    background: #1e40af;
    color: #fff;
    border-radius: 6px;
    padding: 7px 4px;
    font-size: 8pt;
    font-weight: 700;
    letter-spacing: 0.02em;
  }
  .flow-arrow {
    display: flex;
    align-items: center;
    color: #94a3b8;
    font-weight: 700;
    font-size: 11pt;
    padding: 0 1px;
  }

  .steps {
    margin: 0;
    padding: 0;
    list-style: none;
  }
  .steps li {
    display: grid;
    grid-template-columns: 22px 1fr;
    gap: 8px;
    margin-bottom: 7px;
    align-items: start;
  }
  .num {
    width: 22px;
    height: 22px;
    border-radius: 50%;
    background: #2563eb;
    color: #fff;
    font-size: 9pt;
    font-weight: 700;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .two-col {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 10px;
    margin-top: 4px;
  }

  .card {
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    padding: 9px 10px;
    background: #fff;
  }
  .card h3 {
    margin: 0 0 5px;
    font-size: 9.5pt;
    font-weight: 700;
    color: #0f172a;
  }
  .card p, .card li {
    margin: 0;
    font-size: 9pt;
    color: #334155;
  }
  .card ul {
    margin: 0;
    padding-left: 14px;
  }
  .card li { margin-bottom: 3px; }

  .qa {
    margin-bottom: 9px;
  }
  .qa-q {
    font-weight: 700;
    font-size: 9.5pt;
    color: #1e3a8a;
    margin-bottom: 3px;
  }
  .qa-a {
    margin: 0;
    font-size: 9.5pt;
    color: #334155;
    padding-left: 8px;
    border-left: 3px solid #bfdbfe;
  }

  .benefits {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 6px;
    margin: 6px 0 10px;
  }
  .benefit {
    border: 1px dashed #cbd5e1;
    border-radius: 6px;
    padding: 6px 8px;
    font-size: 9pt;
    color: #1e293b;
    background: #f8fafc;
  }

  .dont {
    background: #fff7ed;
    border: 1px solid #fed7aa;
    border-radius: 8px;
    padding: 9px 11px;
  }
  .dont h3 {
    margin: 0 0 6px;
    font-size: 9.5pt;
    color: #9a3412;
  }
  .dont ul {
    margin: 0;
    padding-left: 16px;
  }
  .dont li {
    margin-bottom: 3px;
    font-size: 9pt;
    color: #7c2d12;
  }

  .footer-cta {
    margin-top: 12px;
    padding: 10px 12px;
    background: #0f172a;
    color: #f8fafc;
    border-radius: 8px;
    text-align: center;
  }
  .footer-cta strong {
    display: block;
    font-size: 11pt;
    margin-bottom: 3px;
  }
  .footer-cta span {
    font-size: 9pt;
    color: #cbd5e1;
  }

  .page-foot {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    display: flex;
    justify-content: space-between;
    font-size: 7.5pt;
    color: #94a3b8;
    border-top: 1px solid #e2e8f0;
    padding-top: 5px;
  }
</style>
</head>
<body>

<!-- ========== PAGE 1 ========== -->
<section class="page">
  <header class="topbar">
    <div class="brand">
      <img src="${logoDataUri}" alt="Novixa" />
      <div class="brand-text">
        <span class="brand-name">Novixa Health</span>
      </div>
    </div>
    <div class="doc-code">
      NVX-CS-STAFF-01 · v1.0<br />
      Tài liệu in quầy · Dành cho NV &amp; chủ nhà thuốc
    </div>
  </header>

  <h1>Hướng dẫn nhân viên<br />giới thiệu Novixa Health</h1>
  <p class="subtitle">Không bán app — bán sự tiện lợi. Quét trước, thấy tiện, rồi khách tự nguyện cài nếu muốn.</p>

  <h2>1. Nguyên tắc</h2>
  <div class="principle">
    <div class="chip">
      <strong>Không ép cài</strong>
      <p>Không nói «Chị tải app của nhà thuốc em nhé».</p>
    </div>
    <div class="chip">
      <strong>Tiện ích ngay</strong>
      <p>Giúp khách nhận thông tin thuốc / lịch dùng ngay sau khi mua.</p>
    </div>
  </div>

  <h2>2. Câu nói chuẩn tại quầy</h2>
  <div class="script-box">
    <div class="script-label">Sau khi hoàn tất đơn hàng — hỏi trước, rồi mới đưa QR</div>
    <p class="script-line">«Chị có muốn em gửi thông tin thuốc và lịch dùng thuốc vào điện thoại không ạ?»</p>
    <p class="script-note">Nếu khách đồng ý → hiển thị <strong>QR Novixa Health</strong> trên POS → khách dùng camera điện thoại quét → xem thông tin ngay, không bắt buộc cài app.</p>
  </div>

  <div class="flow">
    <div class="flow-step">HỎI</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">QUÉT</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">DÙNG</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">THẤY TIỆN</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">TỰ NGUYỆN CÀI</div>
  </div>

  <h2>3. Khi khách đồng ý</h2>
  <ol class="steps">
    <li><span class="num">1</span><span>Mở <strong>QR Novixa Health</strong> trên Admin POS (nút cạnh OTP quầy).</span></li>
    <li><span class="num">2</span><span>Khách dùng <strong>camera điện thoại</strong> quét mã (không cần App Store trước).</span></li>
    <li><span class="num">3</span><span>Khách xem thông tin thuốc / lịch dùng ngay trên trình duyệt.</span></li>
  </ol>

  <div class="two-col" style="margin-top:14px">
    <div class="card">
      <h3>«Có phải tải app không?»</h3>
      <p>«Không ạ. Chị quét mã là xem được ngay. Nếu thấy tiện thì sau này chị có thể cài để lưu lịch sử và nhận nhắc thuốc.»</p>
    </div>
    <div class="card">
      <h3>Khi khách lo dữ liệu</h3>
      <p>«Thông tin của chị là do chị quyết định. Chị có quyền lựa chọn thông tin nào muốn lưu và chia sẻ.» — Không giải thích dài. Không tự ý nhập thông tin sức khỏe.</p>
    </div>
  </div>

  <div class="page-foot">
    <span>Novixa · Công ty TNHH Truyền thông và Công nghệ KIT</span>
    <span>Trang 1 / 2 · In A4 · Có thể cán màng dán quầy</span>
  </div>
</section>

<!-- ========== PAGE 2 ========== -->
<section class="page">
  <header class="topbar">
    <div class="brand">
      <img src="${logoDataUri}" alt="Novixa" />
      <div class="brand-text">
        <span class="brand-name">Novixa Health</span>
      </div>
    </div>
    <div class="doc-code">
      NVX-CS-STAFF-01 · v1.0<br />
      Mặt sau — FAQ · Lợi ích · Không được làm
    </div>
  </header>

  <h2>4. Khi nào mới gợi ý cài app?</h2>
  <p style="margin:0 0 8px;font-size:9.5pt;color:#334155">
    <strong>Không cần giới thiệu ngay.</strong> Chỉ sau khi khách đã dùng và thấy hữu ích:
  </p>
  <div class="script-box" style="margin-top:0">
    <p class="script-line" style="font-size:11pt">«Nếu chị thường xuyên mua thuốc hoặc muốn quản lý thuốc cho cả gia đình, chị có thể cài Novixa Health để tiện theo dõi và nhận nhắc thuốc ạ.»</p>
  </div>

  <h2>5. Lợi ích — chỉ nêu 1–2 ý phù hợp</h2>
  <p style="margin:0 0 6px;font-size:9pt;color:#64748b">Không đọc cả danh sách. Chọn theo tình huống khách.</p>
  <div class="benefits">
    <div class="benefit">📋 Xem lại thuốc đã mua</div>
    <div class="benefit">💊 Nhắc lịch dùng thuốc</div>
    <div class="benefit">🔔 Nhắc mua lại khi cần</div>
    <div class="benefit">👨‍👩‍👧‍👦 Quản lý thuốc cho gia đình</div>
    <div class="benefit">📱 Xem thông tin thuốc trên điện thoại</div>
    <div class="benefit">🏥 Lưu thông tin sức khỏe do khách chủ động cung cấp</div>
  </div>

  <h2>6. Không được làm</h2>
  <div class="dont">
    <h3>Cấm tại quầy</h3>
    <ul>
      <li>Ép khách cài app / nói «bắt buộc phải cài».</li>
      <li>Gọi Novixa Health là «app bắt buộc của nhà thuốc».</li>
      <li>Tự ý nhập thông tin sức khỏe của khách.</li>
      <li>Hỏi những thông tin không cần thiết.</li>
      <li>Cam kết «không thu thập bất kỳ dữ liệu nào» nếu chưa có chính sách cụ thể.</li>
      <li>Gây cảm giác khách bị theo dõi.</li>
    </ul>
  </div>

  <h2>7. Công thức nhớ nhanh</h2>
  <div class="flow">
    <div class="flow-step">HỎI</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">QUÉT</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">DÙNG</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">THẤY TIỆN</div>
    <div class="flow-arrow">→</div>
    <div class="flow-step">TỰ NGUYỆN CÀI</div>
  </div>

  <div class="qa">
    <div class="qa-q">Câu nói quan trọng nhất</div>
    <p class="qa-a">«Chị có muốn em gửi thông tin thuốc và lịch dùng thuốc vào điện thoại không ạ?»</p>
  </div>

  <div class="footer-cta">
    <strong>Đừng bán app. Hãy bán sự tiện lợi cho khách hàng.</strong>
    <span>Admin POS → QR Novixa Health · care@novixa.vn · novixa.vn</span>
  </div>

  <div class="page-foot">
    <span>Novixa · Công ty TNHH Truyền thông và Công nghệ KIT</span>
    <span>Trang 2 / 2 · Giữ bản in tại quầy / sổ SOP nhà thuốc</span>
  </div>
</section>

</body>
</html>
`;

fs.writeFileSync(htmlPath, html, 'utf8');

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();
await page.goto(pathToFileURL(htmlPath).href, { waitUntil: 'networkidle' });
await page.pdf({
  path: pdfPath,
  format: 'A4',
  printBackground: true,
  preferCSSPageSize: true,
  margin: { top: '0mm', bottom: '0mm', left: '0mm', right: '0mm' },
});
await browser.close();

console.log('HTML:', htmlPath);
console.log('PDF: ', pdfPath);

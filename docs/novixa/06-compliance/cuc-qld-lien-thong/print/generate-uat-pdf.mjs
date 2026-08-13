/**
 * Biên bản kiểm thử API CSDL dược (Sandbox) — PDF A4 mẫu hành chính.
 * Chạy: node .\print\generate-uat-pdf.mjs
 */
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dossierRoot = path.resolve(__dirname, '..');
const outPdf = path.join(dossierRoot, 'word', 'NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.pdf');
const htmlOut = path.join(__dirname, 'uat-report.html');

const require = createRequire(import.meta.url);

function resolvePlaywright() {
  const candidates = [
    path.join(__dirname, 'node_modules', 'playwright'),
    path.join(dossierRoot, '..', '..', '04-gtm', 'white-papers', 'print', 'node_modules', 'playwright'),
  ];
  for (const c of candidates) {
    try {
      return require(path.join(c, 'index.js'));
    } catch {
      /* try next */
    }
  }
  try {
    return require('playwright');
  } catch {
    console.error('Install playwright: npm i playwright && npx playwright install chromium');
    process.exit(1);
  }
}

const { chromium } = resolvePlaywright();

function fontDataUri(fontPath) {
  return `data:font/truetype;base64,${fs.readFileSync(fontPath).toString('base64')}`;
}

const times = fontDataUri('C:/Windows/Fonts/times.ttf');
const timesBd = fontDataUri('C:/Windows/Fonts/timesbd.ttf');
const timesIt = fontDataUri('C:/Windows/Fonts/timesi.ttf');

const html = `<!DOCTYPE html>
<html lang="vi">
<head>
<meta charset="utf-8" />
<title>NVX-CQD-UAT-01 — Biên bản kiểm thử API Sandbox</title>
<style>
  @font-face { font-family: "TimesDoc"; src: url("${times}") format("truetype"); font-weight: 400; font-style: normal; }
  @font-face { font-family: "TimesDoc"; src: url("${timesBd}") format("truetype"); font-weight: 700; font-style: normal; }
  @font-face { font-family: "TimesDoc"; src: url("${timesIt}") format("truetype"); font-weight: 400; font-style: italic; }

  * { box-sizing: border-box; }
  html, body {
    margin: 0;
    padding: 0;
    font-family: "TimesDoc", "Times New Roman", Times, serif;
    font-size: 12pt;
    line-height: 1.35;
    color: #111;
    background: #fff;
  }

  .sheet {
    padding: 0;
  }

  /* Header hành chính: trái cơ quan+Số | phải quốc hiệu+ngày */
  .doc-head {
    display: table;
    width: 100%;
    table-layout: fixed;
    margin: 0 0 1mm;
    border-collapse: separate;
  }
  .doc-head .col-left {
    display: table-cell;
    vertical-align: top;
    width: 48%;
    text-align: center;
    padding-right: 2mm;
    overflow: hidden;
  }
  .doc-head .col-right {
    display: table-cell;
    vertical-align: top;
    width: 52%;
    text-align: center;
    padding-left: 2mm;
    overflow: hidden;
  }
  .head-block {
    display: block;
    width: 100%;
    text-align: center;
  }
  .org-name {
    font-weight: 700;
    font-size: 11pt;
    text-transform: uppercase;
    margin: 0 auto;
    line-height: 1.25;
  }
  .rule {
    width: 32mm;
    border-top: 1.1pt solid #111;
    margin: 1.6mm auto 0;
  }
  .org-sub {
    font-size: 10pt;
    margin: 1.6mm 0 0;
  }
  .country {
    font-size: 11.5pt;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0;
    margin: 0;
    line-height: 1.2;
    white-space: nowrap;
  }
  .motto {
    font-size: 11.5pt;
    font-weight: 700;
    margin: 0.8mm 0 0;
    line-height: 1.2;
    white-space: nowrap;
  }
  .rule.motto-rule {
    width: 40mm;
  }
  .so {
    font-size: 11pt;
    margin: 2.5mm 0 0;
  }
  .place-date {
    font-size: 11pt;
    font-style: italic;
    margin: 2.5mm 0 0;
  }

  .main-title {
    text-align: center;
    margin: 6mm 0 3mm;
  }
  .main-title h1 {
    margin: 0;
    font-size: 14pt;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }
  .main-title h2 {
    margin: 2mm 0 0;
    font-size: 12.5pt;
    font-weight: 700;
    text-transform: uppercase;
  }
  .main-title .sub {
    margin: 2mm 0 0;
    font-size: 11pt;
    font-style: italic;
  }

  h3 {
    font-size: 12pt;
    font-weight: 700;
    text-transform: uppercase;
    margin: 5.5mm 0 2.5mm;
  }
  h4 {
    font-size: 12pt;
    font-weight: 700;
    margin: 3.5mm 0 2mm;
  }
  p { margin: 0 0 2.2mm; text-align: justify; }
  ol, ul { margin: 0 0 2.5mm; padding-left: 6mm; }
  li { margin-bottom: 1mm; text-align: justify; }
  .note { font-style: italic; font-size: 11pt; }

  table {
    width: 100%;
    border-collapse: collapse;
    margin: 0 0 3.5mm;
    font-size: 10.5pt;
  }
  th, td {
    border: 0.6pt solid #222;
    padding: 2.2mm 2.8mm;
    vertical-align: top;
    text-align: left;
  }
  th {
    background: #f2f2f2;
    font-weight: 700;
    text-align: center;
  }
  td.center, th.center { text-align: center; }
  td.pass { font-weight: 700; }

  .mono {
    font-family: Consolas, "Courier New", monospace;
    font-size: 9.5pt;
  }
  .box {
    border: 0.6pt solid #333;
    padding: 2.5mm 3mm;
    margin: 0 0 3.5mm;
    font-size: 10.5pt;
    background: #fafafa;
  }
  .box p { margin: 0 0 1mm; }
  .box p:last-child { margin: 0; }

  .sign {
    display: table;
    width: 100%;
    margin-top: 8mm;
    table-layout: fixed;
    page-break-inside: avoid;
  }
  .sign .col {
    display: table-cell;
    width: 50%;
    text-align: center;
    vertical-align: top;
    padding: 0 3mm;
  }
  .sign .role {
    font-weight: 700;
    text-transform: uppercase;
    font-size: 11pt;
    margin: 0;
  }
  .sign .org {
    font-size: 10.5pt;
    margin: 1mm 0 0;
  }
  .sign .hint {
    font-style: italic;
    font-size: 10.5pt;
    margin: 2mm 0 14mm;
  }
  .sign .name-line {
    margin-top: 14mm;
    font-size: 11pt;
  }

  .recv {
    margin-top: 6mm;
    font-size: 11pt;
  }
  .footer-note {
    margin-top: 5mm;
    font-size: 10pt;
    font-style: italic;
    color: #333;
  }

  .page-break { page-break-before: always; }
</style>
</head>
<body>
  <div class="sheet">

  <div class="doc-head">
    <div class="col-left">
      <div class="head-block">
        <div class="org-name">CÔNG TY TNHH<br/>TRUYỀN THÔNG VÀ CÔNG NGHỆ KIT</div>
        <div class="rule" aria-hidden="true"></div>
        <div class="org-sub">Mã số thuế: 4601239671</div>
        <div class="so"><strong>Số:</strong> 01/BB-KT-CSDL/2026</div>
      </div>
    </div>
    <div class="col-right">
      <div class="head-block">
        <div class="country">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
        <div class="motto">Độc lập - Tự do - Hạnh phúc</div>
        <div class="rule motto-rule" aria-hidden="true"></div>
        <div class="place-date">Thái Nguyên, ngày 13 tháng 08 năm 2026</div>
      </div>
    </div>
  </div>

  <div class="main-title">
    <h1>BIÊN BẢN</h1>
    <h2>Kiểm thử kết nối API Hệ thống cơ sở dữ liệu về dược</h2>
    <p class="sub">(Môi trường Sandbox — theo Tài liệu kỹ thuật đặc tả API phiên bản 1.1)</p>
  </div>

  <table>
    <tr><th style="width:32%">Hạng mục</th><th>Nội dung</th></tr>
    <tr><td>Mã tài liệu</td><td>NVX-CQD-UAT-01 &nbsp;|&nbsp; Phiên bản biên bản: <strong>1.1</strong></td></tr>
    <tr><td>Phần mềm</td><td>Novixa — phần mềm quản lý nhà thuốc</td></tr>
    <tr><td>Đơn vị thực hiện</td><td>Công ty TNHH Truyền thông và Công nghệ KIT</td></tr>
    <tr><td>Cơ quan vận hành CSDL</td><td>Trung tâm Thông tin Y tế Quốc gia — Bộ Y tế</td></tr>
    <tr><td>Môi trường / Base URL</td><td>Sandbox API v2 — <span class="mono">https://api-sandbox.csdlduoc.com.vn/v2</span></td></tr>
    <tr><td>Tài khoản API kiểm thử</td><td><span class="mono">019189002577</span></td></tr>
    <tr><td>Căn cứ kỹ thuật</td><td>Tài liệu kỹ thuật đặc tả API Hệ thống cơ sở dữ liệu về dược phiên bản 1.1 và Hướng dẫn sử dụng (ký số ngày 17/07/2026)</td></tr>
  </table>

  <h3>I. Mục đích</h3>
  <p>Biên bản này ghi nhận kết quả kiểm thử kỹ thuật kết nối API giữa phần mềm <strong>Novixa</strong> với <strong>Hệ thống cơ sở dữ liệu về dược</strong> trên môi trường <strong>Sandbox</strong>, nhằm:</p>
  <ol>
    <li>Xác nhận khả năng xác thực, đọc danh mục thuốc và ghi giao dịch nhập–xuất theo đặc tả API phiên bản 1.1;</li>
    <li>Làm căn cứ đối soát kỹ thuật trước khi phối hợp triển khai trên môi trường Production;</li>
    <li>Phục vụ trao đổi với chuyên viên hỗ trợ tích hợp của Trung tâm Thông tin Y tế Quốc gia.</li>
  </ol>
  <p class="note">Biên bản này là tài liệu kỹ thuật phục vụ kiểm thử; không thay thế hồ sơ pháp lý đăng ký liên thông.</p>

  <h3>II. Thông tin đơn vị và sản phẩm</h3>
  <table>
    <tr><th style="width:32%">Hạng mục</th><th>Nội dung</th></tr>
    <tr><td>Tên pháp nhân</td><td>Công ty TNHH Truyền thông và Công nghệ KIT</td></tr>
    <tr><td>Thương hiệu sản phẩm</td><td>Novixa</td></tr>
    <tr><td>Mã số thuế</td><td>4601239671</td></tr>
    <tr><td>Địa chỉ trụ sở</td><td>KĐT Hồ Xương Rồng, phường Phan Đình Phùng, tỉnh Thái Nguyên</td></tr>
    <tr><td>Website / Email / Điện thoại</td><td>https://novixa.vn &nbsp;|&nbsp; care@novixa.vn &nbsp;|&nbsp; 0984.660.399</td></tr>
    <tr><td>Người phụ trách tích hợp</td><td>Ông Tuấn</td></tr>
  </table>

  <h3>III. Căn cứ và phạm vi kiểm thử</h3>
  <h4>1. Căn cứ</h4>
  <ul>
    <li>Tài liệu kỹ thuật đặc tả API Hệ thống cơ sở dữ liệu về dược phiên bản <strong>1.1</strong> (thay thế API 1.0 theo 522/QĐ-TTYQG ngày 18/12/2025);</li>
    <li>Hướng dẫn sử dụng kèm theo; môi trường Sandbox nêu tại Phần đầu biên bản.</li>
  </ul>
  <h4>2. Phạm vi và phương pháp</h4>
  <p>Kiểm thử gồm: (A) xác thực; (B) danh mục thuốc; (C0) nhập tồn đầu kỳ; (C1) xuất bán lẻ. Portal web không thuộc phạm vi biên bản này.</p>
  <p>Phương pháp: gọi API RESTful HTTPS; mật khẩu đăng nhập truyền dạng Base64; dùng Bearer Token; thực hiện đúng trình tự <strong>nhập tồn đầu kỳ trước, xuất bán lẻ sau</strong>; tra cứu trạng thái qua API <span class="mono">/status</span>.</p>
  <p><strong>Thời điểm kiểm thử ghi nhận:</strong> 13/08/2026, khoảng 15:10 (GMT+7).</p>

  <h3>IV. Kết quả kiểm thử chi tiết</h3>

  <h4>1. Nhóm A — Xác thực</h4>
  <table>
    <tr>
      <th style="width:8%">STT</th>
      <th>Hạng mục</th>
      <th style="width:28%">Endpoint</th>
      <th style="width:12%">Kết quả</th>
      <th style="width:10%">HTTP</th>
    </tr>
    <tr>
      <td class="center">A1</td>
      <td>Đăng nhập lấy Access Token</td>
      <td class="mono">/auth/login</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
    </tr>
    <tr>
      <td class="center">A2</td>
      <td>Gọi API nghiệp vụ với Bearer Token</td>
      <td class="mono">/master/drugs</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
    </tr>
  </table>
  <p><strong>Nhận xét:</strong> Tài khoản <span class="mono">019189002577</span> đăng nhập thành công; nhận <span class="mono">access_token</span> (<span class="mono">token_type=Bearer</span>, <span class="mono">expires_in=86400</span>), có <span class="mono">refresh_token</span>.</p>

  <h4>2. Nhóm B — Danh mục thuốc</h4>
  <table>
    <tr>
      <th style="width:8%">STT</th>
      <th>Hạng mục</th>
      <th style="width:30%">Endpoint</th>
      <th style="width:10%">Kết quả</th>
      <th style="width:8%">HTTP</th>
      <th>Ghi chú</th>
    </tr>
    <tr>
      <td class="center">B1</td>
      <td>Danh sách thuốc (phân trang)</td>
      <td class="mono">/master/drugs?page=1&amp;page_size=3</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
      <td><span class="mono">total</span> = 54.540</td>
    </tr>
    <tr>
      <td class="center">B2</td>
      <td>Chi tiết thuốc theo <span class="mono">drug_id</span></td>
      <td class="mono">/master/drugs/893110130226</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
      <td>TBZemitin 500; <span class="mono">unit_id=U31</span></td>
    </tr>
  </table>
  <p><strong>Nhận xét:</strong> Kết nối đọc danh mục thuốc trên Sandbox thành công.</p>

  <h4>3. Nhóm C0 — Nhập tồn đầu kỳ (<span class="mono">opening-balance</span>)</h4>
  <p>Theo Mục 5.4.1 và từ điển lý do nhập hàng Mục 6.4.1.1.</p>
  <table>
    <tr>
      <th style="width:8%">STT</th>
      <th>Hạng mục</th>
      <th style="width:28%">Endpoint</th>
      <th style="width:10%">Kết quả</th>
      <th style="width:8%">HTTP</th>
      <th>Ghi chú</th>
    </tr>
    <tr>
      <td class="center">C0</td>
      <td>Tạo phiếu nhập tồn đầu kỳ</td>
      <td class="mono">/transactions/stock-in</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
      <td class="mono">transaction_id = 580c233a-d0a3-c824-d3df-176c96820bc0</td>
    </tr>
    <tr>
      <td class="center">C0s</td>
      <td>Tra cứu trạng thái</td>
      <td class="mono">/transactions/stock-in/{id}/status</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
      <td><strong>status = Completed</strong></td>
    </tr>
  </table>
  <div class="box">
    <p><strong>Tóm tắt request</strong> (đã che thông tin xác thực):</p>
    <p>• <span class="mono">reason</span>: <span class="mono">opening-balance</span></p>
    <p>• <span class="mono">reference_number</span>: <span class="mono">NVX-OB-20260813-151002</span></p>
    <p>• Thuốc: <span class="mono">drug_id=893110130226</span>, <span class="mono">unit_id=U31</span>, <span class="mono">quantity=10</span>, <span class="mono">batch_no=OB20260813-151002</span>, <span class="mono">expiry_date=2027-12-31</span></p>
  </div>

  <h4>4. Nhóm C1 — Xuất bán lẻ (<span class="mono">sale-retail</span>)</h4>
  <p>Theo Mục 5.4.4 và Mục 6.4.2.1. Thực hiện <strong>sau</strong> khi phiếu tồn đầu kỳ đã ở trạng thái Completed.</p>
  <table>
    <tr>
      <th style="width:8%">STT</th>
      <th>Hạng mục</th>
      <th style="width:28%">Endpoint</th>
      <th style="width:10%">Kết quả</th>
      <th style="width:8%">HTTP</th>
      <th>Ghi chú</th>
    </tr>
    <tr>
      <td class="center">C1</td>
      <td>Tạo phiếu xuất bán lẻ</td>
      <td class="mono">/transactions/stock-out</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
      <td class="mono">transaction_id = 580c233a-f3ac-b476-e19e-0f07d83b6022</td>
    </tr>
    <tr>
      <td class="center">C1s</td>
      <td>Tra cứu trạng thái</td>
      <td class="mono">/transactions/stock-out/{id}/status</td>
      <td class="center pass">Đạt</td>
      <td class="center">200</td>
      <td><strong>status = Completed</strong></td>
    </tr>
  </table>
  <div class="box">
    <p><strong>Tóm tắt request</strong> (đã che thông tin xác thực):</p>
    <p>• <span class="mono">reason</span>: <span class="mono">sale-retail</span></p>
    <p>• <span class="mono">reference_number</span>: <span class="mono">NVX-SO-20260813-151002</span></p>
    <p>• Thuốc: cùng <span class="mono">drug_id</span> / <span class="mono">unit_id</span> / lô với phiếu tồn đầu kỳ; <span class="mono">quantity=1</span></p>
  </div>

  <h3>V. Tổng hợp kết luận</h3>
  <table>
    <tr><th style="width:40%">Nhóm</th><th>Kết luận</th></tr>
    <tr><td>A — Xác thực</td><td class="pass">Đạt</td></tr>
    <tr><td>B — Danh mục thuốc</td><td class="pass">Đạt</td></tr>
    <tr><td>C0 — Nhập tồn đầu kỳ</td><td class="pass">Đạt (Completed)</td></tr>
    <tr><td>C1 — Xuất bán lẻ</td><td class="pass">Đạt (Completed)</td></tr>
    <tr>
      <td><strong>Tổng thể</strong></td>
      <td><strong>Kết nối đọc danh mục và ghi giao dịch trên Sandbox với tài khoản được cấp thành công, theo đúng trình tự đặc tả API phiên bản 1.1.</strong></td>
    </tr>
  </table>

  <h4>Diễn biến các lần kiểm thử (tham chiếu)</h4>
  <table>
    <tr>
      <th style="width:8%">Lần</th>
      <th style="width:16%">Ngày</th>
      <th style="width:18%">Tài khoản</th>
      <th>Kết quả tóm tắt</th>
    </tr>
    <tr>
      <td class="center">1</td>
      <td class="center">13/08/2026</td>
      <td class="mono">4601239671</td>
      <td>Xác thực / danh mục đạt; <span class="mono">stock-out</span> trả HTTP 500</td>
    </tr>
    <tr>
      <td class="center">2</td>
      <td class="center">13/08/2026</td>
      <td class="mono">019189002577</td>
      <td><span class="mono">stock-out</span> nhận phiếu nhưng trạng thái Error: <em>Chưa nhập phiếu tồn đầu kỳ</em></td>
    </tr>
    <tr>
      <td class="center">3</td>
      <td class="center">13/08/2026 (15:10)</td>
      <td class="mono">019189002577</td>
      <td><span class="mono">opening-balance</span> rồi <span class="mono">sale-retail</span> đều <strong>Completed</strong> (biên bản này)</td>
    </tr>
  </table>

  <h3>VI. Kiến nghị</h3>
  <p>Kính đề nghị Trung tâm Thông tin Y tế Quốc gia / chuyên viên hỗ trợ tích hợp:</p>
  <ol>
    <li>Ghi nhận kết quả kiểm thử Sandbox theo đặc tả API 1.1 tại biên bản này;</li>
    <li>Khi chuyển hỗ trợ kỹ thuật chuyên sâu, hỗ trợ các hạng mục tiếp theo (nếu có): kiểm kê (<span class="mono">stock-taking</span>), hướng dẫn tìm kiếm thuốc theo tên trên API danh mục, và cấu hình môi trường Production;</li>
    <li>Đơn vị tiếp tục hoàn thiện tích hợp trong phần mềm Novixa và sẵn sàng phối hợp UAT Production khi được hướng dẫn.</li>
  </ol>

  <h3>VII. Cam kết</h3>
  <ol>
    <li>Chỉ sử dụng tài khoản được cấp cho mục đích kiểm thử Sandbox; không đẩy dữ liệu giả lên môi trường Production.</li>
    <li>Bảo mật thông tin đăng nhập; không công khai mật khẩu.</li>
    <li>Tuân thủ đặc tả API phiên bản 1.1 và các hướng dẫn cập nhật từ cơ quan vận hành.</li>
    <li>Phối hợp đầy đủ khi được yêu cầu bổ sung kịch bản kiểm thử hoặc đối soát nhật ký hệ thống.</li>
  </ol>

  <h3>VIII. Phụ lục</h3>
  <h4>Phụ lục 1 — Đối chiếu với mục đặc tả API 1.1</h4>
  <table>
    <tr><th>Kiểm thử</th><th>Mục đặc tả</th></tr>
    <tr><td>A1 Login</td><td>5.1.1</td></tr>
    <tr><td>B1–B2 Drugs</td><td>5.3.1 / 5.3.2</td></tr>
    <tr><td>C0 Stock-in / <span class="mono">opening-balance</span></td><td>5.4.1 / 6.4.1.1</td></tr>
    <tr><td>C1 Stock-out / <span class="mono">sale-retail</span></td><td>5.4.4 / 6.4.2.1</td></tr>
  </table>
  <h4>Phụ lục 2 — Môi trường kỹ thuật</h4>
  <table>
    <tr><th style="width:40%">Hạng mục</th><th>Giá trị</th></tr>
    <tr><td>Base URL Production</td><td class="mono">https://api.csdlduoc.com.vn/v2</td></tr>
    <tr><td>Base URL Sandbox</td><td class="mono">https://api-sandbox.csdlduoc.com.vn/v2</td></tr>
    <tr><td>Phiên bản API / Định dạng</td><td>v2 / JSON UTF-8</td></tr>
    <tr><td>Xác thực</td><td>Bearer Token sau <span class="mono">/auth/login</span></td></tr>
  </table>

  <h3>IX. Xác nhận</h3>
  <p>Biên bản được lập thành <strong>02</strong> bản có giá trị như nhau; đơn vị phần mềm giữ <strong>01</strong> bản, gửi chuyên viên hỗ trợ tích hợp <strong>01</strong> bản (bản điện tử PDF).</p>

  <div class="sign">
    <div class="col">
      <p class="role">Đại diện đơn vị phần mềm</p>
      <p class="org">Công ty TNHH Truyền thông và Công nghệ KIT</p>
      <p class="hint">(Ký, ghi rõ họ tên)</p>
      <p class="name-line">........................................</p>
      <p class="org">Ngày 13/08/2026</p>
    </div>
    <div class="col">
      <p class="role">Chuyên viên hỗ trợ</p>
      <p class="org">Trung tâm Thông tin Y tế Quốc gia</p>
      <p class="hint">(Ký, ghi rõ họ tên — nếu có)</p>
      <p class="name-line">........................................</p>
      <p class="org">&nbsp;</p>
    </div>
  </div>

  <div class="recv">
    <p><strong>Nơi nhận:</strong></p>
    <ul>
      <li>Chuyên viên hỗ trợ tích hợp — Trung tâm Thông tin Y tế Quốc gia;</li>
      <li>Lưu hồ sơ kỹ thuật Novixa tại Công ty TNHH Truyền thông và Công nghệ KIT.</li>
    </ul>
  </div>

  <p class="footer-note">Tài liệu kỹ thuật phục vụ phối hợp kiểm thử liên thông CSDL dược — Công ty TNHH Truyền thông và Công nghệ KIT.</p>

  </div>
</body>
</html>`;

fs.mkdirSync(path.dirname(outPdf), { recursive: true });
fs.writeFileSync(htmlOut, html, 'utf8');

const browser = await chromium.launch();
const page = await browser.newPage();
// setContent ổn định hơn file:// với margin PDF
await page.setContent(html, { waitUntil: 'networkidle' });
await page.evaluate(async () => {
  if (document.fonts?.ready) await document.fonts.ready;
});
await page.pdf({
  path: outPdf,
  format: 'A4',
  printBackground: true,
  displayHeaderFooter: false,
  margin: { top: '25mm', right: '20mm', bottom: '22mm', left: '25mm' },
});
await browser.close();

try {
  const { spawnSync } = await import('node:child_process');
  const stamp = spawnSync('python', [path.join(__dirname, 'stamp-pdf-page-numbers.py'), outPdf], {
    encoding: 'utf8',
  });
  if (stamp.status !== 0) {
    console.warn('Page-number stamp skipped:', stamp.stderr || stamp.stdout);
  } else {
    process.stdout.write(stamp.stdout || '');
  }
} catch (e) {
  console.warn('Page-number stamp skipped:', e.message);
}

console.log(`OK: ${outPdf}`);

/**
 * Strategic infographics for NOVIXA-WP-001 Full PDF.
 * Style: McKinsey / Apple — diagrams & numbers, no stock photos.
 */

/** Catalog — thứ tự = số hình trong PDF */
export const FIGURE_CATALOG = [
  {
    id: 'volume',
    num: '01',
    where: 'Chương 2',
    title: 'Một câu hỏi sau ~29.000 lượt mua mỗi năm',
    caption: 'Từ lượng lượt mua lớn — câu hỏi then chốt là bao nhiêu người quay lại.',
  },
  {
    id: 'invoice-compare',
    num: '02',
    where: 'Chương 3',
    title: 'Cùng một hóa đơn — hai điểm kết thúc khác nhau',
    caption: 'Khác biệt không nằm ở thao tác bán, mà ở những việc sau khi bán.',
  },
  {
    id: 'refill-timeline',
    num: '03',
    where: 'Chương 3',
    title: 'Hành trình mua lại theo chu kỳ ~30 ngày',
    caption: 'Timeline tái mua — lớp mà nhiều hệ thống chỉ quản lý giao dịch chưa nói tới.',
  },
  {
    id: 'lifecycle',
    num: '04',
    where: 'Chương 3',
    title: 'Vòng đời của một khách hàng',
    caption:
      'Mục tiêu không phải bán thêm một đơn — mà giúp mỗi khách trở thành người đồng hành lâu dài.',
  },
  {
    id: 'invoice-begin',
    num: '05',
    where: 'Chương 6',
    title: 'Hóa đơn là điểm bắt đầu — không phải điểm kết thúc',
    caption: 'Từ thanh toán đến theo dõi chu kỳ và gợi ý chăm sóc đúng lúc.',
  },
  {
    id: 'pos-vs-novixa',
    num: '06',
    where: 'Chương 8',
    title: 'POS quản lý giao dịch và Novixa mở rộng',
    caption: 'Cùng nền vận hành — Novixa bổ sung lớp phát triển và đồng hành sau bán.',
  },
  {
    id: 'family-book',
    num: '07',
    where: 'Chương 10',
    title: 'Một tài khoản — sổ sức khỏe số cho cả gia đình',
    caption: 'Big Idea: nhà thuốc trở thành người bạn đồng hành sức khỏe của cả gia đình.',
  },
  {
    id: 'ecosystem',
    num: '08',
    where: 'Chương 10',
    title: 'Novixa không chỉ là tầng POS',
    caption: 'Tháp giá trị từ vận hành quầy lên gắn kết sức khỏe gia đình.',
  },
  {
    id: 'hub',
    num: '09',
    where: 'Chương 10',
    title: 'Người dân — nhà thuốc — sổ sức khỏe trên cùng một hệ',
    caption: 'Lớp kết nối mà phần mềm chỉ quản lý giao dịch quầy thường chưa có.',
  },
  {
    id: 'dashboard',
    num: '10',
    where: 'Chương 12',
    title: 'Việc cần quan tâm hôm nay (minh họa)',
    caption: 'Mockup bảng việc — không phải ảnh chụp màn hình sản phẩm.',
  },
  {
    id: 'app-day',
    num: '11',
    where: 'Phụ lục I',
    title: 'Một ngày với Sổ sức khỏe số (minh họa)',
    caption: 'Giá trị hằng ngày cho người dân — không tự bán thuốc, không tự kê đơn.',
  },
  {
    id: 'two-way',
    num: '12',
    where: 'Phụ lục J',
    title: 'Người dân nhận giá trị trước — nhà thuốc nhận giá trị sau',
    caption:
      'Mối quan hệ hai chiều tạo rào cản cạnh tranh bền hơn POS hay CRM đơn thuần.',
  },
];

const catalogById = Object.fromEntries(FIGURE_CATALOG.map((f) => [f.id, f]));

export const figureCss = `
  .fig {
    font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
    margin: 10pt 0 14pt;
    page-break-inside: avoid;
    color: #1a1f24;
  }
  .fig__eyebrow {
    font-size: 9pt; letter-spacing: 0.12em; text-transform: uppercase;
    color: #5a6a72; margin: 0 0 3pt; font-weight: 700;
  }
  .fig__title {
    font-size: 12pt; font-weight: 700; color: #0f2f38;
    margin: 0 0 8pt; line-height: 1.3;
  }
  .fig__note {
    font-family: "Times New Roman", Times, serif;
    font-size: 10.5pt; color: #44515a; margin: 8pt 0 0;
    font-style: italic; line-height: 1.35;
  }
  .fig__caption {
    font-family: "Times New Roman", Times, serif;
    font-size: 11pt; color: #1a1f24; margin: 8pt 0 0;
    line-height: 1.4; border-top: 0.7pt solid #c9d1d6; padding-top: 6pt;
  }
  .fig__caption strong { color: #0f2f38; }

  .fig-toc {
    page-break-before: always;
    page-break-after: always;
    margin: 0 0 8pt;
  }
  .fig-toc h2 {
    font-family: "Times New Roman", Times, serif;
    font-size: 17pt; color: #0f2f38; margin: 0 0 8pt;
    page-break-before: avoid;
  }
  .fig-toc__lede {
    font-family: "Times New Roman", Times, serif;
    font-size: 11.5pt; color: #44515a; margin: 0 0 12pt; line-height: 1.45;
  }
  .fig-toc__list {
    list-style: none; margin: 0; padding: 0;
    font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
  }
  .fig-toc__list li {
    display: grid; grid-template-columns: 18mm 1fr auto;
    gap: 8pt; align-items: baseline;
    padding: 5pt 0; border-bottom: 0.5pt solid #d5dde1;
    font-size: 10.5pt; line-height: 1.35; margin: 0;
  }
  .fig-toc__num { font-weight: 700; color: #0f2f38; letter-spacing: 0.04em; }
  .fig-toc__title { color: #1a1f24; }
  .fig-toc__where { color: #5a6a72; font-size: 9.5pt; white-space: nowrap; }

  .flow {
    display: flex; flex-direction: column; align-items: stretch; gap: 0;
    max-width: 95mm; margin: 0 auto;
  }
  .flow__step {
    background: #0f2f38; color: #fff; text-align: center;
    padding: 7pt 10pt; font-size: 11pt; font-weight: 600;
    border-radius: 2pt;
  }
  .flow__step--mute { background: #6b7c84; }
  .flow__step--end { background: #8a3a3a; }
  .flow__step--gold { background: #c4a35a; color: #1a1f24; }
  .flow__arrow {
    text-align: center; color: #0f2f38; font-size: 12pt;
    line-height: 1; padding: 2pt 0; font-weight: 700;
  }

  .compare {
    display: grid; grid-template-columns: 1fr 1fr; gap: 10pt;
  }
  .compare__col {
    border: 0.7pt solid #b7c3c9; border-radius: 2pt; overflow: hidden;
  }
  .compare__head {
    padding: 7pt 9pt; font-size: 11pt; font-weight: 700; text-align: center;
  }
  .compare__head--now { background: #e8ecee; color: #44515a; }
  .compare__head--nvx { background: #0f2f38; color: #fff; }
  .compare__body { padding: 10pt 8pt 12pt; background: #fff; }

  .tl {
    display: flex; flex-wrap: nowrap; gap: 0; align-items: stretch;
    overflow: hidden;
  }
  .tl__item {
    flex: 1; min-width: 0; text-align: center;
    border-top: 2.5pt solid #0f2f38; padding: 8pt 4pt 0; position: relative;
  }
  .tl__item::before {
    content: ""; position: absolute; top: -5pt; left: 50%;
    width: 8pt; height: 8pt; margin-left: -4pt;
    background: #0f2f38; border-radius: 50%;
  }
  .tl__day {
    font-size: 9pt; font-weight: 700; color: #c4a35a;
    letter-spacing: 0.04em; margin-bottom: 3pt;
  }
  .tl__act { font-size: 10pt; line-height: 1.25; color: #1a1f24; font-weight: 600; }

  .dash {
    background: #f4f7f8; border: 0.7pt solid #b7c3c9; border-radius: 3pt;
    padding: 12pt 12pt 10pt;
  }
  .dash__bar {
    display: flex; justify-content: space-between; align-items: baseline;
    margin-bottom: 10pt; padding-bottom: 7pt; border-bottom: 0.7pt solid #c9d1d6;
  }
  .dash__bar strong { font-size: 12pt; color: #0f2f38; }
  .dash__bar span { font-size: 9.5pt; color: #5a6a72; }
  .dash__grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8pt; }
  .dash__card {
    background: #fff; border: 0.7pt solid #c9d1d6; border-radius: 2pt;
    padding: 10pt 11pt;
  }
  .dash__num { font-size: 22pt; font-weight: 700; color: #0f2f38; line-height: 1.1; }
  .dash__label { font-size: 10pt; color: #44515a; margin-top: 2pt; line-height: 1.3; }

  .pyramid { display: flex; flex-direction: column; align-items: center; gap: 0; }
  .pyramid__row {
    text-align: center; color: #fff; font-size: 10.5pt; font-weight: 600;
    padding: 8pt 10pt; margin: 0 auto 5pt; border-radius: 2pt; line-height: 1.3;
  }
  .pyramid__row:nth-child(1) { width: 92%; background: #0a4a3a; }
  .pyramid__row:nth-child(2) { width: 84%; background: #0f2f38; }
  .pyramid__row:nth-child(3) { width: 76%; background: #1a4550; }
  .pyramid__row:nth-child(4) { width: 68%; background: #2a5560; }
  .pyramid__row:nth-child(5) { width: 60%; background: #3d6570; }
  .pyramid__up {
    color: #c4a35a; font-size: 11pt; line-height: 1; margin: -2pt 0 3pt; font-weight: 700;
  }

  .hub {
    display: grid; grid-template-rows: auto auto auto; gap: 6pt;
    justify-items: center; text-align: center; padding: 4pt 0;
  }
  .hub__node {
    background: #0f2f38; color: #fff; padding: 8pt 14pt;
    font-size: 11pt; font-weight: 700; border-radius: 2pt; min-width: 42mm;
  }
  .hub__node--soft {
    background: #eef3f4; color: #0f2f38; border: 0.7pt solid #0f2f38;
  }
  .hub__node--gold { background: #c4a35a; color: #1a1f24; }
  .hub__mid {
    display: grid; grid-template-columns: 1fr auto 1fr; gap: 8pt;
    align-items: center; width: 100%; max-width: 150mm;
  }
  .hub__v { color: #0f2f38; font-weight: 700; font-size: 12pt; line-height: 1; }

  .vol {
    background: #0f2f38; color: #fff; padding: 14pt 14pt 12pt;
    border-radius: 2pt;
  }
  .vol .flow__step {
    background: #163d48; border: 0.7pt solid #3d6570; color: #ffffff;
  }
  .vol .flow__step--ask {
    background: #c4a35a; color: #1a1f24; font-size: 12pt; border-color: #c4a35a;
  }
  .vol .flow__step--gold {
    background: #e8d5a3; color: #1a1f24; border-color: #c4a35a; font-weight: 700;
  }
  .vol .flow__arrow { color: #c4a35a; }
  .vol__hint {
    text-align: center; margin-top: 12pt; font-size: 11pt;
    color: #d7e2e5; line-height: 1.4;
  }

  .life {
    display: flex; flex-wrap: wrap; gap: 4pt 0; justify-content: center;
    align-items: center;
  }
  .life__chip {
    background: #0f2f38; color: #fff; font-size: 9.5pt; font-weight: 600;
    padding: 5pt 7pt; border-radius: 2pt; white-space: nowrap;
  }
  .life__chip--last { background: #c4a35a; color: #1a1f24; }
  .life__arr { color: #0f2f38; font-weight: 700; padding: 0 3pt; font-size: 10pt; }

  .cap {
    display: grid; grid-template-columns: 1.35fr 0.9fr 0.9fr; gap: 0;
    border: 0.7pt solid #b7c3c9; border-radius: 2pt; overflow: hidden;
    font-size: 10pt;
  }
  .cap__h {
    padding: 7pt 8pt; font-weight: 700; text-align: center;
    border-bottom: 0.7pt solid #b7c3c9; border-right: 0.7pt solid #b7c3c9;
  }
  .cap__h:last-child { border-right: none; }
  .cap__h--q { background: #eef3f4; color: #0f2f38; text-align: left; }
  .cap__h--pos { background: #e8ecee; color: #44515a; }
  .cap__h--nvx { background: #0f2f38; color: #fff; }
  .cap__c {
    padding: 6pt 8pt; border-bottom: 0.7pt solid #d5dde1;
    border-right: 0.7pt solid #d5dde1; line-height: 1.3;
  }
  .cap__c:nth-child(3n) { border-right: none; }
  .cap__c--q { color: #1a1f24; font-weight: 600; background: #fafbfc; }
  .cap__c--yes { text-align: center; color: #0a4a3a; font-weight: 700; }
  .cap__c--partial { text-align: center; color: #6b7c84; }
  .cap__c--novixa { text-align: center; color: #0f2f38; font-weight: 700; background: #f3f7f8; }

  .fam { border: 0.7pt solid #b7c3c9; border-radius: 2pt; overflow: hidden; }
  .fam__account {
    background: #0f2f38; color: #fff; text-align: center;
    padding: 10pt 12pt; font-size: 12pt; font-weight: 700;
  }
  .fam__account span {
    display: block; font-size: 9.5pt; font-weight: 500;
    color: #c4a35a; margin-top: 3pt;
  }
  .fam__grid {
    display: grid; grid-template-columns: 1fr 1fr; gap: 0;
    background: #c9d1d6;
  }
  .fam__card { background: #fff; padding: 10pt 11pt; min-height: 28mm; }
  .fam__who { font-size: 11pt; font-weight: 700; color: #0f2f38; margin-bottom: 4pt; }
  .fam__items {
    margin: 0; padding-left: 14pt; font-size: 9.5pt; color: #44515a; line-height: 1.35;
  }
  .fam__items li { margin: 0 0 2pt; }
  .fam__foot {
    background: #f4f7f8; text-align: center; padding: 8pt 10pt;
    font-size: 10.5pt; font-weight: 600; color: #0f2f38;
    border-top: 0.7pt solid #c9d1d6;
  }

  .twoway {
    display: grid; grid-template-columns: 1fr auto 1fr; gap: 8pt;
    align-items: stretch;
  }
  .twoway__col {
    border: 0.7pt solid #b7c3c9; border-radius: 2pt; overflow: hidden;
  }
  .twoway__head {
    padding: 7pt 9pt; font-size: 11pt; font-weight: 700; text-align: center;
  }
  .twoway__head--ppl { background: #0a4a3a; color: #fff; }
  .twoway__head--rx { background: #0f2f38; color: #fff; }
  .twoway__body { padding: 8pt 10pt 10pt; background: #fff; }
  .twoway__body ul {
    margin: 0; padding-left: 14pt; font-size: 10pt; line-height: 1.4;
  }
  .twoway__body li { margin: 0 0 3pt; }
  .twoway__mid {
    display: flex; flex-direction: column; align-items: center;
    justify-content: center; gap: 6pt; min-width: 18mm;
    font-size: 9pt; font-weight: 700; color: #0f2f38; text-align: center;
  }
  .twoway__arrow {
    background: #c4a35a; color: #1a1f24; padding: 5pt 6pt;
    border-radius: 2pt; font-size: 9pt; line-height: 1.25; width: 100%;
  }
`;

function figShell({ id, body, note, extraClass = '' }) {
  const meta = catalogById[id];
  if (!meta) throw new Error(`[wp001-figures] Missing catalog entry: ${id}`);
  return `
<figure class="fig ${extraClass}" id="fig-${id}">
  <div class="fig__eyebrow">Hình ${meta.num} · ${meta.where}</div>
  <div class="fig__title">${meta.title}</div>
  ${body}
  ${note ? `<p class="fig__note">${note}</p>` : ''}
  <p class="fig__caption"><strong>Hình ${meta.num}.</strong> ${meta.caption}</p>
</figure>`;
}

export function buildFigureTocHtml() {
  const rows = FIGURE_CATALOG.map(
    (f) => `
    <li>
      <span class="fig-toc__num">Hình ${f.num}</span>
      <span class="fig-toc__title">${f.title}</span>
      <span class="fig-toc__where">${f.where}</span>
    </li>`,
  ).join('');
  return `
<section class="fig-toc" id="muc-luc-hinh">
  <h2>Mục lục hình</h2>
  <p class="fig-toc__lede">Mười hai sơ đồ chính của tài liệu — có thể đọc lướt các hình này trước nếu chỉ có vài phút; sau đó quay lại chương tương ứng khi cần chiều sâu.</p>
  <ol class="fig-toc__list">${rows}
  </ol>
</section>`;
}

function vFlow(steps, opts = {}) {
  const parts = [];
  steps.forEach((s, i) => {
    const cls = typeof s === 'string' ? 'flow__step' : `flow__step ${s.cls || ''}`;
    const label = typeof s === 'string' ? s : s.label;
    parts.push(`<div class="${cls}">${label}</div>`);
    if (i < steps.length - 1) {
      parts.push(`<div class="flow__arrow">${opts.arrow || '↓'}</div>`);
    }
  });
  return `<div class="flow">${parts.join('')}</div>`;
}

export const FIGURES = {
  volume: figShell({
    id: 'volume',
    body: `
    <div class="vol">
      ${vFlow([
        '80 lượt mua / ngày',
        '~2.400 lượt / tháng',
        '~29.000 lượt / năm',
        { label: 'Bao nhiêu người quay lại?', cls: 'flow__step--ask' },
        'Nếu tăng thêm vài phần trăm khách quay lại…',
        { label: '→ Cơ hội doanh thu & lợi nhuận bền vững hơn', cls: 'flow__step--gold' },
      ])}
      <p class="vol__hint">Không phải thống kê ngành — là khung để đối chiếu với nhà thuốc mình.</p>
    </div>`,
  }),

  'invoice-compare': figShell({
    id: 'invoice-compare',
    body: `
    <div class="compare">
      <div class="compare__col">
        <div class="compare__head compare__head--now">Một hóa đơn hiện nay</div>
        <div class="compare__body">
          ${vFlow([
            { label: 'Khách đến', cls: 'flow__step--mute' },
            { label: 'Mua thuốc', cls: 'flow__step--mute' },
            { label: 'Thanh toán', cls: 'flow__step--mute' },
            { label: 'Kết thúc', cls: 'flow__step--end' },
          ])}
        </div>
      </div>
      <div class="compare__col">
        <div class="compare__head compare__head--nvx">Với Novixa</div>
        <div class="compare__body">
          ${vFlow([
            'Khách đến',
            'Mua thuốc',
            'Thanh toán',
            'Lưu hồ sơ',
            'Theo dõi chu kỳ',
            'Nhắc đúng lúc',
            'Quay lại',
            { label: 'Tiếp tục đồng hành', cls: 'flow__step--gold' },
          ])}
        </div>
      </div>
    </div>`,
    note: 'Đọc ~5 giây.',
  }),

  'refill-timeline': figShell({
    id: 'refill-timeline',
    body: `
    <div class="tl">
      <div class="tl__item"><div class="tl__day">NGÀY 1</div><div class="tl__act">Mua thuốc</div></div>
      <div class="tl__item"><div class="tl__day">NGÀY 7</div><div class="tl__act">Nhắc uống<br/>(nếu dùng App)</div></div>
      <div class="tl__item"><div class="tl__day">NGÀY 20</div><div class="tl__act">Theo dõi<br/>hành trình</div></div>
      <div class="tl__item"><div class="tl__day">NGÀY 28</div><div class="tl__act">Có thể sắp<br/>hết thuốc</div></div>
      <div class="tl__item"><div class="tl__day">NGÀY 30+</div><div class="tl__act">Tư vấn ·<br/>đặt lại</div></div>
    </div>`,
    note: 'Thời điểm thực tế theo nhóm sản phẩm và sự đồng ý của khách.',
  }),

  lifecycle: figShell({
    id: 'lifecycle',
    body: `
    <div class="life">
      <span class="life__chip">Biết đến nhà thuốc</span><span class="life__arr">→</span>
      <span class="life__chip">Lần mua đầu</span><span class="life__arr">→</span>
      <span class="life__chip">Có hồ sơ</span><span class="life__arr">→</span>
      <span class="life__chip">Theo dõi</span><span class="life__arr">→</span>
      <span class="life__chip">Chăm sóc</span><span class="life__arr">→</span>
      <span class="life__chip">Quay lại</span><span class="life__arr">→</span>
      <span class="life__chip">Tin tưởng</span><span class="life__arr">→</span>
      <span class="life__chip">Giới thiệu bạn bè</span><span class="life__arr">→</span>
      <span class="life__chip">Gia đình cùng dùng</span><span class="life__arr">→</span>
      <span class="life__chip life__chip--last">Đồng hành nhiều năm</span>
    </div>`,
  }),

  'invoice-begin': figShell({
    id: 'invoice-begin',
    body: vFlow([
      'Khách mua',
      'Thanh toán',
      'Lưu hồ sơ mua hàng',
      'Theo dõi chu kỳ sử dụng',
      'Gợi ý chăm sóc đúng lúc',
      { label: 'Khách quay lại · tiếp tục đồng hành', cls: 'flow__step--gold' },
    ]),
  }),

  dashboard: figShell({
    id: 'dashboard',
    body: `
    <div class="dash">
      <div class="dash__bar">
        <strong>Bảng việc hôm nay</strong>
        <span>Nhìn 10 giây · biết bắt đầu từ đâu</span>
      </div>
      <div class="dash__grid">
        <div class="dash__card"><div class="dash__num">15</div><div class="dash__label">Khách sắp hết thuốc</div></div>
        <div class="dash__card"><div class="dash__num">12</div><div class="dash__label">Khách lâu chưa quay lại</div></div>
        <div class="dash__card"><div class="dash__num">3</div><div class="dash__label">Đơn đặt trước</div></div>
        <div class="dash__card"><div class="dash__num">2</div><div class="dash__label">Hàng cận hạn cần ưu tiên</div></div>
      </div>
    </div>`,
    note: 'Số liệu minh họa. Khi triển khai, lấy từ dữ liệu thật của nhà thuốc.',
  }),

  ecosystem: figShell({
    id: 'ecosystem',
    body: `
    <div class="pyramid">
      <div class="pyramid__row">Người dân / gia đình gắn bó hơn</div>
      <div class="pyramid__up">▲</div>
      <div class="pyramid__row">Sổ sức khỏe số · đồng hành hằng ngày</div>
      <div class="pyramid__up">▲</div>
      <div class="pyramid__row">Hồ sơ sức khỏe theo từng thành viên</div>
      <div class="pyramid__up">▲</div>
      <div class="pyramid__row">Nhà thuốc = người bạn đồng hành của gia đình</div>
      <div class="pyramid__up">▲</div>
      <div class="pyramid__row">POS + Quản lý vận hành (nền tảng)</div>
    </div>`,
    note: 'POS truyền thống làm tốt lớp dưới cùng. Khoảng cách nằm ở các tầng phía trên.',
  }),

  hub: figShell({
    id: 'hub',
    body: `
    <div class="hub">
      <div class="hub__node hub__node--soft">Phòng khám / chăm sóc cộng đồng<br/><span style="font-size:9pt;font-weight:500">(Định hướng · không thay bệnh viện)</span></div>
      <div class="hub__v">│</div>
      <div class="hub__mid">
        <div class="hub__node">Người dân<br/>&amp; gia đình</div>
        <div style="font-weight:700;color:#0f2f38">────</div>
        <div class="hub__node">Nhà thuốc</div>
      </div>
      <div class="hub__v">│</div>
      <div class="hub__node hub__node--gold">Sổ sức khỏe số</div>
      <div class="hub__v">│</div>
      <div class="hub__node hub__node--soft">Hồ sơ · nhắc · lịch sử · đồng hành</div>
    </div>`,
  }),

  'app-day': figShell({
    id: 'app-day',
    body: `
    <div class="tl">
      <div class="tl__item"><div class="tl__day">7:00</div><div class="tl__act">Nhắc uống<br/>thuốc HA</div></div>
      <div class="tl__item"><div class="tl__day">8:30</div><div class="tl__act">Xem chỉ số<br/>tuần này</div></div>
      <div class="tl__item"><div class="tl__day">12:00</div><div class="tl__act">Lưu đơn<br/>khám mới</div></div>
      <div class="tl__item"><div class="tl__day">19:00</div><div class="tl__act">Lịch sử mua<br/>của bố</div></div>
      <div class="tl__item"><div class="tl__day">Ngày 28</div><div class="tl__act">Gợi ý liên hệ<br/>nhà thuốc</div></div>
    </div>`,
    note: 'Không tự bán thuốc · không tự kê đơn · không thay dược sĩ.',
  }),

  'pos-vs-novixa': figShell({
    id: 'pos-vs-novixa',
    body: `
    <div class="cap">
      <div class="cap__h cap__h--q">Câu hỏi của chủ</div>
      <div class="cap__h cap__h--pos">Lớp quản lý giao dịch</div>
      <div class="cap__h cap__h--nvx">Novixa hướng tới</div>

      <div class="cap__c cap__c--q">Hôm nay bán được bao nhiêu?</div>
      <div class="cap__c cap__c--yes">Thường có</div>
      <div class="cap__c cap__c--novixa">Có</div>

      <div class="cap__c cap__c--q">Tồn kho · lô · hạn dùng?</div>
      <div class="cap__c cap__c--yes">Thường có</div>
      <div class="cap__c cap__c--novixa">Có</div>

      <div class="cap__c cap__c--q">Ai sắp cần mua tiếp?</div>
      <div class="cap__c cap__c--partial">Không mặc định</div>
      <div class="cap__c cap__c--novixa">Có / mở dần</div>

      <div class="cap__c cap__c--q">Khách lâu chưa quay lại?</div>
      <div class="cap__c cap__c--partial">Không mặc định</div>
      <div class="cap__c cap__c--novixa">Có / mở dần</div>

      <div class="cap__c cap__c--q">Hôm nay nên làm việc gì trước?</div>
      <div class="cap__c cap__c--partial">Không mặc định</div>
      <div class="cap__c cap__c--novixa">Có / mở dần</div>

      <div class="cap__c cap__c--q">Sổ sức khỏe số · đồng hành gia đình?</div>
      <div class="cap__c cap__c--partial">Thường không có</div>
      <div class="cap__c cap__c--novixa">Lớp khác biệt</div>
    </div>`,
    note: '“Không mặc định” = thường không phải nhiệm vụ cốt lõi của lớp chỉ quản lý giao dịch.',
  }),

  'family-book': figShell({
    id: 'family-book',
    body: `
    <div class="fam">
      <div class="fam__account">
        Tài khoản gia đình Novixa
        <span>Sổ sức khỏe số · người dùng chủ động mở mỗi ngày</span>
      </div>
      <div class="fam__grid">
        <div class="fam__card">
          <div class="fam__who">Bố</div>
          <ul class="fam__items">
            <li>Thuốc huyết áp · nhắc uống</li>
            <li>Chỉ số · lịch sử mua</li>
          </ul>
        </div>
        <div class="fam__card">
          <div class="fam__who">Mẹ</div>
          <ul class="fam__items">
            <li>Vitamin · liệu trình</li>
            <li>Đơn thuốc đã lưu</li>
          </ul>
        </div>
        <div class="fam__card">
          <div class="fam__who">Con</div>
          <ul class="fam__items">
            <li>Dị ứng đã ghi nhận</li>
            <li>Lịch sử mua gần đây</li>
          </ul>
        </div>
        <div class="fam__card">
          <div class="fam__who">Ông / Bà</div>
          <ul class="fam__items">
            <li>Bệnh nền · nhật ký</li>
            <li>Nhắc tái mua / tái khám</li>
          </ul>
        </div>
      </div>
      <div class="fam__foot">→ Nhà thuốc trở thành người bạn đồng hành sức khỏe của cả gia đình</div>
    </div>`,
    note: 'Khi gia đình đồng ý sử dụng. Ready / Pilot / Định hướng — demo sẽ nói rõ.',
  }),

  'two-way': figShell({
    id: 'two-way',
    body: `
    <div class="twoway">
      <div class="twoway__col">
        <div class="twoway__head twoway__head--ppl">Đối với người dân</div>
        <div class="twoway__body">
          <ul>
            <li>Quản lý sức khỏe dễ hơn</li>
            <li>Ít quên thuốc hơn</li>
            <li>Có lịch sử để tham khảo</li>
            <li>Quản lý cả gia đình</li>
            <li>Có nơi lưu thông tin sức khỏe</li>
          </ul>
        </div>
      </div>
      <div class="twoway__mid">
        <div class="twoway__arrow">Giá trị<br/>TRƯỚC →</div>
        <div style="font-size:14pt;line-height:1">⇄</div>
        <div class="twoway__arrow">← Gắn bó<br/>SAU</div>
      </div>
      <div class="twoway__col">
        <div class="twoway__head twoway__head--rx">Đối với nhà thuốc</div>
        <div class="twoway__body">
          <ul>
            <li>Hiểu khách hàng tốt hơn</li>
            <li>Chăm sóc đúng thời điểm</li>
            <li>Tăng sự gắn kết</li>
            <li>Thêm cơ hội tư vấn phù hợp</li>
            <li>Khách hàng trung thành bền hơn</li>
          </ul>
        </div>
      </div>
    </div>`,
  }),
};

/** Replace <!--FIG:id--> and <!--FIGTOC--> markers. */
export function injectFigures(html) {
  let out = html.replace(/<!--\s*FIGTOC\s*-->/gi, () => buildFigureTocHtml());
  out = out.replace(/<!--\s*FIG:([\w-]+)\s*-->/gi, (_, id) => {
    const fig = FIGURES[id];
    if (!fig) {
      console.warn(`[wp001-figures] Unknown figure id: ${id}`);
      return '';
    }
    return fig;
  });
  return out;
}

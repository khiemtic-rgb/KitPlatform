export type VideoShotSource = 'live' | 'ai' | 'screen' | 'asset';

export type VideoLabShot = {
  id: string;
  scene: string;
  shot: string;
  content: string;
  sources: VideoShotSource[];
  location: string;
  talent: string;
  gear: string;
  reuse: string;
  clock: string;
};

export type VideoLabItem = {
  code: string;
  brandCode: string;
  brandName: string;
  formula: string;
  title: string;
  durationSec: number;
  aspect: '9:16' | '16:9';
  crew: 'screen' | 'half-day' | 'full-day';
  purpose: string;
  shots: VideoLabShot[];
};

export const VIDEO_SHOT_SOURCE_LABEL: Record<VideoShotSource, string> = {
  live: 'QUAY',
  ai: 'AI',
  screen: 'SCREEN',
  asset: 'ASSET',
};

export const VIDEO_LAB_PHASE1: VideoLabItem[] = [
  {
    code: 'P1-03',
    brandCode: 'KIT_MKT',
    brandName: 'KIT Marketing',
    formula: 'Product / Vision',
    title: 'Một người + AI có thể làm được bao nhiêu?',
    durationSec: 60,
    aspect: '16:9',
    crew: 'screen',
    purpose: 'Demo factory: 1 insight → 6 góc. Không thuê đoàn.',
    shots: [
      s('03-01', 'Hook', 'CU màn hình', '0–4s', 'Người ngồi máy. Text: Một người vận hành bao nhiêu thương hiệu?', ['screen', 'live'], 'Văn phòng KIT', '1 operator', 'Laptop + cam cố định', 'Hook vision'),
      s('03-02', 'Insight', 'Screen UI', '5–12s', 'Nhập Core Idea. Text: Một Insight. AI chấm Fit.', ['screen'], 'Admin KIT_MKT', '—', 'Ghi màn Idea Pool', 'UI factory'),
      s('03-03', '6 brand', 'Grid UI', '13–22s', 'Lần lượt Novixa → Famixa → KIT → Vân Đỉnh → Xuân Hòa → TN Life. VO: không phải 6 bài copy.', ['screen'], 'Admin Góc brand', '—', 'Ghi màn matrix Fit', 'Brand grid'),
      s('03-04', 'Adapt', 'UI + cut', '23–35s', '6 góc khác nhau cùng insight. VO: câu chuyện khác nhau.', ['screen'], 'Admin packages', '—', 'Ghi 6 title góc', 'Adapt proof'),
      s('03-05', 'Pipeline', 'UI flow', '36–46s', 'Script → Facebook → Website → Video → Calendar.', ['screen'], 'Admin Ops/Lịch', '—', 'Ghi màn lịch', 'Pipeline'),
      s('03-06', 'Human', 'CU + UI', '47–55s', 'Người duyệt Approve. VO: AI làm nặng, người quyết định.', ['screen', 'live'], 'Văn phòng', '1 operator', 'Cam + màn Approve', 'Human-in-loop'),
      s('03-07', 'Endcard', 'Logo', '56–60s', 'Logo KIT Marketing. Text: One person. AI. Content Factory.', ['asset', 'ai'], '—', '—', 'Logo pack', 'Endcard KIT MKT'),
    ],
  },
  {
    code: 'P1-02',
    brandCode: 'KIT',
    brandName: 'KIT Technology',
    formula: 'Before → After',
    title: 'Một doanh nghiệp không lớn lên bằng cách làm nhiều việc hơn',
    durationSec: 60,
    aspect: '16:9',
    crew: 'screen',
    purpose: 'Test motion + screen + so sánh đôi khung. Rẻ, làm trước đoàn.',
    shots: [
      s('02-01', 'Hook', 'Split 2 khung', '0–5s', 'Trái: Thêm việc. Phải: Xây hệ thống. VO: thêm người hay xây hệ thống?', ['screen', 'ai'], 'Studio / văn phòng', '—', 'After Effects / CapCut', 'Split template'),
      s('02-02', 'Before', 'MS nhân viên', '6–12s', 'Nhập liệu, gửi file, gọi điện, hỏi nhau.', ['live', 'ai'], 'Văn phòng', '2 nhân viên', 'Cam + điện thoại', 'Chaos office'),
      s('02-03', 'Before', 'Insert file', '12–18s', 'Excel, Zalo, thư mục Drive chồng lên nhau.', ['screen'], 'Màn hình thật', '—', 'Ghi màn', 'Tool clutter'),
      s('02-04', 'Vấn đề', 'CU quản lý', '19–30s', 'Nhìn nhiều bảng tính. VO: lớn lên bị giới hạn cách vận hành.', ['live', 'screen'], 'Phòng họp', '1 quản lý', 'Cam + laptop', 'Manager wait'),
      s('02-05', 'After', 'UI hệ thống', '31–43s', 'Dữ liệu tập trung, quy trình, nhiệm vụ, báo cáo.', ['screen'], 'Demo KIT', '—', 'Ghi dashboard', 'System UI'),
      s('02-06', 'Giá trị', 'MS làm việc', '44–54s', 'Nhân viên làm việc với khách / việc quan trọng.', ['live', 'ai'], 'Văn phòng gọn', '1–2 người', 'Cam', 'Calm work'),
      s('02-07', 'Endcard', 'Logo', '55–60s', 'Text: Doanh nghiệp muốn lớn cần nền tảng vận hành. Logo KIT.', ['asset'], '—', '—', 'Logo KIT', 'Endcard KIT'),
    ],
  },
  {
    code: 'P1-04',
    brandCode: 'NOVIXA',
    brandName: 'Novixa',
    formula: 'Mini Story',
    title: 'Một ngày quá bận của chủ nhà thuốc',
    durationSec: 60,
    aspect: '9:16',
    crew: 'half-day',
    purpose: 'Test storytelling + footage đời thật. Ưu tiên quay đầu.',
    shots: [
      s('04-01', 'Mở cửa', 'WS cửa', '0–4s', 'Cửa mở. Đồng hồ 7:30. Text: 7:30 sáng.', ['live'], 'Nhà thuốc set / DEMO', 'Chủ NT', 'Cam + đồng hồ', 'Open shop'),
      s('04-02', 'Khách 1', 'MS quầy', '5–8s', 'Khách đầu tiên. Điện thoại reo.', ['live'], 'Quầy', 'Chủ + khách', 'Cam + phone playback', 'Queue chaos'),
      s('04-03', 'Hỏi hàng', 'CU / OS', '8–12s', 'Nhân viên: Chị ơi mã này còn hàng không?', ['live'], 'Quầy + kệ', '1 NV', 'Cam + lav', 'Staff ask'),
      s('04-04', 'Dồn việc', 'Montage', '13–22s', 'Khách / nhập hàng / điện thoại / kiểm tồn / giấy tờ.', ['live'], 'NT', 'Chủ + NV + 2 khách', 'Cam + B-roll', 'Busy montage'),
      s('04-05', 'Sổ / Excel', 'CU tay + screen', '23–32s', 'Mở sổ hoặc Excel. Nhìn kệ. VO: việc nào cũng cần nhớ.', ['live', 'screen'], 'Quầy máy', 'Chủ', 'Cam + laptop', 'Manual memory'),
      s('04-06', 'Mệt', 'CU mặt + đồng hồ', '33–42s', '18:30 vẫn còn việc. VO: 24 giờ không đủ.', ['live'], 'NT chiều', 'Chủ', 'Cam', 'Tired closeup'),
      s('04-07', 'Hệ thống', 'UI Novixa', '43–52s', 'Tồn, cảnh báo, doanh thu, FEFO, báo cáo.', ['screen'], 'Demo Novixa', '—', 'Ghi màn', 'Novixa UI'),
      s('04-08', 'Đóng cửa', 'WS + endcard', '53–60s', 'Đóng cửa. Text: Bớt việc phải nhớ. Logo Novixa.', ['live', 'asset'], 'Cửa NT', 'Chủ', 'Cam + logo', 'Close shop'),
    ],
  },
  {
    code: 'P1-05',
    brandCode: 'NOVIXA',
    brandName: 'Novixa',
    formula: 'Problem → Solution',
    title: 'Hàng cận hạn không tự biến mất',
    durationSec: 60,
    aspect: '9:16',
    crew: 'half-day',
    purpose: 'Test explainer: macro hộp thuốc + screen FEFO.',
    shots: [
      s('05-01', 'Hook', 'Macro hộp', '0–3s', 'Hộp thuốc trên kệ. Text: Hộp này còn bao lâu?', ['live'], 'Kệ thuốc', '—', 'Macro lens', 'Expiry macro'),
      s('05-02', 'Kệ', 'WS kệ', '4–12s', 'Nhiều SKU. Một hộp nằm phía sau. VO: sai vị trí thành tồn.', ['live'], 'Kệ', '—', 'Cam', 'Shelf depth'),
      s('05-03', 'FIFO sai', 'MS lấy hàng', '13–22s', 'NV lấy hộp mới phía trước. Hộp cũ còn sau. Text: Hàng cũ vẫn còn.', ['live'], 'Kệ', '1 NV', 'Cam', 'Wrong pick'),
      s('05-04', 'Thời gian', 'Insert calendar', '23–31s', 'Calendar chạy. VO: cận hạn không tự biến mất.', ['ai', 'asset'], '—', '—', 'Motion calendar', 'Time lapse'),
      s('05-05', 'FEFO UI', 'Screen', '32–42s', 'Danh sách xếp theo hạn dùng. VO: ưu tiên xuất gần hạn.', ['screen'], 'Demo Novixa', '—', 'Ghi màn FEFO', 'FEFO UI'),
      s('05-06', 'Cảnh báo', 'UI + MS', '43–52s', 'Alert cận hạn. NV xử lý. VO: nhìn sớm, xử lý sớm.', ['screen', 'live'], 'Quầy + màn', '1 NV', 'Cam + UI', 'Alert act'),
      s('05-07', 'Endcard', 'Logo', '53–60s', 'Logo Novixa. Text: không chỉ biết còn bao nhiêu — biết xử lý trước.', ['asset'], '—', '—', 'Logo Novixa', 'Endcard Novixa'),
    ],
  },
  {
    code: 'P1-07',
    brandCode: 'FAMIXA',
    brandName: 'Famixa',
    formula: 'Emotional Story',
    title: 'Ba chỉ có 10 phút',
    durationSec: 60,
    aspect: '16:9',
    crew: 'half-day',
    purpose: 'Test cảm xúc đời thật. Quay nhà, 2 diễn viên. Để sau 04/05 nếu chưa có set.',
    shots: [
      s('07-01', 'Về nhà', 'WS cửa', '0–5s', 'Ba về. Con: Ba! Ba nhìn điện thoại.', ['live'], 'Nhà ở', 'Ba + con (6–8 tuổi)', 'Cam + lav', 'Door hello'),
      s('07-02', 'Bị hoãn', 'MS / CU phone', '6–15s', 'Con: Ba xem cái này. Ba: đợi một chút. Tin nhắn.', ['live'], 'Phòng khách', 'Ba + con', 'Cam + phone', 'Phone vs child'),
      s('07-03', 'Một mình', 'WS con', '16–25s', 'Con ngồi một mình. Ba vẫn làm việc.', ['live'], 'Phòng khách', 'Ba + con', 'Cam', 'Child alone'),
      s('07-04', 'Đi ngủ', 'MS phòng ngủ', '26–34s', 'Con đi ngủ. Ba bước vào, nhìn con.', ['live'], 'Phòng ngủ', 'Ba + con', 'Cam low light', 'Bed look'),
      s('07-05', 'Bức vẽ', 'CU tranh', '35–45s', 'Tranh gia đình. VO: khoảnh khắc chỉ vài phút.', ['live'], 'Tủ / tường', '—', 'Cam + tranh prop', 'Kids drawing'),
      s('07-06', 'Có mặt', 'MS giường', '46–54s', 'Ba đặt điện thoại xuống. Ngồi cạnh con.', ['live'], 'Phòng ngủ', 'Ba + con', 'Cam', 'Put phone down'),
      s('07-07', 'Endcard', 'Card', '55–60s', 'Text: Con không cần cả ngày. Con cần ba thật sự ở bên. Logo Famixa.', ['asset'], '—', '—', 'Logo Famixa', 'Endcard Famixa'),
    ],
  },
  {
    code: 'P1-10',
    brandCode: 'VANDINH',
    brandName: 'Vân Đỉnh Trà',
    formula: 'Documentary',
    title: 'Một búp trà đi qua những gì?',
    durationSec: 60,
    aspect: '16:9',
    crew: 'full-day',
    purpose: 'Thư viện footage. Thuê đoàn sau khi chốt shot. Tái dùng hàng chục clip.',
    shots: [
      s('10-01', 'Bình minh', 'WS / drone', '0–5s', 'Đồi chè bình minh. Ambient. VO: trước khi thành chén trà…', ['live'], 'Đồi chè TN', '—', 'Drone + tripod', 'Dawn tea hill'),
      s('10-02', 'Búp', 'Macro', '6–12s', 'Macro búp trà. VO: đã qua rất nhiều bàn tay.', ['live'], 'Đồi chè', '—', 'Macro', 'Tea bud'),
      s('10-03', 'Hái', 'MS + CU tay', '13–20s', 'Người hái. Cận tay, cận búp, toàn cảnh đồi.', ['live'], 'Đồi chè', '1–2 người hái', 'Cam + gimbal', 'Harvest hands'),
      s('10-04', 'Chế biến', 'MS xưởng', '21–30s', 'Héo / sao / vò. VO: hái, chọn, làm héo, chế biến.', ['live'], 'Xưởng / nhà sao', '1 nghệ nhân', 'Cam', 'Process tea'),
      s('10-05', 'Texture', 'Insert macro', '31–39s', 'Lá, máy, bàn tay, texture trà khô.', ['live'], 'Xưởng', '—', 'Macro', 'Tea texture'),
      s('10-06', 'Đóng gói', 'MS', '40–47s', 'Đóng gói hộp / túi Vân Đỉnh.', ['live'], 'Xưởng / studio', '1 người', 'Cam', 'Pack tea'),
      s('10-07', 'Pha', 'CU ấm', '48–55s', 'Nước nóng, trà nở, khói.', ['live'], 'Bàn trà', '1 người pha', 'Cam + steam light', 'Pour tea'),
      s('10-08', 'Chén', 'CU + endcard', '56–60s', 'Chén trà. Text: Từ đỉnh mây đến chén trà. Logo.', ['live', 'asset'], 'Bàn trà', '—', 'Cam + logo', 'Cup hero'),
    ],
  },
  {
    code: 'P1-11',
    brandCode: 'VANDINH',
    brandName: 'Vân Đỉnh Trà',
    formula: 'Cinematic Story',
    title: 'Cuộc sống quá nhanh',
    durationSec: 60,
    aspect: '16:9',
    crew: 'full-day',
    purpose: 'Test nhịp nhanh đô thị → im lặng → đồi chè. Cùng chuyến với P1-10 nếu brief sẵn.',
    shots: [
      s('11-01', 'Vội', 'Montage city', '0–10s', 'Điện thoại, email, xe, họp, đồng hồ. Âm dồn. VO: chúng ta luôn vội.', ['live', 'ai'], 'Thành phố / stock', '1 người đi làm', 'Cam + stock fallback', 'City rush'),
      s('11-02', 'Đồng hồ', 'CU', '11–18s', 'Nhìn đồng hồ. VO: vội đi làm, trả lời, hoàn thành.', ['live', 'ai'], 'Văn phòng / phố', '1 người', 'Cam', 'Watch check'),
      s('11-03', 'Cắt', 'Black', '19–23s', 'Màn hình đen. Im lặng.', ['asset'], 'NLE', '—', 'Edit', 'Hard cut'),
      s('11-04', 'Đồi', 'WS + gió', '24–35s', 'Đồi chè, gió, lá. VO: có những thứ không cần phải vội.', ['live'], 'Đồi chè TN', '—', 'Drone / tripod (reuse 10-01)', 'Slow hill'),
      s('11-05', 'Pha trà', 'CU tay', '36–48s', 'Pha, rót, khói, ánh sáng.', ['live'], 'Bàn trà', '1 người', 'Reuse 10-07', 'Pour cinematic'),
      s('11-06', 'Thưởng', 'MS', '49–56s', 'Người ngồi thưởng trà.', ['live'], 'Hiên / đồi', '1 người', 'Cam', 'Taste tea'),
      s('11-07', 'Endcard', 'Card', '57–60s', 'Text: Đôi khi chậm lại cũng là một cách sống. Logo Vân Đỉnh.', ['asset'], '—', '—', 'Logo trà', 'Endcard VDT'),
    ],
  },
];

function s(
  id: string,
  scene: string,
  shot: string,
  clock: string,
  content: string,
  sources: VideoShotSource[],
  location: string,
  talent: string,
  gear: string,
  reuse: string,
): VideoLabShot {
  return { id, scene, shot, clock, content, sources, location, talent, gear, reuse };
}

export function countLabSources(shots: VideoLabShot[]) {
  const n = { live: 0, ai: 0, screen: 0, asset: 0 };
  for (const shot of shots) {
    for (const src of shot.sources) n[src] += 1;
  }
  return n;
}

export function flattenLabShots(videos: VideoLabItem[] = VIDEO_LAB_PHASE1) {
  return videos.flatMap((v) =>
    v.shots.map((shot) => ({
      ...shot,
      videoCode: v.code,
      videoTitle: v.title,
      brandCode: v.brandCode,
      brandName: v.brandName,
      formula: v.formula,
    })),
  );
}

export function labShotsToCsv(videos: VideoLabItem[] = VIDEO_LAB_PHASE1) {
  const header = [
    'Video',
    'Brand',
    'Công thức',
    'Scene',
    'Shot',
    'Clock',
    'Nội dung',
    'QUAY',
    'AI',
    'SCREEN',
    'ASSET',
    'Địa điểm',
    'Diễn viên',
    'Thiết bị',
    'Reuse',
  ];
  const lines = [header.join(',')];
  for (const row of flattenLabShots(videos)) {
    const mark = (src: VideoShotSource) => (row.sources.includes(src) ? 'X' : '');
    lines.push(
      [
        row.videoCode,
        row.brandName,
        row.formula,
        row.scene,
        row.shot,
        row.clock,
        row.content,
        mark('live'),
        mark('ai'),
        mark('screen'),
        mark('asset'),
        row.location,
        row.talent,
        row.gear,
        row.reuse,
      ]
        .map(csvCell)
        .join(','),
    );
  }
  return lines.join('\n');
}

function csvCell(value: string) {
  if (/[",\n]/.test(value)) return `"${value.replace(/"/g, '""')}"`;
  return value;
}

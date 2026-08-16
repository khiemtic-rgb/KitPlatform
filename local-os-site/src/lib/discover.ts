import type { LocalListing } from './api';

export type DiscoverPillar = 'dia-diem' | 'am-thuc' | 'du-lich' | 'van-hoa';

export type DiscoverSource = { label: string; url: string };

export type DiscoverPlace = {
  kind: 'place';
  slug: string;
  title: string;
  kicker: string;
  summary: string;
  placeText: string;
  pillars: DiscoverPillar[];
  cover: string;
  tag?: string;
  area?: string;
  featured?: boolean;
  intro: string[];
  doWhat: string[];
  eatWhat: string[];
  nearby: string[];
  eventHints: string[];
  mapsQuery: string;
  sources: DiscoverSource[];
};

export type DiscoverGuide = {
  kind: 'guide';
  slug: string;
  title: string;
  kicker: string;
  summary: string;
  pillars: DiscoverPillar[];
  cover: string;
  featured?: boolean;
  sections: { heading: string; body: string; placeSlugs?: string[] }[];
  sources: DiscoverSource[];
};

export const HUB_CATS: {
  id: string;
  label: string;
  lead: string;
  href?: string;
  soon?: boolean;
  tone: string;
}[] = [
  { id: 'dia-diem', label: 'Địa điểm', lead: 'Danh lam, thắng cảnh, điểm tham quan', href: '/kham-pha/dia-diem', tone: 'place' },
  { id: 'am-thuc', label: 'Ẩm thực', lead: 'Chè, đặc sản — chưa sổ quán', href: '/kham-pha/am-thuc', tone: 'food' },
  { id: 'du-lich', label: 'Du lịch', lead: 'Đi đâu, chơi gì vừa sức cuối tuần', href: '/kham-pha/du-lich', tone: 'travel' },
  { id: 'van-hoa', label: 'Văn hóa', lead: 'Trà, di sản, địa chỉ đỏ', href: '/kham-pha/van-hoa', tone: 'culture' },
  { id: 'quan-hay', label: 'Quán hay', lead: 'Sắp có — chưa mở sổ quán', soon: true, tone: 'cafe' },
  { id: 'check-in', label: 'Check-in', lead: 'Sắp có — chưa mở điểm sống ảo', soon: true, tone: 'photo' },
  { id: 'cam-nang', label: 'Cẩm nang', lead: 'Hướng dẫn, mẹo hay, thông tin hữu ích', href: '/kham-pha#cam-nang', tone: 'guide' },
];

export const HUB_FAQS: { q: string; href: string }[] = [
  { q: 'Cuối tuần ở Thái Nguyên nên đi đâu?', href: '/kham-pha/cam-nang/cuoi-tuan' },
  { q: 'Ăn gì ở Thái Nguyên?', href: '/kham-pha/cam-nang/an-gi' },
  { q: 'Nửa ngày trong phố đi đâu?', href: '/kham-pha/cam-nang/trong-pho' },
  { q: 'Về nguồn ở Thái Nguyên?', href: '/kham-pha/cam-nang/ve-nguon' },
  { q: 'Văn hóa trà Tân Cương / Festival?', href: '/kham-pha/cam-nang/van-hoa-tra' },
  { q: 'Gần phố hay đi huyện — chọn thế nào?', href: '/kham-pha/cam-nang/gan-hay-xa' },
  { q: 'Sinh viên mới đến Thái Nguyên?', href: '/kham-pha/cam-nang/sinh-vien-moi' },
  { q: 'Mùa nào đi đâu?', href: '/kham-pha/cam-nang/mua-nao' },
];

export const HUB_RELATED: { label: string; lead: string; href: string }[] = [
  { label: 'Sự kiện còn hạn', lead: 'Lễ hội, thể thao, hội chợ — chỉ tin đã duyệt', href: '/su-kien' },
  { label: 'Việc làm', lead: 'Tin tuyển còn hạn, không phải tour', href: '/viec' },
  { label: 'Phòng trọ', lead: 'Tin phòng đã duyệt — giá liên hệ', href: '/tro' },
];

export const PILLARS: { id: DiscoverPillar; label: string; lead: string }[] = [
  { id: 'dia-diem', label: 'Địa điểm', lead: 'Hồ, đồi chè, bảo tàng, di tích — chỗ để hiểu Thái Nguyên.' },
  { id: 'am-thuc', label: 'Ẩm thực', lead: 'Chè và món địa phương. Chưa phải danh bạ quán.' },
  { id: 'du-lich', label: 'Du lịch', lead: 'Trong phố nửa ngày, cuối tuần hồ / hang / về nguồn.' },
  { id: 'van-hoa', label: 'Văn hóa', lead: 'Trà, di sản, bảo tàng, địa chỉ đỏ — không phải tin lễ hội đã hết.' },
];

const SRC_TOP12: DiscoverSource = {
  label: 'Cổng Du lịch Thái Nguyên — Top 12 điểm đến',
  url: 'http://thainguyentourism.vn/vi/news/Tin-tuc/TOP-12-DIEM-DEN-DU-LICH-HAP-DAN-TAI-THAI-NGUYEN-1007.html',
};

export const PLACES: DiscoverPlace[] = [
  {
    kind: 'place',
    slug: 'ho-nui-coc',
    title: 'Hồ Núi Cốc',
    kicker: 'Địa điểm · Du lịch',
    summary: 'Khu du lịch quốc gia ~15 km tây nam trung tâm. Hồ nhân tạo, đảo, dịch vụ trong khu — hỏi vé tại chỗ.',
    placeText: 'Phía nam huyện Đại Từ — khoảng 15 km tây nam TP. Thái Nguyên',
    pillars: ['dia-diem', 'du-lich'],
    cover: '/banner-ho-nui-coc.png',
    tag: 'Thiên nhiên',
    area: 'Đại Từ',
    featured: true,
    intro: [
      'Hồ Núi Cốc là hồ nhân tạo chắn sông Công, mặt hồ khoảng 25 km², nằm trên địa bàn huyện Đại Từ. Cục Du lịch Quốc gia ghi nhận đây là thắng cảnh, cách trung tâm thành phố khoảng 15 km về phía tây nam. Cổng du lịch tỉnh xếp đây là Khu du lịch quốc gia.',
      'Khu vực có bến, đảo, tượng / động / dịch vụ vui chơi do đơn vị quản lý vận hành. Giá vé, giờ mở cửa và trò chơi đổi theo mùa — hỏi tại cổng hoặc trang quản lý, không lấy số cũ trên mạng.',
    ],
    doWhat: [
      'Đi hồ / xem cảnh — nửa ngày hoặc cả ngày từ ký túc / nhà trọ trong phố.',
      'Kết hợp đường Tân Cương nếu muốn xem đồi chè cùng chuyến.',
      'Dịch vụ trong khu (thuyền, vui chơi) do đơn vị quản lý niêm yết — không đặt qua Thái Nguyên Life.',
    ],
    eatWhat: [
      'Ăn trong khu theo nhà hàng / quán tại chỗ. Không có menu cố định trên site này.',
      'Trên một số lộ trình từ thành phố, trang quản lý khu du lịch nhắc làng nghề bánh chưng Bờ Đậu — mua nếu đi qua, không phải “review quán”.',
    ],
    nearby: ['tan-cuong', 'suoi-kem-la-bang', 'ditich-27-7', 'lang-thai-hai'],
    eventHints: ['núi cốc', 'hồ núi'],
    mapsQuery: 'Hồ Núi Cốc, Đại Từ, Thái Nguyên',
    sources: [
      { label: 'Cục Du lịch Quốc gia — Thắng cảnh hồ Núi Cốc', url: 'https://vietnamtourism.vn/index.php/tourism/items/1416' },
      { label: 'Du lịch Hồ Núi Cốc — địa chỉ / liên hệ', url: 'https://dulichhonuicoc.vn/dia-chi-ho-nui-coc-thai-nguyen/' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'tan-cuong',
    title: 'Vùng chè Tân Cương',
    kicker: 'Địa điểm · Văn hóa · Ẩm thực',
    summary: 'Vùng chè đặc sản, di sản phi vật thể quốc gia. Đi đồi, uống chè, ghé Không gian văn hóa Trà — không phải tour bắt buộc.',
    placeText: 'Tân Cương, Phúc Trìu, Phúc Xuân — TP. Thái Nguyên',
    pillars: ['dia-diem', 'am-thuc', 'van-hoa', 'du-lich'],
    cover: '/discover/doi-che.jpg',
    tag: 'Ẩm thực',
    area: 'TP. Thái Nguyên',
    featured: true,
    intro: [
      'Nghề chè Tân Cương hình thành từ những năm 1920. Bộ VHTTDL công nhận “Tri thức trồng và chế biến chè Tân Cương” là di sản văn hóa phi vật thể quốc gia (QĐ 240/QĐ-BVHTTDL, 14/2/2023). Lễ công bố tại Tân Cương ngày 5/4/2023; xã được công nhận điểm du lịch cộng đồng.',
      'Vùng chè gồm Tân Cương, Phúc Trìu, Phúc Xuân. Báo chính thống ghi diện tích khoảng trên 1.300 ha. Chỉ dẫn địa lý “Tân Cương” cho sản phẩm chè được cấp năm 2017.',
      'Cổng du lịch tỉnh ghi thêm Không gian văn hóa Trà Tân Cương — công trình lưu tài liệu / hiện vật về trà, không gian mở. Sau đó tỏa ra các đồi chè quanh vùng.',
    ],
    doWhat: [
      'Đi đồi chè, chụp ảnh, mua chè tại hộ / hợp tác xã — hỏi giá tại chỗ, không niêm yết trên site.',
      'Ghé Không gian văn hóa Trà nếu đang mở — hỏi giờ tại chỗ.',
      'Đường Tân Cương cũng là một lối sang Hồ Núi Cốc (theo trang quản lý khu du lịch).',
      'Festival Trà Quốc tế 2026 của tỉnh gắn vùng chè (Tân Cương và các vùng khác) — xem lịch trên trang Sự kiện khi tin còn hạn.',
    ],
    eatWhat: [
      'Chè Tân Cương là đặc sản — uống / mua về. Không liệt kê quán cụ thể khi chưa có nguồn đã đăng ký.',
      'Bữa ăn ven đường: hỏi dân địa phương; Thái Nguyên Life không review nhà hàng.',
    ],
    nearby: ['ho-nui-coc', 'suoi-kem-la-bang', 'lang-thai-hai'],
    eventHints: ['chè', 'trà', 'tân cương', 'festival trà'],
    mapsQuery: 'Đồi chè Tân Cương, Thái Nguyên',
    sources: [
      { label: 'Nhân Dân — di sản tri thức chè Tân Cương', url: 'https://nhandan.vn/tri-thuc-trong-va-che-bien-che-tan-cuong-la-di-san-van-hoa-phi-vat-the-quoc-gia-post746391.html' },
      { label: 'Báo Thái Nguyên — công bố di sản', url: 'https://baothainguyen.vn/van-hoa/202304/cong-bo-di-san-van-hoa-phi-vat-the-quoc-gia-tri-thuc-trong-va-che-bien-che-tan-cuong-79a138b/' },
      { label: 'VnExpress — chỉ dẫn địa lý Tân Cương (2017)', url: 'https://vnexpress.net/trong-va-che-bien-che-tan-cuong-la-di-san-quoc-gia-4572915.html' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'bao-tang-van-hoa-cac-dan-toc',
    title: 'Bảo tàng Văn hóa các dân tộc Việt Nam',
    kicker: 'Địa điểm · Văn hóa',
    summary: 'Một trong 5 bảo tàng quốc gia — ngay trong phố, gần SV. Trưng bày văn hóa 54 dân tộc, trong nhà và ngoài trời.',
    placeText: 'Số 1 đường Đội Cấn, Thái Nguyên',
    pillars: ['dia-diem', 'van-hoa'],
    cover: '/discover/bao-tang.jpg',
    tag: 'Văn hóa',
    area: 'TP. Thái Nguyên',
    featured: true,
    intro: [
      'Bảo tàng thành lập 1960 (Bảo tàng Việt Bắc), đổi tên 1990. Cổng du lịch tỉnh ghi đây là một trong 5 bảo tàng quốc gia, trưng bày và bảo quản trên 40.000 tài liệu / hiện vật gốc về văn hóa 54 dân tộc; có khu trong nhà và ngoài trời.',
      'Địa chỉ thống nhất trên trang bảo tàng và cổng du lịch: số 1 Đội Cấn, Thái Nguyên. Giờ mở cửa / vé / đoàn trải nghiệm: hỏi bảo tàng (cổng du lịch ghi hoạt động “động” cần đặt lịch trước).',
    ],
    doWhat: [
      'Tham quan trưng bày — buổi sáng hoặc chiều, dễ kết hợp lịch học.',
      'Khu ngoài trời nếu thời tiết đẹp.',
      'Không gửi đồ / không đặt vé qua Thái Nguyên Life.',
    ],
    eatWhat: [
      'Trong phố, ăn quanh khu Đội Cấn / trung tâm — tự chọn. Site chưa có sổ quán đã đăng ký.',
    ],
    nearby: ['chua-hang', 'dai-doi-915', 'lang-thai-hai'],
    eventHints: ['bảo tàng', 'văn hóa các dân tộc'],
    mapsQuery: 'Bảo tàng Văn hóa các dân tộc Việt Nam, 1 Đội Cấn, Thái Nguyên',
    sources: [
      { label: 'Bảo tàng — liên hệ (mcve.org.vn)', url: 'http://mcve.org.vn/lien-he/' },
      { label: 'My Thái Nguyên — trang bảo tàng', url: 'https://mythainguyen.vn/vi/baotangvanhoacacdantoc' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'chua-hang',
    title: 'Chùa Hang — Kim Sơn Tự',
    kicker: 'Địa điểm · Văn hóa',
    summary: 'Chùa cổ trong phố, khoảng 3 km phía bắc trung tâm. Dễ đi buổi chiều — không cần cả ngày.',
    placeText: 'Phường Linh Sơn (khu Chùa Hang) — khoảng 3 km phía bắc trung tâm tỉnh',
    pillars: ['dia-diem', 'van-hoa'],
    cover: '/discover/chua-hang.jpg',
    tag: 'Văn hóa',
    area: 'Linh Sơn',
    intro: [
      'Cổng du lịch tỉnh và cổng My Thái Nguyên ghi Chùa Hang — Kim Sơn Tự là ngôi chùa cổ (tương truyền thế kỷ XI), khoảng 3 km phía bắc trung tâm. Đã trùng tu: chính điện tam bảo, tam quan, lầu chuông / trống, nhà thờ tổ.',
      'Lễ hội chính hằng năm vào ngày 20 tháng Giêng, thường kéo dài khoảng 3 ngày — chỉ hiện trên mục Sự kiện khi tin đã duyệt còn hạn. Giờ mở cửa / lệ: hỏi tại chùa.',
    ],
    doWhat: [
      'Đi xe máy / xe buýt nội thị — nửa buổi, kết hợp bảo tàng hoặc về ký túc.',
      'Tham quan, lễ — giữ im lặng, không biến thành điểm check-in ồn.',
    ],
    eatWhat: ['Ăn trong phố trên đường về. Site chưa có sổ quán.'],
    nearby: ['bao-tang-van-hoa-cac-dan-toc', 'dai-doi-915'],
    eventHints: ['chùa hang', 'kim sơn'],
    mapsQuery: 'Chùa Hang Kim Sơn Tự, Thái Nguyên',
    sources: [
      { label: 'My Thái Nguyên — Chùa Hang', url: 'https://mythainguyen.vn/vi/chuahang' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'dai-doi-915',
    title: 'Di tích Đại đội 915',
    kicker: 'Địa điểm · Văn hóa',
    summary: 'Địa chỉ đỏ trong thành phố — phường Gia Sàng. Tưởng niệm 60 liệt sĩ TNXP đêm Noel 1972.',
    placeText: 'Phường Gia Sàng, TP. Thái Nguyên',
    pillars: ['dia-diem', 'van-hoa'],
    cover: '/discover/tnxp-915.jpg',
    tag: 'Lịch sử',
    area: 'Gia Sàng',
    intro: [
      'Cổng du lịch tỉnh: Di tích lịch sử quốc gia nơi tưởng niệm 60 liệt sĩ TNXP Đại đội 915, Đội 91 Bắc Thái, tại phường Gia Sàng. Các chiến sĩ hy sinh khi làm nhiệm vụ đêm Noel năm 1972.',
      'Khu có không gian trưng bày — cổng du lịch ghi gần 350 hiện vật / tài liệu. Giờ mở cửa / đoàn học sinh: hỏi tại di tích.',
    ],
    doWhat: [
      'Dâng hương, xem trưng bày — buổi sáng hoặc chiều, gần SV hơn ATK.',
      'Không tổ chức vui chơi / picnic trong khu tưởng niệm.',
    ],
    eatWhat: ['Ăn ngoài khu di tích, trong phố. Site chưa có sổ quán.'],
    nearby: ['bao-tang-van-hoa-cac-dan-toc', 'chua-hang'],
    eventHints: ['915', 'tnxp', 'thanh niên xung phong'],
    mapsQuery: 'Di tích Đại đội 915 Gia Sàng, Thái Nguyên',
    sources: [
      { label: 'My Thái Nguyên — Đại đội 915', url: 'https://mythainguyen.vn/en/daidoi915' },
      {
        label: 'Cổng Du lịch Thái Nguyên — di tích 915',
        url: 'http://thainguyentourism.vn/vi/news/Tin-tuc/Di-tich-lich-su-Quoc-gia-60-liet-sy-thanh-nien-xung-phong-Dai-doi-915-Doi-91-Bac-Thai-Dia-chi-do-ve-giao-duc-truyen-thong-cach-mang-1103.html',
      },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'den-duom',
    title: 'Đền Đuổm',
    kicker: 'Địa điểm · Văn hóa',
    summary: 'Đền thờ Dương Tự Minh, chân núi Đuổm, sát QL3 — cổng du lịch ghi cách thành phố khoảng 24 km.',
    placeText: 'Xã Động Đạt, huyện Phú Lương — sát quốc lộ 3',
    pillars: ['dia-diem', 'van-hoa', 'du-lich'],
    cover: '/discover/den-duom.jpg',
    tag: 'Văn hóa',
    area: 'Phú Lương',
    intro: [
      'Cổng du lịch tỉnh: Đền Đuổm ở chân núi Đuổm, xã Động Đạt, huyện Phú Lương; thờ Dương Tự Minh — thủ lĩnh phủ Phú Lương, phò mã hai đời vua nhà Lý, thế kỷ XII. Bài tổng quan ghi cách thành phố khoảng 24 km, sát QL3.',
      'Lễ hội hằng năm ngày mùng 6 tháng Giêng. Giờ / lệ: hỏi tại đền. Không lấy lịch “đang diễn ra” từ site bên ngoài nếu mục đã hết ngày.',
    ],
    doWhat: [
      'Đi QL3 — nửa ngày nếu kết hợp về phố buổi chiều.',
      'Tham quan, lễ — giữ trang phục lịch sự.',
    ],
    eatWhat: ['Ăn ven QL3 hoặc về phố. Site chưa có sổ quán.'],
    nearby: ['atk-dinh-hoa', 'ditich-27-7'],
    eventHints: ['đuổm', 'dương tự minh'],
    mapsQuery: 'Đền Đuổm, Động Đạt, Phú Lương, Thái Nguyên',
    sources: [
      { label: 'Cổng Du lịch Thái Nguyên — điểm nổi tiếng (Đền Đuổm 24 km)', url: 'http://thainguyentourism.vn/vi/news/Tin-tuc/Nhung-diem-du-lich-noi-tieng-cua-Thai-Nguyen-38.html' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'atk-dinh-hoa',
    title: 'ATK Định Hóa',
    kicker: 'Địa điểm · Văn hóa · Du lịch',
    summary: 'Di tích quốc gia đặc biệt — “Thủ đô Gió Ngàn”. Cả ngày, huyện Định Hóa. Không phải dạo phố.',
    placeText: 'Huyện Định Hóa — trung tâm quần thể quanh xã Phú Đình / đèo De',
    pillars: ['dia-diem', 'van-hoa', 'du-lich'],
    cover: '/discover/atk.jpg',
    tag: 'Lịch sử',
    area: 'Định Hóa',
    featured: true,
    intro: [
      'Trang quản lý di tích: ATK Định Hóa là An toàn khu Trung ương, nơi ở và làm việc của Chủ tịch Hồ Chí Minh, Trung ương Đảng, Chính phủ thời kháng chiến chống Pháp (1946–1954). Được đánh giá là quần thể di tích quan trọng bậc nhất của dân tộc trong thế kỷ 20.',
      'Quần thể trải dài khoảng 520 km², 182 điểm trên 23 xã / thị trấn. Điểm thường thăm: Nhà tưởng niệm (khánh thành 19/5/2005), Tỉn Keo (họp Bộ Chính trị 6/12/1953 quyết định chiến dịch Điện Biên Phủ), Khuôn Tát, Đồi Phong Tướng.',
    ],
    doWhat: [
      'Cả ngày — xuất phát sớm. Không nhét chung với hồ nếu chỉ có nửa ngày.',
      'Dâng hương Nhà tưởng niệm, xem lán / điểm di tích theo hướng dẫn tại chỗ.',
      'Giờ / vé / xe đưa trong khu: hỏi trung tâm di tích, không lấy số cũ trên mạng.',
    ],
    eatWhat: [
      'Ăn tại nhà hàng / quán quanh khu di tích nếu có. Site không review món “đặc sản ATK”.',
    ],
    nearby: ['den-duom', 'ditich-27-7'],
    eventHints: ['atk', 'định hóa', 'tỉn keo'],
    mapsQuery: 'Nhà tưởng niệm Chủ tịch Hồ Chí Minh ATK Định Hóa, Phú Đình',
    sources: [
      { label: 'Khu di tích ATK Định Hóa — giới thiệu', url: 'http://ditich.atkthainguyen.org.vn/gioi-thieu-atk' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'hang-phuong-hoang',
    title: 'Hang Phượng Hoàng — Suối Mỏ Gà',
    kicker: 'Địa điểm · Du lịch',
    summary: 'Danh thắng cấp quốc gia (1994), huyện Võ Nhai. Hang khô + hang suối — cả ngày, mang giày.',
    placeText: 'Xã Phú Thượng, huyện Võ Nhai — cổng du lịch / My Thái Nguyên ghi khoảng 45 km, gần QL1B',
    pillars: ['dia-diem', 'du-lich'],
    cover: '/discover/hang-phuong-hoang.jpg',
    tag: 'Thiên nhiên',
    area: 'Võ Nhai',
    intro: [
      'Cổng du lịch tỉnh: hang Phượng Hoàng và suối Mỏ Gà là danh thắng xếp hạng cấp quốc gia ở xã Phú Thượng, huyện Võ Nhai. Hang Phượng Hoàng là hang khô, lòng hang rộng, nhiều tầng, nhũ đá. Suối Mỏ Gà ở chân núi — hang nước, nước đổ thành thác nhỏ.',
      'My Thái Nguyên ghi điểm sinh thái Phượng Hoàng cách trung tâm khoảng 45 km, gần QL1B Thái Nguyên — Lạng Sơn; công nhận di tích / danh thắng quốc gia từ 1994.',
    ],
    doWhat: [
      'Cả ngày. Mang giày, đèn / áo khoác tùy thời tiết trong hang.',
      'Vé, giờ, bơi / khuôn viên: hỏi tại cổng khu — không lấy giá cũ.',
      'Không đi một mình vào hang sâu nếu chưa rõ lối.',
    ],
    eatWhat: ['Ăn tại quán trong khu nếu có, hoặc mang theo. Site không review quán Võ Nhai.'],
    nearby: [],
    eventHints: ['phượng hoàng', 'mỏ gà', 'võ nhai'],
    mapsQuery: 'Hang Phượng Hoàng Suối Mỏ Gà, Phú Thượng, Võ Nhai, Thái Nguyên',
    sources: [
      { label: 'My Thái Nguyên — điểm sinh thái Phượng Hoàng', url: 'https://mythainguyen.vn/vi/detailnews/?id=news_1538&t=diem-du-lich-sinh-thai-phuong-hoang-vien-ngoc-xanh-giua-long-vo-nhai' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'suoi-kem-la-bang',
    title: 'Suối Kẹm — chè La Bằng',
    kicker: 'Địa điểm · Du lịch · Ẩm thực',
    summary: 'Suối + vùng chè Đại Từ. Cổng du lịch ghi khoảng 35 km từ thành phố — cả ngày cuối tuần.',
    placeText: 'Xã La Bằng, huyện Đại Từ — khoảng 35 km từ TP. Thái Nguyên',
    pillars: ['dia-diem', 'du-lich', 'am-thuc'],
    cover: '/discover/suoi-kem.jpg',
    tag: 'Thiên nhiên',
    area: 'Đại Từ',
    intro: [
      'Cổng du lịch tỉnh: Suối Kẹm (xã La Bằng, Đại Từ) cách trung tâm thành phố khoảng 35 km, cách thị trấn Đại Từ hơn 10 km. Suối có ghềnh, tầng thác; mùa hè người ta tắm — tự đánh giá an toàn, không có cứu hộ trên site này.',
      'Cùng xã là vùng chè La Bằng. Cổng du lịch nhắc Trung tâm thông tin làng nghề / HTX chè La Bằng — hỏi giờ khi đến.',
    ],
    doWhat: [
      'Cả ngày: suối buổi sáng, đồi chè buổi chiều — hoặc ngược lại tùy nắng.',
      'Không niết giá vé / thuê đồ trên site. Hỏi tại chỗ.',
    ],
    eatWhat: [
      'Chè La Bằng — mua / uống tại hộ hoặc HTX nếu mở. Không list quán.',
    ],
    nearby: ['ho-nui-coc', 'tan-cuong', 'ditich-27-7'],
    eventHints: ['suối kẹm', 'la bằng'],
    mapsQuery: 'Suối Kẹm, La Bằng, Đại Từ, Thái Nguyên',
    sources: [SRC_TOP12],
  },
  {
    kind: 'place',
    slug: 'ditich-27-7',
    title: 'Khu di tích quốc gia 27/7',
    kicker: 'Địa điểm · Văn hóa',
    summary: 'Nơi gắn nguồn cội ngày Thương binh — Liệt sĩ 27/7/1947. Đại Từ — xếp hạng quốc gia 1997. Hỏi giờ tại di tích.',
    placeText: 'Xóm Bàn Cờ — xã Đại Phúc (trước là Hùng Sơn), huyện Đại Từ',
    pillars: ['dia-diem', 'van-hoa', 'du-lich'],
    cover: '/discover/guide-ve-nguon.jpg',
    tag: 'Lịch sử',
    area: 'Đại Từ',
    featured: true,
    intro: [
      'Cổng thông tin tỉnh và cổng du lịch: tối 27/7/1947, tại gốc đa xóm Bàn Cờ (xã Hùng Sơn, nay thuộc xã Đại Phúc, huyện Đại Từ) diễn ra lễ công bố lấy ngày 27/7 hằng năm làm ngày Thương binh toàn quốc — sau đổi thành ngày Thương binh — Liệt sĩ.',
      'Địa điểm được xếp hạng di tích cấp quốc gia năm 1997. Năm 1997 khánh thành khu di tích / bia kỷ niệm; các đợt tôn tạo sau đó mở rộng khuôn viên. Cổng du lịch ghi đây là điểm nối tuyến Hồ Núi Cốc với ATK Định Hóa trên lộ trình về nguồn.',
      'Giờ mở cửa, đoàn học sinh, lễ dâng hương: hỏi tại di tích. Không lấy lịch “đang diễn ra” từ site bên ngoài nếu mục đã hết ngày.',
    ],
    doWhat: [
      'Dâng hương, xem nhà tưởng niệm / trưng bày — giữ trang phục lịch sự.',
      'Kết hợp Hồ Núi Cốc cùng huyện Đại Từ nếu còn cả ngày; không nhét ATK vào cùng buổi nếu xuất phát muộn.',
      'Tháng Bảy thường có đoàn về nguồn — hỏi tại chỗ, không đặt qua Thái Nguyên Life.',
    ],
    eatWhat: ['Ăn ngoài khu di tích, thị trấn / ven đường Đại Từ. Site không review quán.'],
    nearby: ['ho-nui-coc', 'suoi-kem-la-bang', 'atk-dinh-hoa'],
    eventHints: ['27/7', 'thương binh', 'bàn cờ', 'đại từ'],
    mapsQuery: 'Khu di tích lịch sử quốc gia 27/7, Đại Phúc, Đại Từ, Thái Nguyên',
    sources: [
      {
        label: 'Cổng TTĐT Thái Nguyên — khu di tích 27/7',
        url: 'http://thainguyen.gov.vn/den-on-dap-nghia/khu-di-tich-lich-su-quoc-gia-27-7-dia-chi-do-lan-toa-gia-tri-lich-su-282309',
      },
      {
        label: 'Cổng Du lịch Thái Nguyên — địa điểm công bố 27/7',
        url: 'http://thainguyentourism.vn/vi/du-khach/Kham-pha-diem-den/Dia-diem-cong-bo-ngay-Thuong-binh-Liet-si-toan-quoc-73.html',
      },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'lang-thai-hai',
    title: 'Làng nhà sàn Thái Hải',
    kicker: 'Địa điểm · Văn hóa · Du lịch',
    summary: 'Khu bảo tồn làng nhà sàn Tày — Nùng, xã Thịnh Đức. Cổng du lịch / My Thái Nguyên ghi UNWTO 2022. Hỏi dịch vụ tại chỗ.',
    placeText: 'Xã Thịnh Đức, TP. Thái Nguyên — gần trung tâm hơn ATK / hang',
    pillars: ['dia-diem', 'van-hoa', 'du-lich'],
    cover: '/discover/doi-che.jpg',
    tag: 'Văn hóa',
    area: 'Thịnh Đức',
    featured: true,
    intro: [
      'Cổng du lịch tỉnh: Khu bảo tồn làng nhà sàn dân tộc sinh thái Thái Hải (còn gọi Bản làng Thái Hải) ở xã Thịnh Đức, thành phố Thái Nguyên — gần trung tâm hơn các điểm huyện. Bài cổng ghi khoảng 30 ngôi nhà sàn Tày, Nùng; quy mô khuôn viên khoảng 70 ha (một phần đã khai thác).',
      'My Thái Nguyên ghi nhà sàn chuyển từ Định Hóa; Tổ chức Du lịch Thế giới (UNWTO) công nhận làng du lịch tốt nhất thế giới năm 2022. Cổng du lịch còn nhắc các giải ASEAN / OCOP trên bài riêng — xem nguồn, không copy giá khuyến mãi đã hết hạn.',
      'Dịch vụ lưu trú / ẩm thực / trải nghiệm do đơn vị quản lý vận hành. Giờ, vé, đặt chỗ: hỏi tại làng. Không đặt qua Thái Nguyên Life.',
    ],
    doWhat: [
      'Tham quan làng, nhà sàn, không gian xanh — nửa ngày hoặc cả ngày từ phố.',
      'Tôn trọng nhà ở của hộ — cổng du lịch nhắc đăng ký trước nếu dùng dịch vụ trải nghiệm.',
      'Kết hợp Tân Cương / Hồ Núi Cốc nếu còn thời gian; không nhét ATK cùng buổi.',
    ],
    eatWhat: [
      'Ẩm thực trong làng nếu đang phục vụ — hỏi thực đơn / giá tại chỗ. Site không review nhà hàng.',
    ],
    nearby: ['tan-cuong', 'ho-nui-coc', 'bao-tang-van-hoa-cac-dan-toc'],
    eventHints: ['thái hải', 'nhà sàn'],
    mapsQuery: 'Làng nhà sàn Thái Hải, Thịnh Đức, Thái Nguyên',
    sources: [
      { label: 'My Thái Nguyên — Thái Hải', url: 'https://mythainguyen.vn/vi/thaihai' },
      {
        label: 'Cổng Du lịch Thái Nguyên — khu du lịch Thái Hải',
        url: 'http://thainguyentourism.vn/vi/news/Tin-tuc/Khu-du-lich-Thai-Hai-Dam-da-ban-sac-dan-toc-o-Thai-Nguyen-11.html',
      },
      SRC_TOP12,
    ],
  },
  {
    kind: 'place',
    slug: 'dung-tan',
    title: 'Trung tâm TM & DL Dũng Tân',
    kicker: 'Địa điểm · Du lịch',
    summary: 'Điểm trong Top 12 cổng du lịch tỉnh — khu thương mại / vườn. Hỏi giờ tại chỗ; không phải review cửa hàng.',
    placeText: 'TP. Thái Nguyên',
    pillars: ['dia-diem', 'du-lich'],
    cover: '/discover/guide-an-gi.jpg',
    tag: 'Địa điểm',
    area: 'TP. Thái Nguyên',
    intro: [
      'Cổng du lịch tỉnh xếp Trung tâm Thương mại và Du lịch Dũng Tân trong Top 12 điểm đến hấp dẫn tại Thái Nguyên. Đây là khu thương mại / vườn do đơn vị tư nhân vận hành — không phải danh lam nhà nước.',
      'Giờ mở cửa, sự kiện trong khu, giá dịch vụ: hỏi tại chỗ. Thái Nguyên Life không review cửa hàng, không niêm yết khuyến mãi.',
    ],
    doWhat: [
      'Đi xem / mua sắm nếu đang mở — nửa buổi trong phố.',
      'Không gửi đơn, không đặt chỗ qua site này.',
    ],
    eatWhat: ['Ăn trong khu nếu có — tự chọn. Site chưa có sổ quán.'],
    nearby: ['bao-tang-van-hoa-cac-dan-toc', 'chua-hang'],
    eventHints: ['dũng tân'],
    mapsQuery: 'Trung tâm Thương mại Du lịch Dũng Tân, Thái Nguyên',
    sources: [SRC_TOP12],
  },
];

export const GUIDES: DiscoverGuide[] = [
  {
    kind: 'guide',
    slug: 'cuoi-tuan',
    title: 'Cuối tuần ở Thái Nguyên đi đâu?',
    kicker: 'Cẩm nang',
    summary: 'Trong phố nửa ngày; đồi chè / hồ cả ngày; hang hoặc ATK nếu xuất phát sớm. Không phải lịch trình bán tour.',
    pillars: ['du-lich', 'dia-diem'],
    cover: '/banner-ho-nui-coc.png',
    featured: true,
    sections: [
      {
        heading: 'Nửa ngày trong phố',
        body: 'Bảo tàng (1 Đội Cấn), Chùa Hang (~3 km bắc), hoặc di tích Đại đội 915 (Gia Sàng). Đi xe máy / xe buýt nội thị. Kiểm tra giờ trên trang điểm đến trước khi đi.',
        placeSlugs: ['bao-tang-van-hoa-cac-dan-toc', 'chua-hang', 'dai-doi-915'],
      },
      {
        heading: 'Đồi chè Tân Cương',
        body: 'Cách trung tâm không xa. Đi xem đồi, uống chè, ghé Không gian văn hóa Trà nếu mở. Mùa lá xanh đẹp hơn để chụp; không hẹn “hoàng hôn đẹp nhất”.',
        placeSlugs: ['tan-cuong'],
      },
      {
        heading: 'Hồ Núi Cốc — cả ngày',
        body: 'Khoảng 15 km tây nam. Nên đi sớm, mang nước, hỏi vé tại chỗ. Có thể đi đường Tân Cương để xem chè rồi xuống hồ.',
        placeSlugs: ['ho-nui-coc', 'tan-cuong'],
      },
      {
        heading: 'Xa hơn — cả ngày, xuất phát sớm',
        body: 'Hang Phượng Hoàng / Suối Mỏ Gà (Võ Nhai, ~45 km). Suối Kẹm + chè La Bằng (Đại Từ, ~35 km). ATK Định Hóa hoặc di tích 27/7 — về nguồn, không nhét chung với hồ trong một buổi.',
        placeSlugs: ['hang-phuong-hoang', 'suoi-kem-la-bang', 'atk-dinh-hoa', 'ditich-27-7'],
      },
      {
        heading: 'Làng nhà sàn Thái Hải',
        body: 'Xã Thịnh Đức — gần phố hơn hang / ATK. Tham quan làng Tày — Nùng; hỏi dịch vụ tại chỗ. Có thể kết hợp Tân Cương cùng ngày nếu còn sức.',
        placeSlugs: ['lang-thai-hai', 'tan-cuong'],
      },
      {
        heading: 'Sự kiện trong tuần',
        body: 'Lễ hội / ngày hội chỉ hiện khi tin đã duyệt còn hạn — mở mục Sự kiện. Đừng tin lịch “52 sự kiện đang diễn ra” trên site bên ngoài nếu mục đã hết ngày.',
      },
    ],
    sources: [
      { label: 'Cục Du lịch Quốc gia — hồ Núi Cốc', url: 'https://vietnamtourism.vn/index.php/tourism/items/1416' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'guide',
    slug: 'an-gi',
    title: 'Ăn gì ở Thái Nguyên?',
    kicker: 'Cẩm nang · Ẩm thực',
    summary: 'Chè có nguồn: Tân Cương và La Bằng. Quán cụ thể chưa mở — không bịa list “top 10”.',
    pillars: ['am-thuc', 'van-hoa'],
    cover: '/discover/guide-an-gi.jpg',
    featured: true,
    sections: [
      {
        heading: 'Chè Tân Cương',
        body: 'Đặc sản có chỉ dẫn địa lý và di sản tri thức trồng / chế biến. Uống tại vùng chè, Không gian văn hóa Trà, hoặc mua hộp về. Giá đổi theo vụ và hộ — hỏi trực tiếp.',
        placeSlugs: ['tan-cuong'],
      },
      {
        heading: 'Chè La Bằng',
        body: 'Cổng du lịch tỉnh xếp vùng chè La Bằng (Đại Từ) cùng Suối Kẹm. Có thể ghé HTX / trung tâm làng nghề nếu đang mở — không phải “top chè số 1”.',
        placeSlugs: ['suoi-kem-la-bang'],
      },
      {
        heading: 'Trên đường đi hồ',
        body: 'Trang quản lý Hồ Núi Cốc nhắc làng nghề bánh chưng Bờ Đậu trên một lộ trình từ thành phố. Đó là gợi ý đường đi, không phải đánh giá quán.',
        placeSlugs: ['ho-nui-coc'],
      },
      {
        heading: 'Quán trong phố',
        body: 'Sổ “quán hay” chưa mở. Khi có cộng đồng và nguồn đã đăng ký sẽ gắn vào Khám phá. Đừng lấy review ẩn danh làm SoT.',
      },
    ],
    sources: [
      { label: 'VnExpress — chè Tân Cương / chỉ dẫn địa lý', url: 'https://vnexpress.net/trong-va-che-bien-che-tan-cuong-la-di-san-quoc-gia-4572915.html' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'guide',
    slug: 'trong-pho',
    title: 'Nửa ngày trong phố đi đâu?',
    kicker: 'Cẩm nang',
    summary: 'Ba điểm gần SV: bảo tàng, chùa Hang, di tích 915. Không cần xe đi huyện.',
    pillars: ['dia-diem', 'van-hoa', 'du-lich'],
    cover: '/discover/bao-tang.jpg',
    sections: [
      {
        heading: 'Bảo tàng — 1 Đội Cấn',
        body: 'Một trong 5 bảo tàng quốc gia. Trong nhà + ngoài trời. Hỏi giờ / vé tại bảo tàng; đoàn trải nghiệm “động” cần đặt trước (cổng du lịch tỉnh).',
        placeSlugs: ['bao-tang-van-hoa-cac-dan-toc'],
      },
      {
        heading: 'Chùa Hang — ~3 km bắc',
        body: 'Kim Sơn Tự, khu Chùa Hang / phường Linh Sơn. Đi được buổi chiều. Lễ hội 20 tháng Giêng — xem mục Sự kiện khi tin còn hạn.',
        placeSlugs: ['chua-hang'],
      },
      {
        heading: 'Đại đội 915 — Gia Sàng',
        body: 'Địa chỉ đỏ trong thành phố. Dâng hương, xem trưng bày. Không biến thành điểm picnic.',
        placeSlugs: ['dai-doi-915'],
      },
    ],
    sources: [
      { label: 'My Thái Nguyên — bảo tàng', url: 'https://mythainguyen.vn/vi/baotangvanhoacacdantoc' },
      { label: 'My Thái Nguyên — Chùa Hang', url: 'https://mythainguyen.vn/vi/chuahang' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'guide',
    slug: 've-nguon',
    title: 'Về nguồn ở Thái Nguyên?',
    kicker: 'Cẩm nang · Văn hóa',
    summary: 'ATK Định Hóa cả ngày. Trong phố: Đại đội 915. Đền Đuổm trên QL3. Không nhét ba điểm một buổi.',
    pillars: ['van-hoa', 'du-lich'],
    cover: '/discover/guide-ve-nguon.jpg',
    sections: [
      {
        heading: 'ATK Định Hóa — cả ngày',
        body: 'Quần thể di tích quốc gia đặc biệt, huyện Định Hóa. Nhà tưởng niệm, Tỉn Keo, Khuôn Tát. Xuất phát sớm. Giờ / vé hỏi trung tâm di tích.',
        placeSlugs: ['atk-dinh-hoa'],
      },
      {
        heading: 'Trong phố — Đại đội 915',
        body: 'Phường Gia Sàng. Phù hợp buổi chiều hoặc ngày không đi huyện.',
        placeSlugs: ['dai-doi-915'],
      },
      {
        heading: 'Đền Đuổm — QL3',
        body: 'Khoảng 24 km, xã Động Đạt, Phú Lương. Có thể ghé khi đi / về hướng bắc. Lễ hội mùng 6 tháng Giêng.',
        placeSlugs: ['den-duom'],
      },
      {
        heading: 'Di tích 27/7 — Đại Từ',
        body: 'Xóm Bàn Cờ, xã Đại Phúc (trước Hùng Sơn). Nơi công bố ngày Thương binh toàn quốc 27/7/1947; xếp hạng quốc gia 1997. Có thể ghé khi đi Hồ Núi Cốc / Suối Kẹm cùng huyện. Hỏi giờ tại di tích.',
        placeSlugs: ['ditich-27-7', 'ho-nui-coc'],
      },
    ],
    sources: [
      { label: 'Khu di tích ATK Định Hóa — giới thiệu', url: 'http://ditich.atkthainguyen.org.vn/gioi-thieu-atk' },
      {
        label: 'Cổng TTĐT Thái Nguyên — khu di tích 27/7',
        url: 'http://thainguyen.gov.vn/den-on-dap-nghia/khu-di-tich-lich-su-quoc-gia-27-7-dia-chi-do-lan-toa-gia-tri-lich-su-282309',
      },
      SRC_TOP12,
    ],
  },
  {
    kind: 'guide',
    slug: 'van-hoa-tra',
    title: 'Văn hóa trà ở Thái Nguyên',
    kicker: 'Cẩm nang · Văn hóa',
    summary: 'Tân Cương, La Bằng, Festival / thưởng trà — chỉ tin còn hạn. Không phải tour bán chè.',
    pillars: ['van-hoa', 'am-thuc', 'du-lich'],
    cover: '/discover/doi-che.jpg',
    featured: true,
    sections: [
      {
        heading: 'Tân Cương — di sản + đồi',
        body: 'Tri thức trồng và chế biến chè Tân Cương là di sản phi vật thể quốc gia (2023). Đi đồi, uống chè, ghé Không gian văn hóa Trà nếu mở. Giá hộp chè hỏi tại hộ / HTX.',
        placeSlugs: ['tan-cuong'],
      },
      {
        heading: 'La Bằng — chè + suối',
        body: 'Cổng du lịch xếp vùng chè La Bằng cùng Suối Kẹm (Đại Từ, ~35 km). Cả ngày cuối tuần: suối buổi sáng, đồi buổi chiều — hoặc ngược lại tùy nắng.',
        placeSlugs: ['suoi-kem-la-bang'],
      },
      {
        heading: 'Festival và ngày hội',
        body: 'Festival Trà Quốc tế và các ngày thưởng trà / ẩm thực từ trà chỉ hiện trên mục Sự kiện khi tin đã duyệt còn hạn. Đừng lấy lịch “đang diễn ra” từ site bên ngoài nếu mục đã hết ngày.',
        placeSlugs: ['tan-cuong'],
      },
      {
        heading: 'Không phải sổ quán',
        body: 'Site chưa mở “quán hay”. Mua chè tại vùng sản xuất hoặc cửa hàng bạn tự chọn — không có top 10 ẩn danh.',
      },
    ],
    sources: [
      { label: 'Nhân Dân — di sản tri thức chè Tân Cương', url: 'https://nhandan.vn/tri-thuc-trong-va-che-bien-che-tan-cuong-la-di-san-van-hoa-phi-vat-the-quoc-gia-post746391.html' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'guide',
    slug: 'gan-hay-xa',
    title: 'Gần phố hay đi huyện?',
    kicker: 'Cẩm nang',
    summary: 'Nửa ngày trong thành phố; cả ngày mới đi ATK, hang, suối. Không nhét ba huyện một buổi.',
    pillars: ['du-lich', 'dia-diem'],
    cover: '/banner-ho-nui-coc.png',
    sections: [
      {
        heading: 'Trong phố — nửa ngày',
        body: 'Bảo tàng (1 Đội Cấn), Chùa Hang (~3 km bắc), Đại đội 915 (Gia Sàng). Xe máy / xe buýt nội thị. Kiểm tra giờ trước khi đi.',
        placeSlugs: ['bao-tang-van-hoa-cac-dan-toc', 'chua-hang', 'dai-doi-915'],
      },
      {
        heading: 'Gần phố — nửa ngày đến cả ngày',
        body: 'Tân Cương và Làng Thái Hải (Thịnh Đức) gần thành phố hơn hang / ATK. Hồ Núi Cốc ~15 km tây nam — nên đi sớm nếu muốn cả khu.',
        placeSlugs: ['tan-cuong', 'lang-thai-hai', 'ho-nui-coc'],
      },
      {
        heading: 'Huyện — cả ngày, xuất phát sớm',
        body: 'ATK Định Hóa. Hang Phượng Hoàng / Mỏ Gà (Võ Nhai, ~45 km). Suối Kẹm + chè La Bằng (~35 km). Di tích 27/7 (Đại Từ) có thể ghé cùng hồ / suối — không nhét ATK vào cùng buổi nếu xuất phát muộn.',
        placeSlugs: ['atk-dinh-hoa', 'hang-phuong-hoang', 'suoi-kem-la-bang', 'ditich-27-7'],
      },
      {
        heading: 'Đền Đuổm trên đường',
        body: 'Khoảng 24 km, sát QL3, Phú Lương. Ghé khi đi / về hướng bắc — không phải điểm “check-in 5 phút” nếu muốn vào đền.',
        placeSlugs: ['den-duom'],
      },
    ],
    sources: [SRC_TOP12],
  },
  {
    kind: 'guide',
    slug: 'sinh-vien-moi',
    title: 'Sinh viên mới đến Thái Nguyên',
    kicker: 'Cẩm nang',
    summary: 'Hiểu phố trước: bảo tàng, chè, tin việc / phòng đã duyệt. Không phải hướng dẫn nhập học.',
    pillars: ['dia-diem', 'du-lich', 'van-hoa'],
    cover: '/discover/bao-tang.jpg',
    sections: [
      {
        heading: 'Một buổi trong phố',
        body: 'Bảo tàng Văn hóa các dân tộc (1 Đội Cấn) gần nhiều trường. Chùa Hang hoặc Đại đội 915 nếu còn chiều. Hỏi giờ tại chỗ.',
        placeSlugs: ['bao-tang-van-hoa-cac-dan-toc', 'chua-hang', 'dai-doi-915'],
      },
      {
        heading: 'Cuối tuần đầu tiên',
        body: 'Đồi chè Tân Cương — gần, không cần xe đi huyện. Hồ Núi Cốc nếu có bạn và cả ngày. Làng Thái Hải nếu muốn xem nhà sàn gần phố.',
        placeSlugs: ['tan-cuong', 'ho-nui-coc', 'lang-thai-hai'],
      },
      {
        heading: 'Việc và phòng',
        body: 'Mục Việc và Phòng trọ chỉ hiện tin đã duyệt còn hạn. Phòng: giá liên hệ, không đặt cọc qua site. Không có phí sinh viên trên Thái Nguyên Life.',
      },
      {
        heading: 'Sự kiện',
        body: 'Lễ hội / ngày hội / thể thao chỉ hiện khi tin còn hạn. Đừng tin lịch “hàng chục sự kiện đang diễn ra” trên site bên ngoài nếu mục đã hết ngày.',
      },
    ],
    sources: [
      { label: 'My Thái Nguyên — bảo tàng', url: 'https://mythainguyen.vn/vi/baotangvanhoacacdantoc' },
      SRC_TOP12,
    ],
  },
  {
    kind: 'guide',
    slug: 'mua-nao',
    title: 'Mùa nào đi đâu ở Thái Nguyên?',
    kicker: 'Cẩm nang',
    summary: 'Gợi ý theo loại điểm — không hẹn “tháng đẹp nhất”. Lễ hội chỉ khi tin còn hạn.',
    pillars: ['du-lich', 'van-hoa'],
    cover: '/discover/suoi-kem.jpg',
    sections: [
      {
        heading: 'Cả năm — đồi chè và phố',
        body: 'Tân Cương, bảo tàng, Chùa Hang, Đại đội 915 đi được quanh năm. Mùa lá xanh dễ chụp hơn; không hẹn hoàng hôn “đẹp nhất”.',
        placeSlugs: ['tan-cuong', 'bao-tang-van-hoa-cac-dan-toc', 'chua-hang'],
      },
      {
        heading: 'Nắng nóng — suối / hồ',
        body: 'Cổng du lịch nhắc Suối Kẹm mùa hè người ta tắm — tự đánh giá an toàn, site không có cứu hộ. Hồ Núi Cốc: hỏi vé / thuyền tại chỗ.',
        placeSlugs: ['suoi-kem-la-bang', 'ho-nui-coc'],
      },
      {
        heading: 'Tháng Bảy — về nguồn 27/7',
        body: 'Di tích 27/7 (Đại Từ) gắn ngày Thương binh — Liệt sĩ. Đoàn về nguồn thường tăng vào tháng Bảy — hỏi tại di tích, không đặt qua site.',
        placeSlugs: ['ditich-27-7', 'atk-dinh-hoa', 'dai-doi-915'],
      },
      {
        heading: 'Mùa lễ hội trà',
        body: 'Festival Trà và ngày hội ẩm thực / thưởng trà chỉ hiện trên mục Sự kiện khi tin đã duyệt còn hạn. Hết ngày thì gỡ — không giữ lịch cũ.',
        placeSlugs: ['tan-cuong'],
      },
    ],
    sources: [
      {
        label: 'Cổng TTĐT Thái Nguyên — khu di tích 27/7',
        url: 'http://thainguyen.gov.vn/den-on-dap-nghia/khu-di-tich-lich-su-quoc-gia-27-7-dia-chi-do-lan-toa-gia-tri-lich-su-282309',
      },
      SRC_TOP12,
    ],
  },
];

export function pillarOf(id: string): (typeof PILLARS)[number] | undefined {
  return PILLARS.find((p) => p.id === id);
}

export function placesIn(pillar?: string): DiscoverPlace[] {
  if (!pillar) return PLACES;
  return PLACES.filter((p) => p.pillars.includes(pillar as DiscoverPillar));
}

export function guidesIn(pillar?: string): DiscoverGuide[] {
  if (!pillar) return GUIDES;
  return GUIDES.filter((g) => g.pillars.includes(pillar as DiscoverPillar));
}

export function featuredPlaces(): DiscoverPlace[] {
  return PLACES.filter((p) => p.featured);
}

export function featuredGuides(): DiscoverGuide[] {
  return GUIDES.filter((g) => g.featured);
}

export function getPlace(slug: string): DiscoverPlace | undefined {
  return PLACES.find((p) => p.slug === slug);
}

export function getGuide(slug: string): DiscoverGuide | undefined {
  return GUIDES.find((g) => g.slug === slug);
}

export function placeTag(p: DiscoverPlace): string {
  return p.tag || (p.pillars.includes('am-thuc') ? 'Ẩm thực' : p.pillars.includes('van-hoa') ? 'Văn hóa' : 'Địa điểm');
}

export function placeTagTone(p: DiscoverPlace): string {
  const t = placeTag(p);
  if (t === 'Thiên nhiên') return 'nature';
  if (t === 'Ẩm thực') return 'food';
  if (t === 'Văn hóa') return 'culture';
  if (t === 'Lịch sử') return 'history';
  return 'place';
}

export function guideTag(g: DiscoverGuide): string {
  const first = g.pillars[0];
  return PILLARS.find((p) => p.id === first)?.label || 'Cẩm nang';
}

export function placeArea(p: DiscoverPlace): string {
  if (p.area) return p.area;
  return p.placeText.split(/[—–,]/)[0]?.trim() || 'Thái Nguyên';
}

export function placeHref(slug: string): string {
  return `/kham-pha/dia-diem/${slug}`;
}

export function guideHref(slug: string): string {
  return `/kham-pha/cam-nang/${slug}`;
}

export function pillarHref(id: DiscoverPillar): string {
  return `/kham-pha/${id}`;
}

export function matchEventsForPlace(place: DiscoverPlace, events: LocalListing[]): LocalListing[] {
  const hints = place.eventHints.map((h) => h.toLowerCase());
  return events.filter((e) => {
    const blob = `${e.title} ${e.summary ?? ''} ${e.placeText ?? ''}`.toLowerCase();
    return hints.some((h) => blob.includes(h));
  }).slice(0, 4);
}

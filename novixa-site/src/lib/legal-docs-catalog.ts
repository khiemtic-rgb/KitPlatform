export type LegalDocType = 'luat' | 'nghi-dinh' | 'thong-tu';

export type LegalDocStatus = 'hieu-luc' | 'sua-doi' | 'theo-doi';

export type LegalCategoryId =
  | 'luat-cot-loi'
  | 'duoc-my-pham'
  | 'kbkb-bhyt'
  | 'trang-thiet-bi'
  | 'dau-thau'
  | 'xu-phat';

export type LegalCategory = {
  id: LegalCategoryId;
  title: string;
  description: string;
};

export type LegalDoc = {
  slug: string;
  number: string;
  title: string;
  shortTitle: string;
  type: LegalDocType;
  categoryId: LegalCategoryId;
  issuedDate: string;
  effectiveDate?: string;
  status: LegalDocStatus;
  agency: string;
  /** Tóm tắt ngắn trên thẻ hub */
  summary: string;
  /** Tóm tắt đủ ý chính trên trang chi tiết */
  overview: string[];
  /** Liên quan nhà thuốc bán lẻ / chuỗi */
  pharmacyRelevant: boolean;
  tags: string[];
  relatedSlugs: string[];
  /** Link nguồn VBPL (trang văn bản hoặc tìm theo số hiệu) */
  sourceUrl: string;
  note?: string;
};

export const LEGAL_TYPE_LABELS: Record<LegalDocType, string> = {
  luat: 'Luật',
  'nghi-dinh': 'Nghị định',
  'thong-tu': 'Thông tư',
};

export const LEGAL_STATUS_LABELS: Record<LegalDocStatus, string> = {
  'hieu-luc': 'Còn hiệu lực',
  'sua-doi': 'Đã được sửa đổi / bổ sung',
  'theo-doi': 'Đang theo dõi cập nhật',
};

/** Tìm trên CSDL quốc gia VBPL theo số hiệu */
function vbplSearch(number: string): string {
  return `https://vbpl.vn/TW/Pages/vbpq-timkiem.aspx?type=0&s=${encodeURIComponent(number)}`;
}

export const LEGAL_CATEGORIES: LegalCategory[] = [
  {
    id: 'luat-cot-loi',
    title: 'Luật cốt lõi',
    description: 'Văn bản cấp Luật — khung pháp lý cao nhất về y tế, dược, BHYT và an toàn thực phẩm.',
  },
  {
    id: 'duoc-my-pham',
    title: 'Dược & Mỹ phẩm',
    description: 'Kinh doanh – đăng ký thuốc, hệ thống GPs (GPP/GDP/GSP…) và quản lý mỹ phẩm.',
  },
  {
    id: 'kbkb-bhyt',
    title: 'Khám chữa bệnh & BHYT',
    description: 'Hướng dẫn Luật KBKB, hành nghề, kê đơn ngoại trú và thanh toán BHYT.',
  },
  {
    id: 'trang-thiet-bi',
    title: 'Trang thiết bị y tế',
    description: 'Phân loại, lưu hành, mua bán và quản lý trang thiết bị y tế.',
  },
  {
    id: 'dau-thau',
    title: 'Đấu thầu thuốc & vật tư',
    description: 'Luật Đấu thầu và quy định mua sắm thuốc, hóa chất, vật tư tại cơ sở y tế.',
  },
  {
    id: 'xu-phat',
    title: 'Xử phạt vi phạm',
    description: 'Xử phạt hành chính trong lĩnh vực y tế, dược, mỹ phẩm và trang thiết bị.',
  },
];

export const LEGAL_DOCS: LegalDoc[] = [
  // ——— 1. Luật cốt lõi ———
  {
    slug: 'luat-kham-benh-chua-benh-15-2023',
    number: '15/2023/QH15',
    title: 'Luật Khám bệnh, chữa bệnh số 15/2023/QH15',
    shortTitle: 'Luật Khám bệnh, chữa bệnh 2023',
    type: 'luat',
    categoryId: 'luat-cot-loi',
    issuedDate: '2023-01-09',
    effectiveDate: '2024-01-01',
    status: 'hieu-luc',
    agency: 'Quốc hội',
    summary:
      'Quy định quyền–nghĩa vụ của người bệnh và người hành nghề; điều kiện hoạt động cơ sở y tế. Có hiệu lực từ 01/01/2024.',
    overview: [
      'Thay thế khung pháp lý khám bệnh, chữa bệnh trước đây; áp dụng từ 01/01/2024.',
      'Quy định quyền và nghĩa vụ của người bệnh, người hành nghề khám bệnh, chữa bệnh.',
      'Điều kiện cấp phép hoạt động cơ sở y tế, quản lý chất lượng và an toàn người bệnh.',
      'Làm cơ sở cho Nghị định 96/2023/NĐ-CP và Thông tư 32/2023/TT-BYT hướng dẫn chi tiết.',
    ],
    pharmacyRelevant: true,
    tags: ['kbkb', 'hanh-nghe', 'co-so-y-te'],
    relatedSlugs: ['nghi-dinh-96-2023', 'thong-tu-32-2023'],
    sourceUrl: vbplSearch('15/2023/QH15'),
  },
  {
    slug: 'luat-duoc-105-2016',
    number: '105/2016/QH13',
    title: 'Luật Dược số 105/2016/QH13',
    shortTitle: 'Luật Dược 2016',
    type: 'luat',
    categoryId: 'luat-cot-loi',
    issuedDate: '2016-04-06',
    effectiveDate: '2017-01-01',
    status: 'theo-doi',
    agency: 'Quốc hội',
    summary:
      'Khung pháp lý gốc về chính sách dược, kinh doanh dược, đăng ký–lưu hành–thu hồi thuốc và quản lý chất lượng.',
    overview: [
      'Quy định chính sách Nhà nước về dược; hành nghề và kinh doanh dược.',
      'Đăng ký, lưu hành, thu hồi thuốc và nguyên liệu làm thuốc; quản lý chất lượng và giá thuốc.',
      'Áp dụng trực tiếp cho nhà thuốc, cơ sở phân phối, sản xuất và cơ sở khám chữa bệnh có hoạt động dược.',
      'Đang có định hướng sửa đổi, bổ sung — cần theo dõi văn bản mới thay thế/bổ sung.',
    ],
    pharmacyRelevant: true,
    tags: ['duoc', 'nha-thuoc', 'kinh-doanh'],
    relatedSlugs: ['nghi-dinh-54-2017', 'nghi-dinh-155-2018', 'thong-tu-02-2018-gpp'],
    sourceUrl: 'https://vbpl.vn/boyte/Pages/vbpq-thuoctinh.aspx?ItemID=101886',
    note: 'Luật sửa đổi, bổ sung một số điều của Luật Dược đang trong quá trình hoàn thiện/cập nhật.',
  },
  {
    slug: 'luat-bao-hiem-y-te-25-2008',
    number: '25/2008/QH12',
    title: 'Luật Bảo hiểm y tế số 25/2008/QH12',
    shortTitle: 'Luật Bảo hiểm y tế',
    type: 'luat',
    categoryId: 'luat-cot-loi',
    issuedDate: '2008-11-14',
    effectiveDate: '2009-07-01',
    status: 'sua-doi',
    agency: 'Quốc hội',
    summary:
      'Quy định về BHYT, quyền–nghĩa vụ các bên tham gia; đã được sửa đổi, bổ sung năm 2014 và các nghị quyết liên quan.',
    overview: [
      'Khung pháp lý về tổ chức thực hiện bảo hiểm y tế và phạm vi hưởng của người tham gia.',
      'Đã được sửa đổi, bổ sung (2014) và hướng dẫn bởi các nghị định, thông tư về thanh toán thuốc/BHYT.',
      'Liên quan trực tiếp tới danh mục thuốc BHYT và điều kiện thanh toán tại cơ sở y tế.',
    ],
    pharmacyRelevant: true,
    tags: ['bhyt', 'thanh-toan'],
    relatedSlugs: ['nghi-dinh-75-2023', 'thong-tu-20-2022'],
    sourceUrl: vbplSearch('25/2008/QH12'),
    note: 'Sửa đổi, bổ sung năm 2014 và các nghị quyết liên quan.',
  },
  {
    slug: 'luat-phong-chong-benh-truyen-nhiem-03-2007',
    number: '03/2007/QH12',
    title: 'Luật Phòng, chống bệnh truyền nhiễm số 03/2007/QH12',
    shortTitle: 'Luật Phòng, chống bệnh truyền nhiễm',
    type: 'luat',
    categoryId: 'luat-cot-loi',
    issuedDate: '2007-11-21',
    effectiveDate: '2008-07-01',
    status: 'hieu-luc',
    agency: 'Quốc hội',
    summary:
      'Quy định biện pháp phòng, chống bệnh truyền nhiễm; trách nhiệm cơ quan, tổ chức, cá nhân và cơ sở y tế.',
    overview: [
      'Phân loại bệnh truyền nhiễm; biện pháp giám sát, cách ly và ứng phó ổ dịch.',
      'Trách nhiệm của cơ sở y tế, chính quyền và người dân trong phòng chống dịch.',
      'Liên quan vận hành nhà thuốc/cơ sở y tế khi có dịch (cung ứng, hướng dẫn sử dụng thuốc…).',
    ],
    pharmacyRelevant: false,
    tags: ['dich-te', 'y-te-du-phong'],
    relatedSlugs: ['nghi-dinh-117-2020'],
    sourceUrl: vbplSearch('03/2007/QH12'),
  },
  {
    slug: 'luat-an-toan-thuc-pham-55-2010',
    number: '55/2010/QH12',
    title: 'Luật An toàn thực phẩm số 55/2010/QH12',
    shortTitle: 'Luật An toàn thực phẩm',
    type: 'luat',
    categoryId: 'luat-cot-loi',
    issuedDate: '2010-06-17',
    effectiveDate: '2011-07-01',
    status: 'hieu-luc',
    agency: 'Quốc hội',
    summary:
      'Quản lý an toàn thực phẩm, gồm thực phẩm chức năng / thực phẩm bảo vệ sức khỏe thường bán tại nhà thuốc.',
    overview: [
      'Điều kiện sản xuất, kinh doanh thực phẩm; trách nhiệm bảo đảm an toàn thực phẩm.',
      'Phạm vi gồm thực phẩm chức năng, thực phẩm bảo vệ sức khỏe — mặt hàng phổ biến tại nhà thuốc.',
      'Cơ sở bán lẻ cần nắm quy định ghi nhãn, nguồn gốc và điều kiện bảo quản tương ứng.',
    ],
    pharmacyRelevant: true,
    tags: ['thuc-pham', 'thuc-pham-chuc-nang'],
    relatedSlugs: ['thong-tu-06-2011-my-pham'],
    sourceUrl: vbplSearch('55/2010/QH12'),
  },

  // ——— 2. Dược & Mỹ phẩm ———
  {
    slug: 'nghi-dinh-54-2017',
    number: '54/2017/NĐ-CP',
    title: 'Nghị định 54/2017/NĐ-CP hướng dẫn Luật Dược',
    shortTitle: 'NĐ 54/2017 — Hướng dẫn Luật Dược',
    type: 'nghi-dinh',
    categoryId: 'duoc-my-pham',
    issuedDate: '2017-05-08',
    effectiveDate: '2017-07-01',
    status: 'sua-doi',
    agency: 'Chính phủ',
    summary:
      'Hướng dẫn chi tiết Luật Dược: điều kiện kinh doanh thuốc, xuất nhập khẩu, quản lý giá và các biện pháp thi hành.',
    overview: [
      'Quy định điều kiện kinh doanh dược, hồ sơ–thủ tục cấp giấy chứng nhận đủ điều kiện.',
      'Xuất nhập khẩu thuốc, nguyên liệu; quản lý giá thuốc và biện pháp thi hành Luật Dược.',
      'Văn bản then chốt với nhà thuốc và cơ sở phân phối; được bổ sung bởi NĐ 155/2018/NĐ-CP.',
    ],
    pharmacyRelevant: true,
    tags: ['kinh-doanh', 'gia-thuoc', 'nha-thuoc'],
    relatedSlugs: ['luat-duoc-105-2016', 'nghi-dinh-155-2018'],
    sourceUrl: vbplSearch('54/2017/NĐ-CP'),
  },
  {
    slug: 'nghi-dinh-155-2018',
    number: '155/2018/NĐ-CP',
    title: 'Nghị định 155/2018/NĐ-CP sửa đổi hướng dẫn Luật Dược',
    shortTitle: 'NĐ 155/2018 — Sửa đổi NĐ hướng dẫn Luật Dược',
    type: 'nghi-dinh',
    categoryId: 'duoc-my-pham',
    issuedDate: '2018-11-12',
    status: 'hieu-luc',
    agency: 'Chính phủ',
    summary:
      'Sửa đổi, bổ sung một số quy định hướng dẫn thi hành Luật Dược (đi kèm NĐ 54/2017/NĐ-CP).',
    overview: [
      'Điều chỉnh, bổ sung một số điều kiện và thủ tục liên quan kinh doanh, quản lý thuốc.',
      'Cần đọc cùng Nghị định 54/2017/NĐ-CP để nắm quy định hiện hành.',
    ],
    pharmacyRelevant: true,
    tags: ['kinh-doanh', 'sua-doi'],
    relatedSlugs: ['nghi-dinh-54-2017', 'luat-duoc-105-2016'],
    sourceUrl: vbplSearch('155/2018/NĐ-CP'),
  },
  {
    slug: 'thong-tu-08-2022',
    number: '08/2022/TT-BYT',
    title: 'Thông tư 08/2022/TT-BYT về đăng ký lưu hành thuốc',
    shortTitle: 'TT 08/2022 — Đăng ký lưu hành thuốc',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2022-09-05',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Quy định việc đăng ký lưu hành thuốc, nguyên liệu làm thuốc.',
    overview: [
      'Hồ sơ, trình tự đăng ký lưu hành thuốc và nguyên liệu làm thuốc.',
      'Liên quan nhà sản xuất, đăng ký chủ sở hữu và chuỗi cung ứng đến nhà thuốc.',
      'Ảnh hưởng danh mục thuốc được phép lưu hành trên thị trường.',
    ],
    pharmacyRelevant: true,
    tags: ['dang-ky', 'luu-hanh'],
    relatedSlugs: ['luat-duoc-105-2016', 'thong-tu-01-2018'],
    sourceUrl: vbplSearch('08/2022/TT-BYT'),
  },
  {
    slug: 'thong-tu-01-2018',
    number: '01/2018/TT-BYT',
    title: 'Thông tư 01/2018/TT-BYT về ghi nhãn thuốc',
    shortTitle: 'TT 01/2018 — Ghi nhãn thuốc',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2018-01-22',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary:
      'Quy định ghi nhãn thuốc, nguyên liệu làm thuốc và tờ hướng dẫn sử dụng thuốc.',
    overview: [
      'Nội dung bắt buộc trên nhãn và tờ hướng dẫn sử dụng.',
      'Cơ sở bán lẻ cần kiểm soát thuốc đúng nhãn, hạn dùng và thông tin hướng dẫn.',
      'Liên quan tuân thủ khi nhận hàng, trưng bày và tư vấn tại quầy.',
    ],
    pharmacyRelevant: true,
    tags: ['nhan', 'huong-dan-su-dung'],
    relatedSlugs: ['thong-tu-08-2022', 'thong-tu-02-2018-gpp'],
    sourceUrl: vbplSearch('01/2018/TT-BYT'),
  },
  {
    slug: 'thong-tu-35-2018-gmp',
    number: '35/2018/TT-BYT',
    title: 'Thông tư 35/2018/TT-BYT — Thực hành tốt sản xuất thuốc (GMP)',
    shortTitle: 'TT 35/2018 — GMP',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2018-11-22',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Thực hành tốt sản xuất thuốc, nguyên liệu làm thuốc (GMP).',
    overview: [
      'Tiêu chuẩn GMP cho cơ sở sản xuất thuốc và nguyên liệu.',
      'Ảnh hưởng chất lượng nguồn cung; nhà thuốc nên ưu tiên thuốc từ cơ sở đạt chuẩn.',
    ],
    pharmacyRelevant: false,
    tags: ['gmp', 'san-xuat'],
    relatedSlugs: ['thong-tu-36-2018-gsp', 'thong-tu-03-2018-gdp'],
    sourceUrl: vbplSearch('35/2018/TT-BYT'),
  },
  {
    slug: 'thong-tu-36-2018-gsp',
    number: '36/2018/TT-BYT',
    title: 'Thông tư 36/2018/TT-BYT — Thực hành tốt bảo quản thuốc (GSP)',
    shortTitle: 'TT 36/2018 — GSP',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2018-11-22',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Thực hành tốt bảo quản thuốc, nguyên liệu làm thuốc (GSP).',
    overview: [
      'Yêu cầu về kho, nhiệt độ, độ ẩm, theo dõi điều kiện bảo quản.',
      'Áp dụng chuỗi cung ứng; nhà thuốc cần bảo quản đúng điều kiện ghi trên nhãn.',
    ],
    pharmacyRelevant: true,
    tags: ['gsp', 'bao-quan', 'kho'],
    relatedSlugs: ['thong-tu-02-2018-gpp', 'thong-tu-03-2018-gdp'],
    sourceUrl: vbplSearch('36/2018/TT-BYT'),
  },
  {
    slug: 'thong-tu-02-2018-gpp',
    number: '02/2018/TT-BYT',
    title: 'Thông tư 02/2018/TT-BYT — Thực hành tốt cơ sở bán lẻ thuốc (GPP)',
    shortTitle: 'TT 02/2018 — GPP',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2018-02-08',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary:
      'Thực hành tốt cơ sở bán lẻ thuốc (GPP) — chuẩn vận hành then chốt cho nhà thuốc.',
    overview: [
      'Nguyên tắc GPP: nhân sự, cơ sở vật chất, bảo quản, tư vấn và bán thuốc.',
      'Yêu cầu về bán thuốc kê đơn, theo dõi hạn dùng, ghi chép và kiểm soát chất lượng tại quầy.',
      'Văn bản bắt buộc nắm vững với chủ nhà thuốc và dược sĩ phụ trách chuyên môn.',
    ],
    pharmacyRelevant: true,
    tags: ['gpp', 'nha-thuoc', 'ban-le'],
    relatedSlugs: ['luat-duoc-105-2016', 'thong-tu-36-2018-gsp', 'thong-tu-04-2022'],
    sourceUrl: vbplSearch('02/2018/TT-BYT'),
  },
  {
    slug: 'thong-tu-03-2018-gdp',
    number: '03/2018/TT-BYT',
    title: 'Thông tư 03/2018/TT-BYT — Thực hành tốt phân phối thuốc (GDP)',
    shortTitle: 'TT 03/2018 — GDP',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2018-02-09',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Thực hành tốt phân phối thuốc, nguyên liệu làm thuốc (GDP).',
    overview: [
      'Tiêu chuẩn phân phối, vận chuyển và truy xuất nguồn gốc trong chuỗi cung ứng.',
      'Nhà thuốc nên chọn nhà phân phối tuân thủ GDP để giảm rủi ro chất lượng.',
    ],
    pharmacyRelevant: true,
    tags: ['gdp', 'phan-phoi'],
    relatedSlugs: ['thong-tu-02-2018-gpp', 'thong-tu-36-2018-gsp'],
    sourceUrl: vbplSearch('03/2018/TT-BYT'),
  },
  {
    slug: 'thong-tu-04-2018-glp',
    number: '04/2018/TT-BYT',
    title: 'Thông tư 04/2018/TT-BYT — Thực hành tốt phòng thí nghiệm (GLP)',
    shortTitle: 'TT 04/2018 — GLP',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2018-02-09',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Thực hành tốt phòng thí nghiệm nhằm kiểm tra chất lượng thuốc (GLP).',
    overview: [
      'Tiêu chuẩn hoạt động phòng thí nghiệm kiểm nghiệm thuốc.',
      'Chủ yếu áp dụng đơn vị kiểm nghiệm; hỗ trợ chuỗi đảm bảo chất lượng đến điểm bán.',
    ],
    pharmacyRelevant: false,
    tags: ['glp', 'kiem-nghiem'],
    relatedSlugs: ['thong-tu-35-2018-gmp'],
    sourceUrl: vbplSearch('04/2018/TT-BYT'),
  },
  {
    slug: 'thong-tu-06-2011-my-pham',
    number: '06/2011/TT-BYT',
    title: 'Thông tư 06/2011/TT-BYT về quản lý mỹ phẩm',
    shortTitle: 'TT 06/2011 — Quản lý mỹ phẩm',
    type: 'thong-tu',
    categoryId: 'duoc-my-pham',
    issuedDate: '2011-01-25',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Quản lý mỹ phẩm: công bố sản phẩm, chất lượng, ghi nhãn và lưu hành.',
    overview: [
      'Công bố sản phẩm mỹ phẩm, yêu cầu về hồ sơ và trách nhiệm tổ chức, cá nhân.',
      'Ghi nhãn, chất lượng và điều kiện lưu hành mỹ phẩm trên thị trường.',
      'Liên quan nhà thuốc/cửa hàng có kinh doanh mỹ phẩm, dược mỹ phẩm.',
    ],
    pharmacyRelevant: true,
    tags: ['my-pham', 'cong-bo'],
    relatedSlugs: ['luat-an-toan-thuc-pham-55-2010', 'nghi-dinh-117-2020'],
    sourceUrl: vbplSearch('06/2011/TT-BYT'),
  },

  // ——— 3. KBKB & BHYT ———
  {
    slug: 'nghi-dinh-96-2023',
    number: '96/2023/NĐ-CP',
    title: 'Nghị định 96/2023/NĐ-CP hướng dẫn Luật Khám bệnh, chữa bệnh',
    shortTitle: 'NĐ 96/2023 — Hướng dẫn Luật KBKB',
    type: 'nghi-dinh',
    categoryId: 'kbkb-bhyt',
    issuedDate: '2023-12-30',
    effectiveDate: '2024-01-01',
    status: 'hieu-luc',
    agency: 'Chính phủ',
    summary:
      'Quy định chi tiết Luật KBKB: cấp giấy phép hành nghề, giấy phép hoạt động cơ sở y tế…',
    overview: [
      'Hướng dẫn cấp phép hành nghề và giấy phép hoạt động cơ sở khám bệnh, chữa bệnh.',
      'Chi tiết hóa điều kiện, hồ sơ, thẩm quyền theo Luật 15/2023/QH15.',
      'Liên quan phòng khám, cơ sở y tế; nhà thuốc cần nắm khi phối hợp chuỗi dịch vụ.',
    ],
    pharmacyRelevant: true,
    tags: ['kbkb', 'giay-phep'],
    relatedSlugs: ['luat-kham-benh-chua-benh-15-2023', 'thong-tu-32-2023'],
    sourceUrl: vbplSearch('96/2023/NĐ-CP'),
  },
  {
    slug: 'thong-tu-32-2023',
    number: '32/2023/TT-BYT',
    title: 'Thông tư 32/2023/TT-BYT hướng dẫn Luật Khám bệnh, chữa bệnh',
    shortTitle: 'TT 32/2023 — Hướng dẫn Luật KBKB',
    type: 'thong-tu',
    categoryId: 'kbkb-bhyt',
    issuedDate: '2023-12-31',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary:
      'Hướng dẫn Luật KBKB: tiêu chuẩn sức khỏe, hồ sơ bệnh án, quy trình chuyên môn…',
    overview: [
      'Quy định chuyên môn: hồ sơ bệnh án, tiêu chuẩn sức khỏe người hành nghề.',
      'Quy trình chuyên môn và yêu cầu quản lý tại cơ sở khám chữa bệnh.',
      'Bổ sung chi tiết cho Luật 15/2023 và NĐ 96/2023.',
    ],
    pharmacyRelevant: false,
    tags: ['kbkb', 'ho-so-benh-an'],
    relatedSlugs: ['luat-kham-benh-chua-benh-15-2023', 'nghi-dinh-96-2023'],
    sourceUrl: vbplSearch('32/2023/TT-BYT'),
  },
  {
    slug: 'nghi-dinh-75-2023',
    number: '75/2023/NĐ-CP',
    title: 'Nghị định 75/2023/NĐ-CP sửa đổi hướng dẫn Luật BHYT',
    shortTitle: 'NĐ 75/2023 — Sửa đổi hướng dẫn BHYT',
    type: 'nghi-dinh',
    categoryId: 'kbkb-bhyt',
    issuedDate: '2023-10-19',
    status: 'hieu-luc',
    agency: 'Chính phủ',
    summary:
      'Sửa đổi, bổ sung một số điều của Nghị định 146/2018/NĐ-CP hướng dẫn Luật Bảo hiểm y tế.',
    overview: [
      'Cập nhật quy định hướng dẫn Luật BHYT liên quan quyền lợi và tổ chức thực hiện.',
      'Cần đọc cùng Luật BHYT và các thông tư thanh toán thuốc BHYT.',
    ],
    pharmacyRelevant: true,
    tags: ['bhyt', 'sua-doi'],
    relatedSlugs: ['luat-bao-hiem-y-te-25-2008', 'thong-tu-20-2022'],
    sourceUrl: vbplSearch('75/2023/NĐ-CP'),
  },
  {
    slug: 'thong-tu-04-2022',
    number: '04/2022/TT-BYT',
    title: 'Thông tư 04/2022/TT-BYT về kê đơn thuốc ngoại trú',
    shortTitle: 'TT 04/2022 — Kê đơn thuốc ngoại trú',
    type: 'thong-tu',
    categoryId: 'kbkb-bhyt',
    issuedDate: '2022-06-29',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary:
      'Quy định kê đơn thuốc hóa dược, sinh phẩm trong điều trị ngoại trú — liên quan bán thuốc kê đơn tại nhà thuốc.',
    overview: [
      'Hình thức, nội dung đơn thuốc và trách nhiệm người kê đơn trong điều trị ngoại trú.',
      'Nhà thuốc phải bán thuốc kê đơn đúng quy định, kiểm tra đơn hợp lệ.',
      'Gắn chặt với tuân thủ GPP và xử phạt khi bán sai quy định.',
    ],
    pharmacyRelevant: true,
    tags: ['ke-don', 'ngoai-tru', 'nha-thuoc'],
    relatedSlugs: ['thong-tu-02-2018-gpp', 'nghi-dinh-117-2020'],
    sourceUrl: vbplSearch('04/2022/TT-BYT'),
  },
  {
    slug: 'thong-tu-20-2022',
    number: '20/2022/TT-BYT',
    title: 'Thông tư 20/2022/TT-BYT danh mục thuốc thanh toán BHYT',
    shortTitle: 'TT 20/2022 — Danh mục thuốc BHYT',
    type: 'thong-tu',
    categoryId: 'kbkb-bhyt',
    issuedDate: '2022-12-31',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary:
      'Danh mục và tỷ lệ, điều kiện thanh toán thuốc hóa dược, sinh phẩm, thuốc phóng xạ thuộc phạm vi BHYT.',
    overview: [
      'Danh mục thuốc được quỹ BHYT thanh toán và điều kiện, tỷ lệ thanh toán.',
      'Quan trọng với nhà thuốc/cơ sở ký hợp đồng BHYT và tư vấn chi phí cho người bệnh.',
    ],
    pharmacyRelevant: true,
    tags: ['bhyt', 'danh-muc-thuoc'],
    relatedSlugs: ['luat-bao-hiem-y-te-25-2008', 'nghi-dinh-75-2023'],
    sourceUrl: vbplSearch('20/2022/TT-BYT'),
  },

  // ——— 4. Trang thiết bị ———
  {
    slug: 'nghi-dinh-98-2021',
    number: '98/2021/NĐ-CP',
    title: 'Nghị định 98/2021/NĐ-CP về quản lý trang thiết bị y tế',
    shortTitle: 'NĐ 98/2021 — Trang thiết bị y tế',
    type: 'nghi-dinh',
    categoryId: 'trang-thiet-bi',
    issuedDate: '2021-11-08',
    status: 'sua-doi',
    agency: 'Chính phủ',
    summary:
      'Phân loại, cấp phép lưu hành, mua bán và quản lý giá trang thiết bị y tế.',
    overview: [
      'Phân loại trang thiết bị y tế; điều kiện sản xuất, kinh doanh, nhập khẩu.',
      'Cấp phép lưu hành, quản lý chất lượng và giá.',
      'Được sửa đổi bởi Nghị định 07/2023/NĐ-CP nhằm tháo gỡ nút thắt cấp phép/nhập khẩu.',
    ],
    pharmacyRelevant: true,
    tags: ['trang-thiet-bi', 'luu-hanh'],
    relatedSlugs: ['nghi-dinh-07-2023', 'thong-tu-05-2022'],
    sourceUrl: vbplSearch('98/2021/NĐ-CP'),
  },
  {
    slug: 'nghi-dinh-07-2023',
    number: '07/2023/NĐ-CP',
    title: 'Nghị định 07/2023/NĐ-CP sửa đổi NĐ 98/2021/NĐ-CP',
    shortTitle: 'NĐ 07/2023 — Sửa đổi quản lý TTB Y tế',
    type: 'nghi-dinh',
    categoryId: 'trang-thiet-bi',
    issuedDate: '2023-03-03',
    status: 'hieu-luc',
    agency: 'Chính phủ',
    summary:
      'Sửa đổi, bổ sung NĐ 98/2021 nhằm tháo gỡ nút thắt nhập khẩu và cấp phép trang thiết bị y tế.',
    overview: [
      'Điều chỉnh thủ tục, điều kiện liên quan nhập khẩu và cấp phép lưu hành.',
      'Cần đọc cùng Nghị định 98/2021/NĐ-CP và Thông tư 05/2022/TT-BYT.',
    ],
    pharmacyRelevant: true,
    tags: ['trang-thiet-bi', 'nhap-khau'],
    relatedSlugs: ['nghi-dinh-98-2021', 'thong-tu-05-2022'],
    sourceUrl: vbplSearch('07/2023/NĐ-CP'),
  },
  {
    slug: 'thong-tu-05-2022',
    number: '05/2022/TT-BYT',
    title: 'Thông tư 05/2022/TT-BYT hướng dẫn NĐ 98/2021/NĐ-CP',
    shortTitle: 'TT 05/2022 — Hướng dẫn TTB Y tế',
    type: 'thong-tu',
    categoryId: 'trang-thiet-bi',
    issuedDate: '2022-08-01',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Quy định chi tiết thi hành một số điều của Nghị định 98/2021/NĐ-CP.',
    overview: [
      'Chi tiết hồ sơ, quy trình và yêu cầu kỹ thuật theo NĐ quản lý trang thiết bị y tế.',
      'Áp dụng tổ chức sản xuất, kinh doanh, sử dụng trang thiết bị y tế.',
    ],
    pharmacyRelevant: false,
    tags: ['trang-thiet-bi'],
    relatedSlugs: ['nghi-dinh-98-2021', 'nghi-dinh-07-2023'],
    sourceUrl: vbplSearch('05/2022/TT-BYT'),
  },

  // ——— 5. Đấu thầu ———
  {
    slug: 'luat-dau-thau-22-2023',
    number: '22/2023/QH15',
    title: 'Luật Đấu thầu số 22/2023/QH15',
    shortTitle: 'Luật Đấu thầu 2023',
    type: 'luat',
    categoryId: 'dau-thau',
    issuedDate: '2023-06-23',
    effectiveDate: '2024-01-01',
    status: 'hieu-luc',
    agency: 'Quốc hội',
    summary:
      'Có chương, điều khoản ưu tiên mua sắm thuốc, hóa chất, vật tư xét nghiệm và thiết bị y tế. HL từ 01/01/2024.',
    overview: [
      'Khung pháp lý lựa chọn nhà thầu; hiệu lực từ 01/01/2024.',
      'Có quy định riêng liên quan mua sắm thuốc và vật tư y tế.',
      'Quan trọng với cơ sở y tế công lập và nhà cung cấp tham gia thầu thuốc.',
    ],
    pharmacyRelevant: false,
    tags: ['dau-thau', 'mua-sam'],
    relatedSlugs: ['nghi-dinh-24-2024', 'thong-tu-07-2024'],
    sourceUrl: vbplSearch('22/2023/QH15'),
  },
  {
    slug: 'nghi-dinh-24-2024',
    number: '24/2024/NĐ-CP',
    title: 'Nghị định 24/2024/NĐ-CP hướng dẫn Luật Đấu thầu',
    shortTitle: 'NĐ 24/2024 — Hướng dẫn Luật Đấu thầu',
    type: 'nghi-dinh',
    categoryId: 'dau-thau',
    issuedDate: '2024-02-27',
    status: 'hieu-luc',
    agency: 'Chính phủ',
    summary:
      'Hướng dẫn lựa chọn nhà thầu; quy định chi tiết mua sắm tập trung, mua sắm thuốc tại cơ sở y tế công lập.',
    overview: [
      'Chi tiết biện pháp thi hành Luật Đấu thầu về lựa chọn nhà thầu.',
      'Có nội dung mua sắm tập trung và mua sắm thuốc tại cơ sở y tế công lập.',
    ],
    pharmacyRelevant: false,
    tags: ['dau-thau', 'mua-sam-tap-trung'],
    relatedSlugs: ['luat-dau-thau-22-2023', 'thong-tu-07-2024'],
    sourceUrl: vbplSearch('24/2024/NĐ-CP'),
  },
  {
    slug: 'thong-tu-07-2024',
    number: '07/2024/TT-BYT',
    title: 'Thông tư 07/2024/TT-BYT về đấu thầu thuốc tại cơ sở y tế công lập',
    shortTitle: 'TT 07/2024 — Đấu thầu thuốc công lập',
    type: 'thong-tu',
    categoryId: 'dau-thau',
    issuedDate: '2024-05-17',
    status: 'hieu-luc',
    agency: 'Bộ Y tế',
    summary: 'Quy định đấu thầu thuốc tại các cơ sở y tế công lập.',
    overview: [
      'Quy trình, tiêu chí và trách nhiệm trong đấu thầu thuốc tại bệnh viện/cơ sở công lập.',
      'Đối tượng chính: cơ sở y tế công lập và nhà thầu cung ứng thuốc.',
    ],
    pharmacyRelevant: false,
    tags: ['dau-thau', 'cong-lap'],
    relatedSlugs: ['luat-dau-thau-22-2023', 'nghi-dinh-24-2024'],
    sourceUrl: vbplSearch('07/2024/TT-BYT'),
  },

  // ——— 6. Xử phạt ———
  {
    slug: 'nghi-dinh-117-2020',
    number: '117/2020/NĐ-CP',
    title: 'Nghị định 117/2020/NĐ-CP xử phạt VPHC lĩnh vực y tế',
    shortTitle: 'NĐ 117/2020 — Xử phạt VPHC y tế',
    type: 'nghi-dinh',
    categoryId: 'xu-phat',
    issuedDate: '2020-09-28',
    status: 'sua-doi',
    agency: 'Chính phủ',
    summary:
      'Xử phạt vi phạm hành chính trong y tế dự phòng, khám chữa bệnh, dược, mỹ phẩm, trang thiết bị…',
    overview: [
      'Hành vi vi phạm, mức phạt và thẩm quyền xử phạt trong lĩnh vực y tế.',
      'Gồm nhóm hành vi liên quan dược, nhà thuốc, mỹ phẩm — rủi ro tuân thủ vận hành.',
      'Được sửa đổi, bổ sung bởi Nghị định 124/2021/NĐ-CP (tăng mức phạt, thắt chặt quản lý).',
    ],
    pharmacyRelevant: true,
    tags: ['xu-phat', 'tuan-thu', 'nha-thuoc'],
    relatedSlugs: ['nghi-dinh-124-2021', 'thong-tu-02-2018-gpp'],
    sourceUrl: vbplSearch('117/2020/NĐ-CP'),
  },
  {
    slug: 'nghi-dinh-124-2021',
    number: '124/2021/NĐ-CP',
    title: 'Nghị định 124/2021/NĐ-CP sửa đổi NĐ 117/2020/NĐ-CP',
    shortTitle: 'NĐ 124/2021 — Sửa đổi xử phạt VPHC y tế',
    type: 'nghi-dinh',
    categoryId: 'xu-phat',
    issuedDate: '2021-12-28',
    status: 'hieu-luc',
    agency: 'Chính phủ',
    summary:
      'Sửa đổi, bổ sung NĐ 117/2020: tăng mức phạt và thắt chặt quản lý hành vi vi phạm.',
    overview: [
      'Cập nhật mức phạt và một số hành vi vi phạm trong lĩnh vực y tế.',
      'Nhà thuốc cần rà soát lại rủi ro tuân thủ theo mức phạt mới.',
      'Đọc cùng Nghị định 117/2020/NĐ-CP.',
    ],
    pharmacyRelevant: true,
    tags: ['xu-phat', 'sua-doi'],
    relatedSlugs: ['nghi-dinh-117-2020'],
    sourceUrl: vbplSearch('124/2021/NĐ-CP'),
  },
];

export function getLegalDocBySlug(slug: string | null | undefined): LegalDoc | undefined {
  if (!slug) return undefined;
  return LEGAL_DOCS.find((d) => d.slug === slug);
}

export function getLegalCategory(id: LegalCategoryId): LegalCategory | undefined {
  return LEGAL_CATEGORIES.find((c) => c.id === id);
}

export function getLegalDocsByCategory(categoryId: LegalCategoryId): LegalDoc[] {
  return LEGAL_DOCS.filter((d) => d.categoryId === categoryId);
}

export function getRelatedLegalDocs(doc: LegalDoc): LegalDoc[] {
  return doc.relatedSlugs
    .map((s) => getLegalDocBySlug(s))
    .filter((d): d is LegalDoc => Boolean(d));
}

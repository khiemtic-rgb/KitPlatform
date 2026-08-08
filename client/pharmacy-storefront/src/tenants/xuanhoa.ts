import type { PharmacyTenantConfig } from './types';

const APP = 'https://app.novixa.vn/?tenantCode=NT_XUANHOA';
const NOVIXA = 'https://novixa.vn';

/** Nhà thuốc Xuân Hòa — config white-label (đổi file / đăng ký host = NT mới). */
export const xuanhoa: PharmacyTenantConfig = {
  id: 'xuanhoa',
  slug: 'xuanhoa',
  tenantCode: 'NT_XUANHOA',
  hosts: ['xuanhoa.novixa.vn', 'xuanhoa.localhost', 'xuanhoa', '127.0.0.1', 'localhost'],

  brand: {
    name: 'Nhà thuốc Xuân Hòa',
    shortName: 'Xuân Hòa',
    logoText: 'XH',
    logoUrl: '/brand/xuanhoa-logo.png?v=2',
    primaryColor: '#004d40',
    accentColor: '#0a7a5c',
  },

  contact: {
    address: 'Số 30, đường Xuân Hòa, phường Phan Đình Phùng, tỉnh Thái Nguyên',
    hours: '07:00 - 22:00 (Tất cả các ngày)',
    phone: '0914.960.069',
    email: 'hientn85@gmail.com',
    notifyEmail: 'khiemtic@gmail.com',
    social: {
      facebook: 'https://www.facebook.com/nhathuocxuanhoa',
      zalo: 'https://zalo.me/0914960069',
    },
    branches: [
      {
        name: 'Chi nhánh 1 — Xuân Hòa',
        address: 'Số 30, đường Xuân Hòa, phường Phan Đình Phùng, tỉnh Thái Nguyên',
        hours: '07:00 - 22:00',
        phone: '0914.960.069',
      },
      {
        name: 'Chi nhánh 2 — Thống Nhất',
        address: 'Số 526, đường Thống Nhất, phường Phan Đình Phùng, tỉnh Thái Nguyên',
        hours: '07:00 - 22:00',
        phone: '0914.960.069',
      },
    ],
  },

  nav: [
    { key: 'home', label: 'Trang chủ', href: '/' },
    { key: 'about', label: 'Giới thiệu', href: '/gioi-thieu' },
    { key: 'products', label: 'Sản phẩm', href: '/san-pham' },
    { key: 'services', label: 'Dịch vụ', href: '/dich-vu' },
    { key: 'knowledge', label: 'Kiến thức sức khỏe', href: '/kien-thuc' },
    { key: 'contact', label: 'Liên hệ', href: '/lien-he' },
  ],

  hero: {
    headline: 'Nhà thuốc số đồng hành cùng sức khỏe gia đình',
    subhead:
      'Mua thuốc chính hãng, theo dõi sức khỏe, nhắc uống thuốc và kết nối trực tiếp với dược sĩ trong một ứng dụng.',
    imageUrl: '/brand/xuanhoa-hero.png?v=13',
    trustItems: [
      { icon: 'badge', label: 'Thuốc chính hãng', sublabel: 'Đạt chuẩn GPP' },
      { icon: 'advisor', label: 'Dược sĩ tận tâm', sublabel: 'Tư vấn 1-1' },
      { icon: 'scooter', label: 'Giao hàng nhanh', sublabel: 'Giao tận nơi' },
      { icon: 'privacy', label: 'Bảo mật thông tin', sublabel: 'An toàn tuyệt đối' },
    ],
    ctaPrimary: { label: 'Đặt thuốc ngay', href: APP },
    ctaSecondary: { label: 'Tải App Novixa', href: APP },
    heroStats: [
      {
        icon: 'pharmacist',
        value: '',
        label: 'Dược sĩ chuyên môn cao',
        sublabel: 'Tận tâm vì sức khỏe bạn',
      },
      {
        icon: 'orders',
        value: '15.000+',
        label: 'đơn thuốc đã phục vụ',
      },
      {
        icon: 'heart',
        value: '98%',
        label: 'khách hàng hài lòng',
      },
    ],
  },

  trustBand: {
    title: 'Được hơn 3.500 gia đình tin tưởng và lựa chọn',
    titleHighlight: '3.500',
    items: [
      { icon: 'badge', label: 'Thành lập từ', value: '2015' },
      { icon: 'pharmacist', label: 'Đội ngũ', value: '15+ dược sĩ' },
      { icon: 'customers', label: 'Phục vụ', value: '3.500+ khách hàng' },
      { icon: 'hours', label: 'Mở cửa', value: '07:00 - 22:00', sublabel: 'tất cả các ngày' },
    ],
  },

  appPromo: {
    title: 'Quản lý sức khỏe cả gia đình dễ dàng hơn với App Novixa',
    qrImageUrl: '/brand/xuanhoa-app-qr.png',
    appStoreUrl: APP,
    playStoreUrl: APP,
    appUrl: APP,
  },

  services: [
    {
      title: 'Tư vấn sức khỏe',
      description: 'Dược sĩ tư vấn 1-1 tận tâm qua chat hoặc tại quầy',
      icon: 'chat',
    },
    {
      title: 'Đặt thuốc nhanh',
      description: 'Gửi đơn thuốc hoặc đặt lại chỉ trong vài bước',
      icon: 'rx',
    },
    {
      title: 'Giao hàng tận nơi',
      description: 'Giao nhanh nội thành, theo dõi trạng thái đơn',
      icon: 'delivery',
    },
    {
      title: 'Theo dõi sức khỏe',
      description: 'Lưu chỉ số, nhắc uống thuốc theo liệu trình',
      icon: 'health',
    },
    {
      title: 'Hồ sơ gia đình',
      description: 'Quản lý thuốc và lịch sử mua cho cả nhà',
      icon: 'family',
    },
    {
      title: 'Tích điểm & ưu đãi',
      description: 'Tích lũy điểm thưởng, nhận ưu đãi theo mùa',
      icon: 'gift',
    },
  ],

  whyUs: [
    'Hơn 15 năm phục vụ cộng đồng',
    'Dược sĩ chuyên môn cao',
    'Sản phẩm đa dạng, chính hãng',
    'Giá cả minh bạch, hợp lý',
    'Không ngừng cải thiện dịch vụ',
  ],

  products: [
    {
      slug: 'eugica-green',
      name: 'Eugica Green',
      category: 'Hỗ trợ điều trị ho',
      price: '45.000đ',
      priceUnit: '/ Hộp',
      packInfo: 'Theo quy cách trên bao bì',
      imageUrl: '/brand/products/eugica-cough.jpg?v=2',
      href: '/san-pham/eugica-green',
      kind: 'medicine',
      badge: 'bestseller',
      shortDescription:
        'Sản phẩm hỗ trợ giảm triệu chứng ho, cảm lạnh thường gặp. Nên hỏi dược sĩ trước khi dùng.',
      gallery: ['/brand/products/eugica-cough.jpg?v=2'],
      specs: [
        { label: 'Công dụng', value: 'Hỗ trợ giảm ho, long đờm theo hướng dẫn trên bao bì' },
        { label: 'Quy cách', value: 'Theo bao bì sản phẩm' },
        { label: 'Dạng bào chế', value: 'Viên nang mềm / theo bao bì' },
        { label: 'Nhà sản xuất', value: 'Theo thông tin trên bao bì' },
        { label: 'Xuất xứ', value: 'Theo thông tin trên bao bì' },
      ],
      usage: [
        'Đọc kỹ hướng dẫn sử dụng trước khi dùng.',
        'Dùng đúng liều ghi trên bao bì hoặc theo hướng dẫn của dược sĩ / bác sĩ.',
        'Không dùng quá liều khuyến cáo.',
      ],
      notes: [
        'Không dùng cho người dị ứng thành phần của sản phẩm.',
        'Phụ nữ mang thai, cho con bú, trẻ em: hỏi dược sĩ trước khi dùng.',
        'Nếu triệu chứng kéo dài hoặc nặng hơn, cần khám bác sĩ.',
      ],
    },
    {
      slug: 'panadol-extra',
      name: 'Panadol Extra',
      category: 'Giảm đau - Hạ sốt',
      price: '32.000đ',
      priceUnit: '/ Hộp',
      packInfo: 'Theo quy cách trên bao bì',
      imageUrl: '/brand/products/panadol-pain.jpg?v=2',
      href: '/san-pham/panadol-extra',
      kind: 'medicine',
      badge: 'authentic',
      shortDescription:
        'Thuốc giảm đau, hạ sốt thường dùng. Nên dùng đúng liều và hỏi dược sĩ nếu đang dùng thuốc khác.',
      gallery: ['/brand/products/panadol-pain.jpg?v=2'],
      specs: [
        { label: 'Thành phần chính', value: 'Paracetamol + Cafein (theo bao bì)' },
        { label: 'Công dụng', value: 'Giảm đau, hạ sốt' },
        { label: 'Quy cách', value: 'Theo bao bì sản phẩm' },
        { label: 'Dạng bào chế', value: 'Viên nén' },
        { label: 'Nhà sản xuất', value: 'Theo thông tin trên bao bì' },
        { label: 'Xuất xứ', value: 'Theo thông tin trên bao bì' },
      ],
      usage: [
        'Người lớn và trẻ em từ độ tuổi cho phép trên bao bì: dùng đúng hướng dẫn.',
        'Không dùng quá liều paracetamol trong 24 giờ.',
        'Nuốt cả viên với nước; không nhai trừ khi hướng dẫn cho phép.',
      ],
      notes: [
        'Không dùng nếu dị ứng paracetamol hoặc thành phần khác của thuốc.',
        'Thận trọng với người bệnh gan, người uống rượu nhiều.',
        'Nếu đau/sốt kéo dài, cần tham khảo ý kiến bác sĩ.',
      ],
    },
    {
      slug: 'wellwoman',
      name: 'Vitabiotics Wellwoman',
      category: 'Vitamin tổng hợp',
      price: '350.000đ',
      priceUnit: '/ Hộp',
      packInfo: 'Thực phẩm bảo vệ sức khỏe',
      imageUrl: '/brand/products/wellwoman-vitamin.jpg?v=2',
      href: '/san-pham/wellwoman',
      kind: 'supplement',
      badge: 'authentic',
      shortDescription:
        'Vitamin và khoáng chất hỗ trợ sức khỏe phụ nữ. Không thay thế chế độ ăn uống đa dạng.',
      gallery: ['/brand/products/wellwoman-vitamin.jpg?v=2'],
      specs: [
        { label: 'Loại', value: 'Thực phẩm bảo vệ sức khỏe' },
        { label: 'Công dụng', value: 'Bổ sung vitamin, khoáng chất theo công thức sản phẩm' },
        { label: 'Quy cách', value: 'Theo bao bì sản phẩm' },
        { label: 'Nhà sản xuất', value: 'Theo thông tin trên bao bì' },
        { label: 'Xuất xứ', value: 'Theo thông tin trên bao bì' },
      ],
      usage: [
        'Dùng theo hướng dẫn trên bao bì hoặc tư vấn của dược sĩ.',
        'Nên dùng thường xuyên mỗi ngày để đạt hiệu quả hỗ trợ tốt hơn.',
      ],
      notes: [
        'Không dùng thay thế thuốc điều trị.',
        'Người đang dùng thuốc khác hoặc có bệnh nền: hỏi dược sĩ trước.',
      ],
    },
    {
      slug: 'accu-chek-instant',
      name: 'Accu-Chek Instant',
      category: 'Máy đo đường huyết',
      price: '820.000đ',
      priceUnit: '/ Máy',
      packInfo: 'Thiết bị y tế gia đình',
      imageUrl: '/brand/products/accuchek-device.jpg?v=2',
      href: '/san-pham/accu-chek-instant',
      kind: 'device',
      shortDescription:
        'Máy đo đường huyết hỗ trợ theo dõi chỉ số tại nhà. Kết quả cần được đọc trong bối cảnh tư vấn chuyên môn.',
      gallery: ['/brand/products/accuchek-device.jpg?v=2'],
      specs: [
        { label: 'Loại', value: 'Thiết bị đo đường huyết' },
        { label: 'Công dụng', value: 'Theo dõi đường huyết tại nhà' },
        { label: 'Phụ kiện', value: 'Theo cấu hình bộ sản phẩm trên bao bì' },
        { label: 'Nhà sản xuất', value: 'Theo thông tin trên bao bì' },
      ],
      usage: [
        'Đọc kỹ hướng dẫn trước khi sử dụng lần đầu.',
        'Vệ sinh tay, dùng que thử tương thích đúng loại máy.',
        'Bảo quản máy và que thử theo hướng dẫn nhà sản xuất.',
      ],
      notes: [
        'Không tự ý thay đổi thuốc điều trị dựa trên một lần đo.',
        'Nếu chỉ số bất thường hoặc có triệu chứng nặng, liên hệ cơ sở y tế.',
      ],
    },
    {
      slug: 'hapacol-650',
      name: 'Hapacol 650',
      category: 'Giảm đau - Hạ sốt',
      price: '35.000đ',
      priceUnit: '/ Hộp',
      packInfo: 'Theo quy cách trên bao bì',
      imageUrl: '/brand/products/hapacol-pain.jpg?v=2',
      href: '/san-pham/hapacol-650',
      kind: 'medicine',
      badge: 'new',
      shortDescription:
        'Thuốc giảm đau, hạ sốt chứa paracetamol. Dùng đúng liều và hỏi dược sĩ khi cần.',
      gallery: ['/brand/products/hapacol-pain.jpg?v=2'],
      specs: [
        { label: 'Thành phần chính', value: 'Paracetamol 650mg (theo bao bì)' },
        { label: 'Công dụng', value: 'Giảm đau, hạ sốt' },
        { label: 'Quy cách', value: 'Theo bao bì sản phẩm' },
        { label: 'Dạng bào chế', value: 'Viên nén' },
        { label: 'Nhà sản xuất', value: 'Theo thông tin trên bao bì' },
      ],
      usage: [
        'Dùng đúng liều hướng dẫn trên bao bì hoặc theo dược sĩ / bác sĩ.',
        'Không dùng kèm nhiều thuốc có paracetamol cùng lúc.',
      ],
      notes: [
        'Không dùng nếu dị ứng paracetamol.',
        'Thận trọng với người bệnh gan.',
        'Trẻ em: chỉ dùng khi đủ độ tuổi và liều phù hợp.',
      ],
    },
    {
      slug: 'berocca-performance',
      name: 'Berocca Performance',
      category: 'Vitamin & khoáng chất',
      price: '125.000đ',
      priceUnit: '/ Hộp',
      packInfo: 'Thực phẩm bảo vệ sức khỏe',
      imageUrl: '/brand/products/berocca-vitamin.jpg?v=2',
      href: '/san-pham/berocca-performance',
      kind: 'supplement',
      badge: 'bestseller',
      shortDescription:
        'Bổ sung vitamin nhóm B và khoáng chất, hỗ trợ tỉnh táo khi mệt mỏi hoặc làm việc nhiều.',
      gallery: ['/brand/products/berocca-vitamin.jpg?v=2'],
      specs: [
        { label: 'Loại', value: 'Thực phẩm bảo vệ sức khỏe' },
        { label: 'Công dụng', value: 'Bổ sung vitamin, khoáng chất' },
        { label: 'Quy cách', value: 'Theo bao bì sản phẩm' },
        { label: 'Nhà sản xuất', value: 'Theo thông tin trên bao bì' },
      ],
      usage: [
        'Hòa tan hoặc dùng theo hướng dẫn trên bao bì.',
        'Dùng đều đặn theo nhu cầu hỗ trợ hàng ngày.',
      ],
      notes: [
        'Không dùng thay thế chế độ ăn uống cân bằng và thuốc điều trị.',
        'Người có bệnh thận hoặc chế độ hạn chế khoáng chất: hỏi dược sĩ.',
      ],
    },
  ],

  articles: [
    {
      slug: 'phong-cam-cum',
      title: 'Cách phòng ngừa cảm cúm khi thời tiết thay đổi',
      date: '2024-05-20',
      excerpt: 'Những thói quen đơn giản giúp gia đình giảm nguy cơ cảm cúm khi giao mùa.',
      imageUrl: '/brand/articles/phong-cam-cum.jpg?v=2',
      href: '/kien-thuc/phong-cam-cum',
      topic: 'flu',
      categoryLabel: 'Bệnh thường gặp',
      readingMinutes: 4,
      body: [
        'Mùa giao mùa, virus cảm cúm dễ lây lan. Phòng bệnh đúng cách giúp giảm nguy cơ và gánh nặng điều trị.',
      ],
      sections: [
        {
          id: 'nguyen-nhan',
          title: 'Vì sao dễ cảm cúm khi giao mùa?',
          paragraphs: [
            'Thời tiết thay đổi làm cơ thể dễ giảm sức đề kháng. Virus cảm cúm lây qua đường hô hấp khi tiếp xúc gần hoặc chạm vào bề mặt có virus rồi đưa tay lên mặt.',
          ],
        },
        {
          id: 'trieu-chung',
          title: 'Các dấu hiệu thường gặp',
          paragraphs: ['Nhận biết sớm giúp chăm sóc đúng cách và biết khi nào cần gặp dược sĩ / bác sĩ.'],
          bullets: ['Sốt, ớn lạnh', 'Ho, đau họng', 'Nghẹt mũi, chảy mũi', 'Mệt mỏi, đau đầu', 'Đau người, kém ăn'],
        },
        {
          id: 'phong-ngua',
          title: 'Cách phòng ngừa tại nhà',
          bullets: [
            'Rửa tay thường xuyên bằng xà phòng',
            'Che miệng khi ho, hắt hơi',
            'Giữ nhà thoáng, ngủ đủ, uống đủ nước',
            'Hạn chế tập trung nơi đông người khi đang dịch',
          ],
        },
        {
          id: 'khi-nao-hoi',
          title: 'Khi nào nên hỏi dược sĩ?',
          paragraphs: [
            'Nếu triệu chứng kéo dài, sốt cao, khó thở, hoặc bạn đang mang thai / có bệnh nền / dùng nhiều thuốc, hãy hỏi dược sĩ trước khi tự mua thuốc.',
            'Nhà thuốc Xuân Hòa hỗ trợ tư vấn dùng thuốc cảm an toàn trên App Novixa và tại quầy trong giờ mở cửa.',
          ],
        },
      ],
    },
    {
      slug: 'kiem-soat-huyet-ap',
      title: '5 thói quen giúp kiểm soát huyết áp hiệu quả',
      date: '2024-05-18',
      excerpt: 'Ăn uống, vận động và theo dõi chỉ số tại nhà đúng cách.',
      imageUrl: '/brand/articles/kiem-soat-huyet-ap.jpg?v=2',
      href: '/kien-thuc/kiem-soat-huyet-ap',
      topic: 'bp',
      categoryLabel: 'Lối sống khỏe',
      readingMinutes: 5,
      body: [
        'Tăng huyết áp thường diễn biến thầm lặng. Duy trì thói quen lành mạnh giúp giảm biến chứng.',
      ],
      sections: [
        {
          id: 'do-huyet-ap',
          title: 'Đo huyết áp đúng cách',
          paragraphs: [
            'Đo vào cùng khung giờ, nghỉ ngơi trước khi đo, ngồi đúng tư thế. Ghi chép chỉ số để dễ theo dõi khi tái khám hoặc hỏi dược sĩ.',
          ],
        },
        {
          id: 'an-uong',
          title: 'Điều chỉnh ăn uống',
          bullets: ['Giảm muối', 'Tăng rau xanh và chất xơ', 'Hạn chế đồ uống có cồn', 'Kiểm soát cân nặng hợp lý'],
        },
        {
          id: 'van-dong',
          title: 'Vận động đều đặn',
          paragraphs: [
            'Đi bộ hoặc vận động nhẹ 20–30 phút hầu hết các ngày trong tuần giúp hỗ trợ kiểm soát huyết áp.',
          ],
        },
        {
          id: 'dung-thuoc',
          title: 'Tuân thủ thuốc theo chỉ định',
          paragraphs: [
            'Không tự ý tăng/giảm/ngưng thuốc huyết áp. Nếu quên liều hoặc có tác dụng phụ, hãy hỏi bác sĩ hoặc dược sĩ.',
          ],
        },
      ],
    },
    {
      slug: 'dinh-duong-tieu-duong',
      title: 'Dinh dưỡng cho người tiểu đường: Nguyên tắc vàng',
      date: '2024-05-16',
      excerpt: 'Nguyên tắc vàng giúp ổn định đường huyết qua bữa ăn hàng ngày.',
      imageUrl: '/brand/articles/dinh-duong-tieu-duong.jpg?v=2',
      href: '/kien-thuc/dinh-duong-tieu-duong',
      topic: 'diabetes',
      categoryLabel: 'Dinh dưỡng',
      readingMinutes: 6,
      body: [
        'Chế độ ăn đóng vai trò quan trọng trong kiểm soát đường huyết.',
      ],
      sections: [
        {
          id: 'nguyen-tac',
          title: 'Nguyên tắc vàng trong bữa ăn',
          bullets: [
            'Ưu tiên chất xơ từ rau, đậu, ngũ cốc nguyên hạt',
            'Kiểm soát khẩu phần tinh bột',
            'Hạn chế đường thêm và nước ngọt',
            'Ăn đúng bữa, tránh bỏ bữa',
          ],
        },
        {
          id: 'theo-doi',
          title: 'Theo dõi đường huyết',
          paragraphs: [
            'Đo đường huyết theo hướng dẫn của bác sĩ giúp đánh giá chế độ ăn có phù hợp hay không.',
          ],
        },
        {
          id: 'thuc-pham-chuc-nang',
          title: 'Khi nào hỏi dược sĩ?',
          paragraphs: [
            'Trước khi dùng thực phẩm bảo vệ sức khỏe hoặc thuốc không kê đơn, hãy hỏi dược sĩ để tránh tương tác và chọn sản phẩm phù hợp.',
          ],
        },
      ],
    },
    {
      slug: 'gap-duoc-si',
      title: 'Khi nào nên gặp dược sĩ trước khi tự mua thuốc?',
      date: '2024-03-15',
      excerpt: 'Dấu hiệu giúp bạn biết lúc nào cần tư vấn chuyên môn.',
      imageUrl: '/brand/articles/gap-duoc-si.jpg?v=2',
      href: '/kien-thuc/gap-duoc-si',
      topic: 'medicine',
      categoryLabel: 'Dùng thuốc an toàn',
      readingMinutes: 4,
      body: [
        'Nên hỏi dược sĩ khi mang thai, có bệnh nền, đang dùng nhiều thuốc hoặc dị ứng thuốc trước đây.',
      ],
      sections: [
        {
          id: 'tinh-huong',
          title: 'Những tình huống nên hỏi dược sĩ',
          bullets: [
            'Mang thai hoặc đang cho con bú',
            'Có bệnh nền (tim mạch, thận, gan, tiểu đường...)',
            'Đang dùng nhiều loại thuốc',
            'Từng dị ứng thuốc',
            'Mua thuốc cho trẻ em hoặc người cao tuổi',
          ],
        },
        {
          id: 'loi-ich',
          title: 'Lợi ích khi được tư vấn đúng',
          paragraphs: [
            'Dược sĩ giúp chọn đúng sản phẩm, giải thích cách dùng và lưu ý tương tác thuốc — giảm rủi ro dùng sai hoặc trùng thành phần.',
          ],
        },
        {
          id: 'lien-he',
          title: 'Liên hệ Nhà thuốc Xuân Hòa',
          paragraphs: [
            'Bạn có thể đến quầy hoặc chat trên App Novixa trong giờ mở cửa để được hỗ trợ.',
          ],
        },
      ],
    },
  ],

  appSection: {
    title: 'Quản lý sức khỏe cả gia đình dễ dàng hơn với App Novixa',
    titleHighlight: 'App Novixa',
    features: [
      { icon: 'history', title: 'Lưu lịch sử mua thuốc và đơn thuốc' },
      { icon: 'bell', title: 'Nhắc uống thuốc đúng giờ' },
      { icon: 'pulse', title: 'Theo dõi chỉ số sức khỏe' },
      { icon: 'family', title: 'Quản lý hồ sơ sức khỏe gia đình' },
      { icon: 'chat', title: 'Kết nối trực tiếp với dược sĩ' },
      { icon: 'cart', title: 'Đặt thuốc nhanh giao tận nơi' },
    ],
    familyImageUrl: '/brand/family-health.png?v=7',
  },

  platformPromo: {
    eyebrow: 'Website này được tạo tự động bởi',
    title: 'Novixa Digital Pharmacy Platform',
    subtitle: 'Nền tảng chuyển đổi số toàn diện cho nhà thuốc',
    features: [
      { icon: 'seo', title: 'Website chuyên nghiệp chuẩn SEO' },
      { icon: 'sync', title: 'Đồng bộ quản lý bán hàng kho & khách hàng' },
      { icon: 'care', title: 'Kết nối App, chăm sóc khách hàng toàn diện' },
    ],
    ctaLead: 'Tạo website đẹp như Nhà thuốc Xuân Hòa cho nhà thuốc của bạn chỉ trong 5 phút!',
    ctaLeadHighlight: 'Nhà thuốc Xuân Hòa',
    ctaPrimary: { label: 'Xem demo nhà thuốc', href: NOVIXA },
    ctaSecondary: { label: 'Đăng ký ngay', href: `${NOVIXA}/vi/lien-he/` },
  },

  pages: {
    about: {
      intro:
        'Nhà thuốc Xuân Hòa đồng hành chăm sóc sức khỏe gia đình với thuốc chính hãng, tư vấn dược sĩ tận tâm và trải nghiệm số trên nền tảng Novixa.',
      hero: {
        eyebrow: 'VỀ CHÚNG TÔI',
        title: 'Nhà thuốc Xuân Hòa',
        subtitle: 'Đồng hành chăm sóc sức khỏe cả gia đình',
        body: 'Từ tư vấn tại quầy đến đặt thuốc trên App Novixa, chúng tôi mang đến dịch vụ nhà thuốc gần gũi, minh bạch và an toàn cho từng gia đình tại Thái Nguyên.',
        imageUrl: '/brand/xuanhoa-hero.png?v=13',
        imageAlt: 'Dược sĩ tư vấn khách hàng tại Nhà thuốc Xuân Hòa',
        ctaPrimary: { label: 'Đặt thuốc trên App', href: APP },
        ctaSecondary: { label: 'Liên hệ với chúng tôi', href: '/lien-he' },
      },
      valuesTitle: 'GIÁ TRỊ CHÚNG TÔI THEO ĐUỔI',
      values: [
        {
          icon: 'badge',
          title: 'Thuốc chính hãng',
          description: 'Cam kết nguồn gốc rõ ràng, đạt chuẩn GPP và bảo quản đúng quy định.',
        },
        {
          icon: 'advisor',
          title: 'Tư vấn tận tâm',
          description: 'Dược sĩ giải thích cách dùng dễ hiểu, đồng hành cùng từng liệu trình.',
        },
        {
          icon: 'scooter',
          title: 'Giao hàng nhanh',
          description: 'Đặt thuốc thuận tiện, giao tận nơi giúp gia đình chủ động chăm sóc sức khỏe.',
        },
        {
          icon: 'privacy',
          title: 'Luôn sẵn sàng hỗ trợ',
          description: 'Mở cửa 07:00–22:00 mọi ngày — liên hệ nhanh qua điện thoại hoặc App.',
        },
      ],
      reasonsTitle: 'VÌ SAO NHIỀU GIA ĐÌNH LỰA CHỌN XUÂN HÒA?',
      reasons: [
        {
          icon: 'badge',
          label: '2015',
          description: 'Đồng hành cùng cộng đồng hơn 10 năm với dịch vụ nhà thuốc gần gũi.',
        },
        {
          icon: 'badge',
          label: '100%',
          description: 'Cam kết thuốc chính hãng, hóa đơn rõ ràng và tư vấn minh bạch.',
        },
        {
          icon: 'pharmacist',
          label: 'Đội ngũ dược sĩ',
          description: 'Chuyên môn vững, sẵn sàng hỗ trợ tại quầy và trên App Novixa.',
        },
        {
          icon: 'customers',
          label: '3.500+',
          description: 'Gia đình tin tưởng lựa chọn Xuân Hòa để chăm sóc sức khỏe lâu dài.',
        },
        {
          icon: 'hours',
          label: '07:00 - 22:00',
          description: 'Mở cửa tất cả các ngày trong tuần, linh hoạt theo nhịp sống gia đình.',
        },
      ],
      team: {
        title: 'ĐỘI NGŨ DƯỢC SĨ',
        body: 'Đội ngũ dược sĩ Xuân Hòa được đào tạo chuyên môn, sẵn sàng tư vấn tận tâm tại quầy và trên App Novixa — giải thích cách dùng thuốc dễ hiểu, đồng hành cùng từng gia đình.',
        highlights: [
          '15+ dược sĩ chuyên môn',
          'Tư vấn 1-1 tại quầy và trên App',
          'Mở cửa 07:00 - 22:00 mọi ngày',
        ],
        ctaPrimary: { label: 'Gặp dược sĩ trên App', href: APP },
        ctaSecondary: { label: 'Liên hệ nhà thuốc', href: '/lien-he' },
      },
      digital: {
        title: 'Ứng dụng công nghệ để chăm sóc sức khỏe gia đình tốt hơn',
        bullets: [
          'Đặt thuốc nhanh trên App Novixa',
          'Lưu lịch sử mua thuốc và đơn thuốc',
          'Nhắc uống thuốc đúng giờ',
          'Theo dõi chỉ số sức khỏe',
          'Quản lý hồ sơ sức khỏe gia đình',
          'Kết nối trực tiếp với dược sĩ',
        ],
      },
      gallery: {
        title: 'KHÔNG GIAN NHÀ THUỐC',
        images: [
          { src: '/brand/about/about-gallery-01.jpg', alt: 'Mặt tiền Nhà thuốc Xuân Hòa' },
          { src: '/brand/about/about-gallery-02.jpg', alt: 'Không gian trưng bày thuốc' },
          { src: '/brand/about/about-gallery-03.jpg', alt: 'Quầy tư vấn dược sĩ' },
        ],
        ctaLabel: 'Xem thêm hình ảnh',
        ctaHref: '/lien-he',
      },
      certificates: {
        title: 'GIẤY PHÉP & CHỨNG NHẬN',
        items: [
          { title: 'Giấy phép kinh doanh', imageUrl: '/brand/about/about-cert-01.jpg' },
          { title: 'Chứng nhận GPP', imageUrl: '/brand/about/about-cert-02.jpg' },
          { title: 'Giấy chứng nhận đủ điều kiện', imageUrl: '/brand/about/about-cert-03.jpg' },
        ],
        ctaLabel: 'Xem tất cả giấy phép',
        ctaHref: '#giay-phep',
      },
      supportCta: {
        title: 'Chúng tôi luôn sẵn sàng hỗ trợ bạn!',
        imageUrl: '/brand/about/about-team-banner.jpg',
        imageAlt: 'Đội ngũ Nhà thuốc Xuân Hòa',
        ctaPrimary: { label: 'Đặt thuốc nhanh', href: APP },
        ctaSecondary: { label: 'Liên hệ với chúng tôi', href: '/lien-he' },
      },
      sections: [
        {
          id: 'su-menh',
          title: 'Sứ mệnh',
          body: 'Mang đến dịch vụ nhà thuốc gần gũi, minh bạch và an toàn — từ tư vấn tại quầy đến đặt thuốc trên App Novixa.',
        },
        {
          id: 'doi-ngu',
          title: 'Đội ngũ dược sĩ',
          body: 'Đội ngũ được đào tạo chuyên môn, sẵn sàng giải thích cách dùng thuốc dễ hiểu.',
        },
        {
          id: 'giay-phep',
          title: 'Giấy phép & cam kết',
          body: 'Nhà thuốc tuân thủ quy định kinh doanh dược. Thông tin giấy phép có thể bổ sung trên trang hoặc tại quầy.',
        },
        {
          id: 'cong-nghe',
          title: 'Vận hành trên Novixa',
          body: 'Xuân Hòa dùng Novixa cho POS, kho, khách hàng và App — hóa đơn rõ ràng, kết nối bền vững với từng gia đình.',
        },
      ],
    },
    contact: {
      intro:
        'Liên hệ nhà thuốc Xuân Hòa để được tư vấn, đặt thuốc hoặc hỗ trợ dùng App Novixa.',
      mapNote: 'Chi nhánh 1: Số 30 đường Xuân Hòa · Chi nhánh 2: Số 526 đường Thống Nhất, phường Phan Đình Phùng, Thái Nguyên.',
      hero: {
        eyebrow: 'LIÊN HỆ VỚI CHÚNG TÔI',
        title: 'Nhà thuốc Xuân Hòa',
        subtitle: 'Luôn sẵn sàng lắng nghe và phục vụ bạn!',
        body: 'Bạn cần tư vấn sản phẩm, đặt thuốc hoặc hỗ trợ dùng App Novixa? Đội ngũ dược sĩ sẵn sàng hỗ trợ trong giờ mở cửa tại cả hai chi nhánh Thái Nguyên.',
        imageUrl: '/brand/xuanhoa-hero.png?v=13',
        imageAlt: 'Dược sĩ Nhà thuốc Xuân Hòa hỗ trợ khách hàng',
        floatingCard: {
          title: 'Tư vấn tận tâm',
          description: 'Gọi trực tiếp để được dược sĩ hỗ trợ nhanh.',
        },
      },
      infoTitle: 'THÔNG TIN LIÊN HỆ',
      formTitle: 'GỬI TIN NHẮN CHO CHÚNG TÔI',
      formNote:
        'Nhấn “Gửi tin nhắn” sẽ mở ứng dụng email của bạn gửi tới nhà thuốc. Vui lòng hoàn tất gửi thư trên máy để chúng tôi nhận được.',
      subjects: [
        'Tư vấn dùng thuốc',
        'Đặt thuốc / giao hàng',
        'Hỗ trợ App Novixa',
        'Góp ý dịch vụ',
        'Khác',
      ],
      reasonsTitle: 'VÌ SAO NÊN LIÊN HỆ VỚI CHÚNG TÔI?',
      reasons: [
        {
          icon: 'consult',
          title: 'Tư vấn chuyên môn',
          description: 'Dược sĩ giải đáp đúng nhu cầu sức khỏe của bạn.',
        },
        {
          icon: 'accurate',
          title: 'Thông tin chính xác',
          description: 'Hướng dẫn rõ ràng, minh bạch, dễ hiểu.',
        },
        {
          icon: 'fast',
          title: 'Phản hồi nhanh',
          description: 'Hỗ trợ kịp thời trong giờ mở cửa nhà thuốc.',
        },
        {
          icon: 'care',
          title: 'Chăm sóc tận tâm',
          description: 'Đồng hành lâu dài cùng sức khỏe gia đình.',
        },
      ],
      mapEmbedUrl:
        'https://www.openstreetmap.org/export/embed.html?bbox=105.845%2C21.580%2C105.856%2C21.589&layer=mapnik&marker=21.5844045%2C105.8505815',
      directionsUrl:
        'https://www.google.com/maps/place/Nh%C3%A0+thu%E1%BB%91c+Xu%C3%A2n+Ho%C3%A0/@21.5844045,105.8505815,17z/data=!3m1!4b1!4m6!3m5!1s0x313526c1a16ffc43:0x5d7445013c377bf!8m2!3d21.5844045!4d105.8505815',
      directionsLabel: 'Chỉ đường đến nhà thuốc',
    },
    products: {
      hero: {
        title: 'Sản phẩm chăm sóc sức khỏe',
        bullets: ['Thuốc chính hãng', 'Thực phẩm bảo vệ sức khỏe', 'Thiết bị y tế'],
        body: 'Xem sản phẩm nổi bật và đặt nhanh trên App Novixa, hoặc đến nhà thuốc để được dược sĩ tư vấn đúng nhu cầu.',
        ctaPrimary: { label: 'Đặt trên App Novixa', href: APP },
        ctaSecondary: { label: 'Liên hệ dược sĩ', href: '/lien-he' },
        trustItems: [
          { icon: 'badge', label: '100% Thuốc chính hãng' },
          { icon: 'advisor', label: 'Tư vấn tận tâm bởi dược sĩ' },
          { icon: 'scooter', label: 'Giao hàng nhanh trong ngày' },
        ],
      },
      searchPlaceholder: 'Tìm thuốc, hoạt chất, bệnh lý, thương hiệu...',
      pills: [
        { id: 'all', label: 'Tất cả sản phẩm', kind: '' },
        { id: 'medicine', label: 'Thuốc', kind: 'medicine' },
        { id: 'supplement', label: 'Thực phẩm chức năng', kind: 'supplement' },
        { id: 'device', label: 'Thiết bị y tế', kind: 'device' },
        { id: 'care', label: 'Chăm sóc cá nhân', kind: 'care' },
      ],
      gridTitle: 'Sản phẩm nổi bật',
      needsTitle: 'Mua theo nhu cầu',
      needs: [
        { id: 'cold', label: 'Cảm cúm', icon: 'cold', filter: 'ho' },
        { id: 'pain', label: 'Đau đầu / Hạ sốt', icon: 'pain', filter: 'đau' },
        { id: 'digest', label: 'Tiêu hóa', icon: 'digest', filter: 'tiêu hóa' },
        { id: 'heart', label: 'Tim mạch', icon: 'heart', filter: 'tim' },
        { id: 'diabetes', label: 'Tiểu đường', icon: 'diabetes', filter: 'đường' },
        { id: 'joint', label: 'Xương khớp', icon: 'joint', filter: 'khớp' },
        { id: 'mom', label: 'Mẹ & bé', icon: 'mom', filter: 'vitamin' },
        { id: 'aid', label: 'Sơ cứu', icon: 'aid', filter: 'thiết bị' },
      ],
      trustBlocks: [
        {
          icon: 'badge',
          title: 'Cam kết chính hãng',
          description: 'Nguồn gốc rõ ràng, bảo quản đạt chuẩn nhà thuốc.',
        },
        {
          icon: 'advisor',
          title: 'Tư vấn bởi dược sĩ',
          description: 'Hỗ trợ chọn đúng sản phẩm theo tình trạng sức khỏe.',
        },
        {
          icon: 'return',
          title: 'Đổi trả dễ dàng',
          description: 'Hỗ trợ đổi trả theo chính sách của nhà thuốc.',
        },
        {
          icon: 'scooter',
          title: 'Giao hàng nhanh',
          description: 'Đặt trên App, giao nhanh trong khu vực phục vụ.',
        },
      ],
      emptyMessage: 'Không tìm thấy sản phẩm phù hợp. Thử từ khóa khác hoặc đặt trực tiếp trên App.',
    },
    services: {
      hero: {
        eyebrow: 'Dịch vụ của chúng tôi',
        title: 'Chăm sóc sức khỏe toàn diện cho bạn và gia đình',
        body: 'Từ tư vấn tại quầy đến đặt thuốc và theo dõi liệu trình trên App Novixa — Xuân Hòa đồng hành chuyên nghiệp, gần gũi và minh bạch.',
        imageUrl: '/brand/xuanhoa-hero.png?v=13',
        imageAlt: 'Dược sĩ tư vấn khách hàng tại Nhà thuốc Xuân Hòa',
        ctaPrimary: { label: 'Đặt dịch vụ trên App', href: APP },
        ctaSecondary: { label: 'Liên hệ dược sĩ', href: '/lien-he' },
      },
      trustItems: [
        { icon: 'pharmacist', label: 'Dược sĩ chuyên môn cao' },
        { icon: 'badge', label: 'Sản phẩm chính hãng' },
        { icon: 'scooter', label: 'Phục vụ nhanh chóng' },
        { icon: 'hours', label: 'Mở cửa 07:00 - 22:00' },
      ],
      featuredTitle: 'Các dịch vụ nổi bật',
      featuredSubtitle: 'Lựa chọn dịch vụ phù hợp — đặt trên App hoặc ghé nhà thuốc để được hỗ trợ trực tiếp.',
      featured: [
        {
          id: 'tu-van',
          title: 'Tư vấn dùng thuốc',
          description: 'Dược sĩ giải thích cách dùng, lưu ý tương tác và đồng hành theo liệu trình.',
          icon: 'chat',
          tone: 'green',
          bullets: ['Tư vấn tại quầy hoặc trên App', 'Dễ hiểu, đúng nhu cầu', 'Nhắc lưu ý an toàn khi dùng'],
        },
        {
          id: 'giao-hang',
          title: 'Giao hàng tận nơi',
          description: 'Đặt thuốc thuận tiện, giao nhanh trong khu vực phục vụ của nhà thuốc.',
          icon: 'delivery',
          tone: 'blue',
          bullets: ['Đặt qua App Novixa', 'Theo dõi trạng thái đơn', 'Nhận thuốc tại nhà'],
        },
        {
          id: 'don-thuoc',
          title: 'Đặt theo đơn thuốc',
          description: 'Gửi ảnh đơn thuốc để nhà thuốc chuẩn bị và xác nhận nhanh chóng.',
          icon: 'rx',
          tone: 'yellow',
          bullets: ['Gửi đơn trên App', 'Dược sĩ kiểm tra đơn', 'Chuẩn bị đúng theo chỉ định'],
        },
        {
          id: 'nhac-thuoc',
          title: 'Nhắc uống thuốc',
          description: 'Thiết lập nhắc lịch giúp dùng thuốc đúng giờ, đúng liệu trình.',
          icon: 'bell',
          tone: 'purple',
          bullets: ['Nhắc theo đơn', 'Theo dõi tiến độ uống', 'Giảm quên liều'],
        },
        {
          id: 'ho-so',
          title: 'Hồ sơ sức khỏe',
          description: 'Lưu lịch sử mua thuốc và thông tin sức khỏe để tái khám/mua lại dễ hơn.',
          icon: 'health',
          tone: 'green',
          bullets: ['Lịch sử đơn rõ ràng', 'Lưu chỉ số theo dõi', 'Tra cứu nhanh khi cần'],
        },
        {
          id: 'online',
          title: 'Tư vấn trực tuyến',
          description: 'Kết nối dược sĩ khi không tiện đến quầy — hỗ trợ trong giờ mở cửa.',
          icon: 'chat',
          tone: 'blue',
          bullets: ['Chat trên App', 'Phản hồi kịp thời', 'Chuyển đặt thuốc khi sẵn sàng'],
        },
        {
          id: 'mua-lai',
          title: 'Mua lại nhanh',
          description: 'Đặt lại các sản phẩm/đơn đã mua trước đó chỉ trong vài thao tác.',
          icon: 'cart',
          tone: 'yellow',
          bullets: ['Gợi ý từ lịch sử', 'Giảm thao tác nhập lại', 'Tiện cho liệu trình dài ngày'],
        },
        {
          id: 'gia-dinh',
          title: 'Chăm sóc cả gia đình',
          description: 'Quản lý hồ sơ và đơn thuốc cho nhiều thành viên trong một tài khoản App.',
          icon: 'family',
          tone: 'purple',
          bullets: ['Nhiều hồ sơ thành viên', 'Theo dõi thuốc từng người', 'Ưu đãi / tích điểm khi có'],
        },
      ],
      processTitle: 'Quy trình phục vụ',
      processSubtitle: 'Đơn giản – Nhanh chóng – Chính xác.',
      process: [
        {
          title: 'Chọn dịch vụ / sản phẩm',
          description: 'Tìm trên website hoặc App, hoặc đến trực tiếp nhà thuốc.',
          icon: 'choose',
        },
        {
          title: 'Dược sĩ tiếp nhận & tư vấn',
          description: 'Xác nhận nhu cầu, kiểm tra đơn và tư vấn cách dùng phù hợp.',
          icon: 'consult',
        },
        {
          title: 'Chuẩn bị & giao nhận',
          description: 'Lấy hàng tại quầy hoặc giao tận nơi theo đơn đã xác nhận.',
          icon: 'deliver',
        },
        {
          title: 'Đồng hành & chăm sóc',
          description: 'Nhắc uống thuốc, lưu hồ sơ và hỗ trợ khi cần mua lại.',
          icon: 'care',
        },
      ],
      appBanner: {
        title: 'Trải nghiệm đầy đủ dịch vụ trên App Novixa',
        body: 'Đặt thuốc, chat dược sĩ, nhắc uống thuốc và quản lý hồ sơ gia đình trong một ứng dụng.',
        highlights: [
          { icon: 'convenient', label: 'Tiện lợi', description: 'Đặt mọi lúc trên điện thoại' },
          { icon: 'fast', label: 'Nhanh chóng', description: 'Xử lý đơn kịp thời' },
          { icon: 'save', label: 'Minh bạch', description: 'Giá và lịch sử rõ ràng' },
          { icon: 'safe', label: 'An toàn', description: 'Tư vấn bởi dược sĩ' },
        ],
        cta: { label: 'Tải App Novixa ngay', href: APP },
      },
    },
    knowledge: {
      hero: {
        title: 'Kiến thức sức khỏe',
        subtitle:
          'Chia sẻ thông tin hữu ích từ đội ngũ dược sĩ Xuân Hòa. Nội dung mang tính tham khảo — luôn hỏi dược sĩ khi dùng thuốc.',
      },
      listTitle: 'Bài viết mới nhất',
      sidebarTitle: 'Đọc tiếp',
      tipsTitle: 'Mẹo nhỏ cho sức khỏe mỗi ngày',
      tips: [
        { icon: 'water', text: 'Uống đủ nước mỗi ngày để cơ thể khỏe và tinh thần tỉnh táo.' },
        { icon: 'veg', text: 'Thêm rau xanh vào mỗi bữa ăn để bổ sung chất xơ và vitamin.' },
        { icon: 'move', text: 'Vận động nhẹ 20–30 phút giúp tuần hoàn và kiểm soát cân nặng.' },
        { icon: 'sleep', text: 'Ngủ đủ giấc để hệ miễn dịch phục hồi tốt hơn.' },
        { icon: 'wash', text: 'Rửa tay sạch trước khi ăn và sau khi về nhà.' },
      ],
      newsletter: {
        title: 'Nhận kiến thức sức khỏe hữu ích',
        placeholder: 'Nhập email của bạn',
        ctaLabel: 'Đăng ký',
        note: 'Tính năng đăng ký email sẽ sớm mở. Hiện bạn có thể theo dõi Facebook hoặc liên hệ nhà thuốc.',
      },
    },
  },

  footer: {
    aboutLinks: [
      { label: 'Giới thiệu', href: '/gioi-thieu' },
      { label: 'Đội ngũ dược sĩ', href: '/gioi-thieu#doi-ngu' },
      { label: 'Giấy phép kinh doanh', href: '/gioi-thieu#giay-phep' },
    ],
    categoryLinks: [
      { label: 'Giới thiệu', href: '/gioi-thieu' },
      { label: 'Sản phẩm', href: '/san-pham' },
      { label: 'Dịch vụ', href: '/dich-vu' },
      { label: 'Kiến thức sức khỏe', href: '/kien-thuc' },
      { label: 'Liên hệ', href: '/lien-he' },
    ],
    supportLinks: [
      { label: 'Hướng dẫn đặt thuốc', href: APP },
      { label: 'Chính sách giao hàng', href: '/lien-he' },
      { label: 'Chính sách đổi trả', href: '/lien-he' },
      { label: 'Chính sách bảo mật', href: 'https://novixa.vn/vi/chinh-sach-bao-mat/' },
      { label: 'Điều khoản sử dụng', href: 'https://novixa.vn/vi/dieu-khoan-su-dung/' },
    ],
    tagline: 'Đồng hành sức khỏe gia đình',
    mission:
      'Nhà thuốc Xuân Hòa cam kết cung cấp sản phẩm chính hãng, tư vấn tận tâm và dịch vụ chăm sóc sức khỏe toàn diện.',
    newsletterNote: 'Nhận thông tin khuyến mãi và kiến thức sức khỏe mới nhất từ Nhà thuốc Xuân Hòa.',
    copyright: 'Tất cả quyền được bảo lưu.',
  },

  poweredBy: {
    label: 'POWERED BY NOVIXA',
    href: NOVIXA,
    blurb: 'Nhà thuốc vận hành trên nền tảng Novixa — POS, App khách, CRM và hiện diện số.',
  },
};

import type { AppLocale } from '@/lib/cms/getLanding';

export type SpecialtySeo = {
  title: string;
  description: string;
};

export type LegalSection = {
  heading: string;
  body: string[];
};

export type StoryArticle = {
  name: string;
  place: string;
  title: string;
  body: string[];
  highlights?: string[];
  image: { src: string; alt: string };
};

export type AboutBlock = {
  heading: string;
  body: string[];
};

export type PlanCompareRow = {
  label: string;
  free: boolean | string;
  growth: boolean | string;
  peace: boolean | string;
  aiPlus: boolean | string;
};

type SpecialtyBundle = {
  plans: {
    seo: SpecialtySeo;
    eyebrow: string;
    title: string;
    lead: string;
    trialNote: string;
    matrixTitle: string;
    matrixCaption: string;
    backCta: string;
    appCta: string;
    columns: [string, string, string, string];
    rows: PlanCompareRow[];
  };
  stories: {
    seo: SpecialtySeo;
    eyebrow: string;
    title: string;
    lead: string;
    articles: StoryArticle[];
    appCta: string;
  };
  about: {
    seo: SpecialtySeo;
    eyebrow: string;
    title: string;
    lead: string;
    blocks: AboutBlock[];
    companyHeading: string;
    appCta: string;
  };
  privacy: {
    seo: SpecialtySeo;
    eyebrow: string;
    title: string;
    updated: string;
    sections: LegalSection[];
  };
  terms: {
    seo: SpecialtySeo;
    eyebrow: string;
    title: string;
    updated: string;
    sections: LegalSection[];
  };
};

const vi: SpecialtyBundle = {
  plans: {
    seo: {
      title: 'So sánh gói Famixa',
      description:
        'So sánh Free, Growth, Peace và AI+ — quyền năng theo Family Plan Capability Matrix. Trial 30 ngày = quyền Peace.',
    },
    eyebrow: 'BẢNG GIÁ',
    title: 'Chọn gói đồng hành phù hợp',
    lead: 'Bắt đầu miễn phí. Dùng thử 30 ngày để trải nghiệm Peace (Pro). Nâng cấp khi nhà bạn cần thêm chứng cứ và huấn luyện AI.',
    trialNote: 'Dùng thử 30 ngày = đầy đủ quyền gói Peace (Pro), không cần thẻ để bắt đầu Free.',
    matrixTitle: 'Bảng quyền năng theo gói',
    matrixCaption: 'Nguồn: FamilyPlanCapabilityMatrix · không vượt SoT packaging.',
    backCta: 'Về trang chủ',
    appCta: 'Bắt đầu trên Famixa',
    columns: ['Free', 'Growth', 'Peace', 'AI+'],
    rows: [
      { label: 'Giá / tháng', free: '0đ', growth: '99.000đ', peace: '199.000đ', aiPlus: '399.000đ' },
      { label: 'Số trẻ tối đa', free: '1', growth: '2', peace: 'Không giới hạn', aiPlus: 'Không giới hạn' },
      { label: 'Nhịp ngày (core routine)', free: true, growth: true, peace: true, aiPlus: true },
      { label: 'Nhận xét tuần', free: true, growth: true, peace: true, aiPlus: true },
      { label: 'Dòng thời gian kỷ niệm', free: false, growth: true, peace: true, aiPlus: true },
      { label: 'Bản đôi hành vi (Behavior Twin)', free: false, growth: true, peace: true, aiPlus: true },
      { label: 'AI đề xuất thích nghi', free: false, growth: true, peace: true, aiPlus: true },
      { label: 'Huấn luyện hành vi', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Huấn luyện AI cho cha mẹ', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Báo cáo tăng trưởng', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Thỏa thuận dùng thiết bị', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Thư AI + xem lại khoảnh khắc', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Check-in thành công phụ huynh', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Playbook sâu theo tuần (AI+)', free: false, growth: false, peace: false, aiPlus: true },
    ],
  },
  stories: {
    seo: {
      title: 'Câu chuyện cha mẹ với Famixa',
      description: 'Những chia sẻ từ gia đình Việt đang đồng hành cùng Famixa — bớt nhắc, thêm tự giác.',
    },
    eyebrow: 'CÂU CHUYỆN',
    title: 'Cha mẹ nói gì về Famixa?',
    lead: 'Không phải bảng điểm con cái — mà là nhịp nhà nhẹ hơn, và vài bước nhỏ đúng lúc.',
    appCta: 'Bắt đầu hành trình của nhà bạn',
    articles: [
      {
        name: 'Chị Lan Anh',
        place: 'Hà Nội',
        title: 'Nhắc ít đi — nhà mình dịu lại',
        body: [
          'Trước đây mỗi tối mình cứ phải nhắc đi ngủ, nhắc học, nhắc cất điện thoại. Căng thẳng lan cả nhà.',
          'Với Famixa, mình chỉ duyệt vài đề xuất ngắn. Con bắt đầu tự giác hơn; mình cũng bớt “làm cảnh sát”.',
        ],
        highlights: ['tự giác', 'bớt nhắc'],
        image: { src: '/images/quote-lan-card.png', alt: 'Gia đình chị Lan Anh' },
      },
      {
        name: 'Anh Minh Hoàng',
        place: 'TP. Hồ Chí Minh',
        title: 'Gợi ý đúng lúc, đúng nhà mình',
        body: [
          'Mình từng thử app checklist — con thấy như bị quản. Famixa khác: đề xuất dựa trên nhịp nhà, không xếp hạng.',
          'Có hôm chỉ một câu gợi ý buổi sáng là đủ để cả nhà đổi cách nói chuyện với nhau.',
        ],
        highlights: ['hiểu con', 'đúng lúc'],
        image: { src: '/images/quote-minh-card.png', alt: 'Gia đình anh Minh Hoàng' },
      },
      {
        name: 'Chị Thu Thảo',
        place: 'Đà Nẵng',
        title: 'Tóm tắt buổi sáng thành thói quen',
        body: [
          'Mình không có nhiều thời gian. Tóm tắt buổi sáng một việc cần chú ý — đúng kiểu ≤ 1 phút.',
          'Cả nhà coi đó là nghi thức nhỏ trước khi đi làm, đi học. Nhẹ mà đều.',
        ],
        highlights: ['thói quen', '≤ 1 phút'],
        image: { src: '/images/quote-thu-card.png', alt: 'Gia đình chị Thu Thảo' },
      },
      {
        name: 'Chị Hương',
        place: 'Bắc Ninh',
        title: 'Thỏa thuận điện thoại thay vì cấm',
        body: [
          'Nhà có hai con tuổi khác nhau. Cấm máy không bền; thỏa thuận trong nhà mới đi được lâu.',
          'Famixa giúp mình ghi nhận thỏa thuận và nhắc nhẹ — không đo máy, không khóa app.',
        ],
        highlights: ['thỏa thuận', 'không khóa app'],
        image: { src: '/images/quote-lan-card.png', alt: 'Gia đình chị Hương' },
      },
      {
        name: 'Anh Tuấn',
        place: 'Cần Thơ',
        title: 'Thấy tiến bộ nhà, không phải điểm con',
        body: [
          'Mình sợ app biến thành bảng điểm. Famixa đo “nhà đang nhẹ hơn không” — đúng thứ mình cần.',
          'Khi có thư / xem lại khoảnh khắc, cả nhà cười với nhau thay vì soi lỗi.',
        ],
        highlights: ['tiến bộ nhà', 'không xếp hạng'],
        image: { src: '/images/quote-minh-card.png', alt: 'Gia đình anh Tuấn' },
      },
    ],
  },
  about: {
    seo: {
      title: 'Về Famixa',
      description:
        'Famixa — AI giúp gia đình hạnh phúc hơn mỗi ngày. Sản phẩm của KIT Technology, Thái Nguyên.',
    },
    eyebrow: 'VỀ CHÚNG TÔI',
    title: 'Famixa đồng hành cùng gia đình Việt',
    lead: 'Every Family is Unique. Every Family Can Grow. — Không có một công thức nuôi dạy đúng cho mọi nhà.',
    appCta: 'Trải nghiệm Famixa',
    companyHeading: 'Đơn vị phát triển',
    blocks: [
      {
        heading: 'Sứ mệnh',
        body: [
          'Giúp cha mẹ nhẹ nhõm hơn, con cái tự giác hơn, và gia đình có thêm thời gian chất lượng — bằng AI thấu hiểu từng nhà, không phải app checklist.',
        ],
      },
      {
        heading: 'Cách chúng tôi làm',
        body: [
          'AI chuẩn bị phần lớn đề xuất; phụ huynh quyết định bằng vài chạm. Không tự sửa routine sau lưng bạn.',
          'Không đo máy / MDM. Thời gian màn hình là thỏa thuận trong nhà. Growth Zone không phải bảng xếp hạng.',
        ],
      },
      {
        heading: 'Fami',
        body: [
          'Fami là hạt mầm / mascot đồng hành — giọng brief và coach cho bố mẹ, không phải chatbot LLM nói chuyện tự do với trẻ.',
        ],
      },
    ],
  },
  privacy: {
    seo: {
      title: 'Chính sách bảo mật',
      description: 'Cách Famixa và KIT Technology thu thập, dùng và bảo vệ dữ liệu gia đình.',
    },
    eyebrow: 'PHÁP LÝ',
    title: 'Chính sách bảo mật',
    updated: 'Cập nhật: 30/07/2026',
    sections: [
      {
        heading: '1. Phạm vi',
        body: [
          'Chính sách này áp dụng cho website famixa.vn và ứng dụng Famixa (home.famixa.vn) do Công ty TNHH Truyền thông và Công nghệ KIT vận hành.',
        ],
      },
      {
        heading: '2. Dữ liệu chúng tôi xử lý',
        body: [
          'Thông tin tài khoản (email, tên hiển thị), hồ sơ gia đình do bạn nhập (thành viên, nhịp ngày, thỏa thuận), nhật ký sử dụng sản phẩm cần thiết để cung cấp dịch vụ, và dữ liệu kỹ thuật cơ bản (nhật ký truy cập, cookie / Web Analytics nếu bật).',
          'Chúng tôi không yêu cầu dữ liệu thanh toán thẻ trên website marketing; thanh toán (nếu có) qua quy trình checkout của ứng dụng.',
        ],
      },
      {
        heading: '3. Mục đích sử dụng',
        body: [
          'Cung cấp và cải thiện dịch vụ Famixa, cá nhân hóa đề xuất theo nhà bạn, bảo mật tài khoản, hỗ trợ khách hàng, và tuân thủ nghĩa vụ pháp lý.',
          'Không bán dữ liệu cá nhân cho bên thứ ba để quảng cáo.',
        ],
      },
      {
        heading: '4. Lưu trữ & bảo mật',
        body: [
          'Dữ liệu được lưu trên hạ tầng có kiểm soát truy cập. Chúng tôi áp dụng biện pháp kỹ thuật và tổ chức phù hợp để giảm rủi ro truy cập trái phép.',
          'Bạn kiểm soát quyền thành viên trong gia đình trên ứng dụng.',
        ],
      },
      {
        heading: '5. Quyền của bạn',
        body: [
          'Bạn có thể yêu cầu xem, chỉnh sửa, hoặc xóa dữ liệu tài khoản theo khả năng sản phẩm và quy định pháp luật, bằng cách liên hệ qua kênh hỗ trợ trên ứng dụng hoặc email công ty.',
        ],
      },
      {
        heading: '6. Liên hệ',
        body: [
          'Công ty TNHH Truyền thông và Công nghệ KIT — KĐT Hồ Xương Rồng, P. Phan Đình Phùng, Thái Nguyên.',
          'Tạm thời: Fanpage Facebook https://www.facebook.com/famixa.vn · Zalo 0984.660.399 · Tư vấn qua chat Zalo.',
        ],
      },
    ],
  },
  terms: {
    seo: {
      title: 'Điều khoản sử dụng',
      description: 'Điều khoản sử dụng website famixa.vn và dịch vụ Famixa.',
    },
    eyebrow: 'PHÁP LÝ',
    title: 'Điều khoản sử dụng',
    updated: 'Cập nhật: 30/07/2026',
    sections: [
      {
        heading: '1. Chấp nhận',
        body: [
          'Khi truy cập famixa.vn hoặc dùng Famixa, bạn đồng ý với các điều khoản này. Nếu không đồng ý, vui lòng ngừng sử dụng.',
        ],
      },
      {
        heading: '2. Mô tả dịch vụ',
        body: [
          'Famixa là nền tảng đồng hành AI cho gia đình: hỗ trợ nhịp ngày, đề xuất và công cụ cho phụ huynh. Famixa không phải dịch vụ y tế, tâm lý lâm sàng, hay công cụ giám sát thiết bị (MDM).',
        ],
      },
      {
        heading: '3. Tài khoản & gói',
        body: [
          'Bạn chịu trách nhiệm bảo mật tài khoản. Các gói Free / Growth / Peace / AI+ và quyền năng đi kèm được mô tả trên trang So sánh gói và trong ứng dụng; có thể thay đổi với thông báo hợp lý.',
          'Dùng thử 30 ngày (nếu có) cung cấp quyền Peace trong thời hạn trial theo chính sách thương mại hiện hành.',
        ],
      },
      {
        heading: '4. Nội dung & hành vi',
        body: [
          'Bạn cam kết không lạm dụng dịch vụ, không xâm phạm quyền của người khác, và chỉ dùng Famixa cho mục đích hợp pháp trong gia đình của bạn.',
        ],
      },
      {
        heading: '5. Giới hạn trách nhiệm',
        body: [
          'Dịch vụ được cung cấp “như hiện có”. Trong phạm vi pháp luật cho phép, KIT không chịu trách nhiệm cho thiệt hại gián tiếp phát sinh từ việc sử dụng hoặc không sử dụng được dịch vụ.',
        ],
      },
      {
        heading: '6. Thay đổi điều khoản',
        body: [
          'Chúng tôi có thể cập nhật điều khoản; bản mới sẽ có ngày cập nhật trên trang này. Việc tiếp tục sử dụng sau khi cập nhật đồng nghĩa với việc chấp nhận thay đổi.',
        ],
      },
    ],
  },
};

const en: SpecialtyBundle = {
  plans: {
    seo: {
      title: 'Compare Famixa plans',
      description:
        'Compare Family, Premium, Pro, and Family Plus — international pricing. 30-day trial = Pro entitlements.',
    },
    eyebrow: 'PRICING',
    title: 'Choose the right companion plan',
    lead: 'Start free on Family. Try Pro for 30 days. Upgrade when your family needs deeper evidence and AI coaching.',
    trialNote: '30-day trial = full Pro entitlements. Family (Free) does not require a card.',
    matrixTitle: 'Capability matrix by plan',
    matrixCaption: 'Source: FamilyPlanCapabilityMatrix — aligned with packaging SoT. Display names are international.',
    backCta: 'Back to home',
    appCta: 'Start on Famixa',
    columns: ['Family', 'Premium', 'Pro', 'Family Plus'],
    rows: [
      { label: 'Price / month', free: 'Free', growth: '$4.99', peace: '$9.99', aiPlus: '$19.99' },
      { label: 'Max children', free: '1', growth: '2', peace: 'Unlimited', aiPlus: 'Unlimited' },
      { label: 'Core daily rhythm', free: true, growth: true, peace: true, aiPlus: true },
      { label: 'Weekly insight', free: true, growth: true, peace: true, aiPlus: true },
      { label: 'Family timeline', free: false, growth: true, peace: true, aiPlus: true },
      { label: 'Behavior Twin', free: false, growth: true, peace: true, aiPlus: true },
      { label: 'Adaptive AI suggestions', free: false, growth: true, peace: true, aiPlus: true },
      { label: 'Behavior coaching', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'AI parenting coach', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Growth report', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'In-home screen agreement', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'AI letter + family replay', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Parent success check-in', free: false, growth: false, peace: true, aiPlus: true },
      { label: 'Weekly deep playbook', free: false, growth: false, peace: false, aiPlus: true },
    ],
  },
  stories: {
    seo: {
      title: 'Parent stories with Famixa',
      description: 'Stories from Vietnamese families growing with Famixa — less nagging, more self-driven kids.',
    },
    eyebrow: 'STORIES',
    title: 'What parents say about Famixa',
    lead: 'Not a scoreboard for kids — a gentler family rhythm, and small steps at the right time.',
    appCta: 'Start your family’s journey',
    articles: [
      {
        name: 'Lan Anh',
        place: 'Hanoi',
        title: 'Less nagging — home feels softer',
        body: [
          'Every evening used to be reminders: sleep, homework, put the phone away. Stress spread through the house.',
          'With Famixa I only approve a few short suggestions. The kids became more self-driven; I stopped playing the police.',
        ],
        highlights: ['self-driven', 'less nagging'],
        image: { src: '/images/quote-lan-card.png', alt: 'Lan Anh’s family' },
      },
      {
        name: 'Minh Hoàng',
        place: 'Ho Chi Minh City',
        title: 'Suggestions that fit our home',
        body: [
          'Checklist apps made our child feel managed. Famixa is different: suggestions follow our rhythm, without rankings.',
          'Sometimes one morning prompt is enough to change how we speak to each other.',
        ],
        highlights: ['understands our kids', 'timely'],
        image: { src: '/images/quote-minh-card.png', alt: 'Minh Hoàng’s family' },
      },
      {
        name: 'Thu Thảo',
        place: 'Da Nang',
        title: 'Morning brief as a habit',
        body: [
          'I don’t have much time. A one-item morning brief — designed for ≤ 1 minute — fits.',
          'It’s a small ritual before work and school. Light, but steady.',
        ],
        highlights: ['habit', '≤ 1 minute'],
        image: { src: '/images/quote-thu-card.png', alt: 'Thu Thảo’s family' },
      },
      {
        name: 'Hương',
        place: 'Bac Ninh',
        title: 'Phone agreements, not bans',
        body: [
          'Two kids, different ages. Bans don’t last; in-home agreements do.',
          'Famixa helps us record the agreement and nudge gently — no device MDM, no app locks.',
        ],
        highlights: ['agreement', 'no app locks'],
        image: { src: '/images/quote-lan-card.png', alt: 'Hương’s family' },
      },
      {
        name: 'Tuấn',
        place: 'Can Tho',
        title: 'Progress of the home, not kid scores',
        body: [
          'I feared apps becoming scoreboards. Famixa asks whether home feels lighter — what I actually need.',
          'Letters and replays help us smile together instead of auditing mistakes.',
        ],
        highlights: ['home progress', 'no ranking'],
        image: { src: '/images/quote-minh-card.png', alt: 'Tuấn’s family' },
      },
    ],
  },
  about: {
    seo: {
      title: 'About Famixa',
      description: 'Famixa — AI that helps families grow happier every day. A KIT Technology product.',
    },
    eyebrow: 'ABOUT',
    title: 'Walking with Vietnamese families',
    lead: 'Every Family is Unique. Every Family Can Grow. — There is no single parenting formula for every home.',
    appCta: 'Try Famixa',
    companyHeading: 'Built by',
    blocks: [
      {
        heading: 'Mission',
        body: [
          'Help parents feel lighter, kids grow more self-driven, and families gain quality time — with AI that understands each home, not a checklist app.',
        ],
      },
      {
        heading: 'How we work',
        body: [
          'AI prepares most suggestions; parents decide in a tap or two. We never silently mutate routines behind your back.',
          'No device MDM. Screen time is an in-home agreement. Growth Zone is not a leaderboard.',
        ],
      },
      {
        heading: 'Fami',
        body: [
          'Fami is the companion seed/mascot — brief and coach voice for parents, not a free-form LLM chatbot for children.',
        ],
      },
    ],
  },
  privacy: {
    seo: {
      title: 'Privacy policy',
      description: 'How Famixa and KIT Technology collect, use, and protect family data.',
    },
    eyebrow: 'LEGAL',
    title: 'Privacy policy',
    updated: 'Updated: 30 Jul 2026',
    sections: [
      {
        heading: '1. Scope',
        body: [
          'This policy covers famixa.vn and the Famixa app (home.famixa.vn) operated by KIT Media & Technology Co., Ltd.',
        ],
      },
      {
        heading: '2. Data we process',
        body: [
          'Account details, family profile data you enter, product usage needed to provide the service, and basic technical logs (access logs, analytics cookies if enabled).',
          'Card payments are not collected on the marketing site; checkout happens in-app when applicable.',
        ],
      },
      {
        heading: '3. Purposes',
        body: [
          'Provide and improve Famixa, personalize suggestions for your home, secure accounts, support customers, and meet legal duties.',
          'We do not sell personal data to third parties for advertising.',
        ],
      },
      {
        heading: '4. Storage & security',
        body: [
          'Data is stored on access-controlled infrastructure with appropriate technical and organizational measures.',
          'You control member permissions inside the family on the app.',
        ],
      },
      {
        heading: '5. Your rights',
        body: [
          'You may request access, correction, or deletion as allowed by the product and law via in-app support or company email.',
        ],
      },
      {
        heading: '6. Contact',
        body: [
          'KIT Media & Technology Co., Ltd. — Ho Xuong Rong New Urban Area, Phan Dinh Phung Ward, Thai Nguyen, Vietnam.',
          'Temporary: Facebook page https://www.facebook.com/famixa.vn · Zalo 0984.660.399 · Chat consultation via Zalo.',
        ],
      },
    ],
  },
  terms: {
    seo: {
      title: 'Terms of use',
      description: 'Terms of use for famixa.vn and the Famixa service.',
    },
    eyebrow: 'LEGAL',
    title: 'Terms of use',
    updated: 'Updated: 30 Jul 2026',
    sections: [
      {
        heading: '1. Acceptance',
        body: [
          'By using famixa.vn or Famixa you agree to these terms. If you do not agree, please stop using the service.',
        ],
      },
      {
        heading: '2. Service description',
        body: [
          'Famixa is an AI family companion: daily rhythm, suggestions, and parent tools. It is not clinical care and not device MDM.',
        ],
      },
      {
        heading: '3. Accounts & plans',
        body: [
          'You are responsible for account security. Family / Premium / Pro / Family Plus entitlements are described on the plans page and in-app and may change with reasonable notice.',
          'A 30-day trial, when offered, grants Pro entitlements for the trial period under current commercial policy.',
        ],
      },
      {
        heading: '4. Acceptable use',
        body: [
          'You agree not to abuse the service, infringe others’ rights, or use Famixa for unlawful purposes.',
        ],
      },
      {
        heading: '5. Limitation of liability',
        body: [
          'The service is provided as-is. To the extent permitted by law, KIT is not liable for indirect damages arising from use or inability to use the service.',
        ],
      },
      {
        heading: '6. Changes',
        body: [
          'We may update these terms; the new version will show an updated date on this page. Continued use means acceptance of the changes.',
        ],
      },
    ],
  },
};

const byLocale: Record<AppLocale, SpecialtyBundle> = { vi, en };

export function getSpecialtyContent(locale: AppLocale): SpecialtyBundle {
  return byLocale[locale] ?? byLocale.vi;
}

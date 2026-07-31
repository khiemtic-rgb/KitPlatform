import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(__dirname, '../content/landing.json');

const data = {
  _id: 'landing-vi',
  _type: 'landingPage',
  locale: 'vi',
  appUrl: 'https://home.famixa.vn',
  seo: {
    title: 'Famixa — AI Human Growth OS for Families',
    description:
      'AI giúp con tự giác. Cha mẹ không còn phải nhắc mỗi ngày. Famixa — người bạn đồng hành AI cho gia đình Việt.',
    ogImage: '/images/hero-family.png',
    keywords: ['Famixa', 'AI gia đình', 'nuôi dạy con', 'tự giác', 'Family OS', 'Fami'],
  },
  brand: {
    name: 'Famixa',
    tagline: 'AI giúp gia đình hạnh phúc hơn mỗi ngày',
    logo: '/images/logo-famixa-nav.png',
    logoMark: '/images/logo-famixa-icon.png',
  },
  storyChapters: [
    { id: 'hero', label: 'Mở đầu' },
    { id: 'giai-phap', label: 'Fami' },
    { id: 'ai-coach', label: 'Lắng nghe' },
    { id: 'moments', label: 'Khoảnh khắc' },
    { id: 'growth', label: 'Hành trình' },
    { id: 'method', label: 'Công nghệ' },
    { id: 'video', label: 'Vòng lặp' },
    { id: 'testimonials', label: 'Cha mẹ' },
    { id: 'pricing', label: 'Gói' },
    { id: 'blog', label: 'FAQ' },
    { id: 'final-cta', label: 'Bắt đầu' },
  ],
  nav: {
    links: [
      { href: '#giai-phap', label: 'Giải pháp' },
      { href: '#ai-coach', label: 'AI Coach' },
      { href: '#method', label: '3Q Method' },
      { href: '#growth', label: 'Family Growth' },
      { href: '#blog', label: 'Blog' },
      { href: '#about', label: 'Về Famixa' },
    ],
    login: 'Đăng nhập',
    cta: 'Bắt đầu hành trình',
  },
  hero: {
    titleLine1: 'AI giúp con tự giác.',
    titleLine2: 'Cha mẹ không còn phải nhắc mỗi ngày.',
    lead: 'Famixa là người bạn đồng hành AI thấu hiểu từng gia đình, giúp cha mẹ nhẹ nhõm, con cái tự giác và gia đình có thêm thời gian chất lượng bên nhau.',
    primaryCta: 'Bắt đầu hành trình',
    image: { src: '/images/hero-family.png', alt: 'Gia đình bên Fami — trước khi có Famixa' },
    proof: [
      { title: 'Trial 30 ngày', sub: 'Trải nghiệm Peace', icon: 'sparkles' },
      { title: 'Miễn phí bắt đầu', sub: 'Gói Free sẵn sàng', icon: 'heart' },
      { title: 'Bảo mật', sub: 'Dữ liệu gia đình', icon: 'shield' },
      { title: 'Made in Vietnam', sub: 'Vì gia đình Việt', icon: 'sprout' },
    ],
  },
  chapter1: {
    id: 'giai-phap',
    eyebrow: 'Chương 1',
    title: 'Fami xuất hiện.',
    lead: 'Không phải chatbot dạy con. Fami là hạt mầm đồng hành — nhắc nhẹ, ăn mừng đúng lúc, giúp cả nhà nhìn thấy tiến bộ thật: bớt nhắc, thêm tự giác.',
    cta: 'Xem thêm →',
    ctaHref: '#ai-coach',
    image: { src: '/images/fami-appear.png', alt: 'Fami — hạt mầm linh hồn sáng' },
  },
  chapter2: {
    id: 'ai-coach',
    eyebrow: 'Chương 2',
    title: 'Fami lắng nghe và thấu hiểu.',
    lead: 'Quan sát → thấu hiểu → đề xuất → đồng hành — đúng nhịp từng nhà.',
    steps: [
      { title: 'Quan sát', body: 'Lắng nghe nhịp nhà — không soi, không chấm điểm con.', icon: 'eye' },
      { title: 'Thấu hiểu', body: 'Hiểu vì sao nhắc nhiều / căng thẳng — trước khi đề xuất.', icon: 'heart' },
      { title: 'Đề xuất', body: 'Một việc bố mẹ làm hôm nay — Decision Inbox vài giây.', icon: 'lightbulb' },
      { title: 'Đồng hành', body: 'AI học gia đình bạn — càng dùng càng vừa nhà.', icon: 'sparkles' },
    ],
  },
  chapter3: {
    id: 'moments',
    eyebrow: 'Chương 3',
    title: 'Đồng hành trong mọi khoảnh khắc.',
    lead: 'Một OS cho cả ngày — không phải năm app rời rạc.',
    cta: 'Xem chi tiết →',
    ctaHref: '#method',
    moments: [
      {
        title: 'Giờ đi ngủ',
        body: 'Routine tối nhẹ nhàng — bớt căng, ngủ đúng giờ hơn.',
        image: { src: '/images/moment-sleep.png', alt: 'Giờ đi ngủ với Fami' },
      },
      {
        title: 'Giờ học tập',
        body: 'Con chủ động bắt đầu; bố mẹ ít phải đứng nhắc.',
        image: { src: '/images/moment-study.png', alt: 'Giờ học tập với Fami' },
      },
      {
        title: 'Việc nhà',
        body: 'Mission nhỏ mỗi ngày — thành thói quen, không thành trận chiến.',
        image: { src: '/images/moment-chores.png', alt: 'Việc nhà với Fami' },
      },
      {
        title: 'Thời gian gia đình',
        body: 'Movie Night & khoảnh khắc ấm — phần thưởng cả nhà cùng mở.',
        image: { src: '/images/moment-family.png', alt: 'Thời gian gia đình với Fami' },
      },
    ],
  },
  chapter4: {
    id: 'growth',
    eyebrow: 'Chương 4',
    title: 'Hành trình trưởng thành của gia đình bạn.',
    lead: 'Từ hạt mầm đến cây lớn — Famixa đo tiến bộ nhà, không đo điểm con.',
    cta: 'Xem hành trình →',
    ctaHref: '#pricing',
    image: {
      src: '/images/growth-journey.png',
      alt: 'Hành trình Fami từ hạt mầm đến cây trưởng thành',
    },
    stages: [
      { title: 'Hạt mầm', hint: 'Bắt đầu' },
      { title: 'Nảy mầm', hint: 'Thói quen mới' },
      { title: 'Fami nhỏ', hint: 'Ít nhắc hơn' },
      { title: 'Fami lớn', hint: 'Con tự giác' },
      { title: 'Cây trưởng thành', hint: 'Nhà yên' },
    ],
  },
  chapter5: {
    id: 'method',
    eyebrow: 'Chương 5',
    title: 'Công nghệ tinh tế, trải nghiệm đơn giản.',
    lead: 'Morning Brief · Decision Inbox · Growth Report — trên mọi thiết bị bố mẹ đang dùng.',
    cta: 'Khám phá sản phẩm →',
    image: { src: '/images/devices-mock.png', alt: 'Famixa trên laptop, tablet và điện thoại' },
  },
  chapter6: {
    id: 'video',
    eyebrow: 'CHƯƠNG 6',
    title: 'Vòng lặp tăng trưởng độc quyền của Famixa',
    titleAccent: 'của Famixa',
    lead: 'Fami và gia đình cùng học hỏi, cùng tiến bộ mỗi ngày.',
    cta: 'Tìm hiểu thêm',
    ctaHref: '#pricing',
    image: {
      src: '/images/chapter6-loop-cards.png',
      alt: 'Vòng lặp tăng trưởng Famixa: Quan sát, Thấu hiểu, Đề xuất, Gia đình thực hành, AI học hỏi',
    },
    mascot: { src: '/images/loop/fami-center.png', alt: 'Fami' },
    mascotLabel: 'Fami',
    mascotTagline: 'Growing together',
    loop: [
      { title: 'Quan sát', hint: 'Hành vi & cảm xúc' },
      { title: 'Thấu hiểu', hint: 'Nhu cầu riêng' },
      { title: 'Đề xuất', hint: 'Giải pháp phù hợp' },
      { title: 'Gia đình thực hành', hint: 'Hình thành thói quen' },
      { title: 'AI học hỏi', hint: 'Ngày càng chính xác' },
    ],
  },
  chapter7: {
    id: 'testimonials',
    eyebrow: 'Chương 7',
    title: 'Cha mẹ nói gì về Famixa?',
    lead: 'Giọng pilot — câu chuyện ROI cảm xúc, không phải app chấm việc.',
    quotes: [
      {
        name: 'Chị Lan Anh',
        place: 'Hà Nội',
        text: 'Trước phải nhắc con 5–6 lần mỗi sáng. Giờ Morning Brief chỉ chỉ một việc — nhà đỡ ồn hẳn.',
        image: { src: '/images/quote-lan.png', alt: 'Chị Lan Anh' },
      },
      {
        name: 'Anh Minh Hoàng',
        place: 'TP.HCM',
        text: 'Không phải app chấm sao cho vui. Mình thấy rõ tuần nào bớt căng — đó mới là thứ đáng trả tiền.',
        image: { src: '/images/quote-minh.png', alt: 'Anh Minh Hoàng' },
      },
      {
        name: 'Chị Thu Thảo',
        place: 'Đà Nẵng',
        text: '3 câu tối mất chưa tới một phút. Con cũng thích Movie Night khi cả nhà xong nhiệm vụ.',
        image: { src: '/images/quote-thu.png', alt: 'Chị Thu Thảo' },
      },
    ],
  },
  chapter8: {
    id: 'pricing',
    eyebrow: 'Chương 8',
    title: 'Gói đồng hành cho mọi gia đình.',
    lead: 'Chọn nhịp nhà bạn — bắt đầu miễn phí, nâng cấp khi đã thấy yên hơn.',
    plans: [
      {
        name: 'Free',
        tagline: 'Trải nghiệm Famixa · tối đa 1 trẻ',
        price: '0đ',
        period: '/ vĩnh viễn',
        tone: 'start',
        badge: null,
        cta: 'Bắt đầu miễn phí',
        items: [
          'Nhịp ngày (core routine)',
          'Insight tuần',
          'Tối đa 1 trẻ',
          'Không cần thẻ để bắt đầu',
        ],
      },
      {
        name: 'Growth',
        tagline: 'Family Growth Plan · tối đa 2 trẻ',
        price: '99.000đ',
        period: '/ tháng',
        tone: 'peace',
        badge: null,
        cta: 'Nâng cấp Growth',
        items: [
          'Tất cả Free',
          'Timeline kỷ niệm nhà',
          'Behavior Twin',
          'AI đề xuất thích nghi',
          'Tối đa 2 trẻ',
        ],
      },
      {
        name: 'Peace',
        tagline: 'Family Peace Plan · không giới hạn trẻ',
        price: '199.000đ',
        period: '/ tháng',
        tone: 'growth',
        badge: 'Phổ biến',
        cta: 'Dùng thử 30 ngày',
        items: [
          'Tất cả Growth',
          'AI Parenting Coach',
          'ROP / Growth Report',
          'AI Letter + Family Replay',
          'Thỏa thuận screen trong nhà',
          'Không giới hạn số trẻ',
        ],
      },
      {
        name: 'AI+',
        tagline: 'Đồng hành AI chuyên sâu · không giới hạn trẻ',
        price: '399.000đ',
        period: '/ tháng',
        tone: 'thrive',
        badge: null,
        cta: 'Nâng AI+',
        items: [
          'Tất cả Peace',
          'Weekly Deep Playbook',
          'Letter & Replay sâu hơn',
          'Adaptive scan mở rộng',
          'Không giới hạn số trẻ',
        ],
      },
    ],
  },
  faq: {
    id: 'blog',
    eyebrow: 'FAQ',
    title: 'Câu hỏi thường gặp.',
    lead: 'Ngắn gọn — đúng thứ cha mẹ quan tâm trước khi bắt đầu.',
    items: [
      {
        q: 'Famixa có phải app chấm việc cho con không?',
        a: 'Không. Famixa đo tiến bộ nhà (bớt nhắc, thêm tự giác) — không xếp hạng hay điểm con.',
      },
      {
        q: 'Bố mẹ mất bao nhiêu thời gian mỗi ngày?',
        a: 'Thiết kế cho ≤ 1 phút: Morning Brief một việc cần chú ý, Decision Inbox duyệt trong vài giây.',
      },
      {
        q: 'Fami là AI chat với con sao?',
        a: 'Fami là mascot / hạt mầm đồng hành — không phải chatbot LLM nói chuyện với trẻ. AI hỗ trợ bố mẹ quan sát và đề xuất.',
      },
      {
        q: 'Dữ liệu gia đình có an toàn không?',
        a: 'Có. Dữ liệu được bảo vệ theo chính sách bảo mật của KIT Technology; bạn kiểm soát quyền truy cập trong gia đình.',
      },
      {
        q: 'Gói nào phù hợp để bắt đầu?',
        a: 'Free để trải nghiệm nhịp ngày. Trial 30 ngày = quyền Peace (Pro). Growth khi cần Timeline/Twin; Peace khi cần Coach & chứng cứ; AI+ khi cần Deep Playbook.',
      },
    ],
  },
  finalCta: {
    title: 'Mỗi gia đình là một hành trình. Famixa ở đây để đồng hành từng bước nhỏ.',
    cta: 'Bắt đầu hành trình của bạn',
    image: { src: '/images/cta-campfire.png', alt: 'Gia đình và Fami bên lửa trại' },
  },
  footer: {
    blurb:
      'Đồng hành cùng gia đình Việt — giúp con tự giác, bố mẹ bớt nhắc, nhà yên hơn từng ngày.',
    solutions: [
      { href: '#ai-coach', label: 'AI Coach' },
      { href: '#method', label: '3Q Method' },
      { href: '#growth', label: 'Family Growth' },
      { href: '#pricing', label: 'Gói đồng hành' },
    ],
    resources: [
      { href: '#blog', label: 'FAQ / Blog' },
      { href: '#growth', label: 'Hành trình tăng trưởng' },
      { href: 'https://home.famixa.vn', label: 'Hướng dẫn sử dụng' },
      { href: 'https://novixa.vn/vi', label: 'Novixa (KIT)' },
    ],
    about: [
      { href: '#about', label: 'Về chúng tôi' },
      { href: '#about', label: 'Liên hệ' },
      { href: '#about', label: 'Chính sách bảo mật' },
      { href: '#about', label: 'Điều khoản sử dụng' },
    ],
    newsletter: {
      title: 'Nhận bản tin Famixa',
      lead: 'Mẹo nhà yên · cập nhật sản phẩm · không spam.',
      placeholder: 'Email của bạn',
    },
    company: { name: 'KIT Technology', address: 'Thái Nguyên, Việt Nam' },
    copyrightSuffix: 'Famixa. All rights reserved.',
    madeWith: 'Made with ♥ for Vietnamese families',
  },
};

fs.writeFileSync(out, JSON.stringify(data, null, 2), 'utf8');
console.log('Wrote', out);

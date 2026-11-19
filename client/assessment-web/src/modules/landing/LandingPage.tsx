import { useRef, useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Button, Typography, message } from 'antd';
import { createSubmission, rememberPartnerRef, rememberUtm } from '@/shared/api/assessment.api';
import { getMaturityLevel } from '@/shared/score/score-display';

const { Title, Paragraph, Text } = Typography;

const BENEFITS = [
  { icon: '💸', text: 'Chỗ đang để lọt tiền: hàng hết hạn, hết hàng khi khách hỏi' },
  { icon: '👥', text: 'Khả năng giữ chân khách quen' },
  { icon: '📦', text: 'Rủi ro vận hành & tồn kho' },
  { icon: '🎯', text: 'Ba việc nên làm trước — làm được ngay tuần này' },
  { icon: '🗺️', text: 'Lộ trình cải thiện theo giai đoạn' },
] as const;

// Cập nhật số phút theo thời gian làm bài trung vị đo được từ bài thật.
const QUESTION_COUNT = 31;
const DURATION_LABEL = 'khoảng 7–10 phút';

const PROGRESS_CHIPS = [
  { icon: '📝', label: `${QUESTION_COUNT} câu hỏi` },
  { icon: '⏱', label: DURATION_LABEL },
  { icon: '📄', label: 'Kết quả ngay' },
  { icon: '🔒', label: 'Không đăng ký' },
] as const;

const TRUST_ITEMS = [
  'Không cần đăng ký',
  'Không cần cài đặt',
  'Xem sơ bộ ngay — báo cáo đầy đủ khi bạn muốn nhận',
] as const;

const SAMPLE_SCORE = 62;
const SAMPLE_CATEGORIES = [
  { name: 'Khách hàng', score: 68 },
  { name: 'Kho', score: 54 },
  { name: 'Công nghệ', score: 61 },
] as const;

function SamplePreviewCard({ onStart }: { onStart: () => void }) {
  return (
    <div className="hero-preview" id="sample-preview">
      <Text type="secondary" className="hero-preview-label">
        Mẫu minh họa — không phải kết quả của một nhà thuốc thật
      </Text>
      <div className="hero-preview-card">
        <div className="hero-preview-score">
          <Text type="secondary">Điểm trưởng thành</Text>
          <div className="hero-preview-score-value">
            <span className="hero-preview-number">{SAMPLE_SCORE}</span>
            <span className="hero-preview-max">/ 100</span>
          </div>
          <span className="hero-preview-badge">{getMaturityLevel(SAMPLE_SCORE).label}</span>
        </div>

        <div className="hero-preview-bars">
          {SAMPLE_CATEGORIES.map((cat) => (
            <div key={cat.name} className="hero-preview-bar-row">
              <span className="hero-preview-bar-label">{cat.name}</span>
              <div className="hero-preview-bar">
                <span style={{ width: `${cat.score}%` }} />
              </div>
              <span className="hero-preview-bar-score">{cat.score}</span>
            </div>
          ))}
        </div>

        <div className="hero-preview-insights">
          <div className="hero-preview-insight hero-preview-insight--clear">
            <Text strong>💡 Cơ hội cải thiện khách hàng</Text>
            <Paragraph type="secondary" style={{ margin: '0.25rem 0 0', fontSize: '0.85rem' }}>
              Tập trung hồ sơ khách hàng và chăm sóc sau bán…
            </Paragraph>
          </div>
          <div className="hero-preview-insight hero-preview-insight--blur">
            <Text strong>💡 Đề xuất ưu tiên</Text>
            <Paragraph type="secondary" style={{ margin: '0.25rem 0 0', fontSize: '0.85rem' }}>
              Lộ trình cải thiện theo từng giai đoạn…
            </Paragraph>
          </div>
        </div>

        <Button type="default" block onClick={onStart} className="hero-preview-cta">
          Nhận hồ sơ của bạn
        </Button>
      </div>
    </div>
  );
}

export function LandingPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [loading, setLoading] = useState(false);
  const previewRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    rememberPartnerRef(searchParams.get('ref') ?? searchParams.get('partner'));
    rememberUtm(searchParams);
  }, [searchParams]);

  async function handleStart() {
    setLoading(true);
    try {
      const sub = await createSubmission();
      navigate(`/survey/${sub.id}`);
    } catch {
      message.error('Không thể bắt đầu. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }

  function scrollToPreview() {
    previewRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  return (
    <div className="page-shell landing-page">
      <div className="page-header">
        <span className="brand">Novixa</span>
        <Text type="secondary">vi</Text>
      </div>

      <section className="landing-hero">
        <Title level={2} className="landing-headline">
          Nhà thuốc bán tốt chưa chắc đã vận hành tốt. Anh/chị đang để lọt tiền ở đâu?
        </Title>

        <Paragraph className="landing-sub">
          <Text strong>{QUESTION_COUNT} câu, {DURATION_LABEL}</Text>, miễn phí — biết điểm mạnh, điểm
          nghẽn và <Text strong>ba việc nên làm trước</Text>, việc nào cũng làm được ngay tuần này.
        </Paragraph>

        <Text type="secondary" className="landing-persona">
          Dành cho <Text strong>chủ / quản lý nhà thuốc</Text> (1–5 điểm bán, độc lập hoặc chuỗi
          nhỏ)
        </Text>

        <ul className="landing-benefits">
          {BENEFITS.map((item) => (
            <li key={item.text}>
              <span aria-hidden>{item.icon}</span>
              {item.text}
            </li>
          ))}
        </ul>

        <div className="landing-chips">
          {PROGRESS_CHIPS.map((chip) => (
            <span key={chip.label} className="landing-chip">
              <span aria-hidden>{chip.icon}</span> {chip.label}
            </span>
          ))}
        </div>

        <div className="landing-cta-block">
          <Button
            type="primary"
            size="large"
            onClick={handleStart}
            loading={loading}
            block
            className="landing-cta-primary"
          >
            Nhận Hồ sơ phát triển miễn phí
          </Button>
          <button type="button" className="landing-cta-secondary" onClick={scrollToPreview}>
            Xem mẫu kết quả
          </button>
        </div>

        <ul className="landing-trust">
          {TRUST_ITEMS.map((item) => (
            <li key={item}>
              <span className="landing-trust-check">✓</span> {item}
            </li>
          ))}
        </ul>

        <Text type="secondary" className="landing-social-proof">
          Đang pilot cùng nhà thuốc độc lập — tham gia đánh giá sớm để nhận hồ sơ miễn phí
        </Text>
      </section>

      <div ref={previewRef}>
        <SamplePreviewCard onStart={handleStart} />
      </div>
    </div>
  );
}

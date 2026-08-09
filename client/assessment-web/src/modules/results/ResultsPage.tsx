import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { Button, Spin, message } from 'antd';
import {
  completeSubmission,
  getSubmission,
  type CategoryScore,
  type CompleteResult,
} from '@/shared/api/assessment.api';
import {
  annotateInsightText,
  getMaturityLevel,
  toScore100,
} from '@/shared/score/score-display';

const CATEGORY_META: Record<string, { icon: string; desc: string; tone: string }> = {
  BUSINESS: {
    icon: 'biz',
    tone: 'teal',
    desc: 'Doanh thu, khuyến mại và mục tiêu kinh doanh',
  },
  TECH: {
    icon: 'tech',
    tone: 'violet',
    desc: 'Phần mềm, dữ liệu và bảng điều khiển vận hành',
  },
  INVENTORY: {
    icon: 'stock',
    tone: 'amber',
    desc: 'Tồn kho, hạn dùng, nhập hàng và kiểm kê',
  },
  CUSTOMER: {
    icon: 'customer',
    tone: 'blue',
    desc: 'Hồ sơ khách hàng, thân thiết và chăm sóc sau bán',
  },
  OPERATIONS: {
    icon: 'ops',
    tone: 'rose',
    desc: 'Quy trình làm việc, phân quyền và chuẩn vận hành',
  },
  GROWTH: {
    icon: 'growth',
    tone: 'indigo',
    desc: 'Kế hoạch mở rộng và phát triển dài hạn',
  },
};

function Icon({ d, size = 16 }: { d: string; size?: number }) {
  return (
    <svg viewBox="0 0 24 24" width={size} height={size} fill="none" aria-hidden>
      <path d={d} stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

const ICON_PATH: Record<string, string> = {
  biz: 'M4 19V5M4 19h16M8 15V10M12 15V7M16 15v-3',
  tech: 'M4 7h16v10H4V7zm4 13h8M12 17v3M8 11h2M14 11h2',
  stock: 'M4 8h16l-1.5 11H5.5L4 8zm4-4h8l1 4H7l1-4z',
  customer: 'M12 12a4 4 0 100-8 4 4 0 000 8zM5 20c1.5-3.2 4-5 7-5s5.5 1.8 7 5',
  ops: 'M5 7h14M5 12h14M5 17h9',
  growth: 'M4 19V5M4 19h16M8 15l3-4 3 2 4-6',
  pdf: 'M7 3h7l5 5v13a1 1 0 01-1 1H7a1 1 0 01-1-1V4a1 1 0 011-1zm7 0v5h5',
  send: 'M4 12l16-7-7 16-2-6-7-3z',
  lock: 'M7 11V8a5 5 0 0110 0v3M6 11h12v10H6V11z',
  trophy: 'M8 4h8v5a4 4 0 01-8 0V4zM8 5H5a2 2 0 000 4h3M16 5h3a2 2 0 010 4h-3M10 20h4M12 13v7',
  star: 'M12 3l2.2 5.8L20 10l-4.2 3.5L17 20l-5-3.2L7 20l1.2-6.5L4 10l5.8-1.2L12 3z',
  bulb: 'M9 18h6M10 21h4M12 3a6 6 0 00-3 11.2V16h6v-1.8A6 6 0 0012 3z',
  link: 'M10 13a5 5 0 007.07 0l1.41-1.41a5 5 0 00-7.07-7.07L10 5.93M14 11a5 5 0 00-7.07 0L5.5 12.4a5 5 0 007.07 7.07L14 18.07',
  report: 'M7 3h8l4 4v14a1 1 0 01-1 1H7a1 1 0 01-1-1V4a1 1 0 011-1zM9 12h6M9 16h4',
};

function Gauge({
  score100,
  label,
  color,
}: {
  score100: number;
  label: string;
  color: string;
}) {
  const radius = 70;
  const halfLen = Math.PI * radius;
  const fill = Math.max(0, Math.min(100, score100)) / 100;
  const dash = halfLen * fill;

  return (
    <div className="results-gauge" aria-label={`Điểm tổng ${score100} trên 100`}>
      <svg viewBox="0 0 180 118" className="results-gauge__svg">
        <path
          d="M20 95 A70 70 0 0 1 160 95"
          fill="none"
          stroke="#e4e6eb"
          strokeWidth="14"
          strokeLinecap="round"
          pathLength={halfLen}
        />
        <path
          d="M20 95 A70 70 0 0 1 160 95"
          fill="none"
          stroke={color}
          strokeWidth="14"
          strokeLinecap="round"
          pathLength={halfLen}
          strokeDasharray={`${dash} ${halfLen}`}
        />
      </svg>
      <div className="results-gauge__center">
        <p className="results-gauge__caption">Điểm trưởng thành tổng (thang 100)</p>
        <p className="results-gauge__value">
          <strong style={{ color }}>{score100}</strong>
          <span>/100</span>
        </p>
        <span className="results-gauge__badge" style={{ background: `${color}22`, color }}>
          {label}
        </span>
      </div>
      <p className="results-gauge__note">0 = sơ khai · 100 = trưởng thành cao nhất</p>
    </div>
  );
}

function CategoryIcon({ code }: { code: string }) {
  const meta = CATEGORY_META[code];
  const path = ICON_PATH[meta?.icon ?? 'biz'] ?? ICON_PATH.biz;
  return (
    <span className="results-cat__icon" data-tone={meta?.tone ?? 'teal'}>
      <Icon d={path} size={18} />
    </span>
  );
}

function ResultsArt() {
  return (
    <svg className="results-cta__art" viewBox="0 0 220 150" role="img" aria-label="Minh họa báo cáo">
      <rect x="28" y="22" width="120" height="96" rx="10" fill="#fff" stroke="#0f766e" strokeWidth="2" />
      <rect x="42" y="38" width="92" height="12" rx="3" fill="#ccfbf1" />
      <rect x="42" y="58" width="60" height="8" rx="2" fill="#99f6e4" />
      <rect x="42" y="72" width="76" height="8" rx="2" fill="#e2e8f0" />
      <rect x="42" y="86" width="48" height="8" rx="2" fill="#e2e8f0" />
      <circle cx="168" cy="88" r="28" fill="#d1fae5" stroke="#0f766e" strokeWidth="2" />
      <circle cx="168" cy="88" r="12" fill="none" stroke="#0f766e" strokeWidth="2" />
      <path d="M178 98l12 12" stroke="#0f766e" strokeWidth="3" strokeLinecap="round" />
      <rect x="150" y="28" width="36" height="28" rx="6" fill="#fef3c7" stroke="#f59e0b" strokeWidth="1.5" />
      <circle cx="56" cy="128" r="8" fill="#86efac" />
      <rect x="50" y="128" width="12" height="16" fill="#16a34a" />
    </svg>
  );
}

export function ResultsPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const [result, setResult] = useState<CompleteResult | null>(
    (location.state as { result?: CompleteResult } | null)?.result ?? null,
  );
  const [loading, setLoading] = useState(!result);

  useEffect(() => {
    if (result) return;

    let cancelled = false;
    (async () => {
      try {
        const sub = await getSubmission(id);
        if (sub.status === 'lead_captured' || sub.status === 'report_ready') {
          navigate(`/report/${id}`, { replace: true });
          return;
        }
        if (sub.overallScore != null && sub.categoryScores) {
          if (!cancelled) {
            setResult({
              status: sub.status,
              overallScore: sub.overallScore,
              overallPct: sub.overallPct ?? 0,
              categoryScores: sub.categoryScores,
              previewInsights: sub.previewInsights ?? [],
              reportLocked: true,
              leadCaptureRequired: true,
            });
          }
          return;
        }
        const data = await completeSubmission(id);
        if (!cancelled) setResult(data);
      } catch {
        message.error('Không tải được kết quả.');
        navigate('/', { replace: true });
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id, navigate, result]);

  const score100 = result ? toScore100(result.overallPct) : 0;
  const level = getMaturityLevel(score100);
  const categories = useMemo(() => {
    if (!result) return [] as CategoryScore[];
    return [...result.categoryScores].sort((a, b) => b.scorePct - a.scorePct);
  }, [result]);

  const insightCards = result?.previewInsights.slice(0, 2) ?? [];

  if (loading || !result) {
    return (
      <div className="results-page" style={{ textAlign: 'center', paddingTop: '4rem' }}>
        <Spin size="large" tip="Đang tính điểm..." />
      </div>
    );
  }

  function goUnlock() {
    navigate(`/results/${id}/unlock`);
  }

  return (
    <div className="results-page">
      <header className="results-topbar">
        <a className="results-brand" href="https://novixa.vn/vi/">
          <span className="results-brand__mark" aria-hidden>
            N
          </span>
          <span>Novixa</span>
        </a>
        <button type="button" className="results-pdf-btn" onClick={goUnlock}>
          <Icon d={ICON_PATH.pdf} />
          Tải báo cáo (PDF)
        </button>
      </header>

      <section className="results-hero">
        <h1>Kết quả sơ bộ</h1>
        <p>Điểm được quy đổi về thang 100 để dễ so sánh giữa các nhóm năng lực.</p>
      </section>

      <div className="results-summary">
        <article className="results-card results-card--gauge">
          <Gauge score100={score100} label={level.label} color={level.color} />
        </article>
        <article className="results-card results-card--done">
          <div className="results-done">
            <span className="results-done__icon" aria-hidden>
              <Icon d={ICON_PATH.trophy} size={22} />
            </span>
            <div>
              <h2>Bạn đã hoàn thành khảo sát!</h2>
              <p>
                Điểm dưới đây phản ánh mức độ trưởng thành theo từng nhóm năng lực của nhà thuốc —
                dùng để ưu tiên việc cần cải thiện trước.
              </p>
            </div>
          </div>
          <div className="results-tip">
            <Icon d={ICON_PATH.star} size={16} />
            <p>
              <strong>Gợi ý:</strong> Hãy cải thiện từng nhóm để nâng điểm tổng lên mức Khá (≥ 70) và
              Tốt (≥ 85).
            </p>
          </div>
        </article>
      </div>

      <section className="results-card results-detail">
        <h2 className="results-detail__title">Chi tiết theo nhóm năng lực</h2>
        <div className="results-table-head">
          <span>Nhóm năng lực</span>
          <span>Mức độ</span>
          <span>Điểm</span>
        </div>
        <ul className="results-table">
          {categories.map((item) => {
            const s = toScore100(item.scorePct);
            const lv = getMaturityLevel(s);
            const meta = CATEGORY_META[item.code];
            return (
              <li key={item.code} className="results-table__row">
                <div className="results-cat">
                  <CategoryIcon code={item.code} />
                  <div>
                    <strong>{item.name}</strong>
                    <p>{meta?.desc ?? 'Nhóm năng lực trong hồ sơ phát triển nhà thuốc'}</p>
                  </div>
                </div>
                <div className="results-bar" aria-hidden>
                  <span style={{ width: `${s}%`, background: lv.color }} />
                </div>
                <div className="results-score">
                  <strong>
                    {s}
                    <span>/100</span>
                  </strong>
                  <em style={{ color: lv.color }}>{lv.label}</em>
                </div>
              </li>
            );
          })}
        </ul>
      </section>

      <section className="results-insights">
        {insightCards.map((insight, idx) => (
          <article
            key={insight.title}
            className="results-insight"
            data-tone={idx === 0 ? 'amber' : 'blue'}
          >
            <span className="results-insight__icon" aria-hidden>
              <Icon d={idx === 0 ? ICON_PATH.bulb : ICON_PATH.link} size={18} />
            </span>
            <h3>{annotateInsightText(insight.title)}</h3>
            <p>{annotateInsightText(insight.body)}</p>
          </article>
        ))}
        <article className="results-insight" data-tone="teal">
          <span className="results-insight__icon" aria-hidden>
            <Icon d={ICON_PATH.report} size={18} />
          </span>
          <h3>Báo cáo đầy đủ + tư vấn cải thiện</h3>
          <p>
            Nhận PDF chi tiết, so sánh ngành và lộ trình 30–60–90 ngày sau khi để lại thông tin liên
            hệ.
          </p>
          <button type="button" className="results-insight__link" onClick={goUnlock}>
            Nhận báo cáo →
          </button>
        </article>
      </section>

      <section className="results-cta">
        <ResultsArt />
        <div className="results-cta__copy">
          <h2>Nhận báo cáo chi tiết &amp; tư vấn miễn phí</h2>
          <p>
            Báo cáo gồm phân tích chuyên sâu, so sánh ngành và lộ trình cải thiện 30 – 60 – 90 ngày.
          </p>
          <Button
            type="primary"
            size="large"
            className="results-cta__btn"
            icon={<Icon d={ICON_PATH.send} size={18} />}
            onClick={goUnlock}
          >
            Nhận báo cáo chi tiết ngay
          </Button>
          <p className="results-cta__trust">
            <Icon d={ICON_PATH.lock} size={14} />
            Thông tin của bạn được bảo mật tuyệt đối
          </p>
        </div>
      </section>

      <div className="results-skip">
        <Link to="/thank-you">← Bỏ qua — chỉ xem sơ bộ</Link>
      </div>
    </div>
  );
}

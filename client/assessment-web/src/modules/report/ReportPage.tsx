import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Collapse, Spin, message } from 'antd';
import {
  fetchReport,
  fetchReportPdf,
  fetchTemplate,
  getSubmission,
  triggerPdfDownload,
  type FullReport,
  type OwnerPack,
} from '@/shared/api/assessment.api';
import { answersByCode, buildOwnerReport, type OwnerReport } from '@/shared/report/owner-report';
import { getMaturityLevel, toScore100 } from '@/shared/score/score-display';
import { ReportIntelligenceSections } from '@/modules/report/ReportIntelligenceSections';

function Icon({ d, size = 16 }: { d: string; size?: number }) {
  return (
    <svg viewBox="0 0 24 24" width={size} height={size} fill="none" aria-hidden>
      <path d={d} stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

const I = {
  pdf: 'M12 4v10m0 0l-3.5-3.5M12 14l3.5-3.5M5 18h14',
  send: 'M4 12l16-7-7 16-2-6-7-3z',
  mail: 'M4 6h16v12H4V6zm0 0l8 7 8-7',
  thumb: 'M8 11v9H5a1 1 0 01-1-1v-6a1 1 0 011-1h3zm0 0l3-6a2 2 0 012-1h1v5h4.5a2 2 0 011.9 2.6l-1.4 4A2 2 0 0117.1 20H8',
  star: 'M12 3l2.2 5.8L20 10l-4.2 3.5L17 20l-5-3.2L7 20l1.2-6.5L4 10l5.8-1.2L12 3z',
  clock: 'M12 4a8 8 0 100 16 8 8 0 000-16zm0 4v4l3 2',
  chevron: 'M9 6l6 6-6 6',
  headset: 'M4 14v-1a4 4 0 014-4h1M19 14v-1a4 4 0 00-4-4h-1M8 14h8v2a3 3 0 01-3 3h-2a3 3 0 01-3-3v-2z',
};

function Gauge({
  score100,
  label,
  color,
  note = '0 = sơ khai · 100 = trưởng thành cao nhất',
}: {
  score100: number;
  label: string;
  color: string;
  note?: string;
}) {
  const halfLen = Math.PI * 70;
  const dash = halfLen * (Math.max(0, Math.min(100, score100)) / 100);
  return (
    <div className="report-gauge">
      <svg viewBox="0 0 180 118" className="report-gauge__svg">
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
      <div className="report-gauge__center">
        <p className="report-gauge__value">
          <strong style={{ color }}>{score100}</strong>
          <span>/100</span>
        </p>
        <span className="report-gauge__badge" style={{ background: `${color}22`, color }}>
          {label}
        </span>
      </div>
      <p className="report-gauge__note">{note}</p>
    </div>
  );
}

function PharmacyArt() {
  return (
    <svg className="report-hero__art" viewBox="0 0 220 140" role="img" aria-hidden>
      <rect width="220" height="140" rx="16" fill="#d1fae5" opacity="0.45" />
      <rect x="55" y="38" width="110" height="78" rx="10" fill="#fff" stroke="#0f766e" strokeWidth="2" />
      <rect x="72" y="54" width="76" height="30" rx="6" fill="#ecfeff" stroke="#14b8a6" strokeWidth="2" />
      <path d="M100 62h20M110 54v24" stroke="#0f766e" strokeWidth="3" strokeLinecap="round" />
      <rect x="90" y="92" width="40" height="24" rx="3" fill="#0f766e" />
      <text x="110" y="30" textAnchor="middle" fill="#0f766e" fontSize="11" fontWeight="700" fontFamily="system-ui,Segoe UI,sans-serif">
        PHARMACY
      </text>
    </svg>
  );
}

function scoreOverTen(pct: number | undefined): string | null {
  if (pct == null || Number.isNaN(pct)) return null;
  const v = Math.round((Math.max(0, Math.min(100, pct)) / 10) * 10) / 10;
  return `${v % 1 === 0 ? v.toFixed(0) : v.toFixed(1)}/10`;
}

function OwnerPackView({
  pack,
  categoryScoreByCode,
}: {
  pack: OwnerPack;
  categoryScoreByCode: Map<string, number>;
}) {
  return (
    <section className="report-card report-owner">
      <p className="report-owner__eyebrow">Đánh giá chi tiết nhà thuốc</p>
      <h2>Kết quả tóm tắt</h2>
      <p className="report-owner__summary">
        <strong>
          {Math.round(pack.overallScorePct)}/100 · {pack.maturityLabel}
        </strong>
      </p>
      <p className="report-owner__headline">{pack.overallHeadline}</p>

      <div className="report-split">
        <article className="report-box report-box--good">
          <h3>
            <span className="report-box__icon" aria-hidden>
              <Icon d={I.thumb} size={16} />
            </span>
            Đang làm tốt
          </h3>
          <ul>
            {pack.strengths.slice(0, 3).map((s) => {
              const score = s.areaCode ? scoreOverTen(categoryScoreByCode.get(s.areaCode)) : null;
              return (
                <li key={s.title}>
                  <div className="report-box__row">
                    <strong>{s.title}</strong>
                    {score ? <em>{score}</em> : null}
                  </div>
                  <p>{s.body}</p>
                </li>
              );
            })}
          </ul>
        </article>

        <article className="report-box report-box--pain">
          <h3>
            <span className="report-box__icon" aria-hidden>
              <Icon d={I.star} size={16} />
            </span>
            Nỗi đau / cơ hội lớn
          </h3>
          <ol>
            {pack.pains.slice(0, 3).map((p) => {
              const score = p.areaCode ? scoreOverTen(categoryScoreByCode.get(p.areaCode)) : null;
              return (
                <li key={p.title}>
                  <div className="report-box__row">
                    <strong>{p.title}</strong>
                    {score ? <em>{score}</em> : null}
                  </div>
                  <p>→ {p.businessConsequence}</p>
                </li>
              );
            })}
          </ol>
        </article>
      </div>

      <div className="report-priority">
        <h3>
          <Icon d={I.clock} size={16} />
          Nên làm trước (30 ngày)
        </h3>
        <p>{pack.oneThingFirst}</p>
      </div>

      {pack.actions30Days.length > 0 ? (
        <div className="report-actions">
          <h3>Việc làm trong 30 ngày</h3>
          <ul>
            {pack.actions30Days.map((a, idx) => (
              <li key={a.title}>
                <span className="report-actions__icon" data-i={idx % 3} aria-hidden>
                  <Icon d={idx % 3 === 0 ? I.headset : idx % 3 === 1 ? I.thumb : I.star} size={18} />
                </span>
                <div>
                  <strong>{a.title}</strong>
                  <p>
                    Ưu tiên: {a.who} · Thời gian: {a.when}
                  </p>
                  <p className="report-actions__done">Xong khi: {a.doneWhen}</p>
                </div>
                <Icon d={I.chevron} size={16} />
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <div className="report-pilot">
        <span className="report-pilot__art" aria-hidden>
          <Icon d={I.headset} size={28} />
        </span>
        <div>
          <h3>Nếu muốn có người đồng hành</h3>
          <p>{pack.pilotHinge.howToTalk}</p>
          <p>
            <strong>Gợi ý Pilot:</strong> {pack.pilotHinge.recommendedFocus}
          </p>
          <p className="report-pilot__cta">{pack.nextStepCta}</p>
        </div>
      </div>
    </section>
  );
}

const HOTLINE = { tel: '+84984660399', label: '0984.660.399' };

/** Báo cáo cho chủ nhà thuốc (bài PHARMACY_V2): dựng từ câu trả lời, đọc trong vài phút. */
function OwnerReportView({ owner, score100 }: { owner: OwnerReport; score100: number }) {
  const level = getMaturityLevel(score100);
  return (
    <div className="report-page or-page">
      <header className="report-topbar or-noprint">
        <a className="report-brand" href="https://novixa.vn/vi/" aria-label="Novixa">
          <img className="report-brand__logo" src="/logo.png" alt="Novixa" width="140" height="40" />
        </a>
        <button type="button" className="report-pdf-btn" onClick={() => window.print()}>
          <span className="report-pdf-btn__icon" aria-hidden>
            <Icon d={I.pdf} size={16} />
          </span>
          <span className="report-pdf-btn__text">
            <strong>In hoặc lưu PDF</strong>
            <small>Chọn "Lưu dưới dạng PDF" khi in</small>
          </span>
        </button>
      </header>

      <section className="report-card report-score or-hero">
        <div className="report-score__grid">
          <Gauge score100={score100} label={level.label} color={level.color} note="Điểm càng cao, nhà thuốc càng ít phụ thuộc vào trí nhớ và sự có mặt của chủ" />
          <div className="report-score__copy">
            <p className="report-owner__eyebrow">Báo cáo cho nhà thuốc</p>
            <h1 className="or-headline">{owner.headline}</h1>
          </div>
        </div>
      </section>

      {owner.leaks.length > 0 ? (
        <section className="report-card or-section">
          <h2>Nhà thuốc đang mất tiền ở đâu</h2>
          <div className="or-leaks">
            {owner.leaks.map((l) => (
              <article key={l.code} className="or-leak">
                <h3>{l.title}</h3>
                <p className="or-said">{l.said}</p>
                <p>{l.body}</p>
              </article>
            ))}
          </div>
        </section>
      ) : null}

      {owner.concern ? (
        <section className="report-card or-section or-concern">
          <h2>Điều anh/chị lo nhất: {owner.concern.label.toLowerCase()}</h2>
          <p>{owner.concern.text}</p>
        </section>
      ) : null}

      {owner.actions.length > 0 ? (
        <section className="report-card or-section">
          <h2>{owner.actions.length === 3 ? '3 việc nên làm trước' : 'Việc nên làm trước'}</h2>
          <ol className="or-actions">
            {owner.actions.map((a, idx) => (
              <li key={a.code} className="or-action">
                <span className="or-action__num" aria-hidden>
                  {idx + 1}
                </span>
                <div className="or-action__body">
                  <h3>{a.title}</h3>
                  <p className="or-said">{a.said}</p>
                  <p>{a.why}</p>
                  <p className="or-label">Cách làm</p>
                  <ul className="or-steps">
                    {a.steps.map((s) => (
                      <li key={s}>{s}</li>
                    ))}
                  </ul>
                  <dl className="or-meta">
                    <div>
                      <dt>Ai làm</dt>
                      <dd>{a.who}</dd>
                    </div>
                    <div>
                      <dt>Mất bao lâu</dt>
                      <dd>{a.time}</dd>
                    </div>
                    <div>
                      <dt>Sau 30 ngày xem lại</dt>
                      <dd>{a.check}</dd>
                    </div>
                  </dl>
                  {a.tool ? (
                    <p className="or-tool">
                      {owner.hasSoftware ? <strong>Với phần mềm đang dùng: </strong> : null}
                      {a.tool}
                    </p>
                  ) : null}
                </div>
              </li>
            ))}
          </ol>
        </section>
      ) : null}

      {owner.strengths.length > 0 ? (
        <section className="report-card or-section">
          <h2>Anh/chị đang làm tốt</h2>
          <ul className="or-list or-list--good">
            {owner.strengths.map((s) => (
              <li key={s}>{s}</li>
            ))}
          </ul>
        </section>
      ) : null}

      {owner.watch.length > 0 ? (
        <section className="report-card or-section">
          <h2>Nên bắt đầu để ý</h2>
          <ul className="or-list">
            {owner.watch.map((s) => (
              <li key={s}>{s}</li>
            ))}
          </ul>
        </section>
      ) : null}

      <section className="report-card or-section">
        <h2>Điểm từng phần</h2>
        <p className="or-note">
          Thang 100: dưới 40 Cần cải thiện · 40–59 Cơ bản · 60–79 Khá tốt · từ 80 Xuất sắc.
        </p>
        <ul className="or-areas">
          {owner.areas.map((a) => {
            const lv = a.pct == null ? null : getMaturityLevel(a.pct);
            return (
              <li key={a.code}>
                <span className="or-areas__name">{a.name}</span>
                {a.pct == null || !lv ? (
                  <span className="or-areas__note">{a.note}</span>
                ) : (
                  <>
                    <span className="or-areas__bar" aria-hidden>
                      <span style={{ width: `${a.pct}%`, background: lv.color }} />
                    </span>
                    <span className="or-areas__score">
                      <strong>{a.pct}</strong>/100 <em style={{ color: lv.color }}>{lv.label}</em>
                    </span>
                  </>
                )}
              </li>
            );
          })}
        </ul>
      </section>

      <section className="report-card or-section or-cta">
        <h2>Muốn có người cùng làm?</h2>
        <p>
          Novixa có thể gọi 10–15 phút để cùng anh/chị chọn việc làm trước và cách làm cho hợp với nhà thuốc.
          Không bắt buộc.
        </p>
        <p className="or-cta__phone">
          Gọi hoặc nhắn: <a href={`tel:${HOTLINE.tel}`}>{HOTLINE.label}</a>
        </p>
      </section>

      <div className="report-done or-noprint">
        <Link to="/thank-you">Hoàn tất</Link>
      </div>
    </div>
  );
}

export function ReportPage() {
  const { id = '' } = useParams();
  const [report, setReport] = useState<FullReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [downloadingOwner, setDownloadingOwner] = useState(false);
  const [downloadingFull, setDownloadingFull] = useState(false);
  const [owner, setOwner] = useState<OwnerReport | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await fetchReport(id);
        if (data.templateCode === 'PHARMACY_V2') {
          try {
            const [sub, tpl] = await Promise.all([getSubmission(id), fetchTemplate(data.templateCode)]);
            const built = buildOwnerReport(tpl, answersByCode(tpl, sub.responses), data.categoryScores, data.overallPct);
            if (!cancelled) setOwner(built);
          } catch {
            // Không dựng được thì dùng bản báo cáo cũ bên dưới.
          }
        }
        if (!cancelled) setReport(data);
      } catch {
        message.error('Báo cáo chưa mở khóa hoặc session hết hạn.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id]);

  const score100 = report ? toScore100(report.overallPct) : 0;
  const level = getMaturityLevel(score100);
  const ownerPack = report?.intelligence?.ownerPack ?? null;
  const categoryScoreByCode = useMemo(() => {
    const map = new Map<string, number>();
    for (const c of report?.categoryScores ?? []) {
      if (!c.insufficientData) map.set(c.code, c.scorePct);
    }
    return map;
  }, [report]);

  async function downloadPdf(kind: 'owner' | 'consulting', fileName: string) {
    if (!report?.pdf.available) return;
    const setLoadingPdf = kind === 'owner' ? setDownloadingOwner : setDownloadingFull;
    setLoadingPdf(true);
    try {
      const blob = await fetchReportPdf(id, kind);
      triggerPdfDownload(blob, fileName);
      message.success(kind === 'owner' ? 'Đã tải bản đề xuất.' : 'Đã tải báo cáo tư vấn đầy đủ.');
    } catch (err: unknown) {
      const msg =
        err instanceof Error
          ? err.message
          : 'Không tải được PDF. Vui lòng thử lại hoặc kiểm tra kết nối mạng.';
      message.error(msg);
    } finally {
      setLoadingPdf(false);
    }
  }

  if (loading) {
    return (
      <div className="report-page" style={{ textAlign: 'center', paddingTop: '4rem' }}>
        <Spin size="large" />
      </div>
    );
  }

  if (!report) {
    return (
      <div className="report-page">
        <p>Không tải được báo cáo.</p>
        <Link to={`/results/${id}/unlock`}>Mở khóa báo cáo</Link>
      </div>
    );
  }

  if (owner) return <OwnerReportView owner={owner} score100={score100} />;

  return (
    <div className="report-page">
      <header className="report-topbar">
        <a className="report-brand" href="https://novixa.vn/vi/" aria-label="Novixa">
          <img className="report-brand__logo" src="/logo.png" alt="Novixa" width="140" height="40" />
        </a>
        {report.pdf.available ? (
          <button
            type="button"
            className="report-pdf-btn"
            disabled={downloadingOwner}
            onClick={() => void downloadPdf('owner', `danh-gia-nha-thuoc-${id.slice(0, 8)}.pdf`)}
          >
            <span className="report-pdf-btn__icon" aria-hidden>
              <Icon d={I.pdf} size={16} />
            </span>
            <span className="report-pdf-btn__text">
              <strong>{downloadingOwner ? 'Đang tạo PDF…' : 'Tải báo cáo PDF'}</strong>
              <small>Bản đề xuất khuyến nghị</small>
            </span>
          </button>
        ) : null}
      </header>

      <section className="report-hero">
        <div className="report-hero__copy">
          <span className="report-hero__badge">Báo cáo đánh giá nhà thuốc</span>
          <h1>Kết quả đánh giá nhà thuốc</h1>
          <p>Bản đề xuất dành riêng cho nhà thuốc · {report.templateCode}</p>
        </div>
        <PharmacyArt />
      </section>

      <section className="report-card report-score">
        <div className="report-score__grid">
          <Gauge score100={score100} label={ownerPack?.maturityLabel ?? level.label} color={level.color} />
          <div className="report-score__copy">
            <h2>Điểm trưởng thành tổng (thang 100)</h2>
            <p>
              {ownerPack?.overallHeadline ??
                `Nhà thuốc đang ở mức ${ownerPack?.maturityLabel ?? level.label}. Đây là cơ sở để ưu tiên cải thiện đúng việc trong 30 ngày tới.`}
            </p>
          </div>
        </div>
      </section>

      {ownerPack ? (
        <OwnerPackView pack={ownerPack} categoryScoreByCode={categoryScoreByCode} />
      ) : (
        <section className="report-card">
          <p>Báo cáo chi tiết đang được xử lý. Vui lòng tải PDF hoặc tải lại trang sau vài giây.</p>
        </section>
      )}

      {report.pdf.available ? (
        <section className="report-downloads">
          <button
            type="button"
            className="report-dl report-dl--primary"
            disabled={downloadingOwner}
            onClick={() => void downloadPdf('owner', `danh-gia-nha-thuoc-${id.slice(0, 8)}.pdf`)}
          >
            <span className="report-dl__icon" aria-hidden>
              <Icon d={I.send} size={18} />
            </span>
            <span>
              <strong>{downloadingOwner ? 'Đang tạo PDF…' : 'Tải bản đề xuất (khuyến nghị)'}</strong>
              <small>Nhận báo cáo chi tiết dạng PDF đầy đủ</small>
            </span>
          </button>
          <button
            type="button"
            className="report-dl report-dl--secondary"
            disabled={downloadingFull}
            onClick={() => void downloadPdf('consulting', `kap-bao-cao-day-du-${id.slice(0, 8)}.pdf`)}
          >
            <span className="report-dl__icon" aria-hidden>
              <Icon d={I.mail} size={18} />
            </span>
            <span>
              <strong>{downloadingFull ? 'Đang tạo PDF đầy đủ…' : 'Tải báo cáo tư vấn đầy đủ (tuỳ chọn)'}</strong>
              <small>Nhận bản tư vấn chi tiết &amp; kế hoạch 30 ngày</small>
            </span>
          </button>
        </section>
      ) : null}

      {report.intelligence ? (
        <div className="report-collapse">
          <Collapse
            items={[
              {
                key: 'full',
                label: 'Xem phân tích chi tiết hơn (biểu đồ & tư vấn đầy đủ)',
                children: (
                  <ReportIntelligenceSections report={report} intelligence={report.intelligence} />
                ),
              },
            ]}
          />
        </div>
      ) : null}

      <div className="report-done">
        <Link to="/thank-you">Hoàn tất</Link>
      </div>
    </div>
  );
}

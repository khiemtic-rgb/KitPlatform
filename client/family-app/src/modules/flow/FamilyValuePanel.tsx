import { useEffect, useMemo, useState } from 'react';
import type {
  AccountabilityGlance,
  DayFlow,
  FamilyAiLetter,
  FamilyAiWinsDigest,
  FamilyDnaCard,
  FamilyMemoryEntry,
  FamilyReplay,
  FamilySubscription,
  FamilyWeeklyInsight,
  ParentAchievements,
  ParentSuccessCheckin,
  ParentSuccessRop,
} from '@/shared/api/family-os.api';
import {
  fetchWeeklyInsight,
  fetchParentSuccessRop,
  formatParentSuccessRopShare,
  fetchFamilyAiWinsDigest,
  fetchFamilyAiLetter,
  formatFamilyAiLetterShare,
  fetchFamilyMemories,
  fetchParentAchievements,
  fetchFamilyReplay,
  fetchFamilySubscription,
  upsertParentSuccessEveningCheckin,
} from '@/shared/api/family-os.api';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';
import { computeFamilyHealthScore } from '@/shared/value/family-health-score';
import { buildWeeklyReview } from '@/shared/value/weekly-review';
import {
  buildFamilyJourney,
  buildFamilyJourneyFromMemories,
} from '@/shared/value/family-journey';
import { getOnboardingProfile, GOAL_OPTIONS } from '@/shared/onboarding/onboarding';
import {
  buildParentingCoachFaqs,
  formatCoachShare,
  type ParentingCoachScope,
} from '@/shared/value/parenting-coach';
import { resolveParentCoach } from '@/shared/value/resolve-parenting-coach';
import { dnaCaptionForHealth } from '@/shared/value/blueprint-context';
import { isCapabilityPaywallError } from '@/shared/billing/capability-error';

type Props = {
  familyId: string;
  familyName: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  coachScope?: ParentingCoachScope;
  momentCount: number;
  onOpenPaywall?: (reasonVi?: string) => void;
  parentMembershipId?: string;
  eveningCheckin?: ParentSuccessCheckin | null;
  onEveningCheckinChange?: (row: ParentSuccessCheckin) => void;
  /** Wave B — DNA card for Blueprint-first Coach / Health caption. */
  dna?: FamilyDnaCard | null;
  /** Deep-link target: fv-3q | fv-rop | … */
  focusAnchorId?: string | null;
  /** Màn đang mở — để header tab hiển thị tiêu đề + nút quay lại. */
  view?: FvView;
  onViewChange?: (view: FvView) => void;
};

/** Một màn một mục tiêu: hub gọn, chi tiết mở riêng. */
export type FvView =
  | 'hub'
  | 'coach'
  | 'q3'
  | 'rop'
  | 'weekly'
  | 'letter'
  | 'replay'
  | 'timeline'
  | 'recognition';

const VIEW_BY_ANCHOR: Record<string, FvView> = {
  'fv-3q': 'q3',
  'fv-rop': 'rop',
  'fv-ai-letter': 'letter',
  'fv-replay': 'replay',
  'fv-ai-wins': 'recognition',
  'fv-parent-achv': 'recognition',
};

/** Tiêu đề header cho từng màn con — dùng chung với header tab Báo cáo. */
export const FV_DETAIL_TITLES: Record<Exclude<FvView, 'hub'>, [string, string]> = {
  coach: ['Famixa đồng hành', 'Coach cho nhịp nhà mình'],
  q3: ['3Q tối', 'Ba câu nhanh — nhịp nhà hôm nay'],
  rop: ['Growth Report · ROP', 'Tăng trưởng bố mẹ theo hành vi'],
  weekly: ['Insight tuần này', 'Gương tuần — phản ánh, không chấm điểm'],
  letter: ['Letter tháng', 'Thư Famixa gửi bố mẹ'],
  replay: ['Family Replay', 'Kỷ niệm tháng dạng chữ'],
  timeline: ['Timeline kỷ niệm', 'Nhật ký trưởng thành cả nhà'],
  recognition: ['Wins & Ghi nhận', 'Khoảnh khắc đáng nhớ · ghi nhận bố mẹ'],
};

export function FamilyValuePanel({
  familyId,
  familyName,
  flow,
  glance,
  nudgeToday,
  coachScope,
  momentCount,
  onOpenPaywall,
  parentMembershipId,
  eveningCheckin,
  onEveningCheckinChange,
  dna = null,
  focusAnchorId = null,
  view: viewProp,
  onViewChange,
}: Props) {
  const [serverWeekly, setServerWeekly] = useState<FamilyWeeklyInsight | null>(null);
  const [rop, setRop] = useState<ParentSuccessRop | null>(null);
  const [ropBlocked, setRopBlocked] = useState(false);
  const [ropDays, setRopDays] = useState<30 | 90>(30);
  const [winsDigest, setWinsDigest] = useState<FamilyAiWinsDigest | null>(null);
  const [aiLetter, setAiLetter] = useState<FamilyAiLetter | null>(null);
  const [letterBlocked, setLetterBlocked] = useState(false);
  const [memories, setMemories] = useState<FamilyMemoryEntry[]>([]);
  const [timelineBlocked, setTimelineBlocked] = useState(false);
  const [achievements, setAchievements] = useState<ParentAchievements | null>(null);
  const [replay, setReplay] = useState<FamilyReplay | null>(null);
  const [replayBlocked, setReplayBlocked] = useState(false);
  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [qLessNudge, setQLessNudge] = useState(false);
  const [qLessTension, setQLessTension] = useState(false);
  const [qQualityTime, setQQualityTime] = useState(false);
  const [checkinBusy, setCheckinBusy] = useState(false);
  const [checkinMsg, setCheckinMsg] = useState<string | null>(null);
  const [viewLocal, setViewLocal] = useState<FvView>(
    () => (focusAnchorId ? VIEW_BY_ANCHOR[focusAnchorId] : undefined) ?? 'hub',
  );
  const view = viewProp ?? viewLocal;
  const setView = (next: FvView) => {
    setViewLocal(next);
    onViewChange?.(next);
  };

  useEffect(() => {
    if (!focusAnchorId) return;
    const mapped = VIEW_BY_ANCHOR[focusAnchorId];
    if (!mapped) return;
    setViewLocal(mapped);
    onViewChange?.(mapped);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusAnchorId]);

  useEffect(() => {
    if (view === 'hub') return;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, [view]);

  useEffect(() => {
    let cancelled = false;
    void fetchFamilySubscription(familyId)
      .then((s) => {
        if (!cancelled) setSubscription(s);
      })
      .catch(() => {
        if (!cancelled) setSubscription(null);
      });
    void fetchWeeklyInsight(familyId, { asOf: flow.flowDate, days: 7 })
      .then((r) => {
        if (!cancelled) setServerWeekly(r);
      })
      .catch(() => {
        if (!cancelled) setServerWeekly(null);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate]);

  useEffect(() => {
    let cancelled = false;
    void fetchParentSuccessRop(familyId, { days: ropDays, asOf: flow.flowDate })
      .then((r) => {
        if (!cancelled) {
          setRop(r);
          setRopBlocked(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setRop(null);
          setRopBlocked(isCapabilityPaywallError(err));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate, ropDays]);

  useEffect(() => {
    let cancelled = false;
    void fetchFamilyAiWinsDigest(familyId, { to: flow.flowDate, limit: 8 })
      .then((r) => {
        if (!cancelled) setWinsDigest(r);
      })
      .catch(() => {
        if (!cancelled) setWinsDigest(null);
      });
    void fetchFamilyAiLetter(familyId)
      .then((r) => {
        if (!cancelled) {
          setAiLetter(r);
          setLetterBlocked(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setAiLetter(null);
          setLetterBlocked(isCapabilityPaywallError(err));
        }
      });
    void fetchFamilyMemories(familyId, { limit: 40 })
      .then((r) => {
        if (!cancelled) {
          setMemories(r);
          setTimelineBlocked(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setMemories([]);
          setTimelineBlocked(isCapabilityPaywallError(err));
        }
      });
    void fetchParentAchievements(familyId, { asOf: flow.flowDate })
      .then((r) => {
        if (!cancelled) setAchievements(r);
      })
      .catch(() => {
        if (!cancelled) setAchievements(null);
      });
    void fetchFamilyReplay(familyId)
      .then((r) => {
        if (!cancelled) {
          setReplay(r);
          setReplayBlocked(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setReplay(null);
          setReplayBlocked(isCapabilityPaywallError(err));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate]);

  useEffect(() => {
    setQLessNudge(Boolean(eveningCheckin?.qLessNudge));
    setQLessTension(Boolean(eveningCheckin?.qLessTension));
    setQQualityTime(Boolean(eveningCheckin?.qQualityTime));
  }, [eveningCheckin]);

  const localHealth = useMemo(
    () =>
      computeFamilyHealthScore({
        familyId,
        flow,
        glance,
        nudgeToday,
        momentCount,
      }),
    [familyId, flow, glance, nudgeToday, momentCount],
  );

  // Prefer Evidence Engine score when available; fall back to local estimate.
  const health = useMemo(() => {
    const s = serverWeekly?.health;
    if (s?.score != null) {
      return {
        score: s.score,
        breakdown: {
          completion: s.completion ?? 0,
          nudgeCalm: s.reminderCalm ?? 0,
          streak: s.streak ?? 0,
          autonomy: s.onTime ?? 0,
          parentProgress: s.parentProgress,
        },
        label: s.label ?? localHealth.label,
        deltaVsYesterday: localHealth.deltaVsYesterday,
        promiseLine: s.promiseLine ?? localHealth.promiseLine,
        fromServer: true as const,
      };
    }
    return {
      ...localHealth,
      breakdown: { ...localHealth.breakdown, parentProgress: undefined as number | undefined },
      fromServer: false as const,
    };
  }, [serverWeekly, localHealth]);

  const hasCap = (cap: string) => {
    const caps = subscription?.capabilities;
    if (!caps || caps.length === 0) return true;
    return caps.some((c) => c.toLowerCase() === cap.toLowerCase());
  };

  const canCoach = hasCap('parenting_coach') || hasCap('behavior_coach');
  const canGrowth = hasCap('growth_report');
  const canAiPlus = hasCap('ai_plus_deep');

  const reportFromRop = useMemo(() => {
    if (!rop) return null;
    return {
      familyName,
      startDate: rop.periodStart,
      endDate: rop.periodEnd,
      daySpan: rop.windowDays,
      metrics: rop.metrics.map((m) => ({
        id: m.id,
        label: m.labelVi,
        before: m.beforeDisplay,
        after: m.afterDisplay,
        deltaLabel: m.deltaLabelVi,
        positive: m.positive,
        unit: m.unit,
      })),
      aiSummary: rop.summaryVi,
      outcomesHit: rop.outcomesVi,
      readyToPayLine: rop.readyToRenewLineVi,
      headlineVi: rop.headlineVi,
      growthScore: rop.growthScore,
      growthBulletsVi: rop.growthBulletsVi,
      isPartial: rop.isPartial,
      partialNoteVi: rop.partialNoteVi,
      minutesSavedEstimate: rop.minutesSavedEstimate,
      fromServer: true as const,
    };
  }, [rop, familyName]);

  // Never fake Pro ROP on Free/Plus — only show server Growth Report or a teaser.
  const report = reportFromRop;

  const weeklyLocal = useMemo(
    () => buildWeeklyReview({ familyId, flow, glance }),
    [familyId, flow, glance],
  );

  const journey = useMemo(() => {
    if (memories.length > 0) return buildFamilyJourneyFromMemories(memories, familyName);
    if (timelineBlocked) return null;
    return buildFamilyJourney({ flow, glance, familyName });
  }, [memories, flow, glance, familyName, timelineBlocked]);

  const resolvedCoach = useMemo(
    () =>
      canCoach
        ? resolveParentCoach({
            familyId,
            flow,
            glance,
            nudgeToday,
            focusChildName:
              coachScope?.kind === 'child'
                ? coachScope.childName
                : getOnboardingProfile(familyId)?.childName,
            scope: coachScope,
            coachInsight: null,
            familyTwin: null,
            behaviorCoach: null,
            dna,
          })
        : null,
    [familyId, flow, glance, nudgeToday, canCoach, dna, coachScope],
  );
  const coach = resolvedCoach?.primary ?? null;
  const healthDnaCaption = useMemo(() => dnaCaptionForHealth(dna), [dna]);

  const faqs = useMemo(
    () =>
      canCoach
        ? buildParentingCoachFaqs({ familyId, flow, glance, nudgeToday })
        : [],
    [familyId, flow, glance, nudgeToday, canCoach],
  );

  const openUpgrade = (reason?: string) => {
    onOpenPaywall?.(reason);
  };

  const saveEveningCheckin = async () => {
    if (!parentMembershipId || checkinBusy) return;
    setCheckinBusy(true);
    setCheckinMsg(null);
    try {
      const row = await upsertParentSuccessEveningCheckin(familyId, {
        memberId: parentMembershipId,
        flowDate: flow.flowDate,
        qLessNudge,
        qLessTension,
        qQualityTime,
      });
      onEveningCheckinChange?.(row);
      setCheckinMsg(row.reflectionVi || 'Đã lưu phản hồi tối.');
    } catch (err: unknown) {
      if (isCapabilityPaywallError(err)) {
        openUpgrade(
          'Evening check-in có trong Peace Plan — nâng gói để Famixa học nhịp nhà.',
        );
      } else {
        setCheckinMsg('Chưa lưu được — thử lại nhé.');
      }
    } finally {
      setCheckinBusy(false);
    }
  };

  const onboard = getOnboardingProfile(familyId);
  const goalLabel = GOAL_OPTIONS.find((g) => g.value === onboard?.goal)?.label;

  const printReport = () => {
    if (!report) return;
    const w = window.open('', '_blank', 'noopener,noreferrer,width=720,height=900');
    if (!w) return;
    const rows = report.metrics
      .map(
        (m) =>
          `<tr><td>${m.label}</td><td>${m.before}${m.unit ? ' ' + m.unit : ''}</td><td><strong>${m.after}${m.unit ? ' ' + m.unit : ''}</strong></td><td>${m.deltaLabel}</td></tr>`,
      )
      .join('');
    w.document.write(`<!doctype html><html><head><title>ROP Famixa</title>
      <style>
        body{font-family:system-ui,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;padding:32px;color:#14352c;line-height:1.45}
        h1{font-size:1.6rem;margin:0 0 8px}
        .sub{color:#5a7268;margin-bottom:24px}
        table{width:100%;border-collapse:collapse;margin:16px 0}
        th,td{border-bottom:1px solid #d7e6df;padding:10px 8px;text-align:left;font-size:14px}
        .box{background:#eef8f2;padding:14px 16px;border-radius:12px;margin:16px 0}
        .ok{margin:6px 0}
      </style></head><body>
      <h1>${report.headlineVi}</h1>
      <p class="sub">Return on Parenting · ${report.familyName} · Famixa · ${report.daySpan} ngày</p>
      <table><thead><tr><th>Chỉ số</th><th>Nửa đầu</th><th>Nửa sau</th><th>Đổi</th></tr></thead><tbody>${rows}</tbody></table>
      <div class="box"><strong>Growth Report</strong><p>${report.aiSummary}</p>
      ${(report.growthBulletsVi ?? []).map((b) => `<p class="ok">• ${b}</p>`).join('')}
      </div>
      ${report.outcomesHit.map((o) => `<p class="ok">✅ ${o}</p>`).join('')}
      <p class="sub">${report.readyToPayLine}</p>
      </body></html>`);
    w.document.close();
    w.focus();
    window.setTimeout(() => w.print(), 300);
  };

  const weeklyLine = serverWeekly
    ? (serverWeekly.mirror.reflections[0] ??
      serverWeekly.highlights[0] ??
      `${serverWeekly.dataDays}/${serverWeekly.days} ngày có dữ liệu`)
    : `Tự giác ${weeklyLocal.autonomyDeltaPct >= 0 ? '+' : ''}${weeklyLocal.autonomyDeltaPct}% · ${weeklyLocal.nudgeThisWeek} lần nhắc tuần này`;

  type HubRow = {
    view: FvView;
    icon: string;
    title: string;
    hint: string;
    locked: boolean;
  };

  const hubRows: HubRow[] = [];
  if (report) {
    hubRows.push({
      view: 'rop',
      icon: '📘',
      title: 'Growth Report · ROP',
      hint:
        report.growthScore != null
          ? `Growth ${report.growthScore}/100 · ${report.daySpan} ngày`
          : `${report.daySpan} ngày · tăng trưởng bố mẹ`,
      locked: false,
    });
  } else if (ropBlocked || !canGrowth) {
    hubRows.push({
      view: 'rop',
      icon: '📘',
      title: 'Growth Report · ROP',
      hint: 'Đo bớt nhắc / con chủ động hơn',
      locked: true,
    });
  }
  if (aiLetter) {
    hubRows.push({
      view: 'letter',
      icon: '💌',
      title: `Letter · ${aiLetter.monthLabelVi}`,
      hint: 'Thư tháng cho bố mẹ',
      locked: false,
    });
  } else if (letterBlocked) {
    hubRows.push({
      view: 'letter',
      icon: '💌',
      title: 'Letter tháng',
      hint: 'Thư tháng cho bố mẹ',
      locked: true,
    });
  }
  if (replay) {
    hubRows.push({
      view: 'replay',
      icon: '🎬',
      title: 'Family Replay',
      hint: `${replay.scenes.length} cảnh kỷ niệm tháng`,
      locked: false,
    });
  } else if (replayBlocked) {
    hubRows.push({
      view: 'replay',
      icon: '🎬',
      title: 'Family Replay',
      hint: 'Replay chữ kỷ niệm tháng',
      locked: true,
    });
  }
  if (journey && journey.length > 0) {
    hubRows.push({
      view: 'timeline',
      icon: '🗓️',
      title: 'Timeline kỷ niệm',
      hint: `${journey.length} mốc trưởng thành`,
      locked: false,
    });
  } else if (timelineBlocked) {
    hubRows.push({
      view: 'timeline',
      icon: '🗓️',
      title: 'Timeline kỷ niệm',
      hint: 'Nhật ký trưởng thành cả nhà',
      locked: true,
    });
  }
  if (winsDigest || achievements) {
    hubRows.push({
      view: 'recognition',
      icon: '🌟',
      title: 'Wins & Ghi nhận bố mẹ',
      hint: winsDigest?.headlineVi || 'Ghi nhận nhẹ · không xếp hạng',
      locked: false,
    });
  }

  return (
    <div className={`fv-stack${view === 'hub' ? ' is-hub' : ''}`}>
      {view === 'hub' ? (
        <>
          <section className="fv-hero" id="fv-health">
            <p className="fv-eyebrow">
              KPI cốt lõi{health.fromServer ? ' · đo từ dữ liệu thật' : ''}
            </p>
            <div className="fv-hero-main">
              <div className="fv-hero-score">
                <strong>{health.score}</strong>
                <span>/100</span>
              </div>
              <div className="fv-hero-copy">
                <h2>Family Health Score</h2>
                <p className="fv-hero-label">{health.label}</p>
                {health.deltaVsYesterday != null ? (
                  <p className="fv-delta">
                    {health.deltaVsYesterday >= 0 ? '↑' : '↓'}{' '}
                    {Math.abs(health.deltaVsYesterday)} so với hôm qua
                  </p>
                ) : null}
              </div>
            </div>
            <div className="fv-hero-track" aria-hidden>
              <b style={{ width: `${health.score}%` }} />
            </div>
            <p className="fv-promise">{health.promiseLine}</p>
            {healthDnaCaption ? (
              <p className="fv-dna-because muted">{healthDnaCaption}</p>
            ) : null}
            <details className="fv-hero-detail">
              <summary>Chi tiết chỉ số</summary>
              <div className="fv-bars">
                {(
                  [
                    ['Hoàn thành', health.breakdown.completion],
                    ['Ít phải nhắc', health.breakdown.nudgeCalm],
                    ['Streak', health.breakdown.streak],
                    ['Tự giác đúng giờ', health.breakdown.autonomy],
                    ...(health.breakdown.parentProgress != null
                      ? ([['Bố mẹ cùng làm', health.breakdown.parentProgress]] as const)
                      : []),
                  ] as ReadonlyArray<readonly [string, number]>
                ).map(([label, value]) => (
                  <div key={label} className="fv-bar-row">
                    <span>{label}</span>
                    <i>
                      <b style={{ width: `${value}%` }} />
                    </i>
                    <em>{value}</em>
                  </div>
                ))}
              </div>
            </details>
          </section>

          {coach && resolvedCoach ? (
            <section className="fv-card fv-hub-ai">
              <p className="fv-eyebrow">🤖 Hôm nay Famixa đề xuất</p>
              <h2 className="fv-hub-ai-title">{coach.doThis}</h2>
              <p className="fv-promise">{coach.insight}</p>
              <div className="fv-actions">
                <button type="button" className="pill" onClick={() => setView('coach')}>
                  Xem cách làm
                </button>
              </div>
            </section>
          ) : (
            <section className="fv-card fv-hub-ai fv-teaser">
              <p className="fv-eyebrow">🤖 Famixa đồng hành</p>
              <h2 className="fv-hub-ai-title">Coach giúp bớt nhắc, nhẹ tay hơn</h2>
              <div className="fv-actions">
                <button
                  type="button"
                  className="pill"
                  onClick={() =>
                    openUpgrade(
                      subscription?.upgradeHintVi ||
                        'Nâng Family Peace Plan để mở AI Parenting Coach.',
                    )
                  }
                >
                  Xem Peace Plan · 199.000đ
                </button>
              </div>
            </section>
          )}

          <ul className="fv-hub-list">
            <li>
              <button type="button" className="fv-hub-row" onClick={() => setView('weekly')}>
                <span className="fv-hub-row-ico" aria-hidden>
                  💡
                </span>
                <span className="fv-hub-row-body">
                  <strong>Insight tuần này</strong>
                  <em>{weeklyLine}</em>
                </span>
                <span className="fv-hub-row-go" aria-hidden>
                  ›
                </span>
              </button>
            </li>
            {parentMembershipId ? (
              <li>
                <button type="button" className="fv-hub-row" onClick={() => setView('q3')}>
                  <span className="fv-hub-row-ico" aria-hidden>
                    🎯
                  </span>
                  <span className="fv-hub-row-body">
                    <strong>3Q tối</strong>
                    <em>
                      {eveningCheckin
                        ? eveningCheckin.reflectionVi || 'Đã trả lời hôm nay'
                        : 'Chưa trả lời — mất 15 giây'}
                    </em>
                  </span>
                  <span className="fv-hub-row-go" aria-hidden>
                    ›
                  </span>
                </button>
              </li>
            ) : null}
            {hubRows.map((row) => (
              <li key={row.view}>
                <button
                  type="button"
                  className={`fv-hub-row${row.locked ? ' is-locked' : ''}`}
                  onClick={() => setView(row.view)}
                >
                  <span className="fv-hub-row-ico" aria-hidden>
                    {row.icon}
                  </span>
                  <span className="fv-hub-row-body">
                    <strong>{row.title}</strong>
                    <em>{row.hint}</em>
                  </span>
                  {row.locked ? (
                    <span className="fv-hub-row-lock">Peace Plan</span>
                  ) : (
                    <span className="fv-hub-row-go" aria-hidden>
                      ›
                    </span>
                  )}
                </button>
              </li>
            ))}
          </ul>
        </>
      ) : null}
      {view === 'hub' && onboard && !onboard.skipped ? (
        <section className="fv-card" style={{ background: 'linear-gradient(145deg,#eef8f2,#fff)' }}>
          <p className="fv-eyebrow">Mục tiêu Onboarding 30 ngày</p>
          <h2 style={{ margin: 0, fontSize: '1.1rem' }}>
            {onboard.childName}
            {goalLabel ? ` · ${goalLabel}` : ''}
          </h2>
          <p className="fv-promise" style={{ marginTop: 8 }}>
            Starter: {onboard.missionTitles.slice(0, 4).join(' · ') || 'Đã lưu hồ sơ'}
          </p>
        </section>
      ) : null}

      {view === 'coach' && coach && resolvedCoach ? (
        <section className="fv-card fv-coach">
          <p className="fv-label">{coach.childProfile}</p>
          <p className="fv-promise">{coach.insight}</p>
          <p className="fv-ai">
            <strong>Làm:</strong> {coach.doThis}
          </p>
          <p className="fv-ai">
            <strong>Tránh:</strong> {coach.avoid}
          </p>
          <div className="fv-faq">
            {faqs.map((f) => (
              <details key={f.id} className="fv-faq-item">
                <summary>{f.question}</summary>
                <p>{f.answer}</p>
              </details>
            ))}
          </div>
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() => void shareOrCopyNudge(formatCoachShare(coach), { preferShare: true })}
            >
              Chia sẻ Coach
            </button>
          </div>
        </section>
      ) : null}

      {view === 'q3' && parentMembershipId ? (
        <section className="fv-card" id="fv-3q">
          <header className="fv-head">
            <h2>
              {eveningCheckin ? 'Đã trả lời hôm nay' : 'Hôm nay nhà mình thế nào?'}
            </h2>
            <p>Phản hồi nhanh — giúp Brief học nhịp nhà (không phải điểm số con).</p>
          </header>
          <div className="fv-3q-list">
            {(
              [
                ['qLessNudge', 'Đã phải nhắc ít hơn?', qLessNudge, setQLessNudge],
                ['qLessTension', 'Nhà bớt căng thẳng hơn?', qLessTension, setQLessTension],
                [
                  'qQualityTime',
                  'Có thời gian chất lượng với con?',
                  qQualityTime,
                  setQQualityTime,
                ],
              ] as const
            ).map(([key, label, value, setter]) => (
              <label key={key} className="fv-3q-row">
                <span>{label}</span>
                <button
                  type="button"
                  className={`fv-3q-toggle${value ? ' is-on' : ''}`}
                  aria-pressed={value}
                  onClick={() => setter(!value)}
                >
                  {value ? 'Có' : 'Chưa'}
                </button>
              </label>
            ))}
          </div>
          {eveningCheckin?.reflectionVi ? (
            <p className="fv-promise">{eveningCheckin.reflectionVi}</p>
          ) : null}
          {checkinMsg ? <p className="fv-label">{checkinMsg}</p> : null}
          <div className="fv-actions" style={{ marginTop: 12 }}>
            <button
              type="button"
              className="pill"
              disabled={checkinBusy}
              onClick={() => void saveEveningCheckin()}
            >
              {eveningCheckin ? 'Cập nhật' : 'Gửi Famixa'}
            </button>
          </div>
        </section>
      ) : view === 'q3' ? (
        <section className="fv-card" id="fv-3q">
          <header className="fv-head">
            <h2>Cần hồ sơ bố/mẹ để trả lời 3Q</h2>
            <p>Chọn thành viên bố/mẹ rồi mở lại 3Q tối nhé.</p>
          </header>
        </section>
      ) : null}

      {view === 'rop' && report ? (
        <section className="fv-card" id="fv-rop">
          <header className="fv-head">
            <p>Growth Report từ behavior_event — không phải số sao / routine</p>
          </header>
          <div className="fv-actions" style={{ marginBottom: 12 }}>
            <button
              type="button"
              className={`pill${ropDays === 30 ? '' : ' is-soft'}`}
              onClick={() => setRopDays(30)}
            >
              30 ngày
            </button>
            <button
              type="button"
              className={`pill${ropDays === 90 ? '' : ' is-soft'}`}
              onClick={() => setRopDays(90)}
            >
              90 ngày
            </button>
          </div>
          {report.growthScore != null ? (
            <p className="fv-promise">
              <strong>Growth {report.growthScore}/100</strong> · {report.headlineVi}
            </p>
          ) : (
            <p className="fv-promise">{report.headlineVi}</p>
          )}
          {rop?.hasAiPlusDeep && rop.deepPlaybookVi ? (
            <div className="fv-card" style={{ margin: '12px 0', padding: 12, background: '#eef6f1' }}>
              <p className="fv-eyebrow">AI+ · Playbook tuần</p>
              <p className="fv-promise">{rop.deepPlaybookVi}</p>
              {rop.deepActionsVi && rop.deepActionsVi.length > 0 ? (
                <ul className="fv-outcomes">
                  {rop.deepActionsVi.map((a) => (
                    <li key={a}>
                      <span aria-hidden>→</span>
                      {a}
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>
          ) : null}
          {report.isPartial && report.partialNoteVi ? (
            <p className="fv-label">{report.partialNoteVi}</p>
          ) : null}
          {report.minutesSavedEstimate > 0 ? (
            <p className="fv-ai">
              Ước tính tiết kiệm ~{report.minutesSavedEstimate} phút nhắc nhở trong kỳ.
            </p>
          ) : null}
          <ul className="fv-outcomes">
            {report.outcomesHit.map((o) => (
              <li key={o}>
                <span aria-hidden>✅</span>
                {o}
              </li>
            ))}
          </ul>
          {report.growthBulletsVi.length > 0 ? (
            <ul className="fv-outcomes">
              {report.growthBulletsVi.map((b) => (
                <li key={b}>
                  <span aria-hidden>•</span>
                  {b}
                </li>
              ))}
            </ul>
          ) : null}
          <div className="fv-metrics">
            {report.metrics.map((m) => (
              <article key={m.id} className={`fv-metric${m.positive ? ' is-up' : ' is-down'}`}>
                <span>{m.label}</span>
                <strong>
                  {m.before} → {m.after}
                </strong>
                <em>{m.deltaLabel}</em>
              </article>
            ))}
          </div>
          <p className="fv-ai">{report.aiSummary}</p>
          <p className="fv-pay">{report.readyToPayLine}</p>
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() =>
                void shareOrCopyNudge(
                  rop
                    ? formatParentSuccessRopShare(rop, familyName)
                    : '',
                  { preferShare: true },
                )
              }
            >
              Chia sẻ ROP
            </button>
            <button type="button" className="pill is-soft" onClick={printReport}>
              In / PDF
            </button>
          </div>
        </section>
      ) : view === 'rop' && (ropBlocked || !canGrowth) ? (
        <section className="fv-card fv-teaser" id="fv-rop">
          <header className="fv-head">
            <p>Có trong Family Peace Plan</p>
          </header>
          <p className="fv-promise">
            Growth Report đo “bớt nhắc / con chủ động hơn” — không mở trên Free/Plus.
          </p>
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() =>
                openUpgrade(
                  subscription?.upgradeHintVi ||
                    'Nâng Family Peace Plan để mở Growth Report (ROP).',
                )
              }
            >
              Xem Peace Plan · 199.000đ
            </button>
          </div>
        </section>
      ) : null}

      {view === 'weekly' ? (
      <section className="fv-card fv-weekly">
        <header className="fv-head">
          <p>
            {serverWeekly
              ? `${serverWeekly.periodStart} → ${serverWeekly.periodEnd} · ${serverWeekly.dataDays}/${serverWeekly.days} ngày có dữ liệu · phản ánh, không chấm điểm`
              : `${weeklyLocal.weekLabel} · giữ nhịp quay lại mỗi tuần`}
          </p>
        </header>
        {serverWeekly ? (
          <>
            {serverWeekly.isPartial && serverWeekly.note ? (
              <p className="fv-promise">{serverWeekly.note}</p>
            ) : null}

            <div className="fv-mirror-cols">
              <article className="fv-mirror-col">
                <h3>Con</h3>
                <strong>
                  {serverWeekly.mirror.child.doneCount}/
                  {serverWeekly.mirror.child.totalCommitments}
                </strong>
                <em>
                  {serverWeekly.mirror.child.completionRate != null
                    ? `${Math.round(serverWeekly.mirror.child.completionRate * 100)}% routine`
                    : 'chưa có dữ liệu'}
                  {serverWeekly.mirror.child.bestStreakDays > 0
                    ? ` · streak ${serverWeekly.mirror.child.bestStreakDays} ngày`
                    : ''}
                </em>
              </article>
              <article className="fv-mirror-col">
                <h3>Bố mẹ</h3>
                {serverWeekly.mirror.parent.anyShared ? (
                  <>
                    <strong>
                      {serverWeekly.mirror.parent.checkinDoneCount}/
                      {serverWeekly.mirror.parent.checkinExpectedCount}
                    </strong>
                    <em>
                      {serverWeekly.mirror.parent.checkinRate != null
                        ? `${Math.round(serverWeekly.mirror.parent.checkinRate * 100)}% check-in đã chia sẻ`
                        : `${serverWeekly.mirror.parent.sharedGoalCount} mục tiêu`}
                    </em>
                  </>
                ) : (
                  <>
                    <strong>—</strong>
                    <em>Chưa chia sẻ mục tiêu (riêng tư mặc định)</em>
                  </>
                )}
              </article>
              <article className="fv-mirror-col">
                <h3>Cả nhà</h3>
                <strong>
                  {serverWeekly.mirror.challenge
                    ? `${serverWeekly.mirror.challenge.legsComplete}/${serverWeekly.mirror.challenge.legsTotal} chân`
                    : serverWeekly.mirror.household.teamUnlocksConfirmed > 0
                      ? `${serverWeekly.mirror.household.teamUnlocksConfirmed} thưởng chung`
                      : `${serverWeekly.mirror.household.starsEarned} sao`}
                </strong>
                <em>
                  {serverWeekly.mirror.challenge
                    ? `Challenge · ${serverWeekly.mirror.challenge.rewardLabel}`
                    : serverWeekly.mirror.household.remindersTracked
                      ? `${serverWeekly.mirror.household.reminderCount} lần nhắc trong kỳ`
                      : 'chưa theo dõi nhắc'}
                </em>
              </article>
            </div>

            {serverWeekly.mirror.parent.goals.length > 0 ? (
              <ul className="fv-mirror-goals">
                {serverWeekly.mirror.parent.goals.map((g) => (
                  <li key={g.goalId}>
                    <span>
                      {g.emoji ? `${g.emoji} ` : ''}
                      {g.memberName}: {g.title}
                    </span>
                    <strong>
                      {g.doneDays}/{g.targetDaysPerWeek}
                      {g.todayDone ? ' · hôm nay ✓' : ''}
                    </strong>
                  </li>
                ))}
              </ul>
            ) : null}

            <ul className="fv-outcomes">
              {(serverWeekly.mirror.reflections.length > 0
                ? serverWeekly.mirror.reflections
                : serverWeekly.highlights
              ).map((w) => (
                <li key={w}>
                  <span aria-hidden>🪞</span>
                  {w}
                </li>
              ))}
            </ul>

            {serverWeekly.mirror.child.members.length > 0 ? (
              <div className="fv-metrics" style={{ marginTop: 12 }}>
                {serverWeekly.mirror.child.members.map((m) => (
                  <article key={m.memberId ?? m.name} className="fv-metric is-up">
                    <span>{m.name}</span>
                    <strong>
                      {m.doneCount}/{m.totalCommitments}
                      {m.completionRate != null ? ` · ${Math.round(m.completionRate * 100)}%` : ''}
                    </strong>
                    <em>
                      streak {m.currentStreakDays} ngày · {m.starsEarned} sao
                    </em>
                  </article>
                ))}
              </div>
            ) : null}
          </>
        ) : (
          <>
            <div className="fv-weekly-grid">
              <div>
                <span>Tự giác</span>
                <strong>
                  {weeklyLocal.autonomyDeltaPct >= 0 ? '+' : ''}
                  {weeklyLocal.autonomyDeltaPct}%
                </strong>
              </div>
              <div>
                <span>Nhắc tuần này</span>
                <strong>{weeklyLocal.nudgeThisWeek}</strong>
                <em>
                  tuần trước {weeklyLocal.nudgeLastWeek}
                  {weeklyLocal.nudgeDelta > 0 ? ` · ↓${weeklyLocal.nudgeDelta}` : ''}
                </em>
              </div>
            </div>
            <ul className="fv-outcomes">
              {weeklyLocal.wins.map((w) => (
                <li key={w}>
                  <span aria-hidden>🌟</span>
                  {w}
                </li>
              ))}
            </ul>
            <p className="fv-ai">
              <strong>Tuần tới:</strong> {weeklyLocal.focusNextWeek}
            </p>
            <p className="fv-promise">{weeklyLocal.coachNote}</p>
          </>
        )}
      </section>
      ) : null}

      {view === 'recognition' && winsDigest ? (
        <section className="fv-card" id="fv-ai-wins">
          <header className="fv-head">
            <h2>Famixa · Wins</h2>
            <p>{winsDigest.subheadVi || 'Khoảnh khắc đáng nhớ · từ Family Memory'}</p>
          </header>
          <p className="fv-promise">{winsDigest.headlineVi}</p>
          {winsDigest.wins.length > 0 ? (
            <ul className="fv-outcomes">
              {winsDigest.wins.map((w) => (
                <li key={w.id}>
                  <span aria-hidden>{w.icon ?? '✨'}</span>
                  <span>
                    <strong>{w.titleVi}</strong>
                    {w.noteVi ? ` — ${w.noteVi}` : ''}
                  </span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="fv-label">Chưa đủ wins — cứ sống thêm vài ngày đẹp.</p>
          )}
        </section>
      ) : null}

      {view === 'letter' && aiLetter ? (
        <section className="fv-card" id="fv-ai-letter">
          <header className="fv-head">
            <h2>Famixa · Letter · {aiLetter.monthLabelVi}</h2>
            <p>Thư tháng cho bố mẹ — screenshot-worthy</p>
          </header>
          <p className="fv-label">{aiLetter.greetingVi}</p>
          <p className="fv-promise" style={{ whiteSpace: 'pre-wrap' }}>
            {aiLetter.bodyVi}
          </p>
          <ul className="fv-outcomes">
            {aiLetter.highlightsVi.map((h) => (
              <li key={h}>
                <span aria-hidden>•</span>
                {h}
              </li>
            ))}
          </ul>
          <p className="fv-ai" style={{ whiteSpace: 'pre-wrap' }}>
            {aiLetter.closingVi}
          </p>
          {aiLetter.isThinData ? (
            <p className="fv-label">Tháng này còn mỏng dữ liệu — thư sẽ đầy hơn khi nhà có thêm kỷ niệm.</p>
          ) : null}
          {aiLetter.deepHighlightsVi && aiLetter.deepHighlightsVi.length > 0 ? (
            <ul className="fv-outcomes">
              {aiLetter.deepHighlightsVi.map((h) => (
                <li key={h}>
                  <span aria-hidden>✦</span>
                  {h}
                </li>
              ))}
            </ul>
          ) : canAiPlus ? (
            <p className="fv-label">AI+ · Letter đã làm giàu thêm tín hiệu twin / tip đã thử (nếu có).</p>
          ) : null}
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() =>
                void shareOrCopyNudge(formatFamilyAiLetterShare(aiLetter), { preferShare: true })
              }
            >
              Chia sẻ Letter
            </button>
          </div>
        </section>
      ) : view === 'letter' && letterBlocked ? (
        <section className="fv-card fv-teaser" id="fv-ai-letter">
          <header className="fv-head">
            <h2>Famixa · Letter</h2>
            <p>Có trong Family Peace Plan</p>
          </header>
          <p className="fv-promise">Thư tháng cho bố mẹ — mở khi nâng Peace Plan.</p>
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() => openUpgrade('Nâng Peace Plan để mở AI Letter hàng tháng.')}
            >
              Xem Peace Plan · 199.000đ
            </button>
          </div>
        </section>
      ) : null}

      {view === 'replay' && replay ? (
        <section className="fv-card" id="fv-replay">
          <header className="fv-head">
            <h2>{replay.titleVi}</h2>
            <p>Replay chữ · kỷ niệm tháng — không phải video</p>
          </header>
          <p className="fv-promise">{replay.openingVi}</p>
          <ol className="fv-journey">
            {replay.scenes.map((s, i) => (
              <li key={`${s.kind}-${s.date ?? i}-${s.titleVi}`}>
                <span className="fv-journey-icon" aria-hidden>
                  {s.icon}
                </span>
                <div>
                  <strong>
                    {s.date ? `${s.date.slice(8, 10)}/${s.date.slice(5, 7)} · ` : ''}
                    {s.titleVi}
                  </strong>
                  {s.detailVi ? <p>{s.detailVi}</p> : null}
                </div>
              </li>
            ))}
          </ol>
          <p className="fv-ai" style={{ whiteSpace: 'pre-wrap' }}>
            {replay.closingVi}
          </p>
          {replay.isThinData ? (
            <p className="fv-label">Tháng còn mỏng dữ liệu — Replay sẽ đầy hơn khi có thêm kỷ niệm.</p>
          ) : null}
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() =>
                void shareOrCopyNudge(replay.shareTextVi, { preferShare: true })
              }
            >
              Chia sẻ Replay
            </button>
          </div>
        </section>
      ) : view === 'replay' && replayBlocked ? (
        <section className="fv-card fv-teaser" id="fv-replay">
          <header className="fv-head">
            <h2>Family Replay</h2>
            <p>Có trong Family Peace Plan</p>
          </header>
          <p className="fv-promise">Replay chữ tháng — mở khi nâng Peace Plan.</p>
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() => openUpgrade('Nâng Peace Plan để mở Family Replay.')}
            >
              Xem Peace Plan · 199.000đ
            </button>
          </div>
        </section>
      ) : null}

      {view === 'recognition' && achievements ? (
        <section className="fv-card" id="fv-parent-achv">
          <header className="fv-head">
            <h2>Famixa · Ghi nhận bố mẹ</h2>
            <p>Nhẹ · không xếp hạng · không sao</p>
          </header>
          <p className="fv-promise">{achievements.headlineVi}</p>
          <ul className="fv-outcomes">
            {achievements.items.map((a) => (
              <li key={a.code} style={{ opacity: a.unlocked ? 1 : 0.55 }}>
                <span aria-hidden>{a.icon}</span>
                <span>
                  <strong>
                    {a.titleVi}
                    {a.unlocked ? ' · mở' : ''}
                  </strong>
                  {' — '}
                  {a.unlocked ? a.detailVi : a.progressHintVi}
                </span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {view === 'timeline' && journey && journey.length > 0 ? (
        <section className="fv-card" id="fv-timeline">
          <header className="fv-head">
            <h2>Family Timeline</h2>
            <p>
              {memories.length > 0
                ? 'Memory SoT — nhật ký trưởng thành cả nhà'
                : 'Đang dùng ước lượng local — Memory sẽ thay khi có kỷ niệm'}
            </p>
          </header>
          <ol className="fv-journey">
            {journey.map((j) => (
              <li key={j.id}>
                <span className="fv-journey-icon" aria-hidden>
                  {j.icon}
                </span>
                <div>
                  <strong>{j.title}</strong>
                  <p>{j.detail}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>
      ) : view === 'timeline' && timelineBlocked ? (
        <section className="fv-card fv-teaser" id="fv-timeline">
          <header className="fv-head">
            <h2>Family Timeline</h2>
            <p>Có trong Family Growth Plan (Plus)</p>
          </header>
          <p className="fv-promise">
            Timeline kỷ niệm nhà — nâng Plus hoặc Peace Plan để mở.
          </p>
          <div className="fv-actions">
            <button
              type="button"
              className="pill"
              onClick={() =>
                openUpgrade(
                  'Timeline có từ Plus. Peace Plan thêm Coach/ROP — gói khuyến nghị.',
                )
              }
            >
              Xem gói nâng cấp
            </button>
          </div>
        </section>
      ) : null}
    </div>
  );
}

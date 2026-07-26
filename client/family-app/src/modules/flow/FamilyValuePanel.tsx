import { useEffect, useMemo, useState } from 'react';
import type { AccountabilityGlance, DayFlow, FamilyWeeklyInsight } from '@/shared/api/family-os.api';
import { fetchWeeklyInsight } from '@/shared/api/family-os.api';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';
import { computeFamilyHealthScore } from '@/shared/value/family-health-score';
import {
  buildTransformationReport,
  formatReportShareText,
} from '@/shared/value/transformation-report';
import { buildWeeklyReview } from '@/shared/value/weekly-review';
import { buildFamilyJourney } from '@/shared/value/family-journey';
import { getOnboardingProfile, GOAL_OPTIONS } from '@/shared/onboarding/onboarding';
import {
  buildParentingCoach,
  buildParentingCoachFaqs,
  formatCoachShare,
} from '@/shared/value/parenting-coach';

type Props = {
  familyId: string;
  familyName: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  momentCount: number;
};

export function FamilyValuePanel({
  familyId,
  familyName,
  flow,
  glance,
  nudgeToday,
  momentCount,
}: Props) {
  const [serverWeekly, setServerWeekly] = useState<FamilyWeeklyInsight | null>(null);

  useEffect(() => {
    let cancelled = false;
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

  const report = useMemo(
    () =>
      buildTransformationReport({
        familyId,
        familyName,
        flow,
        glance,
        momentCountToday: momentCount,
        movieNightUnlocksApprox: glance?.days.filter((d) => d.isBeautifulDay).length ?? 0,
      }),
    [familyId, familyName, flow, glance, momentCount],
  );

  const weeklyLocal = useMemo(
    () => buildWeeklyReview({ familyId, flow, glance }),
    [familyId, flow, glance],
  );

  const journey = useMemo(
    () => buildFamilyJourney({ flow, glance, familyName }),
    [flow, glance, familyName],
  );

  const coach = useMemo(
    () =>
      buildParentingCoach({
        familyId,
        flow,
        glance,
        nudgeToday,
        focusChildName: getOnboardingProfile(familyId)?.childName,
      }),
    [familyId, flow, glance, nudgeToday],
  );

  const faqs = useMemo(
    () => buildParentingCoachFaqs({ familyId, flow, glance, nudgeToday }),
    [familyId, flow, glance, nudgeToday],
  );

  const onboard = getOnboardingProfile(familyId);
  const goalLabel = GOAL_OPTIONS.find((g) => g.value === onboard?.goal)?.label;

  const printReport = () => {
    const w = window.open('', '_blank', 'noopener,noreferrer,width=720,height=900');
    if (!w) return;
    const rows = report.metrics
      .map(
        (m) =>
          `<tr><td>${m.label}</td><td>${m.before}${m.unit ? ' ' + m.unit : ''}</td><td><strong>${m.after}${m.unit ? ' ' + m.unit : ''}</strong></td><td>${m.deltaLabel}</td></tr>`,
      )
      .join('');
    w.document.write(`<!doctype html><html><head><title>Báo cáo Famixa</title>
      <style>
        body{font-family:Georgia,serif;padding:32px;color:#14352c;line-height:1.45}
        h1{font-size:1.6rem;margin:0 0 8px}
        .sub{color:#5a7268;margin-bottom:24px}
        table{width:100%;border-collapse:collapse;margin:16px 0}
        th,td{border-bottom:1px solid #d7e6df;padding:10px 8px;text-align:left;font-size:14px}
        .box{background:#eef8f2;padding:14px 16px;border-radius:12px;margin:16px 0}
        .ok{margin:6px 0}
      </style></head><body>
      <h1>Báo cáo chuyển đổi ${report.daySpan} ngày</h1>
      <p class="sub">Gia đình ${report.familyName} · Famixa</p>
      <table><thead><tr><th>Chỉ số</th><th>Trước</th><th>Nay</th><th>Đổi</th></tr></thead><tbody>${rows}</tbody></table>
      <div class="box"><strong>Nhận xét</strong><p>${report.aiSummary}</p></div>
      ${report.outcomesHit.map((o) => `<p class="ok">✅ ${o}</p>`).join('')}
      <p class="sub">${report.readyToPayLine}</p>
      </body></html>`);
    w.document.close();
    w.focus();
    window.setTimeout(() => w.print(), 300);
  };

  return (
    <div className="fv-stack">
      {onboard && !onboard.skipped ? (
        <section className="fv-card" style={{ background: 'linear-gradient(145deg,#eef8f2,#fff)' }}>
          <p className="fv-eyebrow">Mục tiêu Onboarding 30 ngày</p>
          <h2 style={{ margin: 0, fontFamily: 'Fraunces, Georgia, serif', fontSize: '1.15rem' }}>
            {onboard.childName}
            {goalLabel ? ` · ${goalLabel}` : ''}
          </h2>
          <p className="fv-promise" style={{ marginTop: 8 }}>
            Starter: {onboard.missionTitles.slice(0, 4).join(' · ') || 'Đã lưu hồ sơ'}
          </p>
        </section>
      ) : null}

      <section className="fv-card fv-coach">
        <header className="fv-head">
          <h2>Family Coach</h2>
          <p>Lời khuyên dựa dữ liệu nhà bạn — không generic</p>
        </header>
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

      <section className="fv-card fv-health">
        <p className="fv-eyebrow">
          KPI cốt lõi · lý do trả phí
          {health.fromServer ? ' · đo từ dữ liệu thật' : ''}
        </p>
        <div className="fv-health-row">
          <div
            className="fv-ring"
            style={{ background: `conic-gradient(#2f9e7b ${health.score}%, #d7ebe3 0)` }}
          >
            <div className="fv-ring-inner">
              <strong>{health.score}</strong>
              <span>/100</span>
            </div>
          </div>
          <div>
            <h2>Family Health Score</h2>
            <p className="fv-label">{health.label}</p>
            {health.deltaVsYesterday != null ? (
              <p className="fv-delta">
                {health.deltaVsYesterday >= 0 ? '↑' : '↓'} {Math.abs(health.deltaVsYesterday)} so
                với hôm qua
              </p>
            ) : null}
            <p className="fv-promise">{health.promiseLine}</p>
          </div>
        </div>
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
      </section>

      <section className="fv-card">
        <header className="fv-head">
          <h2>Báo cáo {report.daySpan} ngày</h2>
          <p>3 kết quả cả nhà có thể nhìn thấy và đo được</p>
        </header>
        <ul className="fv-outcomes">
          {report.outcomesHit.map((o) => (
            <li key={o}>
              <span aria-hidden>✅</span>
              {o}
            </li>
          ))}
        </ul>
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
              void shareOrCopyNudge(formatReportShareText(report), { preferShare: true })
            }
          >
            Chia sẻ báo cáo
          </button>
          <button type="button" className="pill is-soft" onClick={printReport}>
            In / PDF
          </button>
        </div>
      </section>

      <section className="fv-card fv-weekly">
        <header className="fv-head">
          <h2>{serverWeekly ? 'Gương tuần' : 'AI Weekly Review'}</h2>
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

      <section className="fv-card">
        <header className="fv-head">
          <h2>Family Journey</h2>
          <p>Nhật ký trưởng thành — giữ khách theo năm</p>
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
    </div>
  );
}

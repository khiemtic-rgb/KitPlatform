import type { ParentSuccessRop } from '@/shared/api/family-os.api';

/** Trial / renew conversion framing from ROP metrics. */
export function buildTrialRopConversion(rop: ParentSuccessRop): {
  titleVi: string;
  nudgeBefore: number;
  nudgeAfter: number;
  nudgeDeltaPct: number | null;
  selfBefore: number;
  selfAfter: number;
  selfDeltaPct: number | null;
  daysLabelVi: string;
  ctaVi: string;
  summaryVi: string;
} {
  const before = rop.parentNudgesEarly;
  const after = rop.parentNudgesLate;
  let nudgeDeltaPct: number | null = null;
  if (before > 0) {
    nudgeDeltaPct = Math.round(((before - after) / before) * 100);
  } else if (after === 0 && before === 0) {
    nudgeDeltaPct = 0;
  }

  const selfBefore = rop.selfStartsEarly;
  const selfAfter = rop.selfStartsLate;
  let selfDeltaPct: number | null = null;
  if (selfBefore > 0) {
    selfDeltaPct = Math.round(((selfAfter - selfBefore) / selfBefore) * 100);
  } else if (selfAfter > 0) {
    selfDeltaPct = 100;
  }

  const days = rop.dataDays > 0 ? rop.dataDays : rop.windowDays;
  const improved = nudgeDeltaPct != null && nudgeDeltaPct > 0;

  return {
    titleVi: `${days} ngày cùng Famixa`,
    nudgeBefore: before,
    nudgeAfter: after,
    nudgeDeltaPct,
    selfBefore,
    selfAfter,
    selfDeltaPct,
    daysLabelVi: `${rop.periodStart} → ${rop.periodEnd}`,
    ctaVi: 'Giữ Family Peace Plan · 199.000đ',
    summaryVi: improved
      ? `Số lần phải nhắc đang dịu lại — đây là kết quả bạn đang mua, không phải số task.`
      : rop.readyToRenewLineVi ||
        'Famixa đang học nhịp nhà — giữ Pro để Coach và ROP tiếp tục đo giúp bạn.',
  };
}

import type { FamilySubscription } from '@/shared/api/family-os.api';

/** Soft Pro window after trialEndsAt — keep in sync with FamilyCommercialService.TrialGraceDays. */
export const TRIAL_GRACE_DAYS = 3;

export type TrialUrgency =
  | 'quiet'
  | 'warn7'
  | 'warn3'
  | 'warn1'
  | 'day0'
  | 'grace'
  | 'expired'
  | 'paid';

export type TrialLifecycle = {
  phase: 'trial' | 'grace' | 'free' | 'paid' | 'other';
  /** Calendar days left in trial, or grace days when in grace. */
  daysLeft: number | null;
  urgency: TrialUrgency;
  title: string;
  message: string;
  cta: string;
  /** Parent home / Who card should surface this. */
  showCard: boolean;
  warn: boolean;
};

function daysUntilIso(iso?: string): number | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000));
}

/**
 * Trial = full Peace Plan (Pro). After trialEndsAt: 3-day soft grace still Pro,
 * then Free (routine kept). Milestone copy: D-7 / D-3 / D-1 / D-0 / grace.
 */
export function buildTrialLifecycle(sub: FamilySubscription | null | undefined): TrialLifecycle {
  if (!sub) {
    return {
      phase: 'other',
      daysLeft: null,
      urgency: 'quiet',
      title: 'Gói Famixa',
      message: 'Đang tải thông tin gói…',
      cta: 'Xem gói',
      showCard: false,
      warn: false,
    };
  }

  const planLabel = sub.outcomeNameVi || sub.displayNameVi || 'Family Peace Plan';
  const status = (sub.status || '').toLowerCase();
  const tier = (sub.tierCode || '').toLowerCase();

  if (status === 'active' && sub.isEntitled) {
    return {
      phase: 'paid',
      daysLeft: daysUntilIso(sub.currentPeriodEnd),
      urgency: 'paid',
      title: planLabel,
      message: `${planLabel} đang hoạt động.`,
      cta: 'Quản lý gói',
      showCard: false,
      warn: false,
    };
  }

  if (status === 'trial_grace' && sub.isEntitled) {
    const graceLeft =
      sub.trialGraceDaysRemaining ??
      (sub.trialEndsAt
        ? Math.max(
            0,
            Math.ceil(
              (Date.parse(sub.trialEndsAt) + TRIAL_GRACE_DAYS * 86400000 - Date.now()) /
                86400000,
            ),
          )
        : null);
    return {
      phase: 'grace',
      daysLeft: graceLeft,
      urgency: 'grace',
      title: 'Ân hạn dùng thử',
      message:
        graceLeft != null
          ? `Trial đã hết — còn ${graceLeft} ngày ân hạn. Coach/ROP vẫn mở; sau đó về Free (lịch việc vẫn giữ).`
          : 'Trial đã hết — đang trong ân hạn. Gia hạn Peace Plan để không mất Coach.',
      cta: 'Giữ Peace Plan · 199.000đ',
      showCard: true,
      warn: true,
    };
  }

  if (status === 'trial' && sub.isEntitled) {
    const left =
      sub.trialDaysRemaining ?? daysUntilIso(sub.trialEndsAt) ?? null;
    let urgency: TrialUrgency = 'quiet';
    let title = 'Dùng thử Peace Plan';
    let message =
      left != null
        ? `Đang trải nghiệm đầy đủ ${planLabel} (tầng Pro) — còn ${left} ngày.`
        : `Đang trải nghiệm đầy đủ ${planLabel} (tầng Pro).`;

    if (left === 0) {
      urgency = 'day0';
      title = 'Hết hạn hôm nay';
      message =
        'Dùng thử hết hôm nay. Sau đó còn 3 ngày ân hạn trước khi về Free — lịch việc vẫn giữ.';
    } else if (left === 1) {
      urgency = 'warn1';
      title = 'Còn 1 ngày dùng thử';
      message =
        'Mai hết trial. Gia hạn Peace Plan để giữ Coach, ROP và Letter — không mất dữ liệu nhà.';
    } else if (left != null && left <= 3) {
      urgency = 'warn3';
      title = 'Sắp hết dùng thử';
      message = `Còn ${left} ngày Peace Plan thử. Giữ gói để không mất Coach sau khi hết hạn (+ 3 ngày ân hạn).`;
    } else if (left != null && left <= 7) {
      urgency = 'warn7';
      title = 'Còn 1 tuần dùng thử';
      message = `Còn ${left} ngày trải nghiệm Pro. Nếu nhà đang bớt mệt vì nhắc — giữ Peace Plan trước khi hết hạn.`;
    }

    return {
      phase: 'trial',
      daysLeft: left,
      urgency,
      title,
      message,
      cta: 'Giữ Peace Plan · 199.000đ',
      showCard: true,
      warn: urgency !== 'quiet',
    };
  }

  if (!sub.isEntitled || tier === 'free' || status === 'expired' || status === 'canceled') {
    return {
      phase: 'free',
      daysLeft: null,
      urgency: 'expired',
      title: 'Gói Free',
      message:
        sub.upgradeHintVi ||
        'Free giữ lịch việc & sao cơ bản. Nâng Peace Plan để mở lại Coach, ROP và Letter.',
      cta: 'Nâng Peace Plan · 199.000đ',
      showCard: true,
      warn: true,
    };
  }

  return {
    phase: 'other',
    daysLeft: null,
    urgency: 'quiet',
    title: planLabel,
    message: sub.upgradeHintVi || `${planLabel} đang chạy.`,
    cta: 'Xem gói',
    showCard: false,
    warn: false,
  };
}

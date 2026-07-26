import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ensureDayFlow,
  fetchFamilies,
  fetchFamilySubscription,
  fetchTeamUnlocks,
  type FamilyMembership,
  type FamilySubscription,
} from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { isOnboardingDone } from '@/shared/onboarding/onboarding';
import { hydrateFamilyValueState } from '@/shared/value/value-sync';
import { ParentPinSheet } from '@/shared/ui/ParentPinSheet';
import {
  avatarEmoji,
  avatarToneClass,
  inferGenderFromName,
  type AvatarGender,
} from '@/shared/ui/avatarGender';

const TRIAL_TOTAL_FALLBACK = 30;

function daysUntil(iso?: string): number | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.max(0, Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000)));
}

function trialFillRatio(remaining: number, total: number): number {
  if (total <= 0) return 0;
  return Math.min(1, Math.max(0, remaining / total));
}

function ageYears(dob?: string): number | null {
  if (!dob) return null;
  const d = new Date(dob);
  if (Number.isNaN(d.getTime())) return null;
  const now = new Date();
  let age = now.getFullYear() - d.getFullYear();
  const m = now.getMonth() - d.getMonth();
  if (m < 0 || (m === 0 && now.getDate() < d.getDate())) age -= 1;
  return age >= 0 ? age : null;
}

function childRelation(gender: AvatarGender): string {
  if (gender === 'girl') return 'Con gái';
  if (gender === 'boy') return 'Con trai';
  return 'Con';
}

function MemberCard({
  member,
  onPick,
}: {
  member: FamilyMembership;
  onPick: () => void;
}) {
  const gender = inferGenderFromName(member.displayName);
  const isChild = member.roleCode === 'child';
  const age = ageYears(member.dateOfBirth);
  const meta = isChild
    ? [childRelation(gender), age != null ? `${age} tuổi` : null].filter(Boolean).join(' · ')
    : member.roleCode === 'caregiver'
      ? 'Người chăm sóc'
      : 'Phụ huynh';

  return (
    <button type="button" className="home-member-card" onClick={onPick}>
      <span className={`home-member-avatar ${avatarToneClass(gender)}`}>
        {avatarEmoji(gender, member.roleCode)}
      </span>
      <span className="home-member-text">
        <strong>{member.displayName}</strong>
        <em>{meta}</em>
      </span>
      <span className="home-member-chevron" aria-hidden>
        ›
      </span>
    </button>
  );
}

export function WhoAreYouPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const setMember = useSessionStore((s) => s.setMember);
  const clear = useSessionStore((s) => s.clear);
  const verifyParentPin = useSessionStore((s) => s.verifyParentPin);

  const [members, setMembers] = useState<FamilyMembership[]>([]);
  const [sub, setSub] = useState<FamilySubscription | null>(null);
  const [pendingChildTasks, setPendingChildTasks] = useState(0);
  const [starsToday, setStarsToday] = useState(0);
  const [doneToday, setDoneToday] = useState(0);
  const [totalToday, setTotalToday] = useState(0);
  const [movieNightLabel, setMovieNightLabel] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pinOpen, setPinOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);

  useEffect(() => {
    if (!familyId) return;
    void hydrateFamilyValueState(familyId);
  }, [familyId]);

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void Promise.all([
      fetchFamilies(),
      fetchFamilySubscription(familyId).catch(() => null),
      ensureDayFlow(familyId).catch(() => null),
      fetchTeamUnlocks(familyId).catch(() => []),
    ])
      .then(([families, subscription, day, unlocks]) => {
        if (cancelled) return;
        const family = families.find((f) => f.id === familyId) ?? families[0];
        const list = family?.members ?? [];
        setMembers(list);
        setSub(subscription);

        if (day) {
          const childIds = new Set(
            list.filter((m) => m.roleCode === 'child').map((m) => m.id),
          );
          const childOpen = day.commitments.filter(
            (c) =>
              c.status !== 'done' &&
              c.status !== 'skipped' &&
              (!c.memberId || childIds.has(c.memberId)),
          );
          setPendingChildTasks(childOpen.length);
          setDoneToday(day.doneCount);
          setTotalToday(day.totalCommitments);
          const stars = childOpen.reduce(
            (sum, c) => sum + Math.max(0, Number(c.projectedStarDelta ?? c.starReward ?? 0)),
            0,
          );
          setStarsToday(stars);
        } else {
          setPendingChildTasks(0);
          setDoneToday(0);
          setTotalToday(0);
          setStarsToday(0);
        }

        const unlock = unlocks.find((u) =>
          ['pending_confirm', 'confirmed'].includes(String(u.status ?? '').toLowerCase()),
        );
        if (unlock) {
          const st = String(unlock.status).toLowerCase();
          setMovieNightLabel(st === 'confirmed' ? 'đã mở' : 'chờ duyệt');
        } else {
          setMovieNightLabel(null);
        }
      })
      .catch(() => {
        if (!cancelled) setError('Không tải được danh sách thành viên');
      });
    return () => {
      cancelled = true;
    };
  }, [familyId]);

  const children = useMemo(
    () => members.filter((m) => m.roleCode === 'child'),
    [members],
  );
  const adults = useMemo(
    () => members.filter((m) => m.roleCode !== 'child'),
    [members],
  );

  const trialDaysLeft =
    sub?.trialDaysRemaining ?? daysUntil(sub?.trialEndsAt) ?? null;
  const trialDaysTotal =
    sub?.trialDaysTotal && sub.trialDaysTotal > 0
      ? sub.trialDaysTotal
      : TRIAL_TOTAL_FALLBACK;
  const isTrial = sub?.status === 'trial';
  const trialProgress =
    isTrial && trialDaysLeft != null
      ? trialFillRatio(trialDaysLeft, trialDaysTotal)
      : 0;
  const showBilling =
    !sub || isTrial || !sub.isEntitled;

  const pick = async (picked: FamilyMembership) => {
    setMember(picked);
    if (picked.roleCode !== 'child' && familyId) {
      await hydrateFamilyValueState(familyId);
      if (!isOnboardingDone(familyId)) {
        navigate('/onboarding', { replace: true });
        return;
      }
    }
    navigate('/today', { replace: true });
  };

  const goCheckout = () => {
    if (!familyId) return;
    navigate(
      buildCheckoutPath({
        productCode: 'family_os',
        subjectType: 'family',
        subjectId: familyId,
        planCode: 'starter_month',
        returnPath: '/who',
      }),
    );
  };

  const goAdmin = () => navigate('/family-admin');

  return (
    <div className="home-screen">
      <header className="home-topbar">
        <div className="home-topbar-left">
          <img
            className="home-foxy"
            src="/home/foxy-avatar.png"
            alt=""
            width={44}
            height={44}
            onError={(e) => {
              (e.currentTarget as HTMLImageElement).style.display = 'none';
            }}
          />
          <div>
            <p className="home-hello">Xin chào</p>
            <button
              type="button"
              className="home-family-name"
              onClick={() => setMoreOpen(true)}
              aria-label="Tuỳ chọn gia đình"
            >
              {familyName ?? 'Gia đình mình'}
              <span aria-hidden>▾</span>
            </button>
          </div>
        </div>
        <button
          type="button"
          className="home-admin-btn"
          aria-label="Quản trị gia đình"
          title="Quản trị gia đình"
          onClick={goAdmin}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden>
            <path
              d="M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z"
              stroke="currentColor"
              strokeWidth="1.8"
            />
            <path
              d="M19.4 13a7.6 7.6 0 0 0 .05-1l2.05-1.6-2-3.46-2.45.98a7.7 7.7 0 0 0-1.73-1L14.9 2h-5.8L8.68 6.92a7.7 7.7 0 0 0-1.73 1L4.5 6.94l-2 3.46L4.55 12a7.6 7.6 0 0 0 0 2l-2.05 1.6 2 3.46 2.45-.98a7.7 7.7 0 0 0 1.73 1L9.1 22h5.8l.42-4.92a7.7 7.7 0 0 0 1.73-1l2.45.98 2-3.46L19.4 13Z"
              stroke="currentColor"
              strokeWidth="1.8"
              strokeLinejoin="round"
            />
          </svg>
          {pendingChildTasks > 0 ? <span className="home-bell-dot" aria-hidden /> : null}
        </button>
      </header>

      {/* Primary job: pick who is using the app today */}
      <section className="home-who" id="home-who">
        <div className="home-who-head">
          <div>
            <h1>Ai đang dùng hôm nay?</h1>
            <p>Chạm tên để mở lịch ngày — bố/mẹ quản trị, con làm việc.</p>
          </div>
          <button type="button" className="home-manage-btn" onClick={goAdmin}>
            Quản lý
          </button>
        </div>

        {error ? <div className="banner-error">{error}</div> : null}

        {children.length === 0 && adults.length === 0 ? (
          <div className="home-empty-members">
            <p>Chưa có thành viên. Thêm bố/mẹ và con để bắt đầu.</p>
            <button type="button" className="btn btn-primary" onClick={goAdmin}>
              Thêm thành viên
            </button>
          </div>
        ) : null}

        {children.length > 0 ? (
          <>
            <div className="home-group-label">Các con</div>
            <div className="home-member-grid">
              {children.map((member) => (
                <MemberCard key={member.id} member={member} onPick={() => void pick(member)} />
              ))}
            </div>
          </>
        ) : null}

        {adults.length > 0 ? (
          <>
            <div className="home-group-label">Bố mẹ / người lớn</div>
            <div className="home-member-grid">
              {adults.map((member) => (
                <MemberCard key={member.id} member={member} onPick={() => void pick(member)} />
              ))}
            </div>
          </>
        ) : null}
      </section>

      {/* Live glance — one scope, plain labels */}
      <section className="home-today" aria-label="Tóm tắt hôm nay">
        <div className="home-today-title">
          <h2>Hôm nay</h2>
        </div>
        <div className="home-today-stats home-today-stats-grid">
          <div>
            <strong>{pendingChildTasks}</strong>
            <span>việc con còn lại</span>
          </div>
          <div>
            <strong>{doneToday}/{Math.max(totalToday, 0)}</strong>
            <span>cả nhà đã xong</span>
          </div>
          <div>
            <strong>{starsToday}</strong>
            <span>sao còn có thể nhận</span>
          </div>
          {movieNightLabel ? (
            <div className="is-wide">
              <strong>Movie Night</strong>
              <span>{movieNightLabel}</span>
            </div>
          ) : null}
        </div>
      </section>

      {/* Billing only when trial / expired — not a second hero */}
      {showBilling ? (
        <section className="home-billing" aria-label="Gói Family OS">
          <div className="home-billing-copy">
            <p className="home-billing-kicker">
              {isTrial && trialDaysLeft != null
                ? `Dùng thử · còn ${trialDaysLeft} ngày`
                : sub && !sub.isEntitled
                  ? 'Gói đã hết hạn'
                  : 'Gói Family OS'}
            </p>
            <p className="home-billing-note">
              {sub && !sub.isEntitled
                ? 'Gia hạn để mở lại Daily Flow và sao.'
                : 'Cả nhà cùng thói quen — nâng cấp khi sẵn sàng.'}
            </p>
            {isTrial && trialDaysLeft != null ? (
              <div
                className="home-trial-bar home-billing-bar"
                role="progressbar"
                aria-valuenow={trialDaysLeft}
                aria-valuemin={0}
                aria-valuemax={trialDaysTotal}
              >
                <span style={{ width: `${Math.round(trialProgress * 100)}%` }} />
              </div>
            ) : null}
          </div>
          <button type="button" className="home-billing-cta" onClick={goCheckout}>
            {sub && !sub.isEntitled ? 'Gia hạn' : 'Nâng cấp'}
          </button>
        </section>
      ) : null}

      <nav className="home-tabbar" aria-label="Điều hướng chính">
        <button type="button" className="is-active">
          <span aria-hidden>🏠</span>
          Trang chủ
        </button>
        <button
          type="button"
          onClick={() =>
            document.getElementById('home-who')?.scrollIntoView({ behavior: 'smooth' })
          }
        >
          <span aria-hidden>👤</span>
          Thành viên
        </button>
        <button
          type="button"
          className="home-tab-fab"
          aria-label="Quản trị gia đình"
          onClick={goAdmin}
        >
          +
        </button>
        <button type="button" onClick={() => setMoreOpen(true)}>
          <span aria-hidden>▦</span>
          Thêm
        </button>
      </nav>

      {moreOpen ? (
        <div className="home-sheet-backdrop" onClick={() => setMoreOpen(false)}>
          <div
            className="home-sheet"
            role="dialog"
            aria-modal="true"
            onClick={(e) => e.stopPropagation()}
          >
            <h3>Tuỳ chọn gia đình</h3>
            <p className="muted">
              Quản trị trên điện thoại — thành viên, việc hôm nay, chế độ gia đình.
            </p>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                setMoreOpen(false);
                goAdmin();
              }}
            >
              Quản trị gia đình
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => {
                setMoreOpen(false);
                goCheckout();
              }}
            >
              Gói & thanh toán
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => {
                setMoreOpen(false);
                setPinOpen(true);
              }}
            >
              Đổi thiết bị / đăng xuất
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => setMoreOpen(false)}>
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      <ParentPinSheet
        open={pinOpen}
        title="Đăng xuất"
        hint="Nhập mã bố mẹ để đăng xuất thiết bị"
        verify={verifyParentPin}
        onClose={() => setPinOpen(false)}
        onSuccess={() => {
          setPinOpen(false);
          clear();
          navigate('/unlock', { replace: true });
        }}
      />
    </div>
  );
}

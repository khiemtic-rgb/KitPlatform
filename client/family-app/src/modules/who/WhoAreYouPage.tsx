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

/** Fill ratio for trial slider: remaining / total (1 = full trial left). */
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
          // Only remaining (open) tasks contribute to "sao có thể nhận".
          const stars = childOpen.reduce(
            (sum, c) => sum + Number(c.projectedStarDelta ?? c.starReward ?? 0),
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
          ['pending_confirm', 'confirmed', 'deferred'].includes(
            String(u.status ?? '').toLowerCase(),
          ),
        );
        if (unlock) {
          const st = String(unlock.status).toLowerCase();
          setMovieNightLabel(
            st === 'confirmed'
              ? 'đã mở'
              : st === 'pending_confirm'
                ? 'chờ bố mẹ duyệt'
                : unlock.labelVi?.trim() || 'có tín hiệu hôm nay',
          );
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

  return (
    <div className="home-screen">
      <header className="home-topbar">
        <div className="home-topbar-left">
          <img
            className="home-foxy"
            src="/home/foxy-avatar.png"
            alt=""
            width={48}
            height={48}
          />
          <div>
            <p className="home-hello">Xin chào!</p>
            <button type="button" className="home-family-name" onClick={() => setMoreOpen(true)}>
              {familyName ?? 'Gia đình mình'}
              <span aria-hidden>▾</span>
            </button>
          </div>
        </div>
        <button
          type="button"
          className="home-bell"
          aria-label="Quản trị gia đình"
          title="Quản trị gia đình"
          onClick={() => navigate('/family-admin')}
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden>
            <path
              d="M12 22a2.2 2.2 0 0 0 2.2-2.2h-4.4A2.2 2.2 0 0 0 12 22Zm7-5.2V11a7 7 0 1 0-14 0v5.8L3 19v1h18v-1l-2-2.2Z"
              fill="currentColor"
            />
          </svg>
          {pendingChildTasks > 0 ? (
            <span className="home-bell-dot" aria-hidden />
          ) : null}
        </button>
      </header>

      <section className="home-hero">
        <img
          className="home-hero-art"
          src="/home/family-hero.png"
          alt=""
        />
        <article className="home-trial-card">
          <img
            className="home-trial-badge"
            src="/home/calendar-badge.png"
            alt=""
          />
          <div className="home-trial-head">
            <span className="home-trial-crown" aria-hidden>
              ♛
            </span>
            <div className="home-trial-head-copy">
              <div className="home-trial-title-row">
                <h2>Gói Family OS</h2>
                <span className="home-trial-tag">Starter</span>
              </div>
              {isTrial && trialDaysLeft != null ? (
                <>
                  <p className="home-trial-copy">Gia đình bạn đang dùng thử</p>
                  <p className="home-trial-days">
                    còn <strong>{trialDaysLeft} ngày</strong>
                  </p>
                </>
              ) : sub && !sub.isEntitled ? (
                <p className="home-trial-copy">
                  Gói đã hết hạn — gia hạn để mở lại Daily Flow.
                </p>
              ) : (
                <p className="home-trial-copy">
                  Gói Starter đang hoạt động cho cả nhà.
                </p>
              )}
            </div>
          </div>

          {isTrial && trialDaysLeft != null ? (
            <div
              className="home-trial-bar"
              role="progressbar"
              aria-valuenow={trialDaysLeft}
              aria-valuemin={0}
              aria-valuemax={trialDaysTotal}
              aria-label={`Còn ${trialDaysLeft} trên ${trialDaysTotal} ngày dùng thử`}
            >
              <span style={{ width: `${Math.round(trialProgress * 100)}%` }} />
            </div>
          ) : null}

          <p className="home-trial-desc">
            Cả nhà cùng xây thói quen tốt —
            <br />
            giúp gia đình cùng thay đổi.
          </p>

          <div className="home-trial-actions">
            <button
              type="button"
              className="home-trial-link"
              onClick={() =>
                document.getElementById('home-who')?.scrollIntoView({ behavior: 'smooth' })
              }
            >
              <span aria-hidden>🌱</span> Khám phá tính năng
              <span aria-hidden>›</span>
            </button>
            <button type="button" className="home-trial-cta" onClick={goCheckout}>
              {sub && !sub.isEntitled ? 'Gia hạn ngay' : 'Nâng cấp khi sẵn sàng'}{' '}
              <span aria-hidden>›</span>
            </button>
          </div>

          <ul className="home-trial-perks">
            <li>
              <span aria-hidden>★</span> Giữ lịch sử & kỷ niệm
            </li>
            <li>
              <span aria-hidden>★</span> Báo cáo tiến độ chi tiết
            </li>
            <li>
              <span aria-hidden>★</span> Sao & phần thưởng đặc biệt
            </li>
          </ul>
        </article>
      </section>

      <section className="home-who" id="home-who">
        <div className="home-who-head">
          <div>
            <h2>Con là ai hôm nay?</h2>
            <p>Chọn thành viên để xem công việc và tiến độ trong ngày</p>
          </div>
          <button
            type="button"
            className="home-manage-btn"
            onClick={() => navigate('/family-admin')}
          >
            Quản lý thành viên
          </button>
        </div>

        {error ? <div className="banner-error">{error}</div> : null}

        {children.length === 0 && adults.length === 0 ? (
          <div className="home-empty-members">
            <p>Chưa có thành viên nào. Thêm bố/mẹ và con để bắt đầu ngày hôm nay.</p>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => navigate('/family-admin')}
            >
              Thêm thành viên
            </button>
          </div>
        ) : null}

        {children.length > 0 ? (
          <>
            <div className="home-group-label">Các bạn nhỏ</div>
            <div className="home-member-grid">
              {children.map((member) => (
                <MemberCard key={member.id} member={member} onPick={() => void pick(member)} />
              ))}
            </div>
          </>
        ) : null}

        {adults.length > 0 ? (
          <>
            <div className="home-group-label">Bố mẹ</div>
            <div className="home-member-grid">
              {adults.map((member) => (
                <MemberCard key={member.id} member={member} onPick={() => void pick(member)} />
              ))}
            </div>
          </>
        ) : null}
      </section>

      <section className="home-today">
        <div className="home-today-title">
          <span aria-hidden>💚</span>
          <h2>Hôm nay có gì đặc biệt?</h2>
        </div>
        <div className="home-today-stats">
          <div>
            <span aria-hidden>📅</span>
            <p>
              <strong>{pendingChildTasks}</strong> Công việc đang chờ các con
            </p>
          </div>
          <div>
            <span aria-hidden>⭐</span>
            <p>
              <strong>{starsToday}</strong> Sao còn có thể nhận hôm nay
            </p>
          </div>
          <div>
            <span aria-hidden>{movieNightLabel ? '🎬' : '✅'}</span>
            <p>
              {movieNightLabel ? (
                <>
                  <strong>Movie Night</strong> · {movieNightLabel}
                </>
              ) : (
                <>
                  <strong>
                    {doneToday}/{Math.max(totalToday, 1)}
                  </strong>{' '}
                  Việc đã xong hôm nay
                </>
              )}
            </p>
          </div>
        </div>
        <p className="home-today-foot">
          <span aria-hidden>💗</span>
          Mỗi việc nhỏ hôm nay giúp cả nhà cùng lớn lên — không chỉ riêng con.
        </p>
      </section>

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
          onClick={() => navigate('/family-admin')}
        >
          +
        </button>
        <button type="button" onClick={() => navigate('/family-admin')}>
          <span aria-hidden>⚙️</span>
          Quản trị
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
              Quản trị ngay trên điện thoại — thêm thành viên, việc hôm nay, chế độ gia đình.
              Không cần máy tính.
            </p>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                setMoreOpen(false);
                navigate('/family-admin');
              }}
            >
              Quản trị gia đình
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

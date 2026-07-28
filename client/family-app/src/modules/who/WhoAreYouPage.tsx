import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  createFamilyInvite,
  ensureDayFlow,
  fetchFamilies,
  fetchFamilySubscription,
  fetchTeamUnlocks,
  formatFamilyInviteShare,
  type DayFlow,
  type DayFlowCommitment,
  type FamilyInvite,
  type FamilyMembership,
  type FamilySubscription,
  type TeamUnlock,
} from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { isOnboardingDone } from '@/shared/onboarding/onboarding';
import { hydrateFamilyValueState } from '@/shared/value/value-sync';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';
import { ParentPinSheet } from '@/shared/ui/ParentPinSheet';
import {
  avatarEmoji,
  avatarToneClass,
  inferGenderFromName,
  type AvatarGender,
} from '@/shared/ui/avatarGender';
import {
  isCapabilityPaywallError,
  getApiErrorMessage,
} from '@/shared/billing/capability-error';

type MemberTone = 'pink' | 'blue' | 'purple' | 'green' | 'teal';

function daysUntil(iso?: string): number | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.max(0, Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000)));
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

function memberTone(member: FamilyMembership, gender: AvatarGender): MemberTone {
  if (member.roleCode === 'child') {
    if (gender === 'girl') return 'pink';
    if (gender === 'boy') return 'blue';
    return 'teal';
  }
  if (gender === 'girl' || /mẹ|me|mom|mother/i.test(member.displayName)) return 'purple';
  if (gender === 'boy' || /bố|bo|dad|father/i.test(member.displayName)) return 'green';
  return member.roleCode === 'caregiver' ? 'teal' : 'purple';
}

function decorIcon(tone: MemberTone, isChild: boolean): string {
  if (isChild) return tone === 'pink' ? '⭐' : tone === 'blue' ? '🚀' : '✨';
  if (tone === 'purple') return '❤️';
  if (tone === 'green') return '🛡️';
  return '🌿';
}

function countForMember(commitments: DayFlowCommitment[], memberId: string) {
  const mine = commitments.filter((c) => !c.memberId || c.memberId === memberId);
  const total = mine.filter((c) => c.status !== 'skipped').length;
  const done = mine.filter((c) => c.status === 'done').length;
  const open = mine.filter((c) => c.status !== 'done' && c.status !== 'skipped').length;
  const pct = total > 0 ? Math.round((done / total) * 100) : 0;
  return { total, done, open, pct };
}

function parentAttentionCount(day: DayFlow | null, childIds: Set<string>): number {
  if (!day) return 0;
  let n = 0;
  for (const c of day.commitments) {
    if (c.status === 'done' && c.evidenceUrl && !c.starPosted) n += 1;
    else if (
      c.status !== 'done' &&
      c.status !== 'skipped' &&
      (c.reminderState === 'overdue' || c.reminderState === 'due_now') &&
      (!c.memberId || childIds.has(c.memberId))
    ) {
      n += 1;
    }
  }
  return n;
}

function MemberPickCard({
  member,
  statusLabel,
  progressPct,
  onPick,
}: {
  member: FamilyMembership;
  statusLabel: string;
  progressPct?: number | null;
  onPick: () => void;
}) {
  const gender = inferGenderFromName(member.displayName);
  const isChild = member.roleCode === 'child';
  const age = ageYears(member.dateOfBirth);
  const tone = memberTone(member, gender);
  const meta = isChild
    ? [childRelation(gender), age != null ? `${age} tuổi` : null].filter(Boolean).join(' · ')
    : member.roleCode === 'caregiver'
      ? 'Người chăm sóc'
      : 'Phụ huynh';

  return (
    <button
      type="button"
      className={`home-v2-member home-v2-member--${tone}`}
      onClick={onPick}
    >
      <span className="home-v2-member-avatar-wrap">
        <span className={`home-v2-member-avatar ${avatarToneClass(gender)}`}>
          {avatarEmoji(gender, member.roleCode)}
        </span>
        <i className="home-v2-member-badge" aria-hidden>
          {isChild ? (gender === 'girl' ? '👧' : gender === 'boy' ? '👦' : '🧒') : '👤'}
        </i>
      </span>
      <span className="home-v2-member-body">
        <strong>{member.displayName}</strong>
        <em>{meta}</em>
        <span className="home-v2-member-chip">{statusLabel}</span>
        {progressPct != null ? (
          <span className="home-v2-member-bar" aria-hidden>
            <i style={{ width: `${Math.min(100, Math.max(0, progressPct))}%` }} />
          </span>
        ) : null}
      </span>
      <span className="home-v2-member-decor" aria-hidden>
        {decorIcon(tone, isChild)}
      </span>
      <span className={`home-v2-member-go home-v2-member-go--${tone}`} aria-hidden>
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
  const [day, setDay] = useState<DayFlow | null>(null);
  const [unlock, setUnlock] = useState<TeamUnlock | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pinOpen, setPinOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);
  const [inviteBusy, setInviteBusy] = useState(false);
  const [inviteSheet, setInviteSheet] = useState<FamilyInvite | null>(null);
  const [inviteFeedback, setInviteFeedback] = useState<string | null>(null);
  const [inviteErrorToast, setInviteErrorToast] = useState<string | null>(null);

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
      .then(([families, subscription, dayFlow, unlocks]) => {
        if (cancelled) return;
        const family = families.find((f) => f.id === familyId) ?? families[0];
        setMembers(family?.members ?? []);
        setSub(subscription);
        setDay(dayFlow);
        const active = unlocks.find((u) =>
          ['pending_confirm', 'confirmed'].includes(String(u.status ?? '').toLowerCase()),
        );
        setUnlock(active ?? null);
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
  const childIds = useMemo(
    () => new Set(children.map((c) => c.id)),
    [children],
  );

  const pendingOpen = useMemo(() => {
    if (!day) return 0;
    return day.commitments.filter(
      (c) =>
        c.status !== 'done' &&
        c.status !== 'skipped' &&
        (!c.memberId || childIds.has(c.memberId)),
    ).length;
  }, [day, childIds]);

  const attentionForParents = useMemo(
    () => parentAttentionCount(day, childIds),
    [day, childIds],
  );

  const aiLine =
    pendingOpen > 0
      ? `Gia đình mình ơi! Hôm nay có ${pendingOpen} nhiệm vụ đang chờ hoàn thành nhé!`
      : day && day.totalCommitments > 0
        ? 'Gia đình mình ơi! Hôm nay nhịp đang ổn — chạm tên để xem lịch ngày.'
        : 'Gia đình mình ơi! Chạm tên để mở lịch ngày — bố/mẹ quản trị, con làm việc.';

  const trialDaysLeft =
    sub?.trialDaysRemaining ?? daysUntil(sub?.trialEndsAt) ?? null;
  const isTrial = sub?.status === 'trial';
  const showBilling = !sub || isTrial || !sub.isEntitled;

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
        planCode: 'family_pro_month',
        returnPath: '/who',
      }),
    );
  };

  const goAdmin = () => navigate('/family-admin');

  const showInviteError = (msg: string) => {
    setInviteErrorToast(msg);
    window.setTimeout(() => setInviteErrorToast(null), 3200);
  };

  const inviteShareText = (invite: FamilyInvite) =>
    formatFamilyInviteShare({
      code: invite.code,
      familyName,
      expiresAt: invite.expiresAt,
    });

  const formatInviteExpiry = (iso?: string) => {
    if (!iso) return null;
    const t = Date.parse(iso);
    if (Number.isNaN(t)) return null;
    const d = new Date(t);
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    return `${dd}/${mm}/${d.getFullYear()}`;
  };

  const openInviteSheet = async () => {
    if (!familyId || inviteBusy) return;
    setInviteBusy(true);
    setInviteFeedback(null);
    try {
      const invite = await createFamilyInvite(familyId, {
        roleCode: 'guardian',
        maxUses: 3,
        validDays: 7,
      });
      setInviteSheet(invite);
    } catch (err: unknown) {
      if (isCapabilityPaywallError(err)) {
        showInviteError(
          getApiErrorMessage(err) || 'Gói hiện tại chưa mở mời thành viên — nâng Peace Plan.',
        );
        goCheckout();
        return;
      }
      showInviteError(getApiErrorMessage(err) || 'Chưa tạo được mã mời — thử lại nhé.');
    } finally {
      setInviteBusy(false);
    }
  };

  const copyInviteCode = async () => {
    if (!inviteSheet) return;
    try {
      await shareOrCopyNudge(inviteSheet.code);
      setInviteFeedback('Đã sao chép mã — dán vào Zalo / Messenger');
    } catch {
      setInviteFeedback('Chưa copy được — chọn mã và copy tay nhé');
    }
  };

  const shareInviteSystem = async () => {
    if (!inviteSheet) return;
    try {
      const how = await shareOrCopyNudge(inviteShareText(inviteSheet), { preferShare: true });
      setInviteFeedback(
        how === 'shared'
          ? 'Đã mở chia sẻ — chọn Zalo / Messenger / SMS…'
          : 'Đã copy nội dung mời — dán vào app chat',
      );
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        setInviteFeedback('Bạn đã đóng cửa sổ chia sẻ');
        return;
      }
      setInviteFeedback('Chưa chia sẻ được — thử Sao chép mã');
    }
  };

  const shareInviteSms = () => {
    if (!inviteSheet) return;
    const body = encodeURIComponent(inviteShareText(inviteSheet));
    window.location.href = `sms:?&body=${body}`;
    setInviteFeedback('Đang mở SMS…');
  };

  const unlockStatusVi =
    unlock == null
      ? null
      : String(unlock.status).toLowerCase() === 'confirmed'
        ? 'đã mở'
        : 'chờ duyệt';

  return (
    <div className="home-screen home-screen--v2">
      <header className="home-v2-brand">
        <div className="home-v2-brand-left">
          <img
            className="home-v2-logo"
            src="/home/foxy-avatar.png"
            alt=""
            width={40}
            height={40}
            onError={(e) => {
              (e.currentTarget as HTMLImageElement).style.display = 'none';
            }}
          />
          <div>
            <p className="home-v2-brand-name">Famixa</p>
            <p className="home-v2-brand-sub">AI Family OS</p>
          </div>
        </div>
        <button
          type="button"
          className="home-v2-settings"
          aria-label="Quản trị gia đình"
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
          {attentionForParents > 0 ? <span className="home-bell-dot" aria-hidden /> : null}
        </button>
      </header>

      <div className="home-v2-hello">
        <p className="home-v2-hello-line">Xin chào! 👋</p>
        <button
          type="button"
          className="home-v2-family-pill"
          onClick={() => setMoreOpen(true)}
          aria-label="Tuỳ chọn gia đình"
        >
          {familyName ?? 'Gia đình mình'}
          <span aria-hidden>▾</span>
        </button>
      </div>

      <section className="home-v2-ai" aria-label="Famixa AI">
        <div className="home-v2-ai-mascot" aria-hidden>
          <span>🤖</span>
        </div>
        <div className="home-v2-ai-copy">
          <p>{aiLine}</p>
        </div>
        <button type="button" className="home-v2-ai-manage" onClick={goAdmin}>
          <span aria-hidden>👤</span>
          Quản lý
        </button>
      </section>

      <section className="home-v2-who" id="home-who" aria-label="Ai đang dùng">
        {error ? <div className="banner-error">{error}</div> : null}

        {children.length === 0 && adults.length === 0 ? (
          <div className="home-empty-members">
            <p>Chưa có thành viên. Thêm bố/mẹ và con để bắt đầu.</p>
            <button type="button" className="btn btn-primary" onClick={goAdmin}>
              Thêm thành viên
            </button>
          </div>
        ) : null}

        <div className="home-v2-member-list">
          {children.map((member) => {
            const stats = countForMember(day?.commitments ?? [], member.id);
            return (
              <MemberPickCard
                key={member.id}
                member={member}
                statusLabel={
                  stats.total > 0
                    ? `${stats.total} việc hôm nay`
                    : 'Chưa có việc hôm nay'
                }
                progressPct={stats.total > 0 ? stats.pct : null}
                onPick={() => void pick(member)}
              />
            );
          })}
          {adults.map((member) => (
            <MemberPickCard
              key={member.id}
              member={member}
              statusLabel={
                attentionForParents > 0
                  ? `${attentionForParents} việc cần xử lý`
                  : 'Sẵn sàng đồng hành'
              }
              progressPct={null}
              onPick={() => void pick(member)}
            />
          ))}
        </div>
      </section>

      {unlock ? (
        <section className="home-v2-challenge" aria-label="Family Challenge">
          <div className="home-v2-challenge-copy">
            <p className="home-v2-challenge-kicker">Family Challenge</p>
            <h2>
              {unlock.labelVi || 'Movie Night'} <span aria-hidden>🍿</span>
            </h2>
            <p>
              {unlock.teamDone}/{Math.max(unlock.teamTotal, 1)} thành viên ·{' '}
              {unlockStatusVi}
            </p>
            <div className="home-v2-challenge-bar" aria-hidden>
              <i
                style={{
                  width: `${Math.min(100, Math.max(0, unlock.teamPercent))}%`,
                }}
              />
            </div>
          </div>
          <span className="home-v2-challenge-art" aria-hidden>
            🍿
          </span>
          <button
            type="button"
            className="home-v2-challenge-go"
            aria-label="Mở phần thưởng"
            onClick={() => {
              const parent = adults[0];
              if (parent) void pick(parent);
              else goAdmin();
            }}
          >
            ›
          </button>
        </section>
      ) : null}

      {showBilling ? (
        <section className="home-v2-trial" aria-label="Gói Family OS">
          <div className="home-v2-trial-cal" aria-hidden>
            <strong>{isTrial && trialDaysLeft != null ? trialDaysLeft : '✦'}</strong>
            <span>ngày</span>
          </div>
          <div className="home-v2-trial-copy">
            <p>
              {isTrial && trialDaysLeft != null
                ? `Dùng thử · còn ${trialDaysLeft} ngày`
                : sub && !sub.isEntitled
                  ? 'Gói đã hết hạn'
                  : 'Gói Family OS'}
            </p>
            <em>
              {sub && !sub.isEntitled
                ? sub.upgradeHintVi ||
                  'Nâng Family Peace Plan để mở Coach, ROP và Letter.'
                : isTrial
                  ? 'Nâng cấp Pro để mở khóa toàn bộ tính năng.'
                  : 'Cả nhà cùng thói quen — nâng cấp khi sẵn sàng.'}
            </em>
          </div>
          <button type="button" className="home-v2-trial-cta" onClick={goCheckout}>
            <span aria-hidden>👑</span>
            {sub && !sub.isEntitled
              ? 'Peace Plan'
              : isTrial
                ? 'Giữ Peace Plan'
                : 'Nâng cấp'}
          </button>
        </section>
      ) : null}

      <nav className="home-v2-quick" aria-label="Thao tác nhanh">
        <button type="button" onClick={goAdmin}>
          <i className="is-green" aria-hidden>
            +
          </i>
          Thêm thành viên
        </button>
        <button type="button" disabled={inviteBusy} onClick={() => void openInviteSheet()}>
          <i className="is-blue" aria-hidden>
            👥
          </i>
          {inviteBusy ? 'Đang tạo…' : 'Mời tham gia'}
        </button>
        <button type="button" onClick={goCheckout}>
          <i className="is-orange" aria-hidden>
            🎁
          </i>
          Ưu đãi Famixa
        </button>
      </nav>

      {inviteErrorToast ? (
        <div className="home-v2-toast" role="status">
          {inviteErrorToast}
        </div>
      ) : null}

      {inviteSheet ? (
        <div
          className="home-sheet-backdrop"
          onClick={() => {
            setInviteSheet(null);
            setInviteFeedback(null);
          }}
        >
          <div
            className="home-sheet home-invite-sheet"
            role="dialog"
            aria-modal="true"
            aria-labelledby="home-invite-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="home-invite-sheet-head">
              <h3 id="home-invite-title">Mời tham gia nhà</h3>
              <button
                type="button"
                className="home-invite-close"
                aria-label="Đóng"
                onClick={() => {
                  setInviteSheet(null);
                  setInviteFeedback(null);
                }}
              >
                ×
              </button>
            </div>
            <p className="home-invite-lead">
              Gửi mã cho bố/mẹ khác — họ mở Famixa → <strong>Tham gia bằng mã</strong>.
            </p>
            <div className="home-invite-code-box">
              <span className="home-invite-code-label">Mã mời</span>
              <strong className="home-invite-code">{inviteSheet.code}</strong>
              <em>
                {formatInviteExpiry(inviteSheet.expiresAt)
                  ? `Hết hạn ${formatInviteExpiry(inviteSheet.expiresAt)}`
                  : 'Có hiệu lực vài ngày'}
                {' · '}
                dùng tối đa {inviteSheet.maxUses} lần
              </em>
            </div>
            <div className="home-invite-actions">
              <button type="button" className="btn btn-primary" onClick={() => void copyInviteCode()}>
                Sao chép mã
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => void shareInviteSystem()}>
                Chia sẻ… (Zalo / Messenger…)
              </button>
              <button type="button" className="btn btn-ghost" onClick={shareInviteSms}>
                Gửi SMS
              </button>
            </div>
            {inviteFeedback ? (
              <p className="home-invite-feedback" role="status">
                {inviteFeedback}
              </p>
            ) : (
              <p className="home-invite-hint muted">
                “Chia sẻ…” mở menu hệ thống — chọn Zalo, Messenger hoặc app chat khác.
              </p>
            )}
          </div>
        </div>
      ) : null}

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

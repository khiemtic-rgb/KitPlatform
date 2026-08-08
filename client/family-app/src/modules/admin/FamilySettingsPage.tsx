import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  fetchFamilies,
  fetchFamilyBlueprint,
  fetchFamilySubscription,
  fetchSchoolQuietMap,
  patchFamilyBlueprintLayers,
  type FamilyMembership,
  type FamilySubscription,
} from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import { useSessionStore } from '@/shared/auth/session.store';
import {
  fetchParentPushStatus,
  isParentPushSupported,
  registerParentPushSubscription,
  subscribeParentPush,
} from '@/shared/push/parentPush';
import {
  ensureNotificationPermission,
  notificationPermission,
  notificationSupport,
  shouldOfferNotificationOptIn,
} from '@/shared/reminders/localReminders';
import {
  isInAppChimeEnabled,
  playInAppDueChime,
  setInAppChimeEnabled,
} from '@/shared/reminders/inAppChime';
import { clearOnboardingProfile } from '@/shared/onboarding/onboarding';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';
import { ResetParentPinPanel } from '@/shared/ui/ResetParentPinPanel';
import { buildTrialLifecycle } from '@/shared/billing/trial-lifecycle';
import { FamilyAdminShell, ROLE_LABEL } from '@/modules/admin/FamilyAdminShell';
import {
  CHILD_PRAISE_OPTIONS,
  CONTEXT_CHIP_OPTIONS,
  contextChipsPatch,
  memberPraisePatch,
  parseLayersJson,
  readContextChips,
  readMemberPraiseStyle,
} from '@/shared/value/soft-calibration';
import {
  SCHOOL_MODE_OPTIONS,
  hydrateSchoolSchedulesFromLayers,
  resolveSchoolSchedule,
  saveSchoolSchedule,
  schoolPhaseLabelVi,
  syncSaveSchoolSchedule,
  type SchoolDayMode,
  type SchoolSeasonSchedule,
} from '@/shared/school/school-season';

const MASTER_REMINDERS_KEY = 'famixa.reminders.master.v1';
const APP_VERSION = 'v1.0.0';
const SUPPORT_MAIL = 'mailto:support@famixa.vn?subject=Famixa%20h%E1%BB%97%20tr%E1%BB%A3';
const TERMS_URL = 'https://famixa.vn';

function daysLeft(iso?: string | null, trialDays?: number | null): number | null {
  if (trialDays != null) return trialDays;
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.max(0, Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000)));
}

function isMasterRemindersOn(): boolean {
  if (typeof window === 'undefined') return true;
  const raw = localStorage.getItem(MASTER_REMINDERS_KEY);
  if (raw == null) return true;
  return raw === '1' || raw === 'true';
}

function setMasterRemindersOn(on: boolean) {
  localStorage.setItem(MASTER_REMINDERS_KEY, on ? '1' : '0');
}

function memberEmoji(m: FamilyMembership): string {
  return avatarEmoji(inferGenderFromName(m.displayName), m.roleCode);
}

export function FamilySettingsPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const member = useSessionStore((s) => s.member);
  const clear = useSessionStore((s) => s.clear);

  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [subLoading, setSubLoading] = useState(true);
  const [members, setMembers] = useState<FamilyMembership[]>([]);
  const [pushSubscribed, setPushSubscribed] = useState(false);
  const [pushBusy, setPushBusy] = useState(false);
  const [localPerm, setLocalPerm] = useState(() => notificationPermission());
  const [offerLocal, setOfferLocal] = useState(() => shouldOfferNotificationOptIn());
  const [chimeOn, setChimeOn] = useState(() => isInAppChimeEnabled());
  const [masterOn, setMasterOn] = useState(() => isMasterRemindersOn());
  const [notifyDetailOpen, setNotifyDetailOpen] = useState(false);
  const [notifyMsg, setNotifyMsg] = useState<string | null>(null);
  const [confirmLogout, setConfirmLogout] = useState(false);
  const [pinOpen, setPinOpen] = useState(false);
  const [contextChips, setContextChips] = useState<string[]>([]);
  const [memberPraise, setMemberPraise] = useState<Record<string, string>>({});
  const [houseBusy, setHouseBusy] = useState(false);
  const [houseMsg, setHouseMsg] = useState<string | null>(null);
  const [schoolByChild, setSchoolByChild] = useState<Record<string, SchoolSeasonSchedule>>(
    {},
  );
  const [schoolMsg, setSchoolMsg] = useState<string | null>(null);
  const [schoolPhaseByChild, setSchoolPhaseByChild] = useState<
    Record<string, { phase: string; quietNow: boolean; quietEnd: string }>
  >({});
  const schoolSyncTimers = useRef<Record<string, number>>({});

  const childMembers = useMemo(
    () => members.filter((m) => m.roleCode === 'child'),
    [members],
  );

  const reloadSub = useCallback(async () => {
    if (!familyId) {
      setSubscription(null);
      setSubLoading(false);
      return;
    }
    setSubLoading(true);
    try {
      setSubscription(await fetchFamilySubscription(familyId));
    } catch {
      setSubscription(null);
    } finally {
      setSubLoading(false);
    }
  }, [familyId]);

  const reloadMembers = useCallback(async () => {
    if (!familyId) {
      setMembers([]);
      return;
    }
    try {
      const families = await fetchFamilies();
      const fam = families.find((f) => f.id === familyId) ?? families[0];
      setMembers(fam?.members ?? []);
    } catch {
      setMembers([]);
    }
  }, [familyId]);

  const reloadPush = useCallback(async () => {
    if (!familyId || !member || member.roleCode === 'child') {
      setPushSubscribed(false);
      return;
    }
    try {
      const s = await fetchParentPushStatus(familyId, member.id);
      setPushSubscribed(s.subscribed);
    } catch {
      setPushSubscribed(false);
    }
  }, [familyId, member]);

  const reloadHouseContext = useCallback(async () => {
    if (!familyId) {
      setContextChips([]);
      setMemberPraise({});
      setSchoolByChild({});
      setSchoolPhaseByChild({});
      return;
    }
    try {
      const bp = await fetchFamilyBlueprint(familyId);
      const layers = parseLayersJson(bp?.layersJson);
      setContextChips(readContextChips(layers));
      const kids = members.filter((x) => x.roleCode === 'child');
      const praise: Record<string, string> = {};
      for (const m of kids) {
        const style = readMemberPraiseStyle(layers, m.id);
        if (style) praise[m.id] = style;
      }
      setMemberPraise(praise);

      const { byMember, toPush } = hydrateSchoolSchedulesFromLayers(
        familyId,
        kids.map((m) => m.id),
        layers,
      );
      setSchoolByChild(byMember);
      // SCH-01a: push local-only / newer local schedules to blueprint
      for (const sch of toPush) {
        void syncSaveSchoolSchedule(familyId, sch, member?.id).catch(() => {
          /* keep local mirror; next open retries */
        });
      }

      try {
        const quiet = await fetchSchoolQuietMap(familyId);
        const phases: Record<string, { phase: string; quietNow: boolean; quietEnd: string }> = {};
        for (const m of quiet.members) {
          phases[m.memberId] = {
            phase: m.phase,
            quietNow: m.quietNow,
            quietEnd: m.quietEnd,
          };
        }
        setSchoolPhaseByChild(phases);
      } catch {
        setSchoolPhaseByChild({});
      }
    } catch {
      setContextChips([]);
      setMemberPraise({});
      setSchoolPhaseByChild({});
      const kidsFallback = members.filter((x) => x.roleCode === 'child');
      if (kidsFallback.length > 0) {
        const fallback: Record<string, SchoolSeasonSchedule> = {};
        for (const m of kidsFallback) {
          fallback[m.id] = resolveSchoolSchedule(m.id, familyId);
        }
        setSchoolByChild(fallback);
      } else {
        setSchoolByChild({});
      }
    }
  }, [familyId, members, member?.id]);

  useEffect(() => {
    void reloadSub();
    void reloadPush();
    void reloadMembers();
  }, [reloadSub, reloadPush, reloadMembers]);

  useEffect(() => {
    void reloadHouseContext();
  }, [reloadHouseContext]);

  useEffect(() => {
    const timers = schoolSyncTimers.current;
    return () => {
      for (const id of Object.keys(timers)) {
        window.clearTimeout(timers[id]);
      }
    };
  }, []);

  const patchChildSchool = (
    memberId: string,
    patch: Partial<SchoolSeasonSchedule>,
  ) => {
    const prev =
      schoolByChild[memberId] ?? resolveSchoolSchedule(memberId, familyId);
    const next: SchoolSeasonSchedule = {
      ...prev,
      ...patch,
      memberId,
      source: 'parent_settings',
      updatedByMemberId: member?.id,
      updatedAt: new Date().toISOString(),
      schemaVersion: 1,
    };
    saveSchoolSchedule(next);
    setSchoolByChild((s) => ({ ...s, [memberId]: next }));
    setSchoolMsg('Đã lưu lịch mùa học — Fami im lặng đúng giờ từng con.');
    window.setTimeout(() => setSchoolMsg(null), 2800);

    if (!familyId) return;
    window.clearTimeout(schoolSyncTimers.current[memberId]);
    schoolSyncTimers.current[memberId] = window.setTimeout(() => {
      void syncSaveSchoolSchedule(familyId, next, member?.id)
        .then(async (saved) => {
          setSchoolByChild((s) => ({ ...s, [memberId]: saved }));
          try {
            const quiet = await fetchSchoolQuietMap(familyId);
            const phases: Record<string, { phase: string; quietNow: boolean; quietEnd: string }> =
              {};
            for (const row of quiet.members) {
              phases[row.memberId] = {
                phase: row.phase,
                quietNow: row.quietNow,
                quietEnd: row.quietEnd,
              };
            }
            setSchoolPhaseByChild(phases);
          } catch {
            /* keep previous phase badge */
          }
        })
        .catch(() => {
          setSchoolMsg('Đã lưu trên máy — chưa đồng bộ cloud. Thử lại sau.');
          window.setTimeout(() => setSchoolMsg(null), 3200);
        });
    }, 400);
  };

  const toggleContextChip = async (code: string) => {
    if (!familyId || houseBusy) return;
    const prev = contextChips;
    const next = contextChips.includes(code)
      ? contextChips.filter((c) => c !== code)
      : [...contextChips, code];
    setContextChips(next);
    setHouseBusy(true);
    setHouseMsg(null);
    try {
      await patchFamilyBlueprintLayers(familyId, contextChipsPatch(next));
      setHouseMsg('Đã cập nhật bối cảnh nhà mình.');
    } catch {
      setContextChips(prev);
      setHouseMsg('Chưa lưu được. Thử lại sau.');
    } finally {
      setHouseBusy(false);
    }
  };

  const setChildPraise = async (memberId: string, code: string) => {
    if (!familyId || houseBusy) return;
    const prev = memberPraise[memberId];
    const nextCode = prev === code ? '' : code;
    setMemberPraise((m) => {
      const copy = { ...m };
      if (nextCode) copy[memberId] = nextCode;
      else delete copy[memberId];
      return copy;
    });
    if (!nextCode) return;
    setHouseBusy(true);
    setHouseMsg(null);
    try {
      await patchFamilyBlueprintLayers(familyId, memberPraisePatch(memberId, nextCode));
      setHouseMsg('Đã nhớ cách động viên từng con.');
    } catch {
      setMemberPraise((m) => {
        const copy = { ...m };
        if (prev) copy[memberId] = prev;
        else delete copy[memberId];
        return copy;
      });
      setHouseMsg('Chưa lưu được. Thử lại sau.');
    } finally {
      setHouseBusy(false);
    }
  };

  useEffect(() => {
    const hash = window.location.hash.replace('#', '');
    if (!hash) return;
    window.requestAnimationFrame(() => {
      document.getElementById(`fa-set-${hash}`)?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
      if (hash === 'notify' || hash === 'reminders') setNotifyDetailOpen(true);
      if (hash === 'pin') setPinOpen(true);
    });
  }, []);
  const trialLife = useMemo(() => buildTrialLifecycle(subscription), [subscription]);

  const peaceDays = useMemo(() => {
    if (trialLife.phase === 'grace') return trialLife.daysLeft;
    if (trialLife.phase === 'trial') return trialLife.daysLeft;
    return daysLeft(subscription?.currentPeriodEnd, null);
  }, [subscription, trialLife]);

  const planTitle =
    subscription?.displayNameVi ||
    subscription?.outcomeNameVi ||
    (subLoading ? 'Đang tải…' : 'Chưa có gói');

  const planActive = Boolean(subscription?.isEntitled);

  const remindSummary = useMemo(() => {
    if (!masterOn) return 'Đã tắt nhắc trên thiết bị này';
    const parts: string[] = [];
    if (pushSubscribed) parts.push('Push');
    if (localPerm === 'granted') parts.push('Trình duyệt');
    if (chimeOn) parts.push('Chuông app');
    if (parts.length === 0) return 'Bật — chưa cấu hình kênh nào';
    return `Bật · ${parts.join(' · ')}`;
  }, [masterOn, pushSubscribed, localPerm, chimeOn]);

  const goCheckout = () => {
    if (!familyId) return;
    navigate(
      buildCheckoutPath({
        productCode: 'family_os',
        subjectType: 'family',
        subjectId: familyId,
        planCode: subscription?.recommendedUpgradePlanCode || 'family_pro_month',
        returnPath: '/family-admin/settings',
      }),
    );
  };

  const toggleMaster = () => {
    const next = !masterOn;
    setMasterRemindersOn(next);
    setMasterOn(next);
    setNotifyMsg(null);
    if (!next) {
      setInAppChimeEnabled(false);
      setChimeOn(false);
      setNotifyMsg('Đã tắt nhắc trên thiết bị này. Chi tiết kênh vẫn giữ khi bạn bật lại.');
      return;
    }
    if (!isInAppChimeEnabled()) {
      setInAppChimeEnabled(true);
      setChimeOn(true);
    }
    setNotifyMsg('Đã bật nhắc việc. Mở chi tiết để chọn kênh.');
  };

  const enablePush = async () => {
    if (!familyId || !member) return;
    setPushBusy(true);
    setNotifyMsg(null);
    try {
      if (!isParentPushSupported()) {
        setNotifyMsg('Trình duyệt không hỗ trợ Web Push.');
        return;
      }
      const status = await fetchParentPushStatus(familyId, member.id);
      if (!status.supported || !status.publicKey) {
        setNotifyMsg('Push chưa cấu hình trên server.');
        return;
      }
      await Notification.requestPermission();
      const sub = await subscribeParentPush(status.publicKey);
      await registerParentPushSubscription(familyId, {
        membershipId: member.id,
        ...sub,
      });
      setPushSubscribed(true);
      if (!masterOn) {
        setMasterRemindersOn(true);
        setMasterOn(true);
      }
      setNotifyMsg('Đã bật nhắc push phụ huynh.');
    } catch {
      setNotifyMsg('Chưa bật được push. Thử Chrome/Edge (HTTPS hoặc localhost).');
    } finally {
      setPushBusy(false);
    }
  };

  const enableLocalReminders = async () => {
    setNotifyMsg(null);
    const permission = await ensureNotificationPermission();
    setLocalPerm(permission);
    setOfferLocal(shouldOfferNotificationOptIn());
    if (permission === 'granted') {
      if (!masterOn) {
        setMasterRemindersOn(true);
        setMasterOn(true);
      }
      setNotifyMsg('Đã cho phép nhắc trên máy (trình duyệt).');
    } else if (permission === 'denied') {
      setNotifyMsg('Trình duyệt đang chặn thông báo — mở lại trong cài đặt trình duyệt.');
    }
  };

  const toggleChime = () => {
    if (!masterOn) {
      setNotifyMsg('Bật “Nhắc việc” trước, rồi chỉnh chuông trong app.');
      return;
    }
    const next = !isInAppChimeEnabled();
    setInAppChimeEnabled(next);
    setChimeOn(next);
    if (next) void playInAppDueChime();
  };

  const logoutDevice = () => {
    clear();
    navigate('/unlock', { replace: true });
  };

  return (
    <FamilyAdminShell
      title="Tài khoản & Cài đặt"
      subtitle="Quản lý tài khoản, gia đình và ứng dụng"
      backTo="/today"
    >
      {/* 1. Tài khoản & gói */}
      <section className="fa-set-hero" id="fa-set-billing" aria-label="Tài khoản và gói">
        <div className="fa-set-hero-top">
          <div className="fa-set-avatar" aria-hidden>
            🏡
          </div>
          <div className="fa-set-hero-copy">
            <h2>{familyName ?? 'Nhà mình'}</h2>
            <div className="fa-set-badges">
              <span className={`fa-set-pill${planActive ? ' is-pro' : ''}`}>{planTitle}</span>
              {subscription?.status === 'trial' ? (
                <span className="fa-set-pill is-soft">Dùng thử Pro</span>
              ) : null}
              {subscription?.status === 'trial_grace' ? (
                <span className="fa-set-pill is-soft">Ân hạn 3 ngày</span>
              ) : null}
            </div>
            <p className="fa-set-meta">
              {member
                ? `Đang dùng với tư cách ${ROLE_LABEL[member.roleCode] || member.roleCode}`
                : 'Famixa · tài khoản gia đình'}
            </p>
          </div>
        </div>

        {members.length > 0 ? (
          <div className="fa-set-faces" aria-label={`${members.length} thành viên`}>
            {members.slice(0, 5).map((m) => (
              <span
                key={m.id}
                className="fa-set-face"
                title={`${m.displayName} · ${ROLE_LABEL[m.roleCode] || m.roleCode}`}
              >
                {memberEmoji(m)}
              </span>
            ))}
            <button
              type="button"
              className="fa-set-face is-add"
              aria-label="Thêm thành viên"
              onClick={() => navigate('/family-admin/members')}
            >
              +
            </button>
          </div>
        ) : null}

        <div className="fa-set-plan">
          <div>
            <strong>{planTitle}</strong>
            <em>
              {trialLife.phase === 'trial' || trialLife.phase === 'grace' || trialLife.phase === 'free'
                ? trialLife.message
                : peaceDays != null
                  ? `Chu kỳ hiện tại · còn ${peaceDays} ngày`
                  : planActive
                    ? 'Đang hoạt động'
                    : subLoading
                      ? 'Đang tải…'
                      : 'Chưa kích hoạt gói trả phí'}
            </em>
          </div>
          <button type="button" className="btn btn-primary" onClick={goCheckout} disabled={!familyId}>
            Gia hạn gói
          </button>
        </div>
      </section>

      {/* 2. Nhắc việc — master + chi tiết thu gọn */}
      <section className="fa-card" id="fa-set-notify">
        <div className="fa-set-sec-head">
          <h2>
            <span aria-hidden>🔔</span> Nhắc việc
          </h2>
          <button
            type="button"
            className={`fa-set-switch${masterOn ? ' is-on' : ''}`}
            role="switch"
            aria-checked={masterOn}
            aria-label="Bật nhắc việc trên thiết bị này"
            onClick={toggleMaster}
          >
            <i />
          </button>
        </div>
        <p className="fa-hint">{remindSummary}</p>
        <button
          type="button"
          className="fa-set-link-row"
          onClick={() => setNotifyDetailOpen((v) => !v)}
          aria-expanded={notifyDetailOpen}
        >
          <span>Chi tiết kênh nhắc</span>
          <em>{notifyDetailOpen ? 'Thu gọn' : 'Mở'}</em>
        </button>

        {notifyDetailOpen ? (
          <div className="fa-set-notify-detail">
            {!masterOn ? (
              <p className="fa-hint">
                Master đang tắt — bật “Nhắc việc” ở trên trước khi dùng từng kênh.
              </p>
            ) : null}
            <div className="fa-settings-row">
              <div>
                <strong>Thông báo đẩy</strong>
                <em>{pushSubscribed ? 'Đã bật trên thiết bị này' : 'Chưa bật'}</em>
              </div>
              {pushSubscribed ? (
                <span className="fa-settings-badge is-on">Bật</span>
              ) : (
                <button
                  type="button"
                  className="pill"
                  disabled={pushBusy || !member || member.roleCode === 'child' || !masterOn}
                  onClick={() => void enablePush()}
                >
                  {pushBusy ? 'Đang bật…' : 'Bật push'}
                </button>
              )}
            </div>
            <div className="fa-settings-row">
              <div>
                <strong>Trình duyệt</strong>
                <em>
                  {!notificationSupport()
                    ? 'Không hỗ trợ'
                    : localPerm === 'granted'
                      ? 'Đã cho phép'
                      : localPerm === 'denied'
                        ? 'Đang bị chặn'
                        : offerLocal
                          ? 'Chưa hỏi quyền'
                          : 'Chờ cấp quyền'}
                </em>
              </div>
              {localPerm !== 'granted' && notificationSupport() ? (
                <button
                  type="button"
                  className="pill is-soft"
                  disabled={!masterOn}
                  onClick={() => void enableLocalReminders()}
                >
                  Cho phép
                </button>
              ) : localPerm === 'granted' ? (
                <span className="fa-settings-badge is-on">Bật</span>
              ) : null}
            </div>
            <div className="fa-settings-row">
              <div>
                <strong>Chuông trong app</strong>
                <em>Chỉ khi đang mở Hôm nay</em>
              </div>
              <button
                type="button"
                className={`pill${chimeOn && masterOn ? '' : ' is-soft'}`}
                onClick={toggleChime}
              >
                {chimeOn && masterOn ? 'Bật' : 'Tắt'}
              </button>
            </div>
            <p className="fa-hint">Tùy chỉnh việc · giờ · tần suất — sắp có.</p>
          </div>
        ) : null}

        {notifyMsg ? (
          <p className="fa-hint" role="status">
            {notifyMsg}
          </p>
        ) : null}
      </section>

      {/* 3. Famixa hiểu nhà mình — progressive chips (không thu nhập) */}
      <section className="fa-card" id="fa-set-house-dna">
        <h2>
          <span aria-hidden>🌿</span> Famixa hiểu nhà mình
        </h2>
        <p className="fa-hint">
          Vài chip bối cảnh — không hỏi thu nhập. Giúp gợi ý đúng nhịp nhà bạn.
        </p>
        <div className="fa-house-chips" role="group" aria-label="Bối cảnh nhà">
          {CONTEXT_CHIP_OPTIONS.map((o) => {
            const on = contextChips.includes(o.code);
            return (
              <button
                key={o.code}
                type="button"
                className={`fa-house-chip${on ? ' is-on' : ''}`}
                aria-pressed={on}
                disabled={houseBusy || !familyId}
                onClick={() => void toggleContextChip(o.code)}
              >
                {o.labelVi}
              </button>
            );
          })}
        </div>
        {childMembers.length > 0 ? (
          <div className="fa-house-kids">
            <strong>Cách động viên từng con</strong>
            {childMembers.map((m) => (
              <div key={m.id} className="fa-house-kid-row">
                <em>
                  {memberEmoji(m)} {m.displayName}
                </em>
                <div className="fa-house-chips" role="group">
                  {CHILD_PRAISE_OPTIONS.map((o) => {
                    const on = memberPraise[m.id] === o.code;
                    return (
                      <button
                        key={o.code}
                        type="button"
                        className={`fa-house-chip is-soft${on ? ' is-on' : ''}`}
                        aria-pressed={on}
                        disabled={houseBusy || !familyId}
                        onClick={() => void setChildPraise(m.id, o.code)}
                      >
                        {o.labelVi}
                      </button>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        ) : null}
        {houseMsg ? (
          <p className="fa-hint" role="status">
            {houseMsg}
          </p>
        ) : null}
      </section>

      {/* 3b. Mùa học — quiet hours + động viên tối */}
      <section className="fa-card" id="fa-set-school-season">
        <h2>
          <span aria-hidden>🎒</span> Mùa học của con
        </h2>
        <p className="fa-hint">
          Giờ học Fami im lặng (không nhắc trên máy). Sáng vội / tan học mới ghi nhận. Mỗi con một
          lịch.
        </p>
        {childMembers.length === 0 ? (
          <p className="fa-hint">Thêm thành viên trẻ để chỉnh lịch học.</p>
        ) : (
          <div className="fa-school-list">
            {childMembers.map((m) => {
              const sch =
                schoolByChild[m.id] ?? resolveSchoolSchedule(m.id, familyId);
              const live = schoolPhaseByChild[m.id];
              return (
                <div key={m.id} className="fa-school-card">
                  <strong>
                    {memberEmoji(m)} {m.displayName}
                  </strong>
                  {live ? (
                    <p
                      className={`fa-hint fa-school-phase${live.quietNow ? ' is-quiet' : ''}`}
                      role="status"
                    >
                      Bây giờ: {schoolPhaseLabelVi(live.phase)}
                      {live.quietNow && live.quietEnd
                        ? ` · im tới ${live.quietEnd}`
                        : ''}
                    </p>
                  ) : null}
                  <div className="fa-house-chips" role="group" aria-label="Chế độ học">
                    {SCHOOL_MODE_OPTIONS.map((o) => {
                      const selected =
                        o.value === 'off'
                          ? !sch.seasonOn || sch.mode === 'off'
                          : sch.seasonOn && sch.mode === o.value;
                      return (
                        <button
                          key={o.value}
                          type="button"
                          className={`fa-house-chip${selected ? ' is-on' : ''}`}
                          aria-pressed={selected}
                          title={o.hint}
                          onClick={() => {
                            if (o.value === 'off') {
                              patchChildSchool(m.id, { seasonOn: false, mode: 'off' });
                            } else {
                              patchChildSchool(m.id, {
                                seasonOn: true,
                                mode: o.value as SchoolDayMode,
                              });
                            }
                          }}
                        >
                          {o.label}
                        </button>
                      );
                    })}
                  </div>
                  {sch.seasonOn && sch.mode !== 'off' ? (
                    <div className="fa-school-times">
                      <label>
                        Vào học
                        <input
                          type="time"
                          value={sch.schoolStart}
                          onChange={(e) =>
                            patchChildSchool(m.id, { schoolStart: e.target.value })
                          }
                        />
                      </label>
                      <label>
                        Tan học
                        <input
                          type="time"
                          value={sch.schoolEnd}
                          onChange={(e) =>
                            patchChildSchool(m.id, { schoolEnd: e.target.value })
                          }
                        />
                      </label>
                      <label className="fa-school-extra">
                        <input
                          type="checkbox"
                          checked={sch.hasExtraClass}
                          onChange={(e) =>
                            patchChildSchool(m.id, {
                              hasExtraClass: e.target.checked,
                              extraEnd: e.target.checked
                                ? sch.extraEnd || '18:30'
                                : undefined,
                            })
                          }
                        />
                        Có học thêm
                      </label>
                      {sch.hasExtraClass ? (
                        <label>
                          Xong học thêm
                          <input
                            type="time"
                            value={sch.extraEnd || '18:30'}
                            onChange={(e) =>
                              patchChildSchool(m.id, { extraEnd: e.target.value })
                            }
                          />
                        </label>
                      ) : null}
                    </div>
                  ) : (
                    <p className="fa-hint">Đang nghỉ mùa học — nhắc việc bình thường.</p>
                  )}
                </div>
              );
            })}
          </div>
        )}
        {schoolMsg ? (
          <p className="fa-hint" role="status">
            {schoolMsg}
          </p>
        ) : null}
      </section>

      {/* 4. Nhà & thành viên — không lặp tên/ảnh hero */}
      <section className="fa-card" id="fa-set-house">
        <h2>
          <span aria-hidden>🏠</span> Nhà &amp; thành viên
        </h2>
        <div className="fa-settings-row">
          <div>
            <strong>Múi giờ</strong>
            <em>Asia/Ho_Chi_Minh · lịch và nhắc theo giờ Việt Nam</em>
          </div>
          <span className="fa-settings-badge">VN</span>
        </div>
        <button
          type="button"
          className="fa-set-link-row"
          onClick={() => navigate('/family-admin')}
        >
          <span>Quản trị gia đình</span>
          <em>Thành viên · routine</em>
        </button>
        <button
          type="button"
          className="fa-set-link-row"
          onClick={() => navigate('/family-admin/members')}
        >
          <span>Thành viên</span>
          <em>{members.length > 0 ? `${members.length} người` : 'Thêm bố/mẹ hoặc con'}</em>
        </button>
        <button
          type="button"
          className="fa-set-link-row"
          onClick={() => setPinOpen((v) => !v)}
          aria-expanded={pinOpen}
        >
          <span>Mã PIN bố mẹ</span>
          <em>{pinOpen ? 'Thu gọn' : '4 số · trên máy này'}</em>
        </button>
        {pinOpen ? (
          <div className="fa-set-pin-wrap" id="fa-set-pin">
            <ResetParentPinPanel />
          </div>
        ) : null}
        <button
          type="button"
          className="fa-set-link-row"
          onClick={() => navigate('/who')}
        >
          <span>Đổi người / thành viên</span>
          <em>Chọn ai đang dùng máy này</em>
        </button>
        <button
          type="button"
          className="fa-set-link-row"
          onClick={() => {
            if (familyId) clearOnboardingProfile(familyId);
            navigate('/onboarding');
          }}
        >
          <span>Chạy lại AI Onboarding</span>
          <em>Thiết lập lại hành trình 30 ngày</em>
        </button>
      </section>

      {/* 5. An toàn & hỗ trợ */}
      <section className="fa-card" id="fa-set-leave">
        <h2>
          <span aria-hidden>🛡️</span> An toàn &amp; hỗ trợ
        </h2>
        {!confirmLogout ? (
          <button type="button" className="fa-set-link-row" onClick={() => setConfirmLogout(true)}>
            <span>Đăng xuất thiết bị</span>
            <em>Xóa phiên trên máy này · dữ liệu nhà vẫn giữ</em>
          </button>
        ) : (
          <div className="fa-danger-row" style={{ marginBottom: 8 }}>
            <button type="button" className="btn btn-primary" onClick={logoutDevice}>
              Xác nhận đăng xuất
            </button>
            <button type="button" className="pill is-soft" onClick={() => setConfirmLogout(false)}>
              Huỷ
            </button>
          </div>
        )}
        <a className="fa-set-link-row" href={SUPPORT_MAIL}>
          <span>Trung tâm hỗ trợ</span>
          <em>support@famixa.vn</em>
        </a>
        <a className="fa-set-link-row" href={TERMS_URL} target="_blank" rel="noreferrer">
          <span>Điều khoản &amp; chính sách</span>
          <em>famixa.vn</em>
        </a>
        <div className="fa-set-link-row is-static">
          <span>Phiên bản ứng dụng</span>
          <em>{APP_VERSION}</em>
        </div>
      </section>

      <section className="fa-card fa-card-warn" id="fa-set-delete">
        <h2>Xóa nhà</h2>
        <p className="fa-hint">
          Xóa vĩnh viễn thành viên và lịch chưa mở trên app — Famixa xử lý qua hỗ trợ để tránh mất dữ
          liệu nhầm.
        </p>
        <a className="pill is-soft" href={SUPPORT_MAIL}>
          Liên hệ hỗ trợ →
        </a>
      </section>
    </FamilyAdminShell>
  );
}

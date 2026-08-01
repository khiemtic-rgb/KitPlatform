import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  BellOutlined,
  CalendarOutlined,
  CheckCircleOutlined,
  CheckOutlined,
  FileTextOutlined,
  FireFilled,
  GiftOutlined,
  HeartFilled,
  HeartOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  PlusOutlined,
  RightOutlined,
  RobotOutlined,
  ShoppingOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { Badge, message } from 'antd';
import dayjs from 'dayjs';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import {
  fetchDraftOrders,
  fetchDueReminders,
  fetchFamilyDueReminders,
  fetchFamilyMembers,
  fetchHomeSummary,
  fetchLoyaltySummary,
  fetchMedicationAdherenceSummary,
  fetchRepurchaseSuggestions,
  getApiErrorMessage,
  respondMedicationReminder,
  type CustomerConnectInbox,
} from '@/shared/api/customer-app.api';
import axios from 'axios';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  type FamilyMember,
  type MedicationReminder,
} from '@/shared/api/customer-app.types';
import { prefetchPrimaryTabOverviews, useChatOverviewQuery } from '@/shared/api/overview-queries';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useVerifyAccount } from '@/shared/auth/VerifyAccountProvider';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { usePharmacyLink } from '@/shared/config/PharmacyLinkProvider';
import { useCustomerNotificationCount } from '@/shared/hooks/useCustomerNotificationCount';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { familyRoleLabel, resolveFamilyGender } from '@/shared/i18n/family-role-label';
import { SkipReasonModal } from '@/modules/reminders/SkipReasonModal';
import type { MedSkipReasonCode } from '@/shared/care/med-skip-reasons';
import './HomePage.css';

type Adherence = {
  dueCount: number;
  takenToday: number;
  skippedToday: number;
  scheduledToday: number;
  missedStreakDays: number;
  showMissedAlert: boolean;
};

type HomeTask =
  | {
      key: string;
      kind: 'med';
      time: string;
      title: string;
      sub: string;
      reminderId: string;
      done?: boolean;
    }
  | {
      key: string;
      kind: 'cal' | 'order';
      time: string;
      title: string;
      sub: string;
      to: string;
    };

/** Tên gọi ngắn (từ cuối họ tên VN) — chỉ dùng khi trùng quan hệ. */
function shortGivenName(fullName: string) {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  const given = parts[parts.length - 1] ?? '';
  if (!given) return '';
  return given.length > 8 ? `${given.slice(0, 7)}…` : given;
}

/** Avatar minh họa theo vai / giới tính. */
function roleAvatarSrc(
  relationship: string,
  gender: number | null | undefined,
  seed?: string,
): string {
  const inferred = resolveFamilyGender(gender, seed) ?? (relationship === 'child' ? 1 : 2);

  if (relationship === 'self') {
    return inferred === 1 ? '/home/avatars/adult-male.jpg' : '/home/avatars/adult-female.jpg';
  }
  if (relationship === 'child') {
    return inferred === 2 ? '/home/avatars/girl.jpg' : '/home/avatars/boy.jpg';
  }
  return inferred === 1 ? '/home/avatars/adult-male.jpg' : '/home/avatars/adult-female.jpg';
}

function PillIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden>
      <g transform="rotate(-40 12 12)">
        <rect x="4" y="9.25" width="16" height="5.5" rx="2.75" fill="currentColor" />
        <rect x="11.15" y="9.25" width="1.7" height="5.5" fill="rgba(255,255,255,0.45)" />
      </g>
    </svg>
  );
}

/** Icon đơn thuốc — clipboard + chữ Rx. */
function PrescriptionIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M8 3.75h8a1.75 1.75 0 0 1 1.75 1.75v14a1.75 1.75 0 0 1-1.75 1.75H8A1.75 1.75 0 0 1 6.25 19.5v-14A1.75 1.75 0 0 1 8 3.75Z"
        stroke="currentColor"
        strokeWidth="1.7"
      />
      <path d="M9.5 2.75h5v2.5h-5v-2.5Z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
      <path
        d="M9.2 11.2h2.1c.9 0 1.55.55 1.55 1.35 0 .8-.65 1.35-1.55 1.35H10.4v2.35"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M14.4 16.25 11.9 13.1" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <path d="M11.9 16.25 14.4 13.1" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}

function remindClock(value?: string | null) {
  if (!value) return '--:--';
  const parsed = dayjs(value);
  if (parsed.isValid()) return parsed.format('HH:mm');
  const m = String(value).match(/(\d{1,2}):(\d{2})/);
  return m ? `${m[1].padStart(2, '0')}:${m[2]}` : String(value).slice(0, 5);
}

export function HomePage() {
  const { t } = useTranslation();
  const { familyRelationship } = useCustomerLabels();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const profile = useAuthStore((s) => s.profile);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated());
  const { branding } = useCustomerBranding();
  const { requireLink, linked } = usePharmacyLink();
  const { requireAuth } = useVerifyAccount();
  const notificationCount = useCustomerNotificationCount();
  const { data: chatOverview } = useChatOverviewQuery();

  const [statsLoading, setStatsLoading] = useState(true);
  const [pendingOrders, setPendingOrders] = useState(0);
  const [repurchaseCount, setRepurchaseCount] = useState(0);
  const [draftPreview, setDraftPreview] = useState<
    { id: string; code: string; status: number; updatedAt?: string }[]
  >([]);
  const [adherence, setAdherence] = useState<Adherence>({
    dueCount: 0,
    takenToday: 0,
    skippedToday: 0,
    scheduledToday: 0,
    missedStreakDays: 0,
    showMissedAlert: false,
  });
  const [connectInbox, setConnectInbox] = useState<CustomerConnectInbox | null>(null);
  const [dueItems, setDueItems] = useState<MedicationReminder[]>([]);
  const [doneMedTasks, setDoneMedTasks] = useState<Extract<HomeTask, { kind: 'med' }>[]>([]);
  const [family, setFamily] = useState<FamilyMember[]>([]);
  const [familyDueIds, setFamilyDueIds] = useState<Set<string>>(new Set());
  const [actingId, setActingId] = useState<string | null>(null);
  const [skipTarget, setSkipTarget] = useState<MedicationReminder | null>(null);

  const applyHomeData = useCallback(
    (
      loyalty: { programs: { pointsBalance?: number }[] },
      drafts: { id: string; draftNumber?: string; status: number; sentAt?: string | null }[],
      repurchase: { status: string }[],
      summary: Adherence,
      connect?: CustomerConnectInbox | null,
    ) => {
      void loyalty;
      setPendingOrders(
        drafts.filter(
          (d) =>
            d.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
            d.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
        ).length,
      );
      setRepurchaseCount(
        repurchase.filter((r) => r.status === 'pending' || r.status === 'snoozed').length,
      );
      setDraftPreview(
        drafts.slice(0, 2).map((d) => ({
          id: d.id,
          code: d.draftNumber || d.id.slice(0, 8).toUpperCase(),
          status: d.status,
          updatedAt: d.sentAt ?? undefined,
        })),
      );
      setAdherence(summary);
      if (connect !== undefined) setConnectInbox(connect);
    },
    [],
  );

  const load = useCallback(
    async (options?: { silent?: boolean }) => {
      if (!useAuthStore.getState().isAuthenticated()) {
        setStatsLoading(false);
        return;
      }
      if (!options?.silent) setStatsLoading(true);
      try {
        try {
          const summary = await fetchHomeSummary();
          applyHomeData(
            summary.loyalty,
            summary.draftOrders,
            summary.repurchaseSuggestions,
            summary.adherence,
            summary.connectInbox,
          );
        } catch (overviewError) {
          if (axios.isAxiosError(overviewError) && overviewError.response?.status === 404) {
            const [loyalty, drafts, repurchase, summary] = await Promise.allSettled([
              fetchLoyaltySummary(),
              fetchDraftOrders(),
              fetchRepurchaseSuggestions(),
              fetchMedicationAdherenceSummary(),
            ]);
            applyHomeData(
              loyalty.status === 'fulfilled' ? loyalty.value : { programs: [] },
              drafts.status === 'fulfilled' ? drafts.value : [],
              repurchase.status === 'fulfilled' ? repurchase.value : [],
              summary.status === 'fulfilled'
                ? summary.value
                : {
                    dueCount: 0,
                    takenToday: 0,
                    skippedToday: 0,
                    scheduledToday: 0,
                    missedStreakDays: 0,
                    showMissedAlert: false,
                  },
              null,
            );
          } else {
            throw overviewError;
          }
        }
      } catch (error) {
        console.error(getApiErrorMessage(error));
      } finally {
        setStatsLoading(false);
      }
    },
    [applyHomeData],
  );

  const loadCareExtras = useCallback(async () => {
    if (!useAuthStore.getState().isAuthenticated()) return;
    try {
      const [due, members, familyDue] = await Promise.all([
        fetchDueReminders(),
        fetchFamilyMembers(),
        fetchFamilyDueReminders().catch(() => [] as MedicationReminder[]),
      ]);
      setDueItems(due);
      setFamily(members.filter((m) => m.status === 1).slice(0, 3));
      setFamilyDueIds(
        new Set(
          familyDue
            .map((r) => r.familyMemberId)
            .filter((id): id is string => Boolean(id)),
        ),
      );
    } catch (error) {
      console.error(getApiErrorMessage(error));
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, isAuthenticated]);

  useEffect(() => {
    if (!isAuthenticated) return;
    let idleId: number | undefined;
    let timeoutId: number | undefined;
    const run = () => {
      void loadCareExtras();
      void prefetchPrimaryTabOverviews(queryClient);
    };
    if (typeof window.requestIdleCallback === 'function') {
      idleId = window.requestIdleCallback(run, { timeout: 2500 });
    } else {
      timeoutId = window.setTimeout(run, 400);
    }
    return () => {
      if (idleId !== undefined && typeof window.cancelIdleCallback === 'function') {
        window.cancelIdleCallback(idleId);
      }
      if (timeoutId !== undefined) window.clearTimeout(timeoutId);
    };
  }, [isAuthenticated, loadCareExtras, queryClient]);

  const markTaken = async (reminderId: string) => {
    setActingId(reminderId);
    try {
      await respondMedicationReminder(reminderId, 'taken');
      message.success(t('reminders.takenRecorded'));
      const item = dueItems.find((x) => x.id === reminderId);
      if (item) {
        setDoneMedTasks((prev) => [
          {
            key: item.id,
            kind: 'med',
            time: remindClock(item.nextRemindAt || item.remindTime),
            title: t('home.taskTakeMed', { name: item.productName }),
            sub: item.dosageNote || item.productCode || t('home.taskMedFallback'),
            reminderId: item.id,
            done: true,
          },
          ...prev.filter((x) => x.reminderId !== reminderId),
        ]);
      }
      setDueItems((prev) => prev.filter((x) => x.id !== reminderId));
      void load({ silent: true });
      void loadCareExtras();
    } catch (error) {
      message.error(getApiErrorMessage(error));
    } finally {
      setActingId(null);
    }
  };

  const confirmSkip = async (reason: MedSkipReasonCode) => {
    if (!skipTarget) return;
    setActingId(skipTarget.id);
    try {
      await respondMedicationReminder(skipTarget.id, 'skipped', undefined, reason);
      message.success(t('common.skipped'));
      setSkipTarget(null);
      setDueItems((prev) => prev.filter((x) => x.id !== skipTarget.id));
      void load({ silent: true });
      void loadCareExtras();
    } catch (error) {
      message.error(getApiErrorMessage(error));
    } finally {
      setActingId(null);
    }
  };

  const followUpTasks = useMemo(() => {
    const items = connectInbox?.connectEnabled ? connectInbox.items : [];
    return items
      .filter((i) => i.kind.includes('booking') || i.kind.includes('referral') || i.kind === 'rx_ready')
      .slice(0, 2)
      .map((item) => ({
        key: `c-${item.sourceId}`,
        kind: (item.kind === 'rx_ready' ? 'order' : 'cal') as 'cal' | 'order',
        time: item.scheduledAt ? remindClock(item.scheduledAt) : '—',
        title:
          item.kind === 'rx_ready'
            ? t('home.connectKinds.rx_ready')
            : item.kind.includes('booking')
              ? t('home.taskFollowUp')
              : item.detail || t('home.taskFollowUp'),
        sub: item.clinicName || item.detail || branding.tenantName || t('home.taskClinicFallback'),
        to: '/pharmacy',
      }));
  }, [branding.tenantName, connectInbox, t]);

  const tasks: HomeTask[] = useMemo(() => {
    const meds: HomeTask[] = dueItems.slice(0, 3).map((item) => ({
      key: item.id,
      kind: 'med',
      time: remindClock(item.nextRemindAt || item.remindTime),
      title: t('home.taskTakeMed', { name: item.productName }),
      sub: item.dosageNote || item.productCode || t('home.taskMedFallback'),
      reminderId: item.id,
      done: false,
    }));
    const done = doneMedTasks.filter((d) => !dueItems.some((x) => x.id === d.reminderId));
    return [...done, ...meds, ...followUpTasks].slice(0, 4);
  }, [doneMedTasks, dueItems, followUpTasks, t]);

  const openTaskCount = useMemo(() => {
    const open = tasks.filter((x) => x.kind !== 'med' || !x.done).length;
    const hasOrderTask = tasks.some((x) => x.kind === 'order');
    return (
      open +
      (repurchaseCount > 0 ? 1 : 0) +
      (!hasOrderTask && pendingOrders > 0 ? 1 : 0)
    );
  }, [pendingOrders, repurchaseCount, tasks]);

  /** Điểm tuân thủ hôm nay từ adherence API (taken / scheduled). */
  const healthScore = useMemo(() => {
    if (statsLoading) return 0;
    if (adherence.scheduledToday > 0) {
      return Math.max(
        0,
        Math.min(100, Math.round((adherence.takenToday / adherence.scheduledToday) * 100)),
      );
    }
    if (adherence.showMissedAlert) {
      return Math.max(0, 100 - adherence.missedStreakDays * 12);
    }
    return 0;
  }, [adherence, statsLoading]);

  /** Số ngày có uống trong cửa sổ 7 ngày gần nhất (API: missedStreakDays = ngày không uống). */
  const streakDays = useMemo(() => {
    if (statsLoading) return 0;
    if (adherence.showMissedAlert) return 0;
    if (adherence.scheduledToday <= 0 && adherence.takenToday <= 0) return 0;
    return Math.max(0, Math.min(7, 7 - adherence.missedStreakDays));
  }, [adherence, statsLoading]);

  const medTaken = adherence.takenToday;
  const medTotal = Math.max(0, adherence.scheduledToday);
  const medPct =
    medTotal > 0 ? Math.max(0, Math.min(100, Math.round((medTaken / medTotal) * 100))) : 0;

  const streakDotCount = 7;
  const streakFilled = Math.min(streakDotCount, streakDays);
  const showScoreWarn = adherence.showMissedAlert;
  const showScoreOk = !showScoreWarn && adherence.scheduledToday > 0;

  /** Hub chính: chăm sóc — không cần liên kết NT. */
  const careHubs = [
    { to: '/health', label: t('home.hubHealth'), icon: <HeartOutlined />, tone: 'orange' },
    { to: '/medications', label: t('home.hubMedications'), icon: <MedicineBoxOutlined />, tone: 'teal' },
    { to: '/reminders', label: t('home.hubReminders'), icon: <BellOutlined />, tone: 'orange' },
    { to: '/prescriptions', label: t('home.hubPrescriptions'), icon: <PrescriptionIcon />, tone: 'green' },
    { to: '/ai', label: t('home.hubAi'), icon: <RobotOutlined />, tone: 'purple' },
    { to: '/family', label: t('home.hubFamily'), icon: <TeamOutlined />, tone: 'blue' },
  ] as const;

  /** Dịch vụ nhà thuốc — soft-gate khi chưa member. */
  const serviceShortcuts = [
    {
      to: '/orders',
      label: t('home.shortcutOrderMeds'),
      icon: <ShoppingOutlined />,
      tone: 'green',
      needsLink: true,
      intent: t('pharmacyLink.intentOrders'),
    },
    {
      to: '/chat',
      label: t('home.shortcutChat'),
      icon: <MessageOutlined />,
      tone: 'blue',
      needsLink: true,
      intent: t('pharmacyLink.intentChat'),
    },
    {
      to: '/loyalty',
      label: t('home.shortcutPoints'),
      icon: <GiftOutlined />,
      tone: 'purple',
      needsLink: true,
      intent: t('pharmacyLink.intentLoyalty'),
    },
  ] as const;

  const activityRows = useMemo(() => {
    const rows: {
      key: string;
      title: string;
      sub: string;
      when: string;
      to: string;
      kind: 'order' | 'chat';
    }[] = [];
    for (const d of draftPreview) {
      rows.push({
        key: d.id,
        title: t('home.activityOrder', { code: d.code }),
        sub:
          d.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed
            ? t('home.activityOrderDone')
            : t('home.activityOrderPending'),
        when: d.updatedAt ? dayjs(d.updatedAt).format('DD/MM HH:mm') : t('home.activityRecent'),
        to: '/orders',
        kind: 'order',
      });
    }
    const chatPreview = chatOverview?.thread?.lastMessagePreview?.trim();
    const chatAt = chatOverview?.thread?.lastMessageAt;
    if (chatPreview) {
      rows.push({
        key: 'chat',
        title: t('home.activityChat'),
        sub: chatPreview,
        when: chatAt ? dayjs(chatAt).format('DD/MM HH:mm') : t('home.activityRecent'),
        to: '/chat',
        kind: 'chat',
      });
    }
    return rows.slice(0, 3);
  }, [chatOverview?.thread?.lastMessageAt, chatOverview?.thread?.lastMessagePreview, draftPreview, t]);

  const aiPrompts = [
    { key: 'chip1', text: t('home.aiChip1'), icon: <BellOutlined />, tone: 'blue' as const },
    { key: 'chip2', text: t('home.aiChip2'), icon: <MedicineBoxOutlined />, tone: 'teal' as const },
    { key: 'chip3', text: t('home.aiChip3'), icon: <CheckCircleOutlined />, tone: 'green' as const },
  ];

  return (
    <div className="home-v2">
      <header className="home-v2-header">
        <div className="home-v2-header-left">
          <BrandingLogo logoUrl={branding.logoUrl} size={42} style={{ borderRadius: 12 }} />
          <div className="home-v2-header-copy">
            <h1 className="home-v2-greeting">
              {t('home.greetingPrefix')}{' '}
              <span>{profile?.fullName ?? t('home.guestName')}</span>
            </h1>
            <div className="home-v2-tagline">{t('home.careTagline')}</div>
          </div>
        </div>
        <Link
          to={isAuthenticated ? '/notifications' : '/login'}
          aria-label={t('home.notifications')}
          onClick={(e) => {
            if (!isAuthenticated) {
              e.preventDefault();
              requireAuth(t('verifyAccount.intentSync'));
            }
          }}
        >
          <Badge count={isAuthenticated ? notificationCount : 0} size="small" offset={[-2, 4]}>
            <span className="home-v2-bell">
              <BellOutlined />
            </span>
          </Badge>
        </Link>
      </header>

      {!isAuthenticated ? (
        <section className="home-v2-guest" aria-label={t('home.guestBannerAria')}>
          <p className="home-v2-guest-title">{t('home.guestWelcome')}</p>
          <p className="home-v2-guest-body">{t('home.guestWelcomeBody')}</p>
          <div className="home-v2-guest-actions">
            <button type="button" className="home-v2-guest-cta" onClick={() => navigate('/health?add=prescription')}>
              {t('home.guestTryRx')}
            </button>
            <button type="button" className="home-v2-guest-cta home-v2-guest-cta--secondary" onClick={() => navigate('/ai')}>
              {t('home.guestTryAi')}
            </button>
          </div>
        </section>
      ) : null}

      <section className="home-v2-hero" aria-label={t('home.heroAria')}>
        <div className="home-v2-hero-leaf" aria-hidden />
        <div className="home-v2-hero-main">
          <h2 className="home-v2-hero-title">
            <em>{t('home.heroToday')}</em>
            {statsLoading
              ? t('home.heroLoading')
              : openTaskCount > 0
                ? t('home.heroTasks', { count: openTaskCount })
                : t('home.heroAllDone')}
          </h2>

          <div className="home-v2-hero-panel">
            {tasks.length === 0 ? (
              <div className="home-v2-hero-empty">
                {adherence.takenToday > 0 ? t('home.heroEmptyWin') : t('home.heroEmpty')}
              </div>
            ) : (
              tasks.map((task) =>
                task.kind === 'med' ? (
                  <div
                    key={task.key}
                    className={`home-v2-task${task.done ? ' home-v2-task--done' : ''}`}
                    role="group"
                  >
                    <span className="home-v2-task-icon home-v2-task-icon--med">
                      <PillIcon />
                    </span>
                    <div className="home-v2-task-body">
                      <div className="home-v2-task-time">{task.time}</div>
                      <div className="home-v2-task-title">{task.title}</div>
                      <div className="home-v2-task-sub">{task.sub}</div>
                    </div>
                    {task.done || !task.reminderId ? (
                      <span className="home-v2-task-done home-v2-task-done--static" aria-hidden>
                        <CheckOutlined />
                      </span>
                    ) : (
                      <button
                        type="button"
                        className="home-v2-task-done"
                        aria-label={t('common.taken')}
                        disabled={actingId === task.reminderId}
                        onClick={() => void markTaken(task.reminderId)}
                        onContextMenu={(e) => {
                          e.preventDefault();
                          const item = dueItems.find((x) => x.id === task.reminderId);
                          if (item) setSkipTarget(item);
                        }}
                      >
                        <CheckOutlined />
                      </button>
                    )}
                  </div>
                ) : (
                  <button
                    key={task.key}
                    type="button"
                    className="home-v2-task"
                    onClick={() => navigate(task.to)}
                  >
                    <span
                      className={`home-v2-task-icon ${
                        task.kind === 'cal' ? 'home-v2-task-icon--cal' : 'home-v2-task-icon--order'
                      }`}
                    >
                      {task.kind === 'cal' ? <CalendarOutlined /> : <FileTextOutlined />}
                    </span>
                    <div className="home-v2-task-body">
                      <div className="home-v2-task-time">{task.time}</div>
                      <div className="home-v2-task-title">{task.title}</div>
                      <div className="home-v2-task-sub">{task.sub}</div>
                    </div>
                    <RightOutlined className="home-v2-task-chevron" />
                  </button>
                ),
              )
            )}
            <div className="home-v2-hero-footer">
              <button type="button" onClick={() => navigate('/medications')}>
                {t('home.viewAllSchedule')} <RightOutlined />
              </button>
            </div>
          </div>
        </div>
        <img className="home-v2-hero-art" src="/home/hero-meds.jpg" alt="" />
      </section>

      <section className="home-v2-stats" aria-label={t('home.statsAria')}>
        <div className="home-v2-stat">
          <div className="home-v2-stat-head">
            <HeartFilled className="home-v2-stat-ico home-v2-stat-ico--heart" />
            <span>{t('home.healthScore')}</span>
          </div>
          <div className="home-v2-ring" style={{ ['--pct' as string]: healthScore }}>
            <div className="home-v2-ring-value">
              <strong>{statsLoading ? '…' : healthScore}</strong>
              <span>/100</span>
            </div>
          </div>
          <div className={`home-v2-stat-hint${showScoreWarn ? ' home-v2-stat-hint--warn' : ''}`}>
            {showScoreWarn
              ? t('home.healthScoreDown', { days: adherence.missedStreakDays })
              : showScoreOk
                ? t('home.healthScoreToday', {
                    taken: adherence.takenToday,
                    total: adherence.scheduledToday,
                  })
                : t('home.healthScoreIdle')}
          </div>
        </div>

        <div className="home-v2-stat">
          <div className="home-v2-stat-head">
            <FireFilled className="home-v2-stat-ico home-v2-stat-ico--fire" />
            <span>{t('home.streakTitle')}</span>
          </div>
          <div className="home-v2-streak-value">
            <strong>{statsLoading ? '…' : streakDays}</strong>
            <span>{t('home.streakUnit')}</span>
          </div>
          <div className="home-v2-stat-mood">
            {streakDays > 0 ? t('home.streakGreat') : t('home.streakStart')}
          </div>
          <div className="home-v2-dots" aria-hidden>
            {Array.from({ length: streakDotCount }).map((_, i) => (
              <span key={i} className={`home-v2-dot${i < streakFilled ? ' home-v2-dot--on' : ''}`} />
            ))}
          </div>
        </div>

        <div className="home-v2-stat">
          <div className="home-v2-stat-head">
            <span className="home-v2-stat-ico home-v2-stat-ico--pill" aria-hidden>
              <PillIcon />
            </span>
            <span>{t('home.medsToday')}</span>
          </div>
          <div className="home-v2-med-value">
            {statsLoading ? (
              '…'
            ) : (
              <>
                <strong>{medTaken}</strong>
                <span>/{medTotal}</span>
              </>
            )}
          </div>
          <div className="home-v2-stat-mood home-v2-stat-mood--muted">{t('home.medsTaken')}</div>
          <div className="home-v2-bar">
            <span style={{ width: `${medPct}%` }} />
          </div>
        </div>
      </section>

      <section aria-label={t('home.careHubsAria')}>
        <div className="home-v2-section-head">
          <h3 className="home-v2-section-title">{t('home.careHubs')}</h3>
        </div>
        <div className="home-v2-shortcuts home-v2-shortcuts--hubs">
          {careHubs.map((item) => (
            <button
              key={item.to}
              type="button"
              className="home-v2-shortcut"
              onClick={() => navigate(item.to)}
            >
              <span className={`home-v2-shortcut-icon home-v2-shortcut-icon--${item.tone}`}>
                {item.icon}
              </span>
              <span className="home-v2-shortcut-label">{item.label}</span>
            </button>
          ))}
        </div>
        <div className="home-v2-services">
          <div className="home-v2-section-head" style={{ marginBottom: 8 }}>
            <h3 className="home-v2-section-title">{t('home.pharmacyServices')}</h3>
          </div>
          {serviceShortcuts.map((item) => (
            <button
              key={item.to}
              type="button"
              className={`home-v2-service${item.needsLink && !linked ? ' home-v2-service--gated' : ''}`}
              onClick={() => {
                if (item.needsLink && !requireLink(item.intent)) return;
                navigate(item.to);
              }}
            >
              <span className={`home-v2-service-icon home-v2-shortcut-icon--${item.tone}`}>
                {item.icon}
              </span>
              <span>{item.label}</span>
            </button>
          ))}
        </div>
      </section>

      <section className="home-v2-family-wrap">
        <div className="home-v2-section-head">
          <h3 className="home-v2-section-title">{t('home.familyTitle')}</h3>
          <button type="button" className="home-v2-link" onClick={() => navigate('/family')}>
            {t('home.viewAll')} <RightOutlined />
          </button>
        </div>
        <div className="home-v2-family">
          <button type="button" className="home-v2-member" onClick={() => navigate('/reminders')}>
            <div className="home-v2-avatar-wrap">
              <div className="home-v2-avatar home-v2-avatar--photo">
                <img src={roleAvatarSrc('self', 2, 'self')} alt="" />
              </div>
              <span
                className={`home-v2-status-dot${adherence.dueCount > 0 ? ' home-v2-status-dot--warn' : ''}`}
              />
            </div>
            <div className="home-v2-member-name">{t('home.you')}</div>
            <div className="home-v2-member-status">
              {adherence.dueCount > 0 ? t('home.familyPending') : t('home.familyGood')}
            </div>
          </button>
          {(() => {
            const shown = family.slice(0, 3);
            const roleCounts = new Map<string, number>();
            for (const m of shown) {
              const role = familyRoleLabel(m.relationship, m.gender, t, familyRelationship, m.id);
              roleCounts.set(role, (roleCounts.get(role) ?? 0) + 1);
            }
            return shown.map((m) => {
              const pending = familyDueIds.has(m.id);
              const role = familyRoleLabel(m.relationship, m.gender, t, familyRelationship, m.id);
              const label =
                (roleCounts.get(role) ?? 0) > 1 && shortGivenName(m.fullName)
                  ? `${role} ${shortGivenName(m.fullName)}`
                  : role;
              return (
                <button
                  key={m.id}
                  type="button"
                  className="home-v2-member"
                  onClick={() => navigate('/family')}
                  title={m.fullName}
                >
                  <div className="home-v2-avatar-wrap">
                    <div className="home-v2-avatar home-v2-avatar--photo">
                      <img src={roleAvatarSrc(m.relationship, m.gender, m.id)} alt="" />
                    </div>
                    <span className={`home-v2-status-dot${pending ? ' home-v2-status-dot--warn' : ''}`} />
                  </div>
                  <div className="home-v2-member-name">{label}</div>
                  <div className="home-v2-member-status">
                    {pending ? t('home.familyPending') : t('home.familyTaken')}
                  </div>
                </button>
              );
            });
          })()}
          <button type="button" className="home-v2-member" onClick={() => navigate('/family')}>
            <div className="home-v2-avatar-wrap">
              <div className="home-v2-avatar home-v2-avatar--add">
                <PlusOutlined />
              </div>
            </div>
            <div className="home-v2-member-name">{t('home.addMemberShort')}</div>
            <div className="home-v2-member-status">{t('home.addMemberSub')}</div>
          </button>
        </div>
      </section>

      <section className="home-v2-ai-wrap">
        <button type="button" className="home-v2-ai" onClick={() => navigate('/ai')}>
          <img className="home-v2-ai-art" src="/home/ai-robot.jpg" alt="" />
          <div className="home-v2-ai-copy">
            <div className="home-v2-ai-title">
              {t('home.aiTitle')}
              <span className="home-v2-ai-beta">Beta</span>
            </div>
            <div className="home-v2-ai-sub">{t('home.aiSub')}</div>
          </div>
          <RightOutlined className="home-v2-ai-chevron" />
        </button>
        <div className="home-v2-ai-chips">
          {aiPrompts.map((chip) => (
            <button
              key={chip.key}
              type="button"
              className="home-v2-ai-chip"
              onClick={() => navigate(`/ai?q=${encodeURIComponent(chip.text)}`)}
            >
              <span className={`home-v2-ai-chip-ico home-v2-ai-chip-ico--${chip.tone}`}>{chip.icon}</span>
              <span className="home-v2-ai-chip-text">{chip.text}</span>
            </button>
          ))}
        </div>
      </section>

      <section>
        <div className="home-v2-section-head">
          <h3 className="home-v2-section-title">{t('home.activityTitle')}</h3>
          <button type="button" className="home-v2-link" onClick={() => navigate('/timeline')}>
            {t('home.viewAll')} <RightOutlined />
          </button>
        </div>
        <div className="home-v2-activity">
          {activityRows.length === 0 ? (
            <div className="home-v2-activity-empty">{t('home.activityEmpty')}</div>
          ) : (
            activityRows.map((row) => (
              <button
                key={row.key}
                type="button"
                className="home-v2-activity-item"
                onClick={() => {
                  if (
                    (row.kind === 'order' || row.kind === 'chat') &&
                    !requireLink(
                      row.kind === 'order'
                        ? t('pharmacyLink.intentOrders')
                        : t('pharmacyLink.intentChat'),
                    )
                  ) {
                    return;
                  }
                  navigate(row.to);
                }}
              >
                <span
                  className={`home-v2-activity-icon ${
                    row.kind === 'order' ? 'home-v2-activity-icon--order' : 'home-v2-activity-icon--chat'
                  }`}
                >
                  {row.kind === 'order' ? <PrescriptionIcon /> : <MessageOutlined />}
                </span>
                <div className="home-v2-activity-body">
                  <div className="home-v2-activity-title">{row.title}</div>
                  <div className="home-v2-activity-sub">{row.sub}</div>
                </div>
                <div className="home-v2-activity-meta">
                  {row.when}
                  <div>
                    <RightOutlined />
                  </div>
                </div>
              </button>
            ))
          )}
        </div>
      </section>

      <SkipReasonModal
        open={Boolean(skipTarget)}
        productName={skipTarget?.productName}
        confirmLoading={Boolean(skipTarget && actingId === skipTarget.id)}
        onCancel={() => setSkipTarget(null)}
        onConfirm={(reason) => void confirmSkip(reason)}
      />
    </div>
  );
}

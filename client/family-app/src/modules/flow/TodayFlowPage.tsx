import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  decideConsequenceEvent,
  ensureDayFlow,
  fetchAccountabilityGlance,
  fetchConsequenceEvents,
  fetchFamilies,
  fetchMemberStarBalance,
  fetchTeamDay,
  updateCommitmentProgress,
  approveCommitmentStars,
  type AccountabilityGlance,
  type ConsequenceEvent,
  type DayFlow,
  type DayFlowCommitment,
  type SkipReasonCode,
  type SoftLockGuide,
  type TeamDay,
} from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';
import {
  ensureNotificationPermission,
  notifyDueCommitments,
  shouldOfferNotificationOptIn,
} from '@/shared/reminders/localReminders';
import {
  fetchParentPushStatus,
  isParentPushSupported,
  registerParentPushSubscription,
  subscribeParentPush,
} from '@/shared/push/parentPush';
import { ParentPinSheet } from '@/shared/ui/ParentPinSheet';
import { useHoldAction } from '@/shared/ui/useHoldAction';
import { KidFocusView } from '@/modules/flow/KidFocusView';
import { ParentBoardView } from '@/modules/flow/ParentBoardView';
import { buildTeamDayFromChildren, slicesFromCommitments } from '@/modules/flow/teamPlay';
import { isScreenBoundaryCode } from '@/shared/screen/screenBoundary';
import { hydrateFamilyValueState } from '@/shared/value/value-sync';

export function TodayFlowPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const familyId = useSessionStore((s) => s.familyId);
  const member = useSessionStore((s) => s.member);
  const setMember = useSessionStore((s) => s.setMember);
  const verifyParentPin = useSessionStore((s) => s.verifyParentPin);
  const [flow, setFlow] = useState<DayFlow | null>(null);
  const [teamDay, setTeamDay] = useState<TeamDay | null>(null);
  const [events, setEvents] = useState<ConsequenceEvent[]>([]);
  const [glance, setGlance] = useState<AccountabilityGlance | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [celebrating, setCelebrating] = useState(false);
  const [pinOpen, setPinOpen] = useState(false);
  const [pinPurpose, setPinPurpose] = useState<'switch' | 'unlock'>('switch');
  const [softLockBypassed, setSoftLockBypassed] = useState(false);
  const [offerReminders, setOfferReminders] = useState(() => shouldOfferNotificationOptIn());
  const [parentPushSubscribed, setParentPushSubscribed] = useState(false);
  const [familyChildren, setFamilyChildren] = useState<
    Array<{ id: string; displayName: string }>
  >([]);
  const [onboardBanner, setOnboardBanner] = useState<string | null>(null);
  const [starBalance, setStarBalance] = useState(0);

  useEffect(() => {
    const st = location.state as { onboardingAdded?: number; onboardingChild?: string } | null;
    if (st?.onboardingChild) {
      const n = st.onboardingAdded ?? 0;
      setOnboardBanner(
        n > 0
          ? `Foxy đã gắn ${n} việc starter cho ${st.onboardingChild}. Theo dõi Health Score 30 ngày tới nhé!`
          : `Hồ sơ onboarding của ${st.onboardingChild} đã sẵn sàng. Các việc trùng tên đã được giữ nguyên.`,
      );
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [location.state, location.pathname, navigate]);

  const goWho = useCallback(() => {
    setMember(null);
    navigate('/who');
  }, [navigate, setMember]);

  useEffect(() => {
    if (!familyId) return;
    void hydrateFamilyValueState(familyId);
  }, [familyId]);

  useEffect(() => {
    if (!familyId) return;
    void fetchFamilies()
      .then((families) => {
        const family = families.find((f) => f.id === familyId) ?? families[0];
        const kids = (family?.members ?? [])
          .filter((m) => m.roleCode === 'child')
          .map((m) => ({ id: m.id, displayName: m.displayName }));
        setFamilyChildren(kids);
      })
      .catch(() => setFamilyChildren([]));
  }, [familyId]);

  const load = useCallback(async (silent = false) => {
    if (!familyId) return;
    if (!silent) setLoading(true);
    setError(null);
    try {
      const day = await ensureDayFlow(familyId);
      setFlow(day);
      const [ev, gl, team] = await Promise.all([
        fetchConsequenceEvents(familyId, day.flowDate),
        fetchAccountabilityGlance(familyId),
        fetchTeamDay(familyId, day.flowDate).catch(() => null),
      ]);
      setEvents(ev);
      setGlance(gl);
      setTeamDay(team);
      const current = useSessionStore.getState().member;
      if (current?.roleCode === 'child') {
        try {
          const balance = await fetchMemberStarBalance(familyId, current.id);
          setStarBalance(balance);
        } catch {
          setStarBalance(0);
        }
      }
      notifyDueCommitments(day, {
        memberId: current?.roleCode === 'child' ? current.id : undefined,
      });
    } catch (err) {
      const status =
        err && typeof err === 'object' && 'response' in err
          ? (err as { response?: { status?: number } }).response?.status
          : undefined;
      if (status === 401) {
        if (!silent) setError('Phiên đăng nhập hết hạn. Bố mẹ mở khóa lại nhé.');
        return;
      }
      if (!silent) setError('Chưa mở được ngày hôm nay. Thử lại nhé.');
    } finally {
      if (!silent) setLoading(false);
    }
  }, [familyId]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!familyId) return;
    const timer = window.setInterval(() => void load(true), 60_000);
    return () => window.clearInterval(timer);
  }, [familyId, load]);

  const isChild = member?.roleCode === 'child';

  const kidItems = useMemo(() => {
    if (!flow || !member || !isChild) return [] as DayFlowCommitment[];
    return flow.commitments.filter((c) => !c.memberId || c.memberId === member.id);
  }, [flow, member, isChild]);

  const teamSnapshot = useMemo(() => {
    if (teamDay) return teamDay;
    if (!flow) return null;
    const byId = new Map<string, string>();
    for (const c of flow.commitments) {
      if (c.memberId && c.memberName) byId.set(c.memberId, c.memberName);
    }
    for (const ch of familyChildren) {
      if (!byId.has(ch.id)) byId.set(ch.id, ch.displayName);
    }
    const children = [...byId.entries()].map(([id, name]) => ({ id, name }));
    if (children.length === 0 && member && isChild) {
      children.push({ id: member.id, name: member.displayName });
    }
    const slices = slicesFromCommitments(children, flow.commitments);
    const snap = buildTeamDayFromChildren(slices);
    return {
      flowDate: flow.flowDate,
      teamDone: snap.teamDone,
      teamTotal: snap.teamTotal,
      teamPercent: snap.teamPercent,
      remainingMissions: snap.remainingMissions,
      teamComplete: snap.teamComplete,
      heroMissionLine: snap.heroMissionLine,
      children: slices.map((s) => ({
        memberId: s.id,
        displayName: s.name,
        done: s.done,
        total: s.total,
        open: s.open,
        skipped: s.skipped,
      })),
    } satisfies TeamDay;
  }, [teamDay, flow, member, isChild, familyChildren]);

  const softLockEvent = useMemo(
    () =>
      events.find((e) => e.status === 'applied' && isScreenBoundaryCode(e.consequenceCode)) ??
      null,
    [events],
  );
  const softLockActive = Boolean(softLockEvent) && !softLockBypassed;

  useEffect(() => {
    if (softLockEvent) setSoftLockBypassed(false);
  }, [softLockEvent?.id]);

  const requestSwitchUser = useCallback(() => {
    if (softLockActive) {
      setPinPurpose('switch');
      setPinOpen(true);
      return;
    }
    goWho();
  }, [softLockActive, goWho]);

  const hold = useHoldAction(requestSwitchUser);

  useEffect(() => {
    if (!familyId || !member || member.roleCode === 'child') return;
    void fetchParentPushStatus(familyId, member.id)
      .then((s) => setParentPushSubscribed(s.subscribed))
      .catch(() => setParentPushSubscribed(false));
  }, [familyId, member]);

  const enableParentPush = async () => {
    if (!familyId || !member) return;
    try {
      if (!isParentPushSupported()) {
        setError('Trình duyệt không hỗ trợ Web Push.');
        return;
      }
      const status = await fetchParentPushStatus(familyId, member.id);
      if (!status.supported || !status.publicKey) {
        setError('Push chưa cấu hình trên server.');
        return;
      }
      await Notification.requestPermission();
      const sub = await subscribeParentPush(status.publicKey);
      await registerParentPushSubscription(familyId, {
        membershipId: member.id,
        ...sub,
      });
      setParentPushSubscribed(true);
      setOfferReminders(false);
    } catch {
      setError('Chưa bật được nhắc push. Thử lại trên Chrome/Edge (HTTPS hoặc localhost).');
    }
  };

  const enableReminders = async () => {
    const permission = await ensureNotificationPermission();
    setOfferReminders(shouldOfferNotificationOptIn());
    if (permission === 'granted' && flow) {
      notifyDueCommitments(flow, {
        memberId: isChild ? member?.id : undefined,
      });
    }
  };

  const markDone = async (
    item: DayFlowCommitment,
    evidenceUrl?: string,
    parentOverride = false,
  ) => {
    if (!familyId || busyId) return;
    setBusyId(item.id);
    try {
      const result = await updateCommitmentProgress(
        familyId,
        item.id,
        'done',
        undefined,
        evidenceUrl,
        parentOverride,
      );
      if (result.memberStarBalance != null) {
        setStarBalance(result.memberStarBalance);
      }
      setCelebrating(true);
      window.setTimeout(() => setCelebrating(false), 450);
      await load(true);
      return {
        starDelta: result.commitment.starDelta,
        starLabelVi: result.commitment.starLabelVi,
        memberStarBalance: result.memberStarBalance,
      };
    } catch (err) {
      const msg =
        err && typeof err === 'object' && 'response' in err
          ? String(
              (err as { response?: { data?: { message?: string } } }).response?.data?.message ??
                '',
            ).trim()
          : '';
      setError(msg || 'Chưa lưu được. Chạm lại giúp mình.');
      throw new Error('commitment_done_failed');
    } finally {
      setBusyId(null);
    }
  };

  const markReflect = async (item: DayFlowCommitment, reason: SkipReasonCode) => {
    if (!familyId || busyId) return;
    setBusyId(item.id);
    try {
      await updateCommitmentProgress(familyId, item.id, 'skipped', reason);
      await load(true);
    } catch {
      setError('Chưa ghi được lý do. Thử lại nhé.');
    } finally {
      setBusyId(null);
    }
  };

  const approveStars = async (item: DayFlowCommitment) => {
    if (!familyId) throw new Error('approve_stars_no_family');
    if (busyId && busyId !== item.id) throw new Error('approve_stars_busy');
    setBusyId(item.id);
    try {
      const result = await approveCommitmentStars(familyId, item.id);
      if (result.memberStarBalance != null) {
        setStarBalance(result.memberStarBalance);
      }
      await load(true);
    } catch (err) {
      const msg =
        err && typeof err === 'object' && 'response' in err
          ? String(
              (err as { response?: { data?: { message?: string } } }).response?.data?.message ??
                '',
            ).trim()
          : '';
      setError(msg || 'Chưa duyệt được sao — thử lại nhé.');
      throw err;
    } finally {
      setBusyId(null);
    }
  };

  const reopenCommitment = async (item: DayFlowCommitment) => {
    if (!familyId || busyId) return;
    setBusyId(item.id);
    try {
      await updateCommitmentProgress(familyId, item.id, 'pending');
      await load(true);
    } catch {
      setError('Chưa mở lại được. Thử lại nhé.');
    } finally {
      setBusyId(null);
    }
  };

  const decideConsequence = async (
    eventId: string,
    status: 'applied' | 'waived',
  ): Promise<SoftLockGuide | undefined> => {
    if (!familyId || !member || busyId) return undefined;
    setBusyId(eventId);
    try {
      const result = await decideConsequenceEvent(familyId, eventId, {
        status,
        decidedBy: member.id,
        decisionNote: status === 'applied' ? 'Áp dụng trên app' : 'Bỏ qua trên app',
      });
      await load(true);
      return result.softLockGuide;
    } catch {
      setError('Chưa quyết định được hậu quả.');
      return undefined;
    } finally {
      setBusyId(null);
    }
  };

  if (!member) return null;

  return (
    <>
      {error ? (
        <div className="banner-error" style={{ display: 'grid', gap: 10 }}>
          <div>{error}</div>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <button type="button" className="pill" onClick={() => void load()}>
              Thử lại
            </button>
            <button
              type="button"
              className="pill"
              onClick={() => {
                useSessionStore.getState().clear();
                navigate('/unlock', { replace: true });
              }}
            >
              Mở khóa lại
            </button>
          </div>
        </div>
      ) : null}
      {onboardBanner ? (
        <div className="banner-now" style={{ display: 'grid', gap: 8 }}>
          <div>{onboardBanner}</div>
          <p className="muted" style={{ margin: 0, fontSize: 13 }}>
            Việc mới gắn vào routine — Day Flow hôm nay nếu đã mở sẽ giữ cam kết cũ; từ ngày
            mai (hoặc sau khi reset ngày) sẽ thấy đủ starter.
          </p>
          <button type="button" className="pill is-soft" onClick={() => setOnboardBanner(null)}>
            Đã hiểu
          </button>
        </div>
      ) : null}
      {loading && !flow ? <div className="card muted">Đang tải…</div> : null}

      {flow && isChild ? (
        <KidFocusView
          childName={member.displayName}
          items={kidItems}
          busyId={busyId}
          celebrating={celebrating}
          streak={glance?.currentStreak ?? 0}
          flowDate={flow.flowDate}
          localTime={flow.localTime}
          todayBeautiful={glance?.todayIsBeautifulDay ?? false}
          glanceDays={glance?.days ?? []}
          teamPercent={teamSnapshot?.teamPercent ?? 0}
          teamRemaining={teamSnapshot?.remainingMissions ?? 0}
          teamComplete={teamSnapshot?.teamComplete ?? false}
          teamMissionLine={teamSnapshot?.heroMissionLine}
          teamFromApi={Boolean(teamDay)}
          softLockActive={softLockActive}
          softLockLabel={softLockEvent?.labelVi}
          familyId={familyId!}
          childMemberId={member.id}
          starBalance={starBalance}
          onStarBalanceChange={setStarBalance}
          onDone={(item, evidenceUrl) => markDone(item, evidenceUrl)}
          onReflect={(item, reason) => void markReflect(item, reason)}
          onHoldSwitchStart={hold.start}
          onHoldSwitchCancel={hold.cancel}
          holdProgress={hold.progress}
          holdHolding={hold.holding}
          onOpenParentPin={() => {
            setPinPurpose(softLockActive ? 'unlock' : 'switch');
            setPinOpen(true);
          }}
        />
      ) : null}

      {flow && !isChild ? (
        <ParentBoardView
          flow={flow}
          viewerName={member.displayName}
          familyId={familyId!}
          parentMembershipId={member.id}
          familyName={
            familyChildren.length > 0
              ? `Gia đình ${familyChildren.map((c) => c.displayName.split(/\s+/).pop()).join(' · ')}`
              : 'Nhà mình'
          }
          busyId={busyId}
          consequenceEvents={events}
          glance={glance}
          children={familyChildren}
          parentPushSubscribed={parentPushSubscribed}
          onEnableParentPush={() => void enableParentPush()}
          offerLocalReminders={offerReminders}
          onEnableLocalReminders={() => void enableReminders()}
          onMarkDone={(item) => void markDone(item, undefined, true)}
          onReflect={(item, reason) => void markReflect(item, reason)}
          onReopen={(item) => void reopenCommitment(item)}
          onApproveStars={(item) => approveStars(item)}
          onDecideConsequence={(eventId, status) => decideConsequence(eventId, status)}
          onSwitchUser={requestSwitchUser}
        />
      ) : null}

      <ParentPinSheet
        open={pinOpen}
        title="Mã bố mẹ"
        hint={
          softLockActive
            ? 'Soft-lock đang bật — nhập 4 số để mở khóa / đổi người'
            : 'Nhập 4 số để đổi người'
        }
        verify={verifyParentPin}
        onClose={() => setPinOpen(false)}
        onSuccess={() => {
          setPinOpen(false);
          if (pinPurpose === 'unlock') {
            setSoftLockBypassed(true);
            return;
          }
          setSoftLockBypassed(true);
          goWho();
        }}
      />
    </>
  );
}

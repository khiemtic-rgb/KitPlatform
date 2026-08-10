import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  decideConsequenceEvent,
  ensureDayFlow,
  fetchAccountabilityGlance,
  fetchConsequenceEvents,
  fetchFamilies,
  fetchFamilyBlueprint,
  fetchMemberStarBalance,
  fetchTeamDay,
  updateCommitmentProgress,
  approveCommitmentStars,
  submitCommitmentReflection,
  submitRetrievalCheck,
  selfStartCommitment,
  type AccountabilityGlance,
  type ConsequenceEvent,
  type DayFlow,
  type DayFlowCommitment,
  type ReflectionPromptCode,
  type SkipReasonCode,
  type SoftLockGuide,
  type TeamDay,
} from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { notifyDueCommitments } from '@/shared/reminders/localReminders';
import {
  hydrateSchoolSchedulesFromLayers,
  isSchoolQuietNow,
  resolveSchoolSchedule,
  syncSaveSchoolSchedule,
} from '@/shared/school/school-season';
import { ParentPinSheet } from '@/shared/ui/ParentPinSheet';
import { useHoldAction } from '@/shared/ui/useHoldAction';
import { KidFocusView } from '@/modules/flow/KidFocusView';
import { ParentBoardView } from '@/modules/flow/ParentBoardView';
import { ReflectionPromptSheet } from '@/modules/flow/ReflectionPromptSheet';
import { RetrievalCheckSheet } from '@/modules/flow/RetrievalCheckSheet';
import { buildTeamDayFromChildren, slicesFromCommitments } from '@/modules/flow/teamPlay';
import { isScreenBoundaryCode } from '@/shared/screen/screenBoundary';
import { hydrateFamilyValueState } from '@/shared/value/value-sync';
import { parseLayersJson } from '@/shared/value/soft-calibration';
import { getApiErrorMessage } from '@/shared/billing/capability-error';
import { parentRoleFromMembers } from '@/shared/voice/family-voice';

/** Stub when ensure day-flow fails — parent Báo cáo / Nhật ký still usable. */
function emptyDayFlow(familyId: string): DayFlow {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return {
    id: '',
    familyId,
    routineName: '',
    flowDate: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`,
    totalCommitments: 0,
    doneCount: 0,
    pendingCount: 0,
    dueNowCount: 0,
    overdueCount: 0,
    upcomingCount: 0,
    commitments: [],
  };
}

export function TodayFlowPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const familyId = useSessionStore((s) => s.familyId);
  const sessionFamilyName = useSessionStore((s) => s.familyName);
  const member = useSessionStore((s) => s.member);
  const setMember = useSessionStore((s) => s.setMember);
  const verifyParentPin = useSessionStore((s) => s.verifyParentPin);
  const canWrite = useSessionStore((s) => s.canWrite());
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
  const [familyChildren, setFamilyChildren] = useState<
    Array<{ id: string; displayName: string; dateOfBirth?: string }>
  >([]);
  const [familyParents, setFamilyParents] = useState<
    Array<{ id: string; displayName: string; dateOfBirth?: string }>
  >([]);
  const [onboardBanner, setOnboardBanner] = useState<string | null>(null);
  const [starBalance, setStarBalance] = useState(0);
  const [reflectionTarget, setReflectionTarget] = useState<{
    commitmentId: string;
    title: string;
    promptCode: ReflectionPromptCode;
    pendingQuiz?: boolean;
  } | null>(null);
  const [reflectionBusy, setReflectionBusy] = useState(false);
  const [quizTarget, setQuizTarget] = useState<{
    commitmentId: string;
    title: string;
  } | null>(null);
  const [quizBusy, setQuizBusy] = useState(false);

  useEffect(() => {
    const st = location.state as { onboardingAdded?: number; onboardingChild?: string } | null;
    if (st?.onboardingChild) {
      const n = st.onboardingAdded ?? 0;
      setOnboardBanner(
        n > 0
          ? `Famixa đã gắn ${n} việc khởi đầu cho ${st.onboardingChild}. Theo dõi điểm sức khỏe gia đình 30 ngày tới nhé!`
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
    if (!familyId || familyChildren.length === 0) return;
    let cancelled = false;
    void (async () => {
      try {
        const bp = await fetchFamilyBlueprint(familyId);
        if (cancelled) return;
        const layers = parseLayersJson(bp?.layersJson);
        const { toPush } = hydrateSchoolSchedulesFromLayers(
          familyId,
          familyChildren.map((c) => c.id),
          layers,
        );
        for (const sch of toPush) {
          if (cancelled) return;
          await syncSaveSchoolSchedule(familyId, sch).catch(() => {
            /* offline / next open retries */
          });
        }
      } catch {
        /* local schedule still used */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [familyId, familyChildren]);

  useEffect(() => {
    if (!familyId) return;
    void fetchFamilies()
      .then((families) => {
        const family = families.find((f) => f.id === familyId) ?? families[0];
        const members = family?.members ?? [];
        const kids = members
          .filter((m) => m.roleCode === 'child')
          .map((m) => ({
            id: m.id,
            displayName: m.displayName,
            dateOfBirth: m.dateOfBirth,
          }));
        const parents = members
          .filter((m) => m.roleCode !== 'child')
          .map((m) => ({
            id: m.id,
            displayName: m.displayName,
            dateOfBirth: m.dateOfBirth,
          }));
        setFamilyChildren(kids);
        setFamilyParents(parents);
      })
      .catch(() => {
        setFamilyChildren([]);
        setFamilyParents([]);
      });
  }, [familyId]);

  const load = useCallback(async (silent = false) => {
    if (!familyId) return;
    if (!silent) setLoading(true);
    if (!silent) setError(null);
    try {
      const day = await ensureDayFlow(familyId);
      setFlow(day);
      setError(null);
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
        schoolQuiet:
          current?.roleCode === 'child' && familyId
            ? isSchoolQuietNow(resolveSchoolSchedule(current.id, familyId))
            : false,
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

      const apiMsg = getApiErrorMessage(err);
      const current = useSessionStore.getState().member;
      const asParent = current?.roleCode !== 'child';

      // Parent: don't block Báo cáo / Nhật ký when day-flow can't open (e.g. no routine yet).
      if (asParent) {
        setFlow((prev) => prev ?? emptyDayFlow(familyId));
        try {
          const gl = await fetchAccountabilityGlance(familyId);
          setGlance(gl);
        } catch {
          /* glance optional */
        }
        if (!silent) {
          const noRoutine = /routine/i.test(apiMsg) || apiMsg.includes('Chưa có routine');
          setError(
            noRoutine
              ? 'Nhà mình chưa có lịch việc hằng ngày. Bấm “Thiết lập cho nhà” để Famixa gợi ý vài việc phù hợp — chỉ mất 1 phút.'
              : apiMsg || 'Chưa mở được ngày hôm nay. Thử lại nhé.',
          );
        }
        return;
      }

      if (!silent) setError(apiMsg || 'Chưa mở được ngày hôm nay. Thử lại nhé.');
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

  const householdParentRole = useMemo(
    () => parentRoleFromMembers(familyParents.map((p) => p.displayName)),
    [familyParents],
  );

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

  /** Kid → parent gate: always require PIN (same path as header 👤). */
  const requestParentGate = useCallback(() => {
    setPinPurpose(softLockActive ? 'unlock' : 'switch');
    setPinOpen(true);
  }, [softLockActive]);

  const hold = useHoldAction(requestParentGate);

  const markDone = async (
    item: DayFlowCommitment,
    evidenceUrl?: string,
    parentOverride = false,
  ) => {
    if (!familyId || busyId) return;
    if (!canWrite) {
      setError('Chế độ xem demo — nhà này chỉ xem, không sửa được.');
      return;
    }
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
      if (result.commitment.needsReflection && !parentOverride) {
        const code = (result.commitment.suggestedReflectionPrompt ??
          'hardest') as ReflectionPromptCode;
        setReflectionTarget({
          commitmentId: result.commitment.id,
          title: result.commitment.title,
          promptCode: code,
          pendingQuiz: Boolean(result.commitment.needsRetrievalCheck),
        });
      } else if (result.commitment.needsRetrievalCheck && !parentOverride) {
        setQuizTarget({
          commitmentId: result.commitment.id,
          title: result.commitment.title,
        });
      }
      await load(true);
      return {
        starDelta: result.commitment.starDelta,
        starLabelVi: result.commitment.starLabelVi,
        memberStarBalance: result.memberStarBalance,
        starPosted: result.commitment.starPosted,
        evidenceSatisfied: result.commitment.evidenceSatisfied,
        evidenceGateLabelVi: result.commitment.evidenceGateLabelVi,
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
      throw new Error(msg || 'commitment_done_failed');
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

  const markSelfStart = async (item: DayFlowCommitment) => {
    if (!familyId || busyId) return;
    setBusyId(item.id);
    try {
      await selfStartCommitment(familyId, item.id);
      await load(true);
    } catch {
      setError('Chưa ghi được lần bắt đầu. Thử lại nhé.');
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
  const isNoRoutineNotice =
    !isChild && Boolean(error && /lịch việc|routine|onboarding/i.test(error));

  return (
    <>
      {error && isNoRoutineNotice ? (
        <section className="dayflow-setup-notice" role="status" aria-live="polite">
          <div className="dayflow-setup-main">
            <div className="dayflow-setup-art" aria-hidden>
              📋
            </div>
            <div className="dayflow-setup-copy">
              <h2>Nhà mình chưa có lịch việc hằng ngày.</h2>
              <p>
                Bấm “Thiết lập cho nhà” để Famixa gợi ý vài việc phù hợp —{' '}
                <strong>chỉ mất 1 phút.</strong>
              </p>
            </div>
          </div>
          <div className="dayflow-setup-actions">
            <button
              type="button"
              className="dayflow-setup-btn is-primary"
              onClick={() => navigate('/onboarding')}
            >
              <span aria-hidden>🪄</span>
              Thiết lập cho nhà
            </button>
            <button
              type="button"
              className="dayflow-setup-btn is-retry"
              onClick={() => void load()}
            >
              <span aria-hidden>↻</span>
              Thử lại
            </button>
            <button
              type="button"
              className="dayflow-setup-btn is-unlock"
              onClick={() => {
                useSessionStore.getState().clear();
                navigate('/unlock', { replace: true });
              }}
            >
              <span aria-hidden>🔒</span>
              Mở khóa lại
            </button>
          </div>
          <p className="dayflow-setup-note">
            <span aria-hidden>ⓘ</span>
            Thiết lập giúp AI gợi ý lịch phù hợp hơn cho gia đình bạn.
          </p>
        </section>
      ) : error ? (
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
            mai (hoặc sau khi reset ngày) sẽ thấy đủ việc khởi đầu.
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
          parentRole={householdParentRole}
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
          dateOfBirth={member.dateOfBirth}
          starBalance={starBalance}
          onStarBalanceChange={setStarBalance}
          onDone={(item, evidenceUrl) => markDone(item, evidenceUrl)}
          onReflect={(item, reason) => void markReflect(item, reason)}
          onSelfStart={(item) => void markSelfStart(item)}
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

      {flow && !isChild && !isNoRoutineNotice ? (
        <ParentBoardView
          flow={flow}
          viewerName={member.displayName}
          familyId={familyId!}
          parentMembershipId={member.id}
          familyName={
            sessionFamilyName?.trim()
              ? sessionFamilyName.trim()
              : familyChildren.length > 0
                ? `Gia đình ${familyChildren.map((c) => c.displayName.split(/\s+/).pop()).join(' · ')}`
                : 'Nhà mình'
          }
          busyId={busyId}
          consequenceEvents={events}
          glance={glance}
          children={familyChildren}
          parents={familyParents}
          teamDay={
            teamSnapshot
              ? {
                  teamPercent: teamSnapshot.teamPercent,
                  remainingMissions: teamSnapshot.remainingMissions,
                  teamComplete: teamSnapshot.teamComplete,
                  teamTotal: teamSnapshot.teamTotal,
                  heroMissionLine: teamSnapshot.heroMissionLine,
                }
              : null
          }
          onMarkDone={(item) => void markDone(item, undefined, true)}
          onReflect={(item, reason) => void markReflect(item, reason)}
          onReopen={(item) => void reopenCommitment(item)}
          onApproveStars={(item) => approveStars(item)}
          onDecideConsequence={(eventId, status) => decideConsequence(eventId, status)}
          onSwitchUser={requestSwitchUser}
          onRefreshFlow={() => void load(true)}
        />
      ) : null}

      <ParentPinSheet
        open={pinOpen}
        title="Mã bố mẹ"
        hint={
          softLockActive
            ? 'Nhà đang tạm khóa nhẹ — nhập 4 số để mở / đổi người'
            : 'Nhập 4 số để chuyển sang bố mẹ / chọn người'
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

      {reflectionTarget && familyId ? (
        <ReflectionPromptSheet
          title={reflectionTarget.title}
          promptCode={reflectionTarget.promptCode}
          busy={reflectionBusy}
          onSkip={() => {
            const pending = reflectionTarget.pendingQuiz
              ? {
                  commitmentId: reflectionTarget.commitmentId,
                  title: reflectionTarget.title,
                }
              : null;
            setReflectionTarget(null);
            if (pending) setQuizTarget(pending);
          }}
          onSubmit={async (answer) => {
            setReflectionBusy(true);
            try {
              const result = await submitCommitmentReflection(
                familyId,
                reflectionTarget.commitmentId,
                reflectionTarget.promptCode,
                answer,
              );
              const pendingQuiz =
                result.needsRetrievalCheck || reflectionTarget.pendingQuiz
                  ? {
                      commitmentId: reflectionTarget.commitmentId,
                      title: reflectionTarget.title,
                    }
                  : null;
              setReflectionTarget(null);
              if (pendingQuiz) setQuizTarget(pendingQuiz);
              await load(true);
            } catch {
              setError('Chưa gửi được câu trả lời. Thử lại hoặc bỏ qua.');
            } finally {
              setReflectionBusy(false);
            }
          }}
        />
      ) : null}

      {quizTarget && familyId && !reflectionTarget ? (
        <RetrievalCheckSheet
          title={quizTarget.title}
          busy={quizBusy}
          onSkip={() => setQuizTarget(null)}
          onSubmit={async (method, recall) => {
            setQuizBusy(true);
            try {
              await submitRetrievalCheck(
                familyId,
                quizTarget.commitmentId,
                method,
                recall,
              );
              setQuizTarget(null);
              await load(true);
            } catch {
              setError('Chưa gửi được kiểm tra nhớ. Thử lại hoặc bỏ qua.');
            } finally {
              setQuizBusy(false);
            }
          }}
        />
      ) : null}
    </>
  );
}

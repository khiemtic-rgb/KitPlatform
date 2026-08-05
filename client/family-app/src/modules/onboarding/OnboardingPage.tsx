import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilies, type FamilyMembership } from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { applyOnboardingPlan, skipOnboarding } from '@/shared/onboarding/apply-plan';
import {
  AGE_OPTIONS,
  GOAL_OPTIONS,
  PRIORITY_OPTIONS,
  SLEEP_HOUR_OPTIONS,
  STRUGGLE_OPTIONS,
  buildStarterPlan,
  isOnboardingDone,
  suggestStarterWalletMinutes,
  type AgeBand,
  type GoalCode,
  type PriorityCode,
  type StruggleCode,
} from '@/shared/onboarding/onboarding';
import { syncSaveOnboarding } from '@/shared/value/value-sync';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';

type Step =
  | 'welcome'
  | 'child'
  | 'age'
  | 'struggle'
  | 'goal'
  | 'lifestyle'
  | 'priority'
  | 'preview';

const iconProps = {
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.9,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
  'aria-hidden': true,
};

const HeartIcon = () => (
  <svg {...iconProps}>
    <path d="M19 14c1.5-1.46 3-3.2 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.04 3 5.5l7 7Z" />
  </svg>
);

const TargetIcon = () => (
  <svg {...iconProps}>
    <circle cx="12" cy="12" r="9" />
    <circle cx="12" cy="12" r="4.6" />
    <circle cx="12" cy="12" r="1" />
  </svg>
);

const CalendarIcon = () => (
  <svg {...iconProps}>
    <rect x="3.2" y="5" width="17.6" height="16" rx="3" />
    <path d="M8 3v4M16 3v4M3.2 10.5h17.6" />
  </svg>
);

const SparkleIcon = () => (
  <svg {...iconProps}>
    <path d="M12 3.5 13.8 8.2 18.5 10 13.8 11.8 12 16.5 10.2 11.8 5.5 10 10.2 8.2Z" />
    <path d="M18.5 16.5l.7 1.8 1.8.7-1.8.7-.7 1.8-.7-1.8-1.8-.7 1.8-.7Z" />
  </svg>
);

const ClockIcon = () => (
  <svg {...iconProps}>
    <circle cx="12" cy="12" r="9" />
    <path d="M12 7.4V12l3 1.9" />
  </svg>
);

const ShieldIcon = () => (
  <svg {...iconProps}>
    <path d="M12 21.5s7.5-3.6 7.5-9.3V5.6L12 2.5 4.5 5.6v6.6c0 5.7 7.5 9.3 7.5 9.3Z" />
    <path d="m9.2 12.2 2 2 3.6-3.9" />
  </svg>
);

const CaretIcon = () => (
  <svg {...iconProps} className="ob-family-caret">
    <path d="m6.5 9.5 5.5 5.5 5.5-5.5" />
  </svg>
);

const WELCOME_POINTS = [
  {
    icon: <HeartIcon />,
    title: 'Hiểu tuổi & khó khăn của con',
    hint: 'Nắm rõ giai đoạn phát triển và điều con cần.',
  },
  {
    icon: <TargetIcon />,
    title: 'Sinh starter cho con',
    hint: 'Bố/mẹ cũng có thể đặt mục tiêu phù hợp.',
  },
  {
    icon: <CalendarIcon />,
    title: '30 ngày: ít nhắc · tự giác · cả nhà cùng làm gương',
    hint: 'Tạo thói quen bền vững và gắn kết.',
  },
];

export function OnboardingPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const member = useSessionStore((s) => s.member);
  const setFamily = useSessionStore((s) => s.setFamily);

  const [step, setStep] = useState<Step>('welcome');
  const [families, setFamilies] = useState<{ id: string; name: string }[]>([]);
  const [children, setChildren] = useState<FamilyMembership[]>([]);
  const [childId, setChildId] = useState('');
  const [childName, setChildName] = useState('');
  const [ageBand, setAgeBand] = useState<AgeBand>('7-9');
  const [struggles, setStruggles] = useState<StruggleCode[]>([]);
  const [goal, setGoal] = useState<GoalCode>('fewer_nudges');
  const [hasExtraClass, setHasExtraClass] = useState(false);
  const [sleepHour, setSleepHour] = useState<string>('21:00');
  const [priorities, setPriorities] = useState<PriorityCode[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!familyId) {
      navigate('/unlock', { replace: true });
      return;
    }
    if (member?.roleCode === 'child') {
      navigate('/today', { replace: true });
      return;
    }
    void fetchFamilies()
      .then((list) => {
        setFamilies(list.map((f) => ({ id: f.id, name: f.displayName })));
        const family = list.find((f) => f.id === familyId) ?? list[0];
        const kids = (family?.members ?? []).filter((m) => m.roleCode === 'child');
        setChildren(kids);
        if (kids[0] && !childId) {
          setChildId(kids[0].id);
          setChildName(kids[0].displayName);
        }
      })
      .catch(() => setError('Không tải được danh sách con.'));
  }, [familyId, member, navigate, childId]);

  const answers = useMemo(
    () => ({
      childId,
      childName,
      ageBand,
      struggles: struggles.length ? struggles : (['morning_forget'] as StruggleCode[]),
      goal,
      childCount: Math.max(1, children.length),
      hasExtraClass,
      sleepHour,
      priorities,
    }),
    [
      childId,
      childName,
      ageBand,
      struggles,
      goal,
      children.length,
      hasExtraClass,
      sleepHour,
      priorities,
    ],
  );

  const plan = useMemo(() => buildStarterPlan(answers), [answers]);

  const toggleStruggle = (code: StruggleCode) => {
    setStruggles((prev) => {
      if (prev.includes(code)) return prev.filter((x) => x !== code);
      if (prev.length >= 2) return [...prev.slice(1), code];
      return [...prev, code];
    });
  };

  const finish = async () => {
    if (!familyId || !childId) return;
    setBusy(true);
    setError(null);
    try {
      const result = await applyOnboardingPlan({ familyId, answers });
      navigate('/today', {
        replace: true,
        state: { onboardingAdded: result.added, onboardingChild: childName },
      });
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        (err instanceof Error ? err.message : null);
      setError(
        msg ||
          'Chưa gắn được routine lên server. Thử lại — chưa vào Home khi lịch chưa sẵn sàng.',
      );
      try {
        await syncSaveOnboarding(familyId, {
          ...answers,
          completedAt: new Date().toISOString(),
          missionTitles: plan.missions.map((m) => m.title),
        });
      } catch {
        /* profile save optional */
      }
    } finally {
      setBusy(false);
    }
  };

const onSkip = async () => {
    try {
      if (familyId) {
        await skipOnboarding(familyId, { childId, childName, ageBand, struggles, goal });
      }
    } finally {
      navigate('/today', { replace: true });
    }
  };

  if (!familyId) return null;

  return (
    <section className="ob-page">
      <header className="ob-top">
        <p className="ob-brand">Famixa</p>
        {families.length > 1 ? (
          <div className="ob-family-switch">
            <span>{familyName ?? 'Gia đình mình'}</span>
            <CaretIcon />
            <select
              aria-label="Đổi gia đình"
              value={familyId}
              onChange={(e) => {
                const next = families.find((f) => f.id === e.target.value);
                if (next) setFamily({ familyId: next.id, familyName: next.name });
              }}
            >
              {families.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </select>
          </div>
        ) : (
          <p className="ob-family">{familyName ?? 'Gia đình mình'}</p>
        )}
      </header>

      {step === 'welcome' ? (
        <>
          <article className="ob-card ob-welcome">
            <div className="ob-hero">
              <div className="ob-hero-fox" aria-hidden>
                <img src="/home/foxy-avatar.png" alt="" />
              </div>
              <div className="ob-hero-copy">
                <h1>
                  Foxy giúp cả nhà <span>cùng đổi!</span>
                </h1>
                <p>
                  Chỉ vài câu hỏi — Famixa sinh nhịp sống vừa sức cho cả gia đình. Không cần
                  checklist dài.
                </p>
              </div>
            </div>

            <ul className="ob-value-list">
              {WELCOME_POINTS.map((point) => (
                <li key={point.title}>
                  <span className="ob-value-icon" aria-hidden>
                    {point.icon}
                  </span>
                  <span className="ob-value-copy">
                    <strong>{point.title}</strong>
                    <em>{point.hint}</em>
                  </span>
                </li>
              ))}
            </ul>

            {isOnboardingDone(familyId) ? (
              <p className="ob-note">Bạn đã setup trước đó — có thể chạy lại để tinh chỉnh.</p>
            ) : null}

            <button
              type="button"
              className="ob-cta is-primary"
              onClick={() => setStep('child')}
            >
              <SparkleIcon />
              Bắt đầu với Foxy
            </button>
            <button type="button" className="ob-cta is-skip" onClick={() => void onSkip()}>
              <ClockIcon />
              Bỏ qua lần này
            </button>
          </article>

          <p className="ob-privacy">
            <ShieldIcon />
            Thông tin của bạn luôn được bảo mật và an toàn.
          </p>
        </>
      ) : null}

      {step === 'child' ? (
        <article className="ob-card">
          <p className="ob-step">1 / 6</p>
          <h1>Con nào mình tập trung trước?</h1>
          <p>Onboarding gắn routine cho một con — anh/chị em setup thêm lần sau cũng được.</p>
          <div className="ob-choices">
            {children.map((c) => (
              <button
                key={c.id}
                type="button"
                className={`ob-choice${childId === c.id ? ' is-on' : ''}`}
                onClick={() => {
                  setChildId(c.id);
                  setChildName(c.displayName);
                }}
              >
                <span aria-hidden>{avatarEmoji(inferGenderFromName(c.displayName), 'child')}</span>
                {c.displayName}
              </button>
            ))}
          </div>
          {children.length === 0 ? (
            <p className="ob-note">Chưa có thành viên con — thêm trong Admin rồi quay lại.</p>
          ) : null}
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('welcome')}>
              Quay lại
            </button>
            <button
              type="button"
              className="btn btn-primary"
              disabled={!childId}
              onClick={() => setStep('age')}
            >
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'age' ? (
        <article className="ob-card">
          <p className="ob-step">2 / 6</p>
          <h1>{childName} bao nhiêu tuổi?</h1>
          <p>Foxy chỉnh độ dài việc và giọng khen theo độ tuổi.</p>
          <div className="ob-choices is-stack">
            {AGE_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                className={`ob-choice is-wide${ageBand === o.value ? ' is-on' : ''}`}
                onClick={() => setAgeBand(o.value)}
              >
                <strong>{o.label}</strong>
                <span>{o.hint}</span>
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('child')}>
              Quay lại
            </button>
            <button type="button" className="btn btn-primary" onClick={() => setStep('struggle')}>
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'struggle' ? (
        <article className="ob-card">
          <p className="ob-step">3 / 6</p>
          <h1>Khó khăn lớn nhất hiện nay?</h1>
          <p>Chọn 1–2 ý — Foxy ưu tiên đúng chỗ đau.</p>
          <div className="ob-choices">
            {STRUGGLE_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                className={`ob-choice${struggles.includes(o.value) ? ' is-on' : ''}`}
                onClick={() => toggleStruggle(o.value)}
              >
                <span aria-hidden>{o.icon}</span>
                {o.label}
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('age')}>
              Quay lại
            </button>
            <button
              type="button"
              className="btn btn-primary"
              disabled={struggles.length === 0}
              onClick={() => setStep('goal')}
            >
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'goal' ? (
        <article className="ob-card">
          <p className="ob-step">4 / 6</p>
          <h1>Sau 30 ngày, muốn đổi điều gì nhất?</h1>
          <p>Đây là “lý do trả tiền” Famixa sẽ đo.</p>
          <div className="ob-choices is-stack">
            {GOAL_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                className={`ob-choice is-wide${goal === o.value ? ' is-on' : ''}`}
                onClick={() => setGoal(o.value)}
              >
                <span aria-hidden>{o.icon}</span>
                <strong>{o.label}</strong>
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('struggle')}>
              Quay lại
            </button>
            <button type="button" className="btn btn-primary" onClick={() => setStep('lifestyle')}>
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'lifestyle' ? (
        <article className="ob-card">
          <p className="ob-step">5 / 6</p>
          <h1>Lịch học & giờ ngủ?</h1>
          <p>AI chỉnh Routine và đề xuất ví màn hình tuần — không bắt bạn cài Rule từng ngày.</p>
          <p className="ob-note">Có học thêm không?</p>
          <div className="ob-choices">
            <button
              type="button"
              className={`ob-choice${hasExtraClass ? ' is-on' : ''}`}
              onClick={() => setHasExtraClass(true)}
            >
              Có
            </button>
            <button
              type="button"
              className={`ob-choice${!hasExtraClass ? ' is-on' : ''}`}
              onClick={() => setHasExtraClass(false)}
            >
              Không
            </button>
          </div>
          <p className="ob-note">Giờ ngủ mong muốn</p>
          <div className="ob-choices">
            {SLEEP_HOUR_OPTIONS.map((h) => (
              <button
                key={h}
                type="button"
                className={`ob-choice${sleepHour === h ? ' is-on' : ''}`}
                onClick={() => setSleepHour(h)}
              >
                {h}
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('goal')}>
              Quay lại
            </button>
            <button type="button" className="btn btn-primary" onClick={() => setStep('priority')}>
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'priority' ? (
        <article className="ob-card">
          <p className="ob-step">6 / 6</p>
          <h1>Muốn ưu tiên điều gì?</h1>
          <p>Chọn 1–2 — AI sinh Routine + đề xuất ví tuần (~{suggestStarterWalletMinutes(answers)} phút).</p>
          <div className="ob-choices">
            {PRIORITY_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                className={`ob-choice${priorities.includes(o.value) ? ' is-on' : ''}`}
                onClick={() =>
                  setPriorities((prev) => {
                    if (prev.includes(o.value)) return prev.filter((x) => x !== o.value);
                    if (prev.length >= 2) return [...prev.slice(1), o.value];
                    return [...prev, o.value];
                  })
                }
              >
                <span aria-hidden>{o.icon}</span>
                {o.label}
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('lifestyle')}>
              Quay lại
            </button>
            <button
              type="button"
              className="btn btn-primary"
              disabled={priorities.length === 0}
              onClick={() => setStep('preview')}
            >
              Xem kế hoạch Foxy
            </button>
          </div>
        </article>
      ) : null}

      {step === 'preview' ? (
        <article className="ob-card">
          <div className="ob-fox is-small" aria-hidden>
            🦊
          </div>
          <h1>Kế hoạch starter của {childName}</h1>
          <p className="ob-pitch">{plan.coachPitch}</p>
          <p className="ob-focus">{plan.focusLine}</p>
          <p className="ob-note">
            Foxy gắn Routine + đề xuất ví màn hình tuần (~{suggestStarterWalletMinutes(answers)} phút)
            vào hộp thư duyệt. Bố mẹ chỉ 👍 — không cài Settings.
          </p>
          <ul className="ob-missions">
            {plan.missions.map((m) => (
              <li key={m.title}>
                <strong>
                  {m.title}{' '}
                  <em>
                    {m.windowStart}–{m.windowEnd}
                  </em>
                </strong>
                <span>{m.why}</span>
              </li>
            ))}
          </ul>
          {error ? <div className="banner-error">{error}</div> : null}
          <button
            type="button"
            className="btn btn-primary"
            disabled={busy}
            onClick={() => void finish()}
          >
            {busy ? 'Đang gắn routine…' : 'Áp dụng & vào Home'}
          </button>
          <div className="ob-nav">
            <button
              type="button"
              className="pill is-soft"
              disabled={busy}
              onClick={() => setStep('priority')}
            >
              Quay lại
            </button>
            <button type="button" className="pill is-soft" disabled={busy} onClick={() => void onSkip()}>
              Bỏ qua
            </button>
          </div>
        </article>
      ) : null}
    </section>
  );
}
